# Login as admin
$loginBody = @{
    email = "admin@kasserpro.com"
    password = "Admin@123"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "http://localhost:5243/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"

Write-Host "Login successful as: $($loginResponse.data.user.name)"
Write-Host ""

# Get cashiers with permissions
$headers = @{
    "Authorization" = "Bearer $($loginResponse.data.accessToken)"
}

$cashiersResponse = Invoke-RestMethod -Uri "http://localhost:5243/api/permissions/users" -Method Get -Headers $headers

Write-Host "All Cashiers in System:"
Write-Host "=" * 80
Write-Host ""

foreach ($cashier in $cashiersResponse.data) {
    Write-Host "ID: $($cashier.userId)"
    Write-Host "Name: $($cashier.userName)"
    Write-Host "Email: $($cashier.userEmail)"
    Write-Host "Active: $($cashier.isActive)"
    $permList = $cashier.permissions -join ", "
    Write-Host "Permissions: $permList"
    Write-Host ""
}

Write-Host "=" * 80
Write-Host "Total Cashiers: $($cashiersResponse.data.Count)"
