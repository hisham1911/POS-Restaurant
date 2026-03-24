#!/bin/bash
# KasserPro VPS Monitoring & Maintenance Script
# Usage: bash vps-monitor.sh [command]
# Commands: status, logs, backup, restart, update, health

COMMAND=${1:-status}
APP_DIR="/var/www/kasserpro"
BACKUP_DIR="$APP_DIR/backups"
SERVICE_NAME="kasserpro"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Helper functions
print_header() {
    echo -e "${BLUE}=================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}=================================${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

# Check if running as root
check_root() {
    if [ "$EUID" -ne 0 ]; then 
        print_error "Please run as root (use sudo)"
        exit 1
    fi
}

# Status command
cmd_status() {
    print_header "KasserPro Service Status"
    
    # Service status
    if systemctl is-active --quiet $SERVICE_NAME; then
        print_success "Service is running"
    else
        print_error "Service is not running"
    fi
    
    echo ""
    systemctl status $SERVICE_NAME --no-pager -l
    
    echo ""
    print_header "System Resources"
    
    # CPU and Memory
    echo "CPU Usage:"
    top -bn1 | grep "Cpu(s)" | sed "s/.*, *\([0-9.]*\)%* id.*/\1/" | awk '{print "  " 100 - $1"%"}'
    
    echo ""
    echo "Memory Usage:"
    free -h | awk 'NR==2{printf "  Used: %s / %s (%.2f%%)\n", $3, $2, $3*100/$2}'
    
    echo ""
    echo "Disk Usage:"
    df -h $APP_DIR | awk 'NR==2{printf "  Used: %s / %s (%s)\n", $3, $2, $5}'
    
    # Database size
    if [ -f "$APP_DIR/kasserpro.db" ]; then
        DB_SIZE=$(du -h "$APP_DIR/kasserpro.db" | cut -f1)
        echo ""
        echo "Database Size: $DB_SIZE"
    fi
    
    # Backup count
    if [ -d "$BACKUP_DIR" ]; then
        BACKUP_COUNT=$(ls -1 "$BACKUP_DIR"/*.db 2>/dev/null | wc -l)
        echo "Backups: $BACKUP_COUNT files"
    fi
}

# Logs command
cmd_logs() {
    print_header "KasserPro Logs (Press Ctrl+C to exit)"
    echo ""
    
    # Ask which logs to view
    echo "Select logs to view:"
    echo "1) Service logs (systemd)"
    echo "2) Application logs (file)"
    echo "3) Nginx access logs"
    echo "4) Nginx error logs"
    echo "5) All errors (last 50 lines)"
    read -p "Choice [1-5]: " choice
    
    case $choice in
        1)
            journalctl -u $SERVICE_NAME -f
            ;;
        2)
            if [ -d "$APP_DIR/logs" ]; then
                LATEST_LOG=$(ls -t "$APP_DIR/logs"/kasserpro-*.log 2>/dev/null | head -1)
                if [ -n "$LATEST_LOG" ]; then
                    tail -f "$LATEST_LOG"
                else
                    print_error "No application logs found"
                fi
            else
                print_error "Logs directory not found"
            fi
            ;;
        3)
            tail -f /var/log/nginx/access.log
            ;;
        4)
            tail -f /var/log/nginx/error.log
            ;;
        5)
            echo "=== Service Errors ==="
            journalctl -u $SERVICE_NAME -p err -n 20 --no-pager
            echo ""
            echo "=== Nginx Errors ==="
            tail -20 /var/log/nginx/error.log
            ;;
        *)
            print_error "Invalid choice"
            exit 1
            ;;
    esac
}

# Backup command
cmd_backup() {
    check_root
    print_header "Creating Backup"
    
    if [ ! -f "$APP_DIR/kasserpro.db" ]; then
        print_error "Database file not found"
        exit 1
    fi
    
    mkdir -p "$BACKUP_DIR"
    
    TIMESTAMP=$(date +%Y%m%d-%H%M%S)
    BACKUP_FILE="$BACKUP_DIR/kasserpro-manual-$TIMESTAMP.db"
    
    print_info "Creating backup..."
    cp "$APP_DIR/kasserpro.db" "$BACKUP_FILE"
    
    if [ $? -eq 0 ]; then
        BACKUP_SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
        print_success "Backup created: $BACKUP_FILE ($BACKUP_SIZE)"
        
        # Clean old backups (keep last 30)
        print_info "Cleaning old backups..."
        cd "$BACKUP_DIR"
        ls -t kasserpro-*.db | tail -n +31 | xargs -r rm
        REMAINING=$(ls -1 kasserpro-*.db 2>/dev/null | wc -l)
        print_info "Backups remaining: $REMAINING"
    else
        print_error "Backup failed"
        exit 1
    fi
}

# Restart command
cmd_restart() {
    check_root
    print_header "Restarting KasserPro"
    
    print_info "Stopping service..."
    systemctl stop $SERVICE_NAME
    
    sleep 2
    
    print_info "Starting service..."
    systemctl start $SERVICE_NAME
    
    sleep 3
    
    if systemctl is-active --quiet $SERVICE_NAME; then
        print_success "Service restarted successfully"
        systemctl status $SERVICE_NAME --no-pager
    else
        print_error "Service failed to start"
        echo ""
        print_info "Last 20 log lines:"
        journalctl -u $SERVICE_NAME -n 20 --no-pager
        exit 1
    fi
}

# Health check command
cmd_health() {
    print_header "Health Check"
    
    # Check if service is running
    if systemctl is-active --quiet $SERVICE_NAME; then
        print_success "Service is running"
    else
        print_error "Service is not running"
        exit 1
    fi
    
    # Check API endpoint
    print_info "Checking API endpoint..."
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5243/api/health)
    
    if [ "$HTTP_CODE" = "200" ]; then
        print_success "API is responding (HTTP $HTTP_CODE)"
    else
        print_error "API is not responding (HTTP $HTTP_CODE)"
        exit 1
    fi
    
    # Check database
    if [ -f "$APP_DIR/kasserpro.db" ]; then
        print_success "Database file exists"
        DB_SIZE=$(du -h "$APP_DIR/kasserpro.db" | cut -f1)
        print_info "Database size: $DB_SIZE"
    else
        print_error "Database file not found"
        exit 1
    fi
    
    # Check disk space
    DISK_USAGE=$(df -h $APP_DIR | awk 'NR==2{print $5}' | sed 's/%//')
    if [ "$DISK_USAGE" -lt 80 ]; then
        print_success "Disk space OK ($DISK_USAGE% used)"
    elif [ "$DISK_USAGE" -lt 90 ]; then
        print_warning "Disk space warning ($DISK_USAGE% used)"
    else
        print_error "Disk space critical ($DISK_USAGE% used)"
    fi
    
    # Check memory
    MEM_USAGE=$(free | awk 'NR==2{printf "%.0f", $3*100/$2}')
    if [ "$MEM_USAGE" -lt 80 ]; then
        print_success "Memory OK ($MEM_USAGE% used)"
    elif [ "$MEM_USAGE" -lt 90 ]; then
        print_warning "Memory warning ($MEM_USAGE% used)"
    else
        print_error "Memory critical ($MEM_USAGE% used)"
    fi
    
    echo ""
    print_success "All health checks passed"
}

# Update command
cmd_update() {
    check_root
    print_header "Updating KasserPro"
    
    if [ ! -f "/tmp/kasserpro-backend.zip" ]; then
        print_error "Update package not found at /tmp/kasserpro-backend.zip"
        print_info "Please upload the new version first:"
        print_info "  scp kasserpro-backend.zip root@your-vps:/tmp/"
        exit 1
    fi
    
    # Create backup before update
    print_info "Creating pre-update backup..."
    cmd_backup
    
    # Stop service
    print_info "Stopping service..."
    systemctl stop $SERVICE_NAME
    
    # Backup current version
    print_info "Backing up current version..."
    TIMESTAMP=$(date +%Y%m%d-%H%M%S)
    cp -r "$APP_DIR" "/var/www/kasserpro-backup-$TIMESTAMP"
    
    # Extract new version
    print_info "Extracting new version..."
    unzip -o /tmp/kasserpro-backend.zip -d "$APP_DIR/"
    
    # Set permissions
    print_info "Setting permissions..."
    chown -R kasserpro:kasserpro "$APP_DIR"
    chmod +x "$APP_DIR/KasserPro.API"
    
    # Start service
    print_info "Starting service..."
    systemctl start $SERVICE_NAME
    
    sleep 3
    
    if systemctl is-active --quiet $SERVICE_NAME; then
        print_success "Update completed successfully"
        systemctl status $SERVICE_NAME --no-pager
    else
        print_error "Service failed to start after update"
        print_warning "Rolling back..."
        
        # Rollback
        rm -rf "$APP_DIR"
        mv "/var/www/kasserpro-backup-$TIMESTAMP" "$APP_DIR"
        systemctl start $SERVICE_NAME
        
        print_error "Update failed and rolled back"
        exit 1
    fi
}

# Main
case $COMMAND in
    status)
        cmd_status
        ;;
    logs)
        cmd_logs
        ;;
    backup)
        cmd_backup
        ;;
    restart)
        cmd_restart
        ;;
    health)
        cmd_health
        ;;
    update)
        cmd_update
        ;;
    *)
        echo "KasserPro VPS Monitor & Maintenance"
        echo ""
        echo "Usage: $0 [command]"
        echo ""
        echo "Commands:"
        echo "  status   - Show service status and system resources"
        echo "  logs     - View application logs"
        echo "  backup   - Create manual database backup"
        echo "  restart  - Restart the service"
        echo "  health   - Run health checks"
        echo "  update   - Update application (requires package in /tmp)"
        echo ""
        exit 1
        ;;
esac
