# Login as admin
$loginBody = @{
    email = "admin@kasserpro.com"
    password = "Admin@123"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "http://localhost:5243/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    
    Write-Host "Login Response:"
    Write-Host "=" * 80
    $loginResponse | ConvertTo-Json -Depth 10
    Write-Host ""
    Write-Host "=" * 80
    
    if ($loginResponse.data.token) {
        Write-Host "`nToken received successfully!"
        Write-Host "User: $($loginResponse.data.user.name)"
        Write-Host "Email: $($loginResponse.data.user.email)"
        Write-Host "Role: $($loginResponse.data.user.role)"
    }
} catch {
    Write-Host "Error: $_"
    Write-Host $_.Exception.Message
}
