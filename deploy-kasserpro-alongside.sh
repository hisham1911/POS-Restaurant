#!/bin/bash
# Deploy KasserPro alongside existing AZ International site
# Run on VPS after uploading KasserPro files

set -e

echo "🚀 KasserPro Deployment (Alongside AZ International)"
echo "====================================================="
echo ""

# Configuration
KASSERPRO_DIR="/var/www/kasserpro"
KASSERPRO_PORT="5243"
KASSERPRO_DOMAIN="kasserpro.azinternational-eg.com"  # أو استخدم IP

echo "📋 Configuration:"
echo "  Directory: $KASSERPRO_DIR"
echo "  Port: $KASSERPRO_PORT"
echo "  Domain: $KASSERPRO_DOMAIN (optional)"
echo ""

# 1. Check if KasserPro files exist
if [ ! -f "/tmp/kasserpro-backend.zip" ]; then
    echo "❌ KasserPro package not found at /tmp/kasserpro-backend.zip"
    echo ""
    echo "Please upload first:"
    echo "  scp kasserpro-backend.zip root@168.231.106.139:/tmp/"
    exit 1
fi

# 2. Create directory
echo "📁 Creating KasserPro directory..."
mkdir -p $KASSERPRO_DIR
mkdir -p $KASSERPRO_DIR/logs
mkdir -p $KASSERPRO_DIR/backups

# 3. Extract files
echo "📦 Extracting KasserPro..."
unzip -o /tmp/kasserpro-backend.zip -d $KASSERPRO_DIR/

# 4. Set permissions
echo "🔐 Setting permissions..."
chmod +x $KASSERPRO_DIR/KasserPro.API
chown -R root:root $KASSERPRO_DIR

# 5. Configure appsettings
echo "⚙️  Configuring appsettings..."
cat > $KASSERPRO_DIR/appsettings.json << 'EOF'
{
  "Urls": "http://0.0.0.0:5243",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AllowedOrigins": ["*"],
  "Jwt": {
    "Key": "CHANGE_THIS_TO_SECURE_KEY_AT_LEAST_64_CHARACTERS_LONG_FOR_PRODUCTION",
    "Issuer": "KasserPro",
    "Audience": "KasserPro",
    "ExpiryInHours": 24
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/var/www/kasserpro/kasserpro.db;Cache=Shared"
  },
  "ShiftAutoClose": {
    "Enabled": true,
    "HoursThreshold": 12
  }
}
EOF

echo "⚠️  IMPORTANT: Change JWT Key in appsettings.json!"
echo ""

# 6. Detect .NET location
echo "🔍 Detecting .NET Runtime..."
if [ -d "/usr/lib/dotnet" ]; then
    DOTNET_ROOT="/usr/lib/dotnet"
    DOTNET_CMD="/usr/lib/dotnet/dotnet"
elif [ -d "/usr/share/dotnet" ]; then
    DOTNET_ROOT="/usr/share/dotnet"
    DOTNET_CMD="/usr/share/dotnet/dotnet"
else
    DOTNET_CMD=$(which dotnet)
    DOTNET_ROOT=$(dirname $(dirname $DOTNET_CMD))
fi

echo "  DOTNET_ROOT: $DOTNET_ROOT"
echo "  DOTNET_CMD: $DOTNET_CMD"
echo ""

# Verify .NET
$DOTNET_CMD --version || {
    echo "❌ .NET not working properly"
    exit 1
}

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

# 8. Create Nginx configuration
echo "🌐 Creating Nginx configuration..."
cat > /etc/nginx/sites-available/kasserpro << EOF
# KasserPro Backend
server {
    listen 80;
    server_name $KASSERPRO_DOMAIN;

    # Security Headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;

    # Max upload size
    client_max_body_size 10M;

    # Proxy to Backend
    location / {
        proxy_pass http://localhost:$KASSERPRO_PORT;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }

    # SignalR WebSocket Support
    location /hubs/ {
        proxy_pass http://localhost:$KASSERPRO_PORT;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
        
        # WebSocket timeouts
        proxy_read_timeout 86400;
    }
}
EOF

# Enable site
ln -sf /etc/nginx/sites-available/kasserpro /etc/nginx/sites-enabled/

# Test Nginx
nginx -t

# 9. Start services
echo ""
echo "🔄 Starting services..."
systemctl daemon-reload
systemctl enable kasserpro
systemctl start kasserpro

sleep 3

# 10. Reload Nginx
systemctl reload nginx

# 11. Check status
echo ""
echo "📊 Service Status:"
systemctl status kasserpro --no-pager || true

echo ""
echo "🔌 Port Check:"
ss -tulpn | grep :$KASSERPRO_PORT || echo "⚠️  Port not listening"

echo ""
echo "📋 Recent Logs:"
journalctl -u kasserpro -n 20 --no-pager

echo ""
echo "====================================================="
if systemctl is-active --quiet kasserpro; then
    echo "✅ KasserPro deployed successfully!"
    echo ""
    echo "🌐 Access URLs:"
    echo "  - http://168.231.106.139:$KASSERPRO_PORT"
    echo "  - http://$KASSERPRO_DOMAIN (if DNS configured)"
    echo ""
    echo "🧪 Test API:"
    echo "  curl http://localhost:$KASSERPRO_PORT/api/health"
    echo ""
    echo "📊 View logs:"
    echo "  journalctl -u kasserpro -f"
    echo ""
    echo "🔒 Next steps:"
    echo "  1. Change JWT Key in $KASSERPRO_DIR/appsettings.json"
    echo "  2. Setup SSL: certbot --nginx -d $KASSERPRO_DOMAIN"
    echo "  3. Configure firewall: ufw allow $KASSERPRO_PORT/tcp"
else
    echo "❌ KasserPro failed to start"
    echo ""
    echo "Check logs: journalctl -u kasserpro -f"
fi
echo "====================================================="
