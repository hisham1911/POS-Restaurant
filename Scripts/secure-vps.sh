#!/bin/bash
# Secure VPS - SSH Hardening + Fail2ban
# Run on VPS: bash secure-vps.sh

set -e

echo "🔒 VPS Security Hardening"
echo "========================="
echo ""

# 1. Install fail2ban
echo "📦 Installing fail2ban..."
apt update
apt install -y fail2ban

# 2. Configure fail2ban for SSH
echo "⚙️  Configuring fail2ban..."
cat > /etc/fail2ban/jail.local << 'EOF'
[DEFAULT]
bantime = 3600
findtime = 600
maxretry = 5
destemail = root@localhost
sendername = Fail2Ban
action = %(action_mwl)s

[sshd]
enabled = true
port = 22
filter = sshd
logpath = /var/log/auth.log
maxretry = 3
bantime = 7200
EOF

# 3. Start fail2ban
systemctl enable fail2ban
systemctl restart fail2ban

echo "✅ Fail2ban installed and configured"
echo ""

# 4. SSH Hardening (optional - be careful!)
echo "🔐 SSH Hardening Options:"
echo ""
echo "Current SSH settings:"
grep -E "^(PermitRootLogin|PasswordAuthentication|PubkeyAuthentication)" /etc/ssh/sshd_config || echo "Using defaults"
echo ""

read -p "Do you want to disable root password login? (y/N): " -n 1 -r
echo ""
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "⚠️  WARNING: Make sure you have SSH key access before proceeding!"
    read -p "Are you sure? (yes/no): " confirm
    
    if [ "$confirm" = "yes" ]; then
        # Backup original
        cp /etc/ssh/sshd_config /etc/ssh/sshd_config.backup
        
        # Disable root password login
        sed -i 's/^#*PermitRootLogin.*/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config
        
        # Ensure key authentication is enabled
        sed -i 's/^#*PubkeyAuthentication.*/PubkeyAuthentication yes/' /etc/ssh/sshd_config
        
        # Test configuration
        sshd -t
        
        if [ $? -eq 0 ]; then
            systemctl reload sshd
            echo "✅ SSH hardened - root password login disabled"
            echo "⚠️  Make sure you can login with SSH key!"
        else
            echo "❌ SSH config test failed - reverting"
            mv /etc/ssh/sshd_config.backup /etc/ssh/sshd_config
        fi
    fi
else
    echo "⏭️  Skipping SSH hardening"
fi

echo ""

# 5. Check fail2ban status
echo "📊 Fail2ban Status:"
fail2ban-client status sshd

echo ""
echo "========================="
echo "✅ Security hardening complete!"
echo ""
echo "📋 Summary:"
echo "  - Fail2ban installed and active"
echo "  - SSH brute-force protection enabled"
echo "  - Ban time: 2 hours after 3 failed attempts"
echo ""
echo "🔧 Useful commands:"
echo "  Check banned IPs: fail2ban-client status sshd"
echo "  Unban IP: fail2ban-client set sshd unbanip <IP>"
echo "  View logs: tail -f /var/log/fail2ban.log"
echo "========================="
