#!/bin/sh
# Nightly backup of the database and uploaded photos into ./backups on the server.
# Runs once at start-up, then every day at 03:00. Keeps BACKUP_KEEP_DAYS days.
set -eu
KEEP="${BACKUP_KEEP_DAYS:-14}"
mkdir -p /backups

backup() {
  stamp=$(date +%Y%m%d-%H%M)
  pg_dump --format=custom --file="/backups/db-$stamp.dump"
  tar -czf "/backups/uploads-$stamp.tar.gz" -C /uploads .
  find /backups -type f \( -name 'db-*.dump' -o -name 'uploads-*.tar.gz' \) -mtime +"$KEEP" -delete
  echo "$(date '+%F %T') backup $stamp done"
}

until pg_isready -q; do sleep 2; done

# "sh /backup.sh once" makes a single backup now (e.g. before an upgrade) and exits.
if [ "${1:-}" = "once" ]; then backup; exit 0; fi

# Give the app time to apply migrations before the first backup after start-up.
sleep 120
backup
while true; do
  now=$(date +%s)
  next=$(date -d "$(date +%F) 03:00" +%s 2>/dev/null || echo $((now + 86400)))
  [ "$next" -le "$now" ] && next=$((next + 86400))
  sleep $((next - now))
  backup || echo "$(date '+%F %T') backup FAILED"
done
