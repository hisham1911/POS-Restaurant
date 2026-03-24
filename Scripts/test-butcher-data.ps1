# ============================================
# اختبار بيانات المجزر - Butcher Data Test
# ============================================

Write-Host "🔍 اختبار النظام..." -ForegroundColor Cyan
Write-Host ""

# Test 1: Login
Write-Host "1️⃣ اختبار تسجيل الدخول..." -ForegroundColor Yellow
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
    Write-Host "   ✅ تم تسجيل الدخول بنجاح" -ForegroundColor Green
    Write-Host "   👤 المستخدم: $($loginResponse.user.name)" -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "   ❌ فشل تسجيل الدخول: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Get Products
Write-Host "2️⃣ اختبار المنتجات..." -ForegroundColor Yellow
try {
    $headers = @{
        "Authorization" = "Bearer $token"
    }
    
    $products = Invoke-RestMethod -Uri "http://localhost:5243/api/products" `
        -Method GET `
        -Headers $headers
    
    Write-Host "   ✅ تم جلب المنتجات بنجاح" -ForegroundColor Green
    Write-Host "   📦 عدد المنتجات: $($products.Count)" -ForegroundColor Gray
    Write-Host ""
    
    # Show first 5 products
    Write-Host "   📋 أول 5 منتجات:" -ForegroundColor Cyan
    $products | Select-Object -First 5 | ForEach-Object {
        Write-Host "      • $($_.name) - $($_.price) جنيه" -ForegroundColor White
    }
    Write-Host ""
} catch {
    Write-Host "   ❌ فشل جلب المنتجات: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Get Categories
Write-Host "3️⃣ اختبار الفئات..." -ForegroundColor Yellow
try {
    $categories = Invoke-RestMethod -Uri "http://localhost:5243/api/categories" `
        -Method GET `
        -Headers $headers
    
    Write-Host "   ✅ تم جلب الفئات بنجاح" -ForegroundColor Green
    Write-Host "   📂 عدد الفئات: $($categories.Count)" -ForegroundColor Gray
    Write-Host ""
    
    # Show categories
    Write-Host "   📋 الفئات:" -ForegroundColor Cyan
    $categories | ForEach-Object {
        Write-Host "      • $($_.name)" -ForegroundColor White
    }
    Write-Host ""
} catch {
    Write-Host "   ❌ فشل جلب الفئات: $($_.Exception.Message)" -ForegroundColor Red
}

# Summary
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "تم الاختبار بنجاح!" -ForegroundColor Green
Write-Host ""
Write-Host "الروابط:" -ForegroundColor Yellow
Write-Host "   Backend:  http://localhost:5243" -ForegroundColor White
Write-Host "   Frontend: http://localhost:3001" -ForegroundColor White
Write-Host ""
Write-Host "بيانات الدخول:" -ForegroundColor Yellow
Write-Host "   Admin:   admin@kasserpro.com / Admin@123" -ForegroundColor White
Write-Host "   Cashier: ahmed@kasserpro.com / 123456" -ForegroundColor White
Write-Host "===========================================" -ForegroundColor Cyan
