# Test Credit Sale API
$baseUrl = "http://localhost:5243/api"

# Login first
$loginBody = @{
    email = "ahmed@kasserpro.com"
    password = "123456"
} | ConvertTo-Json

Write-Host "🔐 Logging in..." -ForegroundColor Cyan
$loginResponse = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method POST -Body $loginBody -ContentType "application/json"
$token = $loginResponse.data.token
Write-Host "✅ Logged in successfully" -ForegroundColor Green

# Get order 564
Write-Host "`n📦 Getting order 564..." -ForegroundColor Cyan
$headers = @{
    "Authorization" = "Bearer $token"
    "X-Branch-Id" = "1"
}

try {
    $order = Invoke-RestMethod -Uri "$baseUrl/orders/564" -Method GET -Headers $headers
    Write-Host "Order Total: $($order.data.total) ج.م" -ForegroundColor Yellow
    Write-Host "Customer ID: $($order.data.customerId)" -ForegroundColor Yellow
    Write-Host "Customer Name: $($order.data.customerName)" -ForegroundColor Yellow
} catch {
    Write-Host "❌ Failed to get order: $_" -ForegroundColor Red
}

# Try to complete with partial payment (should fail)
Write-Host "`n💰 Attempting credit sale (500 ج.م paid, rest on credit)..." -ForegroundColor Cyan
$completeBody = @{
    payments = @(
        @{
            method = "Cash"
            amount = 500
        }
    )
} | ConvertTo-Json

try {
    $result = Invoke-RestMethod -Uri "$baseUrl/orders/564/complete" -Method POST -Body $completeBody -Headers $headers -ContentType "application/json"
    Write-Host "✅ Order completed: $($result.message)" -ForegroundColor Green
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    Write-Host "❌ Error Code: $($errorResponse.errorCode)" -ForegroundColor Red
    Write-Host "❌ Error Message: $($errorResponse.message)" -ForegroundColor Red
    Write-Host "`nFull Response:" -ForegroundColor Yellow
    $errorResponse | ConvertTo-Json -Depth 5
}
