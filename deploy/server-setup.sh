#!/usr/bin/env bash
# One-time preparation of an Ubuntu server (tested for Oracle Cloud images) for Rentilo.
# Safe to run again. Usage on the server:  sudo bash ~/rentilo/server-setup.sh
set -euo pipefail

if [[ $EUID -ne 0 ]]; then echo "Run with sudo." >&2; exit 1; fi
TARGET_USER="${SUDO_USER:-ubuntu}"

echo "==> Installing Docker Engine + Compose plugin"
if ! command -v docker >/dev/null 2>&1; then
  apt-get update -q
  apt-get install -y -q ca-certificates curl
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
  . /etc/os-release
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${VERSION_CODENAME} stable" \
    > /etc/apt/sources.list.d/docker.list
  apt-get update -q
  apt-get install -y -q docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
else
  echo "    Docker already installed: $(docker --version)"
fi
if [[ -d /run/systemd/system ]]; then systemctl enable --now docker; fi
usermod -aG docker "$TARGET_USER"

echo "==> Opening ports 80 and 443 in the host firewall"
# Oracle's Ubuntu images ship iptables rules that REJECT everything except SSH.
if command -v iptables >/dev/null 2>&1; then
  for port in 80 443; do
    iptables -C INPUT -p tcp --dport "$port" -m state --state NEW -j ACCEPT 2>/dev/null \
      || iptables -I INPUT 1 -p tcp --dport "$port" -m state --state NEW -j ACCEPT
  done
  iptables -C INPUT -p udp --dport 443 -j ACCEPT 2>/dev/null || iptables -I INPUT 1 -p udp --dport 443 -j ACCEPT
  if command -v netfilter-persistent >/dev/null 2>&1; then
    netfilter-persistent save
  else
    DEBIAN_FRONTEND=noninteractive apt-get install -y -q iptables-persistent
    netfilter-persistent save
  fi
fi
if command -v ufw >/dev/null 2>&1 && ufw status | grep -q "Status: active"; then
  ufw allow 80/tcp && ufw allow 443/tcp && ufw allow 443/udp
fi

echo "==> Adding swap if the machine has little memory"
mem_mb=$(awk '/MemTotal/ {print int($2/1024)}' /proc/meminfo)
if [[ $mem_mb -lt 2048 ]] && ! swapon --show | grep -q .; then
  fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
  grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
  echo "    Added 2 GB swap (RAM: ${mem_mb} MB)"
fi

install -d -o "$TARGET_USER" -g "$TARGET_USER" "/home/$TARGET_USER/rentilo"

echo
echo "Server ready. Remember: in the Oracle Cloud console, the instance's VCN security list"
echo "(or network security group) must also allow inbound TCP 80 and 443 from 0.0.0.0/0."
