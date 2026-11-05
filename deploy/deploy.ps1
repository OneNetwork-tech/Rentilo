<#
.SYNOPSIS
  Builds Rentilo with Docker Desktop on this PC and deploys it to the server over SSH.

.EXAMPLE
  # First deploy (installs Docker on the server, plain HTTP on the IP, with demo data):
  .\deploy\deploy.ps1 -Setup -SeedDemoData

.EXAMPLE
  # First deploy with a domain (automatic HTTPS). The domain's DNS must point at the server.
  .\deploy\deploy.ps1 -Setup -Domain rentilo.example.com

.EXAMPLE
  # Every later update: just run it again.
  .\deploy\deploy.ps1
#>
[CmdletBinding()]
param(
    [string]$Server = "ubuntu@207.127.90.194",
    [string]$KeyPath = "C:\Users\SalmanYahya\ORACLE\RMM keys\.ssh\ssh-key-2025-08-28.key",
    # Only used on the first deploy (when the server has no .env yet). Empty = HTTP on the IP.
    [string]$Domain = "",
    # Prepare the server first: Docker, firewall, swap. Needed once.
    [switch]$Setup,
    # Only used on the first deploy: create demo users and listings.
    [switch]$SeedDemoData
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$DeployDir = Join-Path $RepoRoot "deploy"
$OutDir = Join-Path $DeployDir "out"
$Remote = "rentilo"   # relative to the remote user's home directory
$SshOpts = @("-i", $KeyPath, "-o", "StrictHostKeyChecking=accept-new", "-o", "ServerAliveInterval=30")

function Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }
function Invoke-Remote([string]$command) {
    & ssh @SshOpts $Server $command
    if ($LASTEXITCODE -ne 0) { throw "Remote command failed: $command" }
}
function Copy-ToRemote([string[]]$files) {
    & scp @SshOpts @files "${Server}:$Remote/"
    if ($LASTEXITCODE -ne 0) { throw "Copy to server failed." }
}

Step "Checking tools"
foreach ($tool in "docker", "ssh", "scp") {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "'$tool' was not found. Install Docker Desktop and the Windows OpenSSH client." }
}
if (-not (Test-Path $KeyPath)) { throw "SSH key not found: $KeyPath" }
docker info *> $null
if ($LASTEXITCODE -ne 0) { throw "Docker Desktop is not running. Start it and try again." }

Step "Connecting to $Server"
Invoke-Remote "mkdir -p $Remote"
$hasDocker = (& ssh @SshOpts $Server "command -v docker >/dev/null && echo yes || echo no").Trim()

if ($Setup -or $hasDocker -ne "yes") {
    Step "Preparing the server (Docker, firewall, swap)"
    Copy-ToRemote @((Join-Path $DeployDir "server-setup.sh"))
    Invoke-Remote "sed -i 's/\r$//' $Remote/server-setup.sh && sudo bash $Remote/server-setup.sh"
}

$arch = (& ssh @SshOpts $Server "uname -m").Trim()
$platform = if ($arch -in "aarch64", "arm64") { "linux/arm64" } else { "linux/amd64" }

Step "Building the app image for $platform with Docker Desktop"
docker buildx build --platform $platform -t rentilo:latest --load $RepoRoot
if ($LASTEXITCODE -ne 0) { throw "Docker build failed." }

Step "Exporting the image"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$imageTar = Join-Path $OutDir "rentilo-image.tar"
docker save -o $imageTar rentilo:latest
if ($LASTEXITCODE -ne 0) { throw "docker save failed." }
"{0:N0} MB" -f ((Get-Item $imageTar).Length / 1MB) | ForEach-Object { Write-Host "    Image size: $_" }

Step "Uploading to the server"
$files = "docker-compose.prod.yml", "Caddyfile", "backup.sh", "remote-up.sh", ".env.example" |
    ForEach-Object { Join-Path $DeployDir $_ }
Copy-ToRemote ($files + $imageTar)

Step "Starting Rentilo on the server"
$init = "INIT_SITE_ADDRESS='$Domain' INIT_SEED_DEMO_DATA='$(if ($SeedDemoData) { 'true' } else { '' })'"
Invoke-Remote "cd $Remote && sed -i 's/\r$//' *.sh Caddyfile docker-compose.prod.yml .env.example && $init bash remote-up.sh"

Remove-Item $imageTar -ErrorAction SilentlyContinue
Step "Done"
