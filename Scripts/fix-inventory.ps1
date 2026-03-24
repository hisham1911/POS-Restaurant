# Simple PowerShell script to fix missing BranchInventory records
# Run with: .\fix-inventory.ps1

Write-Host "🔧 KasserPro Inventory Fix Script" -ForegroundColor Cyan
Write-Host "================================`n" -ForegroundColor Cyan

# Check if database exists
if (-not (Test-Path "kasserpro.db")) {
    Write-Host "❌ Database file 'kasserpro.db' not found!" -ForegroundColor Red
    Write-Host "Make sure you're running this from the project root directory." -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Database found: kasserpro.db`n" -ForegroundColor Green

# SQL to execute
$sql = @"
-- Check current state
SELECT '🔍 Products with StockQuantity > 0: ' || COUNT(*) FROM Products WHERE StockQuantity > 0 AND IsActive = 1;
SELECT '📦 Products in BranchInventory: ' || COUNT(DISTINCT ProductId) FROM BranchInventories;
SELECT '⚠️  Products missing from BranchInventory: ' || COUNT(*) FROM Products p WHERE p.IsActive = 1 AND p.TrackInventory = 1 AND NOT EXISTS (SELECT 1 FROM BranchInventories bi WHERE bi.ProductId = p.Id);

-- Show sample of missing products
SELECT '📋 Sample of products to fix:';
SELECT '  - ID: ' || p.Id || ', Name: ' || p.Name || ', Stock: ' || COALESCE(p.StockQuantity, 0)
FROM Products p
WHERE p.IsActive = 1 
  AND p.TrackInventory = 1
  AND NOT EXISTS (SELECT 1 FROM BranchInventories bi WHERE bi.ProductId = p.Id)
ORDER BY p.Name
LIMIT 5;

-- Create missing records
INSERT INTO BranchInventories (TenantId, BranchId, ProductId, Quantity, ReorderLevel, LastUpdatedAt, CreatedAt, UpdatedAt)
SELECT 
    p.TenantId,
    b.Id as BranchId,
    p.Id as ProductId,
    COALESCE(p.StockQuantity, 0) as Quantity,
    COALESCE(p.LowStockThreshold, 10) as ReorderLevel,
    COALESCE(p.LastStockUpdate, datetime('now')) as LastUpdatedAt,
    datetime('now') as CreatedAt,
    datetime('now') as UpdatedAt
FROM Products p
CROSS JOIN Branches b
WHERE p.IsActive = 1
  AND p.TrackInventory = 1
  AND p.TenantId = b.TenantId
  AND NOT EXISTS (
      SELECT 1 FROM BranchInventories bi 
      WHERE bi.ProductId = p.Id AND bi.BranchId = b.Id
  );

-- Verify
SELECT '✅ Products now in BranchInventory: ' || COUNT(DISTINCT ProductId) FROM BranchInventories;
SELECT '📊 Total BranchInventory records: ' || COUNT(*) FROM BranchInventories;

-- Summary by tenant
SELECT '📊 Summary: ' || t.Name || ' - ' || COUNT(DISTINCT bi.ProductId) || ' products, ' || COUNT(DISTINCT bi.BranchId) || ' branches, ' || SUM(bi.Quantity) || ' total stock'
FROM BranchInventories bi
JOIN Tenants t ON bi.TenantId = t.Id
GROUP BY t.Id, t.Name;
"@

# Save SQL to temp file
$sqlFile = "temp-fix-inventory.sql"
$sql | Out-File -FilePath $sqlFile -Encoding UTF8

Write-Host "🔧 Executing migration...`n" -ForegroundColor Cyan

# Try to find sqlite3
$sqlite3Paths = @(
    "sqlite3.exe",
    "C:\Program Files\SQLite\sqlite3.exe",
    "C:\sqlite\sqlite3.exe",
    "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\SQLite.SQLite_Microsoft.Winget.Source_8wekyb3d8bbwe\sqlite3.exe"
)

$sqlite3 = $null
foreach ($path in $sqlite3Paths) {
    if (Get-Command $path -ErrorAction SilentlyContinue) {
        $sqlite3 = $path
        break
    }
}

if ($sqlite3) {
    Write-Host "Using SQLite3: $sqlite3`n" -ForegroundColor Green
    & $sqlite3 kasserpro.db < $sqlFile
    Remove-Item $sqlFile
    Write-Host "`n✅ Migration completed!" -ForegroundColor Green
} else {
    Write-Host "⚠️  SQLite3 not found. Using manual SQL execution..." -ForegroundColor Yellow
    Write-Host "`nPlease run the following SQL manually:" -ForegroundColor Yellow
    Write-Host "--------------------------------------" -ForegroundColor Yellow
    Get-Content $sqlFile
    Write-Host "--------------------------------------`n" -ForegroundColor Yellow
    
    Write-Host "Or install SQLite3:" -ForegroundColor Cyan
    Write-Host "  winget install SQLite.SQLite" -ForegroundColor White
    Write-Host "  OR download from: https://www.sqlite.org/download.html`n" -ForegroundColor White
}

Write-Host "Done!" -ForegroundColor Green
