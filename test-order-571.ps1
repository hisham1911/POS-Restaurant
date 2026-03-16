# Test Order 571 Complete API
$baseUrl = "http://localhost:5243"
$orderId = 571

# Get token (use admin credentials)
$loginBody = @{
    email = "admin@kasserpro.com"
    password = "Admin@123"
} | ConvertTo-Json

Write-Host "Logging in..." -ForegroundColor Cyan
$loginResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method POST -Body $loginBody -ContentType "application/json"
$token = $loginResponse.data.token
Write-Host "Token received" -ForegroundColor Green

# Get order details first
Write-Host "Getting order $orderId details..." -ForegroundColor Cyan
$headers = @{
    "Authorization" = "Bearer $token"
    "X-Branch-Id" = "1"
}

try {
    $order = Invoke-RestMethod -Uri "$baseUrl/api/orders/$orderId" -Method GET -Headers $headers
    Write-Host "Order Total: $($order.data.total) EGP" -ForegroundColor Yellow
    Write-Host "Customer ID: $($order.data.customerId)" -ForegroundColor Yellow
    Write-Host "Status: $($order.data.status)" -ForegroundColor Yellow
} catch {
    Write-Host "Failed to get order" -ForegroundColor Red
}

# Try to complete with credit payment
Write-Host "Attempting to complete order with credit payment..." -ForegroundColor Cyan
$completeBody = @{
    payments = @(
        @{
            method = "Credit"
            amount = 0
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders/$orderId/complete" -Method POST -Body $completeBody -ContentType "application/json" -Headers $headers
    Write-Host "Success: $($response.message)" -ForegroundColor Green
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $errorBody = $_.ErrorDetails.Message
    
    Write-Host "Error $statusCode" -ForegroundColor Red
    Write-Host "Raw Response:" -ForegroundColor Yellow
    Write-Host $errorBody -ForegroundColor White
    
    # Try to parse JSON
    try {
        $errorJson = $errorBody | ConvertFrom-Json
        Write-Host "Parsed Error:" -ForegroundColor Cyan
        Write-Host "  Success: $($errorJson.success)" -ForegroundColor White
        Write-Host "  Message: $($errorJson.message)" -ForegroundColor White
        Write-Host "  ErrorCode: $($errorJson.errorCode)" -ForegroundColor White
    } catch {
        Write-Host "Could not parse error as JSON" -ForegroundColor Yellow
    }
}
