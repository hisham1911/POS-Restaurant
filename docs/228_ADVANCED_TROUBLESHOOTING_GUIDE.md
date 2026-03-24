# دليل حل المشاكل المتقدم - Advanced Troubleshooting Guide

**Target Audience:** System Administrators, Developers  
**Complexity Level:** Advanced  
**Last Updated:** February 25, 2026

---

## 🔍 Diagnostic Tools & Commands

### 1. Network Diagnostics

```powershell
# Get current IP address
Write-Host "=== Network Configuration ==="
ipconfig /all | Select-String "IPv4"

# Test connectivity to backend
Write-Host "`n=== Testing Backend Connectivity ==="
$ip = (Test-Connection -ComputerName (hostname) -Count 1).IPV4Address
Test-NetConnection -ComputerName $ip -Port 5243

# Check port availability
Write-Host "`n=== Port 5243 Status ==="
netstat -ano | findstr ":5243"
# Should show: LISTENING 0.0.0.0:5243

# Test DNS resolution
Write-Host "`n=== Hostname Resolution ==="
[System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName())
```

### 2. Backend Diagnostics

```bash
# View detailed logs
tail -f logs/kasserpro-2026-02-25.log

# Check specific error patterns
grep -i "error\|exception\|fail" logs/kasserpro-*.log

# Monitor in real-time
Get-Content logs/kasserpro-*.log -Wait -Tail 20

# Get process information
Get-Process dotnet | Select-Object Id, ProcessName, WorkingSet, CPU
```

### 3. Frontend Diagnostics

**Browser Console (F12):**

```javascript
// Check API URL resolution
console.log('Current origin:', window.location.origin);
console.log('Import.meta.env.DEV:', import.meta.env.DEV);

// Monitor network requests
fetch('/api/system/info')
  .then(r => r.json())
  .then(d => console.table(d))
  .catch(e => console.error('Error:', e));

// Check Redux store state
// (if Redux DevTools installed)
// Redux → Settings → Select "System" slice
```

### 4. Database Diagnostics

```powershell
# Check SQLite database status
$dbPath = "D:\مسح\POS\backend\KasserPro.API\kasserpro.db"
Get-Item $dbPath | Select-Object FullName, LastWriteTime, Length

# Check file permissions
icacls $dbPath

# Verify WAL mode is enabled
# Open with SQLite CLI or IDE and check:
PRAGMA journal_mode;  # Should return "wal"

# Check database integrity
PRAGMA integrity_check;  # Should return "ok"
```

---

## 🚨 Advanced Scenarios & Solutions

### Scenario 1: Intermittent "503 Unavailable" errors

**Symptom:**
- Health endpoint returns 503 sometimes
- Other endpoints work fine
- Issue occurs randomly, not consistently

**Root Causes:**
- Database connection pool exhausted
- Concurrent requests exceeding limits
- Memory pressure causing GC pauses

**Diagnostic Steps:**

```csharp
// Add detailed health check logging
[HttpGet("health")]
[AllowAnonymous]
public ActionResult<HealthCheckDto> Health()
{
    var startTime = DateTime.UtcNow;
    
    try
    {
        // Log entry
        _logger.LogInformation("Health check started at {Time}", startTime);
        
        // Check database
        using (var context = new AppDbContext())
        {
            var connectionTime = DateTime.UtcNow;
            var canConnect = context.Database.CanConnect();
            var connectionDuration = (DateTime.UtcNow - connectionTime).TotalMilliseconds;
            
            _logger.LogInformation(
                "Database check took {Duration}ms, result: {CanConnect}",
                connectionDuration,
                canConnect);
            
            if (!canConnect)
            {
                _logger.LogError("Database connection failed");
                return StatusCode(503, new HealthCheckDto 
                { 
                    Success = false, 
                    Status = "database_unavailable" 
                });
            }
        }
        
        return Ok(new HealthCheckDto 
        { 
            Success = true, 
            Status = "healthy" 
        });
    }
    catch (Exception ex)
    {
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogError(ex, "Health check failed after {Duration}ms", duration);
        return StatusCode(503, new HealthCheckDto 
        { 
            Success = false, 
            Status = "error" 
        });
    }
}
```

**Solutions:**

```csharp
// 1. Increase connection pool size
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(connectionString)
        .ConfigureWarnings(w => 
            w.Ignore(CoreEventId.ContextInitialized));
});

// In appsettings.json:
"ConnectionStrings": {
    "DefaultConnection": "Data Source=kasserpro.db;Mode=Wal;Max Pool Size=50"
}

// 2. Add request throttling
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("health-check", opt =>
    {
        opt.PermitLimit = 100;           // Max 100 requests
        opt.Window = TimeSpan.FromSeconds(1);  // Per second
        opt.QueueLimit = 0;              // No queue
    });
});

[HttpGet("health")]
[RequireRateLimitPolicy("health-check")]
public ActionResult<HealthCheckDto> Health() { ... }

// 3. Add circuit breaker pattern
var circuitBreaker = Policy
    .Handle<Exception>()
    .CircuitBreaker(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak: (outcome, duration) => 
        {
            _logger.LogWarning("Circuit breaker opened for {Duration}s", duration.TotalSeconds);
        });
```

### Scenario 2: CORS Errors from specific network

**Symptom:**
```
Access to XMLHttpRequest at 'http://192.168.1.100:5243/api/system/info' 
from origin 'http://192.168.1.50:5243' has been blocked by CORS policy
```

**Root Causes:**
- CORS middleware not applied
- Incorrect CORS policy configuration
- Middleware order is wrong

**Solution:**

```csharp
// CORRECT order in Program.cs:

var builder = WebApplication.CreateBuilder(args);

// 1. Add CORS service
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", cors =>
    {
        cors.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// 2. Build app
var app = builder.Build();

// 3. CRITICAL: Apply CORS BEFORE routing
app.UseCors("AllowFrontend");  // ← MUST be before UseRouting

// 4. Then routing
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// 5. Finally, map endpoints
app.MapControllers();

app.Run();
```

**Debug CORS:**

```bash
# Make preflight request to see CORS headers
curl -i -H "Origin: http://192.168.1.50:5243" \
     -H "Access-Control-Request-Method: GET" \
     http://192.168.1.100:5243/api/system/info

# Look for response headers:
# Access-Control-Allow-Origin: *
# Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS
# Access-Control-Allow-Headers: *
```

### Scenario 3: Frontend Can't Resolve API URL

**Symptom:**
- API calls fail with "Cannot reach /api"
- Console shows wrong API URL
- Works in dev, breaks in production

**Root Cause:**
- getApiUrl() function not working correctly in production build

**Debug & Fix:**

```typescript
// In baseApi.ts - add debug logging
const getApiUrl = (): string => {
  console.error('[DEBUG] Resolving API URL...');
  console.error('[DEBUG] DEV mode?', import.meta.env.DEV);
  console.error('[DEBUG] window.location.origin:', window.location.origin);
  console.error('[DEBUG] window.location.href:', window.location.href);
  console.error('[DEBUG] window.location.pathname:', window.location.pathname);

  if (import.meta.env.DEV) {
    console.error('[DEBUG] Using dev mode - relative /api');
    return "/api";
  }

  const resolved = `${window.location.origin}/api`;
  console.error('[DEBUG] Production mode - using:', resolved);
  return resolved;
};

const API_URL = getApiUrl();
console.error('[DEBUG] Final API_URL:', API_URL);
```

**Correct Implementation:**

```typescript
// Ensure bundler environment variable is set correctly
// vite.config.ts should have:

export default defineConfig(({ mode }) => ({
  define: {
    'import.meta.env.DEV': JSON.stringify(mode === 'development'),
  },
  // ...
}));

// Or less rely on import.meta.env, use runtime detection:
const getApiUrl = (): string => {
  // Check if running on same host as API
  const isProduction = !window.location.hostname.includes('localhost');
  
  if (!isProduction) {
    return "/api";  // Use relative path in dev
  }
  
  return `${window.location.origin}/api`;  // Use full URL in prod
};
```

### Scenario 4: High Latency on Network Access

**Symptom:**
- Primary device: response fast (< 10ms)
- Secondary device: response slow (> 500ms)
- Intermittent timeouts

**Root Causes:**
- Network congestion
- WiFi signal weak
- Router has traffic shaping
- Firewall rules causing delays

**Performance Test:**

```bash
# Measure latency
for i in {1..10}; do
  echo "Request $i:"
  curl -w "@curl_format.txt" -o /dev/null -s \
    http://192.168.1.100:5243/api/system/health
  echo
done

# curl_format.txt contents:
#     time_namelookup:     %{time_namelookup}s
#     time_connect:        %{time_connect}s
#     time_appconnect:     %{time_appconnect}s
#     time_pretransfer:    %{time_pretransfer}s
#     time_redirect:       %{time_redirect}s
#     time_starttransfer:  %{time_starttransfer}s
#     time_total:          %{time_total}s
```

**Solutions:**

1. **Reduce polling interval** (latency doesn't matter much):

```typescript
// If latency > 100ms, extend polling
useHealthQuery(undefined, {
  pollingInterval: latency > 100 ? 10000 : 5000,
});
```

2. **Implement connection pooling**:

```csharp
services.AddHttpClientFactory()
    .ConfigureHttpClientDefaults(defaults =>
    {
        defaults.ConfigureHttpClient(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            // Enable connection reuse
            client.DefaultRequestHeaders.Connection.Add("keep-alive");
        });
    });
```

3. **Cache responses longer**:

```typescript
getSystemInfo: builder.query<SystemInfoResponse, void>({
  query: () => '/system/info',
  keepUnusedDataFor: 600,  // 10 minutes instead of 5
}),
```

### Scenario 5: Mobile Device Connectivity Issues

**Symptom:**
- Works from Windows/Mac
- Fails from iPhone/Android
- Error: "Cannot reach server"

**Root Causes:**
- Mobile on different network
- Carrier grade NAT
- Guest WiFi isolated
- DNS not resolving

**Solutions:**

```
1. Check network isolation:
   - Router Settings → WiFi Settings
   - Find "AP Isolation" or "WiFi Isolation"
   - Set to DISABLED (off)
   - This allows devices to see each other

2. Use IP instead of hostname:
   - Instead of: http://desktop-abc/...
   - Use: http://192.168.1.100:5243

3. Test network connectivity:
   - Ping from mobile (if available)
   - Try traceroute to server IP
   - Check if both on same subnet

4. If using guest WiFi:
   - Connect primary and secondary to MAIN network
   - Not guest network

5. For corporate networks:
   - May need to whitelist port 5243
   - Contact IT department
   - Consider VPN for cross-network access
```

### Scenario 6: Database Lock Errors

**Symptom:**
```
error: SQLite error (5): 'database is locked'
```

**Root Causes:**
- Multiple processes accessing database
- WAL mode not enabled
- File locked by another program

**Solutions:**

```powershell
# Step 1: Check what's locking the file
Get-Process | Where-Object { $_.Name -eq "dotnet" }

# Step 2: Enable WAL mode (in appsettings)
"ConnectionStrings": {
    "DefaultConnection": "Data Source=kasserpro.db;Mode=Wal"
}

# Step 3: If still locked, restart application
Get-Process dotnet | Stop-Process -Force
Start-Sleep -Seconds 2

# Step 4: Delete WAL files if corrupted
Remove-Item "kasserpro.db-wal" -ErrorAction SilentlyContinue
Remove-Item "kasserpro.db-shm" -ErrorAction SilentlyContinue

# Step 5: Restart
cd backend/KasserPro.API
dotnet run
```

**Preventive Configuration:**

```csharp
services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    options.UseSqlite(connectionString, sqlite =>
    {
        // Enable WAL for better concurrency
        sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });
});

// In fluent API:
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.UseSqlite(
        _connectionString,
        x => x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
    );
}
```

---

## 📊 Log Analysis Guide

### Where to Find Logs

```
Primary log location:
  D:\مسح\POS\backend\KasserPro.API\logs\

Log file naming:
  kasserpro-2026-02-25.log  (today)
  kasserpro-2026-02-24.log  (yesterday)
  ...

Log retention: 30 days (configurable)
```

### Key Log Patterns

| Pattern | Meaning | Action |
|---------|---------|--------|
| `[INF]` | Information (normal) | Check for context |
| `[WRN]` | Warning (potential issue) | Investigate |
| `[ERR]` | Error (problem) | Fix immediately |
| `Device connected` | SignalR connection | Normal |
| `Failed to get` | API call failed | Check endpoint |
| `health_check_failed` | Backend issue | Restart |
| `Unknown user` | Auth failed | Check token |

### Example Log Analysis

```log
[10:30:45 INF] Application started
[10:30:46 INF] Database connected
[10:30:47 INF] Device D0001 connected
[10:30:48 WRN] Health check slow: 5234ms  ← Investigate!
[10:30:49 ERR] Failed to get system info: {exception}  ← Fix!

Debug context:
1. High latency (5.2 seconds) indicates performance issue
2. GetSystemInfo() failed - check network adapters
3. Likely cause: Network adapter enumeration is slow
```

---

## 🔧 Quick Fix Commands

### Fix #1: Port already in use

```powershell
# Find process using port 5243
$processId = (netstat -ano | findstr ":5243").Split()[5]

# Kill it
taskkill /PID $processId /F

# Or kill all dotnet processes
Get-Process dotnet | Stop-Process -Force
```

### Fix #2: Clear cache and rebuild

```powershell
# Frontend cache
cd frontend
Remove-Item node_modules -Recurse -Force
Remove-Item dist -Recurse -Force
npm install
npm run build

# Backend cache
cd ../backend/KasserPro.API
dotnet clean
dotnet build --configuration Release
```

### Fix #3: Reset to clean state

```powershell
# Stop application
Get-Process dotnet | Stop-Process -Force

# Delete generated files
Remove-Item "bin" -Recurse -Force
Remove-Item "obj" -Recurse -Force

# Rebuild
dotnet build --configuration Release
dotnet run
```

### Fix #4: Force firewall rule

```powershell
# Remove old rule
Remove-NetFirewallRule -DisplayName "KasserPro API" -ErrorAction SilentlyContinue

# Add new rule
New-NetFirewallRule `
  -DisplayName "KasserPro API" `
  -Direction Inbound `
  -Protocol TCP `
  -LocalPort 5243 `
  -Action Allow `
  -Enabled True
```

---

## 📞 Escalation Path

If basic troubleshooting doesn't work:

```
Level 1: Self-Service (You are here)
├─ Read this guide
├─ Try quick fixes
└─ Gather diagnostics

Level 2: Team Support
├─ Attach diagnostics
├─ Share error logs
├─ Provide system info
└─ Wait 1-2 hours

Level 3: Developer
├─ Code review
├─ Reproduce issue
├─ Fix in code
└─ Deploy patch

Level 4: External Support
├─ Microsoft support (if .NET issue)
├─ Hardware vendor (if network issue)
└─ ISP (if internet connectivity issue)
```

**Diagnostics to Collect:**

```powershell
# Create diagnostics package
$diagPath = "D:\KasserPro_Diagnostics_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $diagPath

# Collect data
@(
    "System Information",
    ipconfig /all,
    netstat -ano,
    tasklist,
    Get-Process dotnet | Select-Object *,
    Get-Content "logs/kasserpro-*.log" -Tail 100
) | Out-File $diagPath\diagnostics.txt

Write-Host "Diagnostics saved to: $diagPath"
```

---

**Created:** February 25, 2026  
**Status:** Advanced Troubleshooting Guide  
**Version:** 1.0
