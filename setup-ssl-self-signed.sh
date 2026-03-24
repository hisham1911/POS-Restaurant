#!/bin/bash
# Setup Self-Signed SSL for KasserPro (for testing/internal use)
# Run on VPS: bash setup-ssl-self-signed.sh

set -e

SERVER_IP="168.231.106.139"
KASSERPRO_PORT="5243"
SSL_DIR="/etc/nginx/ssl"

echo "🔒 Setting up Self-Signed SSL for KasserPro"
echo "============================================="
echo "IP: $SERVER_IP"
echo ""

# 1. Create SSL directory
echo "📁 Creating SSL directory..."
mkdir -p $SSL_DIR
echo "✅ Directory created"
echo ""

# 2. Generate self-signed certificate
echo "🔐 Generating self-signed certificate..."
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout $SSL_DIR/kasserpro.key \
    -out $SSL_DIR/kasserpro.crt \
    -subj "/C=EG/ST=Cairo/L=Cairo/O=KasserPro/CN=$SERVER_IP"

echo "✅ Certificate generated (valid for 365 days)"
echo ""

# 3. Create Nginx configuration with SSL
echo "📝 Creating Nginx configuration..."
cat > /etc/nginx/sites-available/kasserpro-ssl << EOF
# KasserPro Backend API with Self-Signed SSL
server {
    listen 443 ssl http2;
    server_name $SERVER_IP;

    # Self-signed SSL certificates
    ssl_certificate $SSL_DIR/kasserpro.crt;
    ssl_certificate_key $SSL_DIR/kasserpro.key;
    
    # SSL configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;
    
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

# Keep HTTP for direct access
server {
    listen 80;
    server_name $SERVER_IP;

    location / {
        proxy_pass http://localhost:$KASSERPRO_PORT;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
    }
}
EOF

# Enable site
ln -sf /etc/nginx/sites-available/kasserpro-ssl /etc/nginx/sites-enabled/

echo "✅ Nginx configuration created"
echo ""

# 4. Test Nginx configuration
echo "🧪 Testing Nginx configuration..."
nginx -t

if [ $? -ne 0 ]; then
    echo "❌ Nginx configuration test failed!"
    exit 1
fi
echo "✅ Nginx configuration valid"
echo ""

# 5. Reload Nginx
echo "🔄 Reloading Nginx..."
systemctl reload nginx
echo "✅ Nginx reloaded"
echo ""

echo "============================================="
echo "✅ Self-Signed SSL Setup Complete!"
echo ""
echo "🌐 Your API is now available at:"
echo "   HTTP:  http://$SERVER_IP:5243"
echo "   HTTPS: https://$SERVER_IP (via Nginx)"
echo ""
echo "⚠️  Browser Warning:"
echo "   You'll see a security warning because the certificate"
echo "   is self-signed. Click 'Advanced' → 'Proceed anyway'"
echo ""
echo "💡 For production, use a real domain with Let's Encrypt"
echo "============================================="
