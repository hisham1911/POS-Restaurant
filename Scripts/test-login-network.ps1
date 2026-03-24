# Test login from network IP
Write-Host "Testing login from network IP..." -ForegroundColor Cyan

# Get IP
$ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.*" } | Select-Object -First 1).IPAddress

Write-Host "Testing IP: $ip" -ForegroundColor Yellow

# Test localhost first
Write-Host "`nTest 1: Localhost" -ForegroundColor Yellow
$body = '{"email":"admin@kasserpro.com","password":"Admin@123"}'
try {
    $response = Invoke-RestMethod -Uri 'http://localhost:5243/api/auth/login' -Method POST -Body $body -ContentType 'application/json'
    Write-Host "✅ Localhost login SUCCESS!" -ForegroundColor Green
    Write-Host "Token: $($response.data.accessToken.Substring(0,20))..." -ForegroundColor White
} catch {
    Write-Host "❌ Localhost login FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test network IP
Write-Host "`nTest 2: Network IP ($ip)" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://${ip}:5243/api/auth/login" -Method POST -Body $body -ContentType 'application/json'
    Write-Host "✅ Network IP login SUCCESS!" -ForegroundColor Green
    Write-Host "Token: $($response.data.accessToken.Substring(0,20))..." -ForegroundColor White
} catch {
    Write-Host "❌ Network IP login FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nDone!" -ForegroundColor Cyan
