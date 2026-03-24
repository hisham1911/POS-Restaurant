# KasserPro Logic Audit Script
# Check for common logical errors in code

Write-Host "Checking for logical errors..." -ForegroundColor Cyan

$errors = @()

# 1. Check for Product.StockQuantity usage (should use BranchInventory instead)
Write-Host "`n1. Checking Product.StockQuantity usage..." -ForegroundColor Yellow
$stockUsage = Select-String -Path "backend/**/*.cs" -Pattern "product\.StockQuantity\s*[\+\-\*\/]=" -Exclude "*Test*.cs" -ErrorAction SilentlyContinue
if ($stockUsage) {
    $errors += "WARNING: Product.StockQuantity used for updates (should use BranchInventory)"
    $stockUsage | ForEach-Object { Write-Host "   $($_.Filename):$($_.LineNumber)" -ForegroundColor Red }
}

# 2. Check for missing TenantId/BranchId in queries
Write-Host "`n2. Checking Multi-Tenancy..." -ForegroundColor Yellow
$missingTenant = Select-String -Path "backend/**/*Service.cs" -Pattern "\.Where\(" -ErrorAction SilentlyContinue | 
    Select-String -NotMatch "TenantId" | 
    Select-String -NotMatch "// No tenant filter needed"
if ($missingTenant.Count -gt 5) {
    $errors += "WARNING: Some queries may be missing TenantId filter"
}

# 3. Check for hardcoded IDs
Write-Host "`n3. Checking for hardcoded IDs..." -ForegroundColor Yellow
$hardcodedIds = Select-String -Path "backend/**/*Service.cs" -Pattern "(TenantId|BranchId|UserId)\s*=\s*\d+" -Exclude "*Seeder*.cs" -ErrorAction SilentlyContinue
if ($hardcodedIds) {
    $errors += "WARNING: Hardcoded IDs found (should use ICurrentUserService)"
    $hardcodedIds | ForEach-Object { Write-Host "   $($_.Filename):$($_.LineNumber)" -ForegroundColor Red }
}

# 4. Check for missing stock validation
Write-Host "`n4. Checking stock validation..." -ForegroundColor Yellow
$stockCheck = Select-String -Path "backend/**/*OrderService.cs" -Pattern "GetAvailableQuantityAsync" -ErrorAction SilentlyContinue
if (-not $stockCheck) {
    $errors += "ERROR: OrderService does not check available stock"
}

# 5. Check for AverageCost updates
Write-Host "`n5. Checking AverageCost updates..." -ForegroundColor Yellow
$avgCostUpdate = Select-String -Path "backend/**/*PurchaseInvoiceService.cs" -Pattern "AverageCost\s*=" -ErrorAction SilentlyContinue
if (-not $avgCostUpdate) {
    $errors += "WARNING: PurchaseInvoiceService may not update AverageCost"
}

# Summary
Write-Host "`n" + ("="*60) -ForegroundColor Cyan
Write-Host "AUDIT SUMMARY" -ForegroundColor Cyan
Write-Host ("="*60) -ForegroundColor Cyan

if ($errors.Count -eq 0) {
    Write-Host "No obvious logical errors found" -ForegroundColor Green
} else {
    Write-Host "Found $($errors.Count) potential issues:" -ForegroundColor Yellow
    $errors | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
}

Write-Host "`nRECOMMENDATIONS:" -ForegroundColor Cyan
Write-Host "   1. Review: LOGIC_TESTING_CHECKLIST.md"
Write-Host "   2. Run Unit Tests: dotnet test"
Write-Host "   3. Do Manual Testing for Critical Flows"
Write-Host "   4. Review Reports and verify numbers are logical"

Write-Host "`nAudit completed" -ForegroundColor Green
