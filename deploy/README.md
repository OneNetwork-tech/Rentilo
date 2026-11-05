# Deploying Rentilo

You build Rentilo with **Docker Desktop on your PC**. `deploy.ps1` then copies the app to an Ubuntu server over SSH and starts this stack there:

| Container | What it does |
|---|---|
| `web` | The Rentilo app (not exposed directly) |
| `db` | PostgreSQL 17 (data in a Docker volume) |
| `caddy` | Public web server on ports 80/443, with automatic HTTPS certificates when you use a domain |
| `backup` | Nightly backup of the database and photos to `~/rentilo/backups`, kept for 14 days |

The server never needs the source code or a GitHub login. It also doesn't need to compile anything, which matters on small servers.

## 1. One-time preparation

**On your PC**

1. Install and start [Docker Desktop](https://www.docker.com/products/docker-desktop/).
2. Check that SSH works with your key (it's the same command you already use):

   ```powershell
   ssh -i "C:\Users\SalmanYahya\ORACLE\RMM keys\.ssh\ssh-key-2025-08-28.key" ubuntu@207.127.90.194
   ```

**In the Oracle Cloud console**

3. Open the instance, go to **Networking**, then the subnet's **Security List** (or its network security group). Add **ingress rules** with source `0.0.0.0/0`:
   - TCP port **80**
   - TCP port **443**
   - UDP port **443** (optional, enables HTTP/3)

   The setup script opens the same ports in the server's own firewall. Oracle's Ubuntu images block them by default.

4. *(Optional, for HTTPS)* Create a DNS **A record** for your domain, for example `rentilo.example.com`, pointing to `207.127.90.194`.

## 2. First deploy

Open PowerShell in the repository folder. If scripts are blocked, run this first:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
```

Without a domain (HTTP on the IP address, with demo data for testing):

```powershell
.\deploy\deploy.ps1 -Setup -SeedDemoData
```

With a domain (automatic HTTPS):

```powershell
.\deploy\deploy.ps1 -Setup -Domain rentilo.example.com
```

`-Setup` installs Docker on the server, opens the firewall and adds swap on small machines. You only need it the first time.

The script detects whether the server is ARM (Ampere) or x86, and builds the right image for it.

When it finishes, it prints the address to open. Demo accounts use the password `Rentilo123!`: `anna@rentilo.dev`, `erik@rentilo.dev`, `sara@rentilo.dev`.

The defaults for `-Server` and `-KeyPath` are your server and key. Override them for another machine:

```powershell
.\deploy\deploy.ps1 -Server ubuntu@1.2.3.4 -KeyPath C:\path\to\key
```

On macOS or Linux, use the bash version:

```bash
SSH_KEY=~/.ssh/mykey ./deploy/deploy.sh --setup --seed-demo-data
```

## 3. Updating

Pull the latest code, then run:

```powershell
.\deploy\deploy.ps1
```

Database migrations run automatically when the app starts. Users stay logged in, because the login keys are kept in a volume.

## 4. Configuration on the server

Settings live in `~/rentilo/.env` on the server. It is created on the first deploy with a random database password, and it is never overwritten.

| Setting | Meaning |
|---|---|
| `SITE_ADDRESS` | `:80` for plain HTTP on the IP, or your domain for HTTPS |
| `SEED_DEMO_DATA` | `true` to create demo users and listings on an empty database |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM` | Outgoing email. Leave `SMTP_HOST` empty to only write emails to the log. |
| `REQUIRE_CONFIRMED_ACCOUNT` | `true` once email works, to require email confirmation |
| `BACKUP_KEEP_DAYS` | How many days of backups to keep |

After editing it, apply the changes:

```bash
cd ~/rentilo
bash remote-up.sh
```

To switch from an IP address to a domain later, set `SITE_ADDRESS=your.domain` in `.env` and run `bash remote-up.sh`. Caddy then fetches the certificate itself.

## 5. Day-to-day commands (on the server)

All of these run from `~/rentilo`:

Start, stop and status:

```bash
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml stop
docker compose -f docker-compose.prod.yml start
```

App logs:

```bash
docker compose -f docker-compose.prod.yml logs -f web
```

Back up right now, for example before an upgrade:

```bash
docker compose -f docker-compose.prod.yml exec backup sh /backup.sh once
```

## 6. Restoring a backup

Restoring overwrites the current data, so take a fresh backup first.

1. Stop the app:

   ```bash
   cd ~/rentilo
   docker compose -f docker-compose.prod.yml stop web
   ```

2. Recreate the database and restore the dump. Replace the file name with the backup you want:

   ```bash
   docker compose -f docker-compose.prod.yml exec -T db dropdb -U rentilo --force rentilo
   docker compose -f docker-compose.prod.yml exec -T db createdb -U rentilo rentilo
   docker compose -f docker-compose.prod.yml exec -T db pg_restore -U rentilo -d rentilo --no-owner < backups/db-YYYYMMDD-HHMM.dump
   ```

3. Restore the photos. Again, use your file name:

   ```bash
   docker run --rm -v rentilo_uploads:/uploads -v "$PWD/backups":/b alpine sh -c "rm -rf /uploads/* && tar -xzf /b/uploads-YYYYMMDD-HHMM.tar.gz -C /uploads"
   ```

4. Start the app again:

   ```bash
   docker compose -f docker-compose.prod.yml start web
   ```

The backups are stored on the same server as the database. Copy `~/rentilo/backups` somewhere else regularly, for example to Oracle Object Storage or your PC:

```powershell
scp -i "<key>" -r ubuntu@207.127.90.194:rentilo/backups .\rentilo-backups
```

## Running locally with Docker Desktop

No server is needed. In the repository folder, run:

```powershell
docker compose up --build
```

Then open <http://localhost:8080>. This uses the root `docker-compose.yml`, which includes demo data and a development database password.

## Troubleshooting

- **The site doesn't open from the internet, but the script said "healthy".**
  1. Check the Oracle security list (step 3).
  2. Check the firewall on the server: `sudo iptables -L INPUT -n | head`. It should show ACCEPT rules for ports 80 and 443 near the top.
- **No HTTPS certificate.**
  - Check that the DNS A record points to the server, and that port 80 is reachable (Let's Encrypt uses it).
  - Look at the Caddy logs: `docker compose -f docker-compose.prod.yml logs caddy`
- **`docker: permission denied` right after setup.** Log out of SSH and back in, so that your user's new `docker` group membership takes effect.
- **The build fails on the PC.** Make sure Docker Desktop is running. It needs to be set to Linux containers, which is the default.
