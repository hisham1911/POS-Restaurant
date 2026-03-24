#!/bin/bash
# Setup SSL for KasserPro using subdomain
# Run on VPS: bash setup-ssl-subdomain.sh

set -e

SUBDOMAIN="kasserpro.azinternational-eg.com"
KASSERPRO_PORT="5243"

echo "🔒 Setting up SSL for KasserPro"
echo "================================"
echo "Domain: $SUBDOMAIN"
echo ""

# 1. Check if certbot is installed
echo "📦 Checking Certbot..."
if ! command -v certbot &> /dev/null; then
    echo "Installing Certbot..."
    apt update
    apt install -y certbot python3-certbot-nginx
fi
echo "✅ Certbot ready"
echo ""

# 2. Create Nginx configuration with SSL
echo "📝 Creating Nginx configuration..."
cat > /etc/nginx/sites-available/kasserpro << EOF
# KasserPro Backend API
server {
    listen 80;
    server_name $SUBDOMAIN;
    
    # Redirect to HTTPS
    return 301 https://\$server_name\$request_uri;
}

server {
    listen 443 ssl http2;
    server_name $SUBDOMAIN;

    # SSL certificates (will be added by certbot)
    ssl_certificate /etc/letsencrypt/live/$SUBDOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$SUBDOMAIN/privkey.pem;
    
    # SSL configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;
    
    # Security Headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

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

echo "✅ Nginx configuration created"
echo ""

# 3. Test Nginx configuration
echo "🧪 Testing Nginx configuration..."
nginx -t

if [ $? -ne 0 ]; then
    echo "❌ Nginx configuration test failed!"
    exit 1
fi
echo "✅ Nginx configuration valid"
echo ""

# 4. Get SSL certificate
echo "🔐 Obtaining SSL certificate..."
echo ""
echo "⚠️  IMPORTANT: Make sure DNS is configured!"
echo "   Add A record: kasserpro.azinternational-eg.com → 168.231.106.139"
echo ""
read -p "Is DNS configured? (yes/no): " dns_ready

if [ "$dns_ready" != "yes" ]; then
    echo ""
    echo "⏸️  Paused. Configure DNS first:"
    echo "   1. Go to your domain registrar"
    echo "   2. Add A record:"
    echo "      Name: kasserpro"
    echo "      Type: A"
    echo "      Value: 168.231.106.139"
    echo "   3. Wait 5-10 minutes for DNS propagation"
    echo "   4. Run this script again"
    exit 0
fi

echo ""
echo "Obtaining certificate from Let's Encrypt..."
certbot --nginx -d $SUBDOMAIN --non-interactive --agree-tos --email admin@azinternational-eg.com

if [ $? -eq 0 ]; then
    echo "✅ SSL certificate obtained successfully!"
    echo ""
    
    # Reload Nginx
    systemctl reload nginx
    
    echo "================================"
    echo "✅ SSL Setup Complete!"
    echo ""
    echo "🌐 Your API is now available at:"
    echo "   https://$SUBDOMAIN"
    echo "   https://$SUBDOMAIN/swagger"
    echo "   https://$SUBDOMAIN/api/health"
    echo ""
    echo "🔒 Certificate will auto-renew"
    echo "================================"
else
    echo "❌ Failed to obtain SSL certificate"
    echo ""
    echo "Common issues:"
    echo "  1. DNS not configured correctly"
    echo "  2. Domain not pointing to this server"
    echo "  3. Port 80 blocked"
    echo ""
    echo "Check DNS: dig $SUBDOMAIN"
    exit 1
fi
