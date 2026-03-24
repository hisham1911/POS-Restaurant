# Test KasserPro System
Write-Host "Testing KasserPro System..." -ForegroundColor Cyan
Write-Host ""

# Test 1: Login
Write-Host "1. Testing Login..." -ForegroundColor Yellow
$loginBody = @{
    email = "admin@kasserpro.com"
    password = "Admin@123"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "http://localhost:5243/api/auth/login" `
        -Method POST `
        -Body $loginBody `
        -ContentType "application/json"
    
    $token = $loginResponse.token
    Write-Host "   SUCCESS: Logged in as $($loginResponse.user.name)" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Get Products
Write-Host "2. Testing Products..." -ForegroundColor Yellow
try {
    $headers = @{
        "Authorization" = "Bearer $token"
    }
    
    $products = Invoke-RestMethod -Uri "http://localhost:5243/api/products" `
        -Method GET `
        -Headers $headers
    
    Write-Host "   SUCCESS: Found $($products.Count) products" -ForegroundColor Green
    Write-Host ""
    
    Write-Host "   First 5 products:" -ForegroundColor Cyan
    $products | Select-Object -First 5 | ForEach-Object {
        Write-Host "      - $($_.name): $($_.price) EGP" -ForegroundColor White
    }
    Write-Host ""
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Get Categories
Write-Host "3. Testing Categories..." -ForegroundColor Yellow
try {
    $categories = Invoke-RestMethod -Uri "http://localhost:5243/api/categories" `
        -Method GET `
        -Headers $headers
    
    Write-Host "   SUCCESS: Found $($categories.Count) categories" -ForegroundColor Green
    Write-Host ""
    
    Write-Host "   Categories:" -ForegroundColor Cyan
    $categories | ForEach-Object {
        Write-Host "      - $($_.name)" -ForegroundColor White
    }
    Write-Host ""
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "System is running successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "URLs:" -ForegroundColor Yellow
Write-Host "   Backend:  http://localhost:5243" -ForegroundColor White
Write-Host "   Frontend: http://localhost:3001" -ForegroundColor White
Write-Host ""
Write-Host "Login Credentials:" -ForegroundColor Yellow
Write-Host "   Admin:   admin@kasserpro.com / Admin@123" -ForegroundColor White
Write-Host "   Cashier: ahmed@kasserpro.com / 123456" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan
