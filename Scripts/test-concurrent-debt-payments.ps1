# Concurrent Debt Payment Stress Test
# Tests 5 simultaneous payments to same customer

$baseUrl = "http://localhost:5243/api"
$customerId = 1  # Replace with actual customer ID
$token = "YOUR_AUTH_TOKEN"  # Replace with actual token

Write-Host "=== CONCURRENT DEBT PAYMENT STRESS TEST ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Get initial customer state
Write-Host "Step 1: Getting initial customer state..." -ForegroundColor Yellow
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

try {
    $customer = Invoke-RestMethod -Uri "$baseUrl/customers/$customerId" -Headers $headers -Method Get
    $initialDebt = $customer.data.totalDue
    Write-Host "Initial TotalDue: $initialDebt ج.م" -ForegroundColor Green
} catch {
    Write-Host "Error getting customer: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Prepare 5 concurrent payment requests
Write-Host ""
Write-Host "Step 2: Preparing 5 concurrent payments of 100 ج.م each..." -ForegroundColor Yellow

$payments = @(
    @{ amount = 100; paymentMethod = "Cash"; notes = "Payment 1" },
    @{ amount = 100; paymentMethod = "Cash"; notes = "Payment 2" },
    @{ amount = 100; paymentMethod = "Cash"; notes = "Payment 3" },
    @{ amount = 100; paymentMethod = "Cash"; notes = "Payment 4" },
    @{ amount = 100; paymentMethod = "Cash"; notes = "Payment 5" }
)

# Step 3: Execute concurrent payments
Write-Host ""
Write-Host "Step 3: Executing 5 concurrent payments..." -ForegroundColor Yellow

$jobs = @()
foreach ($payment in $payments) {
    $job = Start-Job -ScriptBlock {
        param($url, $customerId, $payment, $headers)
        
        $body = $payment | ConvertTo-Json
        try {
            $response = Invoke-RestMethod -Uri "$url/customers/$customerId/pay-debt" `
                -Headers $headers `
                -Method Post `
                -Body $body
            
            return @{
                Success = $response.success
                Message = $response.message
                AmountPaid = $payment.amount
                Error = $null
            }
        } catch {
            return @{
                Success = $false
                Message = $null
                AmountPaid = $payment.amount
                Error = $_.Exception.Message
            }
        }
    } -ArgumentList $baseUrl, $customerId, $payment, $headers
    
    $jobs += $job
}

# Wait for all jobs to complete
Write-Host "Waiting for all payments to complete..." -ForegroundColor Yellow
$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job

# Step 4: Analyze results
Write-Host ""
Write-Host "Step 4: Analyzing results..." -ForegroundColor Yellow
Write-Host ""

$successCount = ($results | Where-Object { $_.Success -eq $true }).Count
$failCount = ($results | Where-Object { $_.Success -eq $false }).Count
$totalPaid = ($results | Where-Object { $_.Success -eq $true } | Measure-Object -Property AmountPaid -Sum).Sum

Write-Host "Successful payments: $successCount" -ForegroundColor Green
Write-Host "Failed payments: $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "Total amount paid: $totalPaid ج.م" -ForegroundColor Green

# Step 5: Verify final state
Write-Host ""
Write-Host "Step 5: Verifying final customer state..." -ForegroundColor Yellow

Start-Sleep -Seconds 2  # Give database time to settle

try {
    $customerAfter = Invoke-RestMethod -Uri "$baseUrl/customers/$customerId" -Headers $headers -Method Get
    $finalDebt = $customerAfter.data.totalDue
    
    Write-Host ""
    Write-Host "=== RESULTS ===" -ForegroundColor Cyan
    Write-Host "Initial TotalDue:  $initialDebt ج.م" -ForegroundColor White
    Write-Host "Amount Paid:       $totalPaid ج.م" -ForegroundColor White
    Write-Host "Expected TotalDue: $($initialDebt - $totalPaid) ج.م" -ForegroundColor Yellow
    Write-Host "Actual TotalDue:   $finalDebt ج.م" -ForegroundColor $(if ($finalDebt -eq ($initialDebt - $totalPaid)) { "Green" } else { "Red" })
    
    Write-Host ""
    if ($finalDebt -eq ($initialDebt - $totalPaid)) {
        Write-Host "✅ TEST PASSED: No lost updates!" -ForegroundColor Green
        Write-Host "✅ All concurrent payments processed correctly" -ForegroundColor Green
    } else {
        Write-Host "❌ TEST FAILED: Lost updates detected!" -ForegroundColor Red
        Write-Host "❌ Expected: $($initialDebt - $totalPaid), Got: $finalDebt" -ForegroundColor Red
        Write-Host "❌ Lost amount: $(($initialDebt - $totalPaid) - $finalDebt) ج.م" -ForegroundColor Red
    }
    
    # Check for negative balance
    if ($finalDebt -lt 0) {
        Write-Host ""
        Write-Host "⚠️  WARNING: Negative balance detected!" -ForegroundColor Red
    }
    
} catch {
    Write-Host "Error verifying final state: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== TEST COMPLETE ===" -ForegroundColor Cyan
