#!/bin/bash
# Fix .NET Backend on VPS
# Run on VPS: bash fix-dotnet-backend.sh

set -e

echo "🔧 Fixing .NET Backend Issue"
echo "=============================="
echo ""

# 1. Stop the failing service
echo "⏸️  Stopping az-backend service..."
systemctl stop az-backend.service
systemctl disable az-backend.service
echo "✅ Service stopped"
echo ""

# 2. Fix .NET Runtime Path
echo "🔧 Fixing .NET Runtime Path..."

# Check current .NET location
DOTNET_ACTUAL=$(which dotnet)
echo "Current dotnet: $DOTNET_ACTUAL"

# Check runtime location
if [ -d "/usr/lib/dotnet/shared/Microsoft.NETCore.App" ]; then
    echo "✅ Runtime found in /usr/lib/dotnet"
    DOTNET_ROOT="/usr/lib/dotnet"
elif [ -d "/usr/share/dotnet/shared/Microsoft.NETCore.App" ]; then
    echo "✅ Runtime found in /usr/share/dotnet"
    DOTNET_ROOT="/usr/share/dotnet"
else
    echo "❌ .NET Runtime not found!"
    exit 1
fi

# List available runtimes
echo ""
echo "Available .NET Runtimes:"
ls -la $DOTNET_ROOT/shared/Microsoft.NETCore.App/ 2>/dev/null || echo "None found"
echo ""

# 3. Check backend service file
echo "📝 Checking backend service configuration..."
if [ -f /etc/systemd/system/az-backend.service ]; then
    echo "Current service file:"
    cat /etc/systemd/system/az-backend.service
    echo ""
    
    # Backup original
    cp /etc/systemd/system/az-backend.service /etc/systemd/system/az-backend.service.backup
    echo "✅ Backup created: az-backend.service.backup"
fi
echo ""

# 4. Find backend directory
echo "📁 Looking for backend application..."
BACKEND_DIR=""
if [ -d "/var/www/az-backend" ]; then
    BACKEND_DIR="/var/www/az-backend"
elif [ -d "/home/azinternational/backend" ]; then
    BACKEND_DIR="/home/azinternational/backend"
elif [ -d "/opt/az-backend" ]; then
    BACKEND_DIR="/opt/az-backend"
else
    echo "⚠️  Backend directory not found in common locations"
    echo "Searching..."
    BACKEND_DIR=$(find /var/www /home /opt -name "*.dll" -path "*/az-backend/*" 2>/dev/null | head -1 | xargs dirname)
fi

if [ -z "$BACKEND_DIR" ]; then
    echo "❌ Cannot find backend directory"
    echo "Please specify manually:"
    read -p "Backend directory path: " BACKEND_DIR
fi

echo "Backend directory: $BACKEND_DIR"
echo ""

# Find main DLL
MAIN_DLL=$(find "$BACKEND_DIR" -maxdepth 1 -name "*.dll" | head -1)
if [ -z "$MAIN_DLL" ]; then
    echo "❌ No DLL found in $BACKEND_DIR"
    exit 1
fi

echo "Main DLL: $MAIN_DLL"
echo ""

# 5. Create fixed service file
echo "📝 Creating fixed service file..."

cat > /etc/systemd/system/az-backend-fixed.service << EOF
[Unit]
Description=AZ International Backend API
After=network.target postgresql.service

[Service]
Type=notify
WorkingDirectory=$BACKEND_DIR
ExecStart=$DOTNET_ROOT/dotnet $MAIN_DLL
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=az-backend
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROOT=$DOTNET_ROOT
Environment=ASPNETCORE_URLS=http://0.0.0.0:8080

# Prevent restart loop
StartLimitIntervalSec=60
StartLimitBurst=3

[Install]
WantedBy=multi-user.target
EOF

echo "✅ New service file created: az-backend-fixed.service"
echo ""

# 6. Test dotnet command
echo "🧪 Testing .NET command..."
cd "$BACKEND_DIR"
export DOTNET_ROOT=$DOTNET_ROOT
$DOTNET_ROOT/dotnet --info

echo ""
echo "Testing backend startup (5 seconds)..."
timeout 5 $DOTNET_ROOT/dotnet "$MAIN_DLL" || true
echo ""

# 7. Reload and start
echo "🔄 Reloading systemd..."
systemctl daemon-reload

echo "▶️  Starting fixed service..."
systemctl start az-backend-fixed.service

sleep 3

# 8. Check status
echo ""
echo "📊 Service Status:"
systemctl status az-backend-fixed.service --no-pager || true

echo ""
echo "🔌 Checking port 8080..."
ss -tulpn | grep :8080 || netstat -tulpn | grep :8080 || echo "⚠️  Port 8080 not listening yet"

echo ""
echo "📋 Recent logs:"
journalctl -u az-backend-fixed.service -n 20 --no-pager

echo ""
echo "=============================="
if systemctl is-active --quiet az-backend-fixed.service; then
    echo "✅ Backend is running!"
    echo ""
    echo "Next steps:"
    echo "1. Test API: curl http://localhost:8080/api/health"
    echo "2. If working, enable service: systemctl enable az-backend-fixed.service"
    echo "3. Remove old service: systemctl disable az-backend.service"
else
    echo "❌ Backend failed to start"
    echo ""
    echo "Check logs: journalctl -u az-backend-fixed.service -f"
fi
echo "=============================="
