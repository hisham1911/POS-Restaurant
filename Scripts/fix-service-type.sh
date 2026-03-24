#!/bin/bash
# Fix Service Type to Simple

echo "🔧 Fixing Service Type"
echo "======================"
echo ""

# Stop service
systemctl stop kasserpro

# Update service file
cat > /etc/systemd/system/kasserpro.service << 'EOF'
[Unit]
Description=KasserPro Backend API
After=network.target

[Service]
Type=simple
WorkingDirectory=/var/www/kasserpro
ExecStart=/usr/bin/dotnet /var/www/kasserpro/KasserPro.API.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=kasserpro
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROOT=/usr/share
Environment=ASPNETCORE_URLS=http://0.0.0.0:5243

# Prevent restart loop
StartLimitIntervalSec=60
StartLimitBurst=5

[Install]
WantedBy=multi-user.target
EOF

# Reload and start
systemctl daemon-reload
systemctl start kasserpro

sleep 5

# Check status
systemctl status kasserpro --no-pager

echo ""
if systemctl is-active --quiet kasserpro; then
    echo "✅ Service is running!"
    curl -s http://localhost:5243/api/health | head -c 100
    echo ""
else
    echo "❌ Service failed"
fi
