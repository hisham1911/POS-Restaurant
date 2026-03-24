# User Management API Testing Script
$baseUrl = "http://localhost:5243/api"
$adminEmail = "admin@kasserpro.com"
$adminPassword = "Admin@123"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "🧪 User Management API Testing" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Login as Admin
Write-Host "1️⃣  Logging in as Admin..." -ForegroundColor Yellow
$loginBody = @{
    email = $adminEmail
    password = $adminPassword
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $loginResponse.data.accessToken
    Write-Host "✅ Login successful!" -ForegroundColor Green
    Write-Host "   Token: $($token.Substring(0, 20))..." -ForegroundColor Gray
} catch {
    Write-Host "❌ Login failed: $_" -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# 2. Get All Users
Write-Host "`n2️⃣  Getting all users..." -ForegroundColor Yellow
try {
    $usersResponse = Invoke-RestMethod -Uri "$baseUrl/users" -Method Get -Headers $headers
    $users = $usersResponse.data
    Write-Host "✅ Found $($users.Count) users" -ForegroundColor Green
    foreach ($user in $users) {
        Write-Host "   - $($user.name) ($($user.email)) - Role: $($user.role) - Active: $($user.isActive)" -ForegroundColor Gray
    }
} catch {
    Write-Host "❌ Failed to get users: $_" -ForegroundColor Red
}

# 3. Create New User
Write-Host "`n3️⃣  Creating new test user..." -ForegroundColor Yellow
$newUserBody = @{
    name = "Test User $(Get-Random -Maximum 9999)"
    email = "testuser$(Get-Random -Maximum 9999)@test.com"
    password = "Test@123"
    phone = "01234567890"
    role = "Cashier"
} | ConvertTo-Json

try {
    $createResponse = Invoke-RestMethod -Uri "$baseUrl/users" -Method Post -Body $newUserBody -Headers $headers
    $newUser = $createResponse.data
    Write-Host "✅ User created successfully!" -ForegroundColor Green
    Write-Host "   ID: $($newUser.id)" -ForegroundColor Gray
    Write-Host "   Name: $($newUser.name)" -ForegroundColor Gray
    Write-Host "   Email: $($newUser.email)" -ForegroundColor Gray
    Write-Host "   Role: $($newUser.role)" -ForegroundColor Gray
    $testUserId = $newUser.id
} catch {
    Write-Host "❌ Failed to create user: $_" -ForegroundColor Red
    $testUserId = $null
}

# 4. Get Single User
if ($testUserId) {
    Write-Host "`n4️⃣  Getting user by ID..." -ForegroundColor Yellow
    try {
        $userResponse = Invoke-RestMethod -Uri "$baseUrl/users/$testUserId" -Method Get -Headers $headers
        $user = $userResponse.data
        Write-Host "✅ User retrieved successfully!" -ForegroundColor Green
        Write-Host "   Name: $($user.name)" -ForegroundColor Gray
        Write-Host "   Email: $($user.email)" -ForegroundColor Gray
    } catch {
        Write-Host "❌ Failed to get user: $_" -ForegroundColor Red
    }

    # 5. Update User
    Write-Host "`n5️⃣  Updating user..." -ForegroundColor Yellow
    $updateBody = @{
        name = "Updated Test User"
        email = $newUser.email
        phone = "01111111111"
        role = "Cashier"
    } | ConvertTo-Json

    try {
        $updateResponse = Invoke-RestMethod -Uri "$baseUrl/users/$testUserId" -Method Put -Body $updateBody -Headers $headers
        Write-Host "✅ User updated successfully!" -ForegroundColor Green
        Write-Host "   New Name: $($updateResponse.data.name)" -ForegroundColor Gray
        Write-Host "   New Phone: $($updateResponse.data.phone)" -ForegroundColor Gray
    } catch {
        Write-Host "❌ Failed to update user: $_" -ForegroundColor Red
    }

    # 6. Toggle User Status (Deactivate)
    Write-Host "`n6️⃣  Deactivating user..." -ForegroundColor Yellow
    $toggleBody = @{
        isActive = $false
    } | ConvertTo-Json

    try {
        $toggleResponse = Invoke-RestMethod -Uri "$baseUrl/users/$testUserId/toggle-status" -Method Patch -Body $toggleBody -Headers $headers
        Write-Host "✅ User deactivated successfully!" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to deactivate user: $_" -ForegroundColor Red
    }

    # 7. Toggle User Status (Activate)
    Write-Host "`n7️⃣  Reactivating user..." -ForegroundColor Yellow
    $toggleBody = @{
        isActive = $true
    } | ConvertTo-Json

    try {
        $toggleResponse = Invoke-RestMethod -Uri "$baseUrl/users/$testUserId/toggle-status" -Method Patch -Body $toggleBody -Headers $headers
        Write-Host "✅ User reactivated successfully!" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to reactivate user: $_" -ForegroundColor Red
    }

    # 8. Delete User
    Write-Host "`n8️⃣  Deleting test user..." -ForegroundColor Yellow
    try {
        $deleteResponse = Invoke-RestMethod -Uri "$baseUrl/users/$testUserId" -Method Delete -Headers $headers
        Write-Host "✅ User deleted successfully!" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to delete user: $_" -ForegroundColor Red
    }
}

# 9. Test Permissions API Integration
Write-Host "`n9️⃣  Testing Permissions API..." -ForegroundColor Yellow
try {
    $permissionsResponse = Invoke-RestMethod -Uri "$baseUrl/permissions/users" -Method Get -Headers $headers
    $cashiers = $permissionsResponse.data
    Write-Host "✅ Found $($cashiers.Count) cashiers with permissions" -ForegroundColor Green
    foreach ($cashier in $cashiers | Select-Object -First 3) {
        Write-Host "   - $($cashier.userName): $($cashier.permissions.Count) permissions" -ForegroundColor Gray
    }
} catch {
    Write-Host "❌ Failed to get permissions: $_" -ForegroundColor Red
}

# 10. Test Available Permissions
Write-Host "`n🔟 Getting available permissions..." -ForegroundColor Yellow
try {
    $availablePermsResponse = Invoke-RestMethod -Uri "$baseUrl/permissions/available" -Method Get -Headers $headers
    $permissions = $availablePermsResponse.data
    Write-Host "✅ Found $($permissions.Count) available permissions" -ForegroundColor Green
    $grouped = $permissions | Group-Object -Property groupAr
    foreach ($group in $grouped) {
        Write-Host "   - $($group.Name): $($group.Count) permissions" -ForegroundColor Gray
    }
} catch {
    Write-Host "❌ Failed to get available permissions: $_" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "✅ Testing Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan
