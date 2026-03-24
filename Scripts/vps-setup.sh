#!/bin/bash
# KasserPro VPS Initial Setup Script
# Run this script on your VPS as root user
# Usage: bash vps-setup.sh

set -e

echo "🚀 KasserPro VPS Setup Script"
echo "============================="
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    echo "❌ Please run as root (use sudo)"
    exit 1
fi

# Detect Ubuntu version
if [ -f /etc/os-release ]; then
    . /etc/os-release
    OS=$NAME
    VER=$VERSION_ID
    echo "📋 Detected OS: $OS $VER"
else
    echo "❌ Cannot detect OS version"
    exit 1
fi

echo ""
echo "📦 Step 1: Updating system packages..."
apt update && apt upgrade -y

echo ""
echo "📦 Step 2: Installing .NET 8.0 Runtime..."

# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/${VER}/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET Runtime
apt update
apt install -y aspnetcore-runtime-8.0

# Verify installation
if command -v dotnet &> /dev/null; then
    echo "✅ .NET installed successfully: $(dotnet --version)"
else
    echo "❌ .NET installation failed"
    exit 1
fi

echo ""
echo "📦 Step 3: Installing Nginx..."
apt install -y nginx
systemctl enable nginx
systemctl start nginx
echo "✅ Nginx installed and started"

echo ""
echo "📦 Step 4: Installing Certbot (for SSL)..."
apt install -y certbot python3-certbot-nginx
echo "✅ Certbot installed"

echo ""
echo "📦 Step 5: Installing utilities..."
apt install -y unzip curl wget htop
echo "✅ Utilities installed"

echo ""
echo "👤 Step 6: Creating application user..."
if id "kasserpro" &>/dev/null; then
    echo "⚠️  User 'kasserpro' already exists"
else
    useradd -m -s /bin/bash kasserpro
    echo "✅ User 'kasserpro' created"
fi

echo ""
echo "📁 Step 7: Creating application directories..."
mkdir -p /var/www/kasserpro
mkdir -p /var/www/kasserpro/logs
mkdir -p /var/www/kasserpro/backups
chown -R kasserpro:kasserpro /var/www/kasserpro
echo "✅ Directories created"

echo ""
echo "🔥 Step 8: Configuring firewall..."
ufw --force enable
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
echo "✅ Firewall configured"

echo ""
echo "📝 Step 9: Creating systemd service..."
cat > /etc/systemd/system/kasserpro.service << 'EOF'
[Unit]
Description=KasserPro Backend API
After=network.target

[Service]
Type=notify
User=kasserpro
Group=kasserpro
WorkingDirectory=/var/www/kasserpro
ExecStart=/usr/bin/dotnet /var/www/kasserpro/KasserPro.API.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=kasserpro
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Security hardening
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/www/kasserpro

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
echo "✅ Systemd service created"

echo ""
echo "📝 Step 10: Creating Nginx configuration..."

# Get server IP
SERVER_IP=$(hostname -I | awk '{print $1}')

cat > /etc/nginx/sites-available/kasserpro << EOF
# KasserPro Backend API
server {
    listen 80;
    server_name $SERVER_IP _;

    # Security Headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;

    # Max upload size
    client_max_body_size 10M;

    # Proxy to Backend
    location / {
        proxy_pass http://localhost:5243;
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
        proxy_pass http://localhost:5243;
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
rm -f /etc/nginx/sites-enabled/default

# Test and reload Nginx
nginx -t
systemctl reload nginx
echo "✅ Nginx configured"

echo ""
echo "📝 Step 11: Creating update script..."
cat > /root/update-kasserpro.sh << 'EOF'
#!/bin/bash
set -e

echo "🔄 Starting KasserPro update..."

# Stop service
systemctl stop kasserpro

# Backup current version
if [ -d /var/www/kasserpro ]; then
    cp -r /var/www/kasserpro /var/www/kasserpro-backup-$(date +%Y%m%d-%H%M%S)
fi

# Backup database
if [ -f /var/www/kasserpro/kasserpro.db ]; then
    cp /var/www/kasserpro/kasserpro.db /var/www/kasserpro/backups/kasserpro-pre-update-$(date +%Y%m%d-%H%M%S).db
fi

# Extract new version
unzip -o /tmp/kasserpro-backend.zip -d /var/www/kasserpro/

# Set permissions
chown -R kasserpro:kasserpro /var/www/kasserpro
chmod +x /var/www/kasserpro/KasserPro.API

# Start service
systemctl start kasserpro

echo "✅ Update completed!"
systemctl status kasserpro --no-pager
EOF

chmod +x /root/update-kasserpro.sh
echo "✅ Update script created at /root/update-kasserpro.sh"

echo ""
echo "📝 Step 12: Creating daily backup cron job..."
(crontab -l 2>/dev/null; echo "0 2 * * * [ -f /var/www/kasserpro/kasserpro.db ] && cp /var/www/kasserpro/kasserpro.db /var/www/kasserpro/backups/kasserpro-\$(date +\\%Y\\%m\\%d-\\%H\\%M\\%S).db") | crontab -
echo "✅ Daily backup scheduled at 2:00 AM"

echo ""
echo "✅ VPS Setup completed successfully!"
echo ""
echo "📋 Next Steps:"
echo "1. Upload your application using: ./deploy-to-vps.ps1 -VpsIp $SERVER_IP"
echo "2. Configure your domain DNS to point to: $SERVER_IP"
echo "3. Setup SSL certificate: certbot --nginx -d yourdomain.com"
echo ""
echo "📊 Useful Commands:"
echo "   View logs:        journalctl -u kasserpro -f"
echo "   Restart service:  systemctl restart kasserpro"
echo "   Service status:   systemctl status kasserpro"
echo "   Update app:       /root/update-kasserpro.sh"
echo ""
echo "🌐 Your API will be available at: http://$SERVER_IP"
echo ""
