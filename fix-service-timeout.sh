#!/bin/bash
# Fix KasserPro Service Timeout Issue

echo "🔧 Fixing Service Timeout"
echo "========================="
echo ""

# Stop service
echo "⏸️  Stopping service..."
systemctl stop kasserpro

# Backup service file
cp /etc/systemd/system/kasserpro.service /etc/systemd/system/kasserpro.service.backup

# Update service file with longer timeout
echo "📝 Updating service file..."
cat > /etc/systemd/system/kasserpro.service << 'EOF'
[Unit]
Description=KasserPro Backend API
After=network.target

[Service]
Type=notify
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

# Increase startup timeout to 3 minutes (default is 90 seconds)
TimeoutStartSec=180

# Prevent restart loop
StartLimitIntervalSec=60
StartLimitBurst=5

[Install]
WantedBy=multi-user.target
EOF

echo "✅ Service file updated"
echo ""

# Reload systemd
echo "🔄 Reloading systemd..."
systemctl daemon-reload

# Start service
echo "▶️  Starting service..."
systemctl start kasserpro

# Wait for startup
echo "⏳ Waiting for startup (60 seconds)..."
sleep 60

# Check status
echo ""
echo "📊 Service Status:"
systemctl status kasserpro --no-pager

echo ""
echo "🔌 Port Check:"
ss -tulpn | grep :5243 || echo "Port not listening"

echo ""
echo "🧪 Health Check:"
curl -s http://localhost:5243/api/health || echo "Not responding"

echo ""
echo "========================="
if systemctl is-active --quiet kasserpro; then
    echo "✅ Service is running!"
else
    echo "❌ Service failed to start"
    echo ""
    echo "Recent logs:"
    journalctl -u kasserpro -n 20 --no-pager
fi
echo "========================="
