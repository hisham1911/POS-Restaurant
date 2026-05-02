# Test-CashRegisterScenarios.ps1
# Verifies Scenarios 1 & 2 via API calls and SQLite queries

$baseUrl = "http://localhost:5243"
$adminEmail = "admin@kasserpro.com"
$adminPassword = "Admin@123"

Write-Host "=== KasserPro Cash Register Test Suite ===" -ForegroundColor Cyan

# ── Helper Functions ─────────────────────────────────────────
function Invoke-Api($Method, $Path, $Body, $Headers = @{}) {
    $uri = "$baseUrl$Path"
    $args = @{
        Uri = $uri
        Method = $Method
        Headers = $Headers
        ContentType = "application/json"
        ErrorAction = "Stop"
    }
    if ($Body) { $args.Body = ($Body | ConvertTo-Json -Depth 4) }
    try {
        $resp = Invoke-RestMethod @args
        return $resp
    } catch {
        Write-Host "  API Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $reader.BaseStream.Position = 0
            $reader.DiscardBufferedData()
            $errBody = $reader.ReadToEnd()
            Write-Host "  Response: $errBody" -ForegroundColor Red
        }
        throw
    }
}

function Get-SqlitePath {
    $dbPath = "f:\POS\backend\KasserPro.API\KasserPro.db"
    if (Test-Path $dbPath) { return $dbPath }
    # Fallback: search for it
    $found = Get-ChildItem -Path "f:\POS\backend" -Filter "*.db" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { return $found.FullName }
    return $null
}

function Invoke-SqliteQuery($Sql) {
    $dbPath = Get-SqlitePath
    if (-not $dbPath) {
        Write-Host "  SQLite DB not found. Skipping DB verification." -ForegroundColor Yellow
        return $null
    }
    try {
        Add-Type -Path "C:\ProgramData\chocolatey\lib\SQLite\tools\sqlite-netFx46-static-binary-bundle-x64-2015-1.0.118.0\System.Data.SQLite.dll" -ErrorAction SilentlyContinue
    } catch {}
    try {
        $conn = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$dbPath;Version=3;")
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Sql
        $reader = $cmd.ExecuteReader()
        $rows = @()
        while ($reader.Read()) {
            $row = @{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $row[$reader.GetName($i)] = $reader.GetValue($i)
            }
            $rows += $row
        }
        $reader.Close()
        $conn.Close()
        return $rows
    } catch {
        Write-Host "  DB Error: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

function Invoke-SqliteCount($Sql) {
    $result = Invoke-SqliteQuery $Sql
    if ($result -and $result.Count -gt 0) {
        foreach ($key in $result[0].Keys) {
            return [int]$result[0][$key]
        }
    }
    return 0
}

# ── Step 1: Login ──────────────────────────────────────────
Write-Host "`n[STEP 1] Logging in as Admin..." -ForegroundColor Yellow
$loginBody = @{
    email = $adminEmail
    password = $adminPassword
}
try {
    $loginResponse = Invoke-Api -Method POST -Path "/api/auth/login" -Body $loginBody
    $token = $loginResponse.data.token
    Write-Host "  Login successful. Token obtained." -ForegroundColor Green
} catch {
    Write-Host "  FAILED to login. Is the backend running on $baseUrl ?" -ForegroundColor Red
    exit 1
}
$authHeaders = @{ Authorization = "Bearer $token"; "X-Branch-Id" = "1" }

# Get current user info to find branch
Write-Host "`n  Getting current user info..." -ForegroundColor Yellow
try {
    $userResponse = Invoke-Api -Method GET -Path "/api/users/me" -Headers $authHeaders
    $branchId = $userResponse.data.branchId
    if ($branchId) {
        $authHeaders["X-Branch-Id"] = "$branchId"
        Write-Host "  Using branch ID: $branchId" -ForegroundColor Green
    } else {
        Write-Host "  No branch ID found, using default (1)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  Could not get user info, using default branch (1)" -ForegroundColor Yellow
}

# ── Step 2: Find or Create a Purchase Invoice ─────────────────
Write-Host "`n[STEP 2] Finding a purchase invoice..." -ForegroundColor Yellow
try {
    $invoicesResponse = Invoke-Api -Method GET -Path "/api/purchase-invoices?page=1&pageSize=5&status=Pending,PartiallyPaid" -Headers $authHeaders
    $invoice = $invoicesResponse.data.items | Select-Object -First 1
    if (-not $invoice) {
        Write-Host "  No pending/partially-paid invoices found. Trying any invoice..." -ForegroundColor Yellow
        $invoicesResponse = Invoke-Api -Method GET -Path "/api/purchase-invoices?page=1&pageSize=5" -Headers $authHeaders
        $invoice = $invoicesResponse.data.items | Select-Object -First 1
    }
    if (-not $invoice) {
        Write-Host "  No invoices found. Creating a test invoice..." -ForegroundColor Yellow
        # Get first supplier
        $suppliersResponse = Invoke-Api -Method GET -Path "/api/suppliers?page=1&pageSize=1" -Headers $authHeaders
        $supplier = $suppliersResponse.data.items | Select-Object -First 1
        if (-not $supplier) {
            Write-Host "  No suppliers found. Creating test supplier..." -ForegroundColor Yellow
            $supplierBody = @{ name = "Test Supplier"; phone = "01000000000"; email = "test@supplier.com"; address = "Test Address"; isActive = $true }
            $supplierResponse = Invoke-Api -Method POST -Path "/api/suppliers" -Body $supplierBody -Headers $authHeaders
            $supplier = $supplierResponse.data
            Write-Host "  Created supplier ID: $($supplier.id)" -ForegroundColor Green
        }
        # Get first product
        $productsResponse = Invoke-Api -Method GET -Path "/api/products?page=1&pageSize=1" -Headers $authHeaders
        $product = $productsResponse.data.items | Select-Object -First 1
        if (-not $product) {
            Write-Host "  FAILED: No products found and product creation is complex." -ForegroundColor Red
            exit 1
        }
        # Create draft purchase invoice
        $invoiceBody = @{
            supplierId = $supplier.id
            invoiceDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            dueDate = (Get-Date).AddDays(7).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            notes = "Test invoice for cash register verification"
            items = @(
                @{
                    productId = $product.id
                    quantity = 1
                    unitPrice = 100
                    discount = 0
                }
            )
        }
        $invoiceResponse = Invoke-Api -Method POST -Path "/api/purchase-invoices" -Body $invoiceBody -Headers $authHeaders
        $invoice = $invoiceResponse.data
        Write-Host "  Created invoice ID: $($invoice.id)" -ForegroundColor Green
    }
    $invoiceId = $invoice.id
    Write-Host "  Selected invoice ID: $invoiceId (Status: $($invoice.status), Due: $($invoice.totalAmount - $invoice.paidAmount))" -ForegroundColor Green
} catch {
    Write-Host "  FAILED to fetch or create invoices." -ForegroundColor Red
    exit 1
}

# ── Step 3: Record baseline count ────────────────────────────
Write-Host "`n[STEP 3] Recording baseline SupplierPayment count..." -ForegroundColor Yellow
$baselineCount = Invoke-SqliteCount "SELECT COUNT(*) FROM CashRegisterTransactions WHERE Type = 6;"
Write-Host "  Baseline SupplierPayment count: $baselineCount" -ForegroundColor Cyan

# ── Scenario 1: Cash Payment ────────────────────────────────
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "SCENARIO 1: Pay with CASH" -ForegroundColor Cyan
Write-Host "Expected: New SupplierPayment record in CashRegisterTransactions" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$cashPaymentBody = @{
    amount = 25.00
    paymentDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    method = "Cash"
    referenceNumber = "TEST-CASH-001"
    notes = "Automated test - cash payment should create cash register record"
}

try {
    Write-Host "`n  Sending cash payment ($($cashPaymentBody.amount) LE)..." -ForegroundColor Yellow
    $cashResponse = Invoke-Api -Method POST -Path "/api/purchase-invoices/$invoiceId/payments" -Body $cashPaymentBody -Headers $authHeaders
    Write-Host "  Payment result: isSuccess=$($cashResponse.isSuccess)" -ForegroundColor Green
    if (-not $cashResponse.isSuccess) {
        Write-Host "  Payment failed: $($cashResponse.message)" -ForegroundColor Red
    }
} catch {
    Write-Host "  FAILED to send cash payment." -ForegroundColor Red
}

# Verify
Write-Host "`n  Verifying database after CASH payment..." -ForegroundColor Yellow
Start-Sleep -Seconds 1
$afterCashCount = Invoke-SqliteCount "SELECT COUNT(*) FROM CashRegisterTransactions WHERE Type = 6;"
Write-Host "  SupplierPayment count after CASH: $afterCashCount" -ForegroundColor Cyan

$latestCashRecord = Invoke-SqliteQuery "SELECT Id, Type, Amount, Description, CreatedAt, BranchId, ShiftId FROM CashRegisterTransactions WHERE Type = 6 ORDER BY CreatedAt DESC LIMIT 1;"
if ($latestCashRecord) {
    $r = $latestCashRecord[0]
    Write-Host "  Latest SupplierPayment record:" -ForegroundColor Cyan
    Write-Host "    ID=$($r['Id']), Amount=$($r['Amount']), BranchId=$($r['BranchId']), CreatedAt=$($r['CreatedAt'])" -ForegroundColor Cyan
}

if ($afterCashCount -gt $baselineCount) {
    Write-Host "`n  SCENARIO 1: PASS - Cash payment created CashRegisterTransaction" -ForegroundColor Green
} else {
    Write-Host "`n  SCENARIO 1: FAIL - No new CashRegisterTransaction created" -ForegroundColor Red
}

# ── Scenario 2: Bank Transfer Payment ─────────────────────
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "SCENARIO 2: Pay with BANK TRANSFER" -ForegroundColor Cyan
Write-Host "Expected: NO new SupplierPayment record" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$bankPaymentBody = @{
    amount = 35.00
    paymentDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    method = "BankTransfer"
    referenceNumber = "TEST-BANK-001"
    notes = "Automated test - bank transfer should NOT create cash register record"
}

try {
    Write-Host "`n  Sending bank transfer payment ($($bankPaymentBody.amount) LE)..." -ForegroundColor Yellow
    $bankResponse = Invoke-Api -Method POST -Path "/api/purchase-invoices/$invoiceId/payments" -Body $bankPaymentBody -Headers $authHeaders
    Write-Host "  Payment result: isSuccess=$($bankResponse.isSuccess)" -ForegroundColor Green
    if (-not $bankResponse.isSuccess) {
        Write-Host "  Payment failed: $($bankResponse.message)" -ForegroundColor Red
    }
} catch {
    Write-Host "  FAILED to send bank transfer payment." -ForegroundColor Red
}

# Verify
Write-Host "`n  Verifying database after BANK TRANSFER payment..." -ForegroundColor Yellow
Start-Sleep -Seconds 1
$afterBankCount = Invoke-SqliteCount "SELECT COUNT(*) FROM CashRegisterTransactions WHERE Type = 6;"
Write-Host "  SupplierPayment count after BANK: $afterBankCount" -ForegroundColor Cyan

$latestRecord = Invoke-SqliteQuery "SELECT Id, Type, Amount, Description, CreatedAt FROM CashRegisterTransactions WHERE Type = 6 ORDER BY CreatedAt DESC LIMIT 1;"
if ($latestRecord) {
    $r = $latestRecord[0]
    Write-Host "  Latest SupplierPayment record:" -ForegroundColor Cyan
    Write-Host "    ID=$($r['Id']), Amount=$($r['Amount']), CreatedAt=$($r['CreatedAt'])" -ForegroundColor Cyan
}

if ($afterBankCount -eq $afterCashCount) {
    Write-Host "`n  SCENARIO 2: PASS - Bank transfer did NOT create CashRegisterTransaction" -ForegroundColor Green
} else {
    Write-Host "`n  SCENARIO 2: FAIL - Unexpected CashRegisterTransaction was created" -ForegroundColor Red
}

# ── Summary ──────────────────────────────────────────────
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Baseline count:     $baselineCount" -ForegroundColor White
Write-Host "After CASH:         $afterCashCount" -ForegroundColor White
Write-Host "After BANK:         $afterBankCount" -ForegroundColor White
Write-Host "`nScenario 1 (Cash -> record created):  $(if($afterCashCount -gt $baselineCount){'PASS'}else{'FAIL'})" -ForegroundColor $(if($afterCashCount -gt $baselineCount){'Green'}else{'Red'})
Write-Host "Scenario 2 (Bank -> no record):     $(if($afterBankCount -eq $afterCashCount){'PASS'}else{'FAIL'})" -ForegroundColor $(if($afterBankCount -eq $afterCashCount){'Green'}else{'Red'})
Write-Host "`nDone." -ForegroundColor Cyan
