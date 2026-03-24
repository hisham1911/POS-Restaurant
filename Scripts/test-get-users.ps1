# Login as admin first
$loginBody = @{
    email = "admin@kasserpro.com"
    password = "Admin@123"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "http://localhost:5243/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"

Write-Host "Login successful! Token received."
Write-Host ""

# Get all users
$headers = @{
    "Authorization" = "Bearer $($loginResponse.data.token)"
}

$usersResponse = Invoke-RestMethod -Uri "http://localhost:5243/api/permissions/users" -Method Get -Headers $headers

Write-Host "=" * 100
Write-Host "All Users in System:"
Write-Host "=" * 100
Write-Host ""

foreach ($user in $usersResponse.data) {
    Write-Host "ID: $($user.id) | Name: $($user.name) | Email: $($user.email) | Role: $($user.role) | Active: $($user.isActive)"
}

Write-Host ""
Write-Host "=" * 100
Write-Host "Total Users: $($usersResponse.data.Count)"
Write-Host "=" * 100
