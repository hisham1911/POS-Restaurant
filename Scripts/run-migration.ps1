# PowerShell script to run SQLite migration
# Add SellingPrice column to PurchaseInvoiceItems table

$dbPath = "bin/Debug/net8.0/kasserpro.db"

if (-not (Test-Path $dbPath)) {
    Write-Host "Database not found at: $dbPath" -ForegroundColor Red
    Write-Host "Please build and run the application first to create the database." -ForegroundColor Yellow
    exit 1
}

Write-Host "Running migration on: $dbPath" -ForegroundColor Cyan

# SQL commands
$sql = @"
-- Check if column already exists
PRAGMA table_info(PurchaseInvoiceItems);

-- Add SellingPrice column if it doesn't exist
ALTER TABLE PurchaseInvoiceItems ADD COLUMN SellingPrice REAL NOT NULL DEFAULT 0;

-- Update existing records with product's current price
UPDATE PurchaseInvoiceItems 
SET SellingPrice = (
    SELECT Price 
    FROM Products 
    WHERE Products.Id = PurchaseInvoiceItems.ProductId
)
WHERE SellingPrice = 0;

-- Verify the update
SELECT COUNT(*) as TotalItems, 
       COUNT(CASE WHEN SellingPrice > 0 THEN 1 END) as ItemsWithSellingPrice
FROM PurchaseInvoiceItems;
"@

try {
    # Execute SQL using sqlite3
    $sql | sqlite3 $dbPath
    
    Write-Host "`n✅ Migration completed successfully!" -ForegroundColor Green
    Write-Host "SellingPrice column has been added to PurchaseInvoiceItems table." -ForegroundColor Green
}
catch {
    Write-Host "`n❌ Migration failed: $_" -ForegroundColor Red
    exit 1
}
