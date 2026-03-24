#!/bin/bash
# Finish KasserPro Setup - Create Service and Start
set -e

echo "🔧 Finishing KasserPro Setup"
echo "============================="
echo ""

KASSERPRO_DIR="/var/www/kasserpro"
KASSERPRO_PORT="5243"

# 1. Verify files exist
echo "📁 Checking installation..."
if [ ! -f "$KASSERPRO_DIR/KasserPro.API.dll" ]; then
    echo "❌ KasserPro.API.dll not found!"
    exit 1
fi
echo "✅ Files found"
echo ""

# 2. Set permissions
echo "🔐 Setting permissions..."
chmod +x "$KASSERPRO_DIR/KasserPro.API" 2>/dev/null || true
chown -R root:root "$KASSERPRO_DIR"
echo "✅ Permissions set"
echo ""

# 3. Create directories
echo "📁 Creating directories..."
mkdir -p "$KASSERPRO_DIR/logs"
mkdir -p "$KASSERPRO_DIR/backups"
echo "✅ Directories created"
echo ""

# 4. Detect .NET
echo "🔍 Detecting .NET..."

# Try to find dotnet command
DOTNET_CMD=""
if command -v dotnet >/dev/null 2>&1; then
    DOTNET_CMD=$(which dotnet)
    DOTNET_ROOT=$(dirname $(dirname $(readlink -f $DOTNET_CMD)))
elif [ -f "/usr/bin/dotnet" ]; then
    DOTNET_CMD="/usr/bin/dotnet"
    DOTNET_ROOT=$(dirname $(dirname $(readlink -f $DOTNET_CMD)))
elif [ -f "/usr/lib/dotnet/dotnet" ]; then
    DOTNET_CMD="/usr/lib/dotnet/dotnet"
    DOTNET_ROOT="/usr/lib/dotnet"
elif [ -f "/usr/share/dotnet/dotnet" ]; then
    DOTNET_CMD="/usr/share/dotnet/dotnet"
    DOTNET_ROOT="/usr/share/dotnet"
else
    echo "❌ .NET not found!"
    echo "Please install .NET 8.0 Runtime:"
    echo "  wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb"
    echo "  dpkg -i packages-microsoft-prod.deb"
    echo "  apt update && apt install -y aspnetcore-runtime-8.0"
    exit 1
fi

echo "  DOTNET_CMD: $DOTNET_CMD"
echo "  DOTNET_ROOT: $DOTNET_ROOT"

# Verify .NET works
if ! $DOTNET_CMD --version >/dev/null 2>&1; then
    echo "❌ .NET command not working!"
    echo "Trying to find runtime manually..."
    
    # Look for runtime in common locations
    for dir in /usr/lib/dotnet /usr/share/dotnet /opt/dotnet; do
        if [ -d "$dir/shared/Microsoft.NETCore.App" ]; then
            echo "Found runtime in: $dir"
            DOTNET_ROOT="$dir"
            DOTNET_CMD="$dir/dotnet"
            break
        fi
    done
    
    # Final check
    if ! $DOTNET_CMD --version >/dev/null 2>&1; then
        echo "❌ Cannot execute dotnet!"
        exit 1
    fi
fi

echo "✅ .NET detected: $($DOTNET_CMD --version)"
echo ""

# 5. Generate secure JWT key
echo "🔑 Generating JWT key..."
JWT_KEY=$(openssl rand -base64 64 | tr -d '\n')
echo "✅ JWT key generated"
echo ""

# 6. Create appsettings.json
echo "⚙️  Creating appsettings.json..."
cat > "$KASSERPRO_DIR/appsettings.json" << EOF
{
  "Urls": "http://0.0.0.0:$KASSERPRO_PORT",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AllowedOrigins": ["*"],
  "Jwt": {
    "Key": "$JWT_KEY",
    "Issuer": "KasserPro",
    "Audience": "KasserPro",
    "ExpiryInHours": 24
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=$KASSERPRO_DIR/kasserpro.db;Cache=Shared"
  },
  "ShiftAutoClose": {
    "Enabled": true,
    "HoursThreshold": 12
  }
}
EOF
echo "✅ Configuration created"
echo ""

# 7. Create systemd service
echo "📝 Creating systemd service..."
cat > /etc/systemd/system/kasserpro.service << EOF
[Unit]
Description=KasserPro Backend API
After=network.target

[Service]
Type=notify
WorkingDirectory=$KASSERPRO_DIR
ExecStart=$DOTNET_CMD $KASSERPRO_DIR/KasserPro.API.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=kasserpro
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROOT=$DOTNET_ROOT
Environment=ASPNETCORE_URLS=http://0.0.0.0:$KASSERPRO_PORT

# Prevent restart loop
StartLimitIntervalSec=60
StartLimitBurst=5

[Install]
WantedBy=multi-user.target
EOF
echo "✅ Service file created"
echo ""

# 8. Reload systemd
echo "🔄 Reloading systemd..."
systemctl daemon-reload
echo "✅ Systemd reloaded"
echo ""

# 9. Enable and start service
echo "▶️  Starting KasserPro service..."
systemctl enable kasserpro
systemctl start kasserpro

sleep 3

# 10. Check status
echo ""
echo "📊 Service Status:"
systemctl status kasserpro --no-pager || true

echo ""
echo "🔌 Port Check:"
ss -tulpn | grep :$KASSERPRO_PORT || netstat -tulpn | grep :$KASSERPRO_PORT || echo "⚠️  Port not listening yet"

echo ""
echo "🧪 Testing Health Endpoint:"
sleep 2
curl -s http://localhost:$KASSERPRO_PORT/api/health || echo "⚠️  Not responding yet"

echo ""
echo "📋 Recent Logs:"
journalctl -u kasserpro -n 15 --no-pager

echo ""
echo "============================="
if systemctl is-active --quiet kasserpro; then
    echo "✅ KasserPro is running!"
    echo ""
    echo "🌐 Access URLs:"
    echo "  - http://168.231.106.139:$KASSERPRO_PORT"
    echo "  - http://localhost:$KASSERPRO_PORT/swagger"
    echo ""
    echo "🔧 Next Steps:"
    echo "  1. Open firewall: ufw allow $KASSERPRO_PORT/tcp"
    echo "  2. Test API: curl http://localhost:$KASSERPRO_PORT/api/health"
    echo "  3. View logs: journalctl -u kasserpro -f"
else
    echo "❌ KasserPro failed to start"
    echo ""
    echo "Check logs: journalctl -u kasserpro -n 50"
fi
echo "============================="
