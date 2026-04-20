#!/bin/bash
# Install the Victoria-like systemd unit. Run on the VM (not locally).
#
# Required env:
#   PG_PASS   Postgres password for the victoria role
# Optional env:
#   APP_DIR   default /home/ubuntu/victoria-server
#   APP_USER  default ubuntu

set -euo pipefail

: "${PG_PASS:?Set PG_PASS to the Postgres password before running}"
APP_DIR="${APP_DIR:-/home/ubuntu/victoria-server}"
APP_USER="${APP_USER:-ubuntu}"

# Write the connection-string secret as an EnvironmentFile (mode 600, root-only).
SECRET_FILE=/etc/victoria/server.env
sudo install -d -m 750 -o root -g "$APP_USER" /etc/victoria
sudo tee "$SECRET_FILE" >/dev/null <<EOF
ConnectionStrings__DefaultConnection=Host=localhost;Database=victoria_world;Username=victoria;Password=${PG_PASS};Maximum Pool Size=100;Connection Idle Lifetime=60;Pooling=true
ConnectionStrings__Redis=localhost:6379
EOF
sudo chmod 640 "$SECRET_FILE"
sudo chown root:"$APP_USER" "$SECRET_FILE"

sudo tee /etc/systemd/system/victoria.service >/dev/null <<EOF
[Unit]
Description=Victoria-like Game Server
After=network.target postgresql.service redis-server.service
Wants=postgresql.service redis-server.service

[Service]
Type=simple
User=${APP_USER}
WorkingDirectory=${APP_DIR}
ExecStart=${APP_DIR}/VictoriaLike.Server
Restart=always
RestartSec=5

EnvironmentFile=${SECRET_FILE}
Environment=ASPNETCORE_URLS=http://127.0.0.1:8080
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=Logging__LogLevel__Default=Information
Environment=Logging__LogLevel__Microsoft=Warning

StandardOutput=journal
StandardError=journal

LimitNOFILE=65536
LimitNPROC=65536

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable victoria
echo "✓ Installed. Start with: sudo systemctl start victoria"
echo "  Status: sudo systemctl status victoria"
echo "  Logs:   journalctl -u victoria -f"
