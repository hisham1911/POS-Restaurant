# Test-Scenario3-UI.ps1
# Verifies Scenario 3: Cashier should NOT see Transfer/Reconcile buttons
# Admin SHOULD see Transfer/Reconcile buttons

$baseUrl = "http://localhost:3000"
$apiUrl = "http://localhost:5243"

Write-Host "=== Scenario 3: UI Permission Verification ===" -ForegroundColor Cyan

function Get-AuthToken($Email, $Password) {
    $body = @{ email = $Email; password = $Password } | ConvertTo-Json
    $resp = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method POST -Body $body -ContentType "application/json"
    return $resp.data.token
}

function Test-ButtonsVisible($Token, $ExpectedRole) {
    # We use Playwright to test this - write a temp test file and run it
    $testCode = @"
import { chromium } from 'playwright';

(async () => {
    const browser = await chromium.launch();
    const context = await browser.newContext();
    const page = await context.newPage();
    
    // Navigate to login
    await page.goto('$baseUrl/login');
    
    // Wait for page to load (title is "تاجر برو")
    await page.waitForSelector('input[type="email"]');
    
    // Login
    await page.locator('input[type="email"]').fill('$($Email)');
    await page.locator('input[type="password"]').fill('$($Password)');
    await page.getByRole('button', { name: /تسجيل الدخول/i }).click();
    
    // Wait for navigation (POS or settings)
    await page.waitForURL(/\\/(pos|settings|shift|cash-register)/, { timeout: 15000 });
    
    // Navigate to cash register
    await page.goto('$baseUrl/cash-register');
    await page.waitForSelector('text=/الخزينة/');
    
    // Check buttons
    const transferVisible = await page.getByRole('button', { name: /تحويل نقدي/i }).isVisible().catch(() => false);
    const reconcileVisible = await page.getByRole('button', { name: /مطابقة وإغلاق الشيفت/i }).isVisible().catch(() => false);
    const depositVisible = await page.getByRole('button', { name: /إيداع/i }).isVisible().catch(() => false);
    
    console.log(JSON.stringify({
        role: '$ExpectedRole',
        transferVisible,
        reconcileVisible,
        depositVisible
    }));
    
    await browser.close();
})();
"@
    
    $testFile = [System.IO.Path]::GetTempFileName() + ".mjs"
    Set-Content -Path $testFile -Value $testCode
    
    try {
        $output = & npx playwright test $testFile --reporter=line 2>&1
        Write-Host $output
    } finally {
        Remove-Item $testFile -ErrorAction SilentlyContinue
    }
}

# Write inline test
Write-Host "`n--- Testing Cashier (should NOT see Transfer/Reconcile) ---" -ForegroundColor Yellow
$testCode = @"
const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch();
    const page = await browser.newPage();
    
    await page.goto('$baseUrl/login');
    await page.waitForSelector('input[type="email"]');
    
    await page.locator('input[type="email"]').fill('ahmed@kasserpro.com');
    await page.locator('input[type="password"]').fill('123456');
    await page.getByRole('button', { name: /تسجيل الدخول/i }).click();
    
    await page.waitForTimeout(3000);
    
    await page.goto('$baseUrl/cash-register');
    await page.waitForTimeout(2000);
    
    const results = {
        role: 'Cashier',
        depositVisible: await page.getByRole('button', { name: /إيداع/i }).isVisible().catch(() => false),
        withdrawVisible: await page.getByRole('button', { name: /سحب/i }).isVisible().catch(() => false),
        transferVisible: await page.getByRole('button', { name: /تحويل نقدي/i }).isVisible().catch(() => false),
        reconcileVisible: await page.getByRole('button', { name: /مطابقة وإغلاق الشيفت/i }).isVisible().catch(() => false)
    };
    
    console.log('CASHIER_RESULT:' + JSON.stringify(results));
    await browser.close();
})();
"@

$testFile = "C:\Temp\test-cashier.mjs"
New-Item -ItemType Directory -Path "C:\Temp" -Force | Out-Null
Set-Content -Path $testFile -Value $testCode
$output = & node $testFile 2>&1
$cashierResult = ($output | Select-String "CASHIER_RESULT:") -replace "CASHIER_RESULT:", "" | ConvertFrom-Json
Write-Host "Cashier results: $($cashierResult | ConvertTo-Json)" -ForegroundColor Cyan

# Test Admin
Write-Host "`n--- Testing Admin (should see Transfer/Reconcile) ---" -ForegroundColor Yellow
$testCode = @"
const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch();
    const page = await browser.newPage();
    
    await page.goto('$baseUrl/login');
    await page.waitForSelector('input[type="email"]');
    
    await page.locator('input[type="email"]').fill('admin@kasserpro.com');
    await page.locator('input[type="password"]').fill('Admin@123');
    await page.getByRole('button', { name: /تسجيل الدخول/i }).click();
    
    await page.waitForTimeout(3000);
    
    await page.goto('$baseUrl/cash-register');
    await page.waitForTimeout(2000);
    
    const results = {
        role: 'Admin',
        depositVisible: await page.getByRole('button', { name: /إيداع/i }).isVisible().catch(() => false),
        withdrawVisible: await page.getByRole('button', { name: /سحب/i }).isVisible().catch(() => false),
        transferVisible: await page.getByRole('button', { name: /تحويل نقدي/i }).isVisible().catch(() => false),
        reconcileVisible: await page.getByRole('button', { name: /مطابقة وإغلاق الشيفت/i }).isVisible().catch(() => false)
    };
    
    console.log('ADMIN_RESULT:' + JSON.stringify(results));
    await browser.close();
})();
"@

$testFile = "C:\Temp\test-admin.mjs"
Set-Content -Path $testFile -Value $testCode
$output = & node $testFile 2>&1
$adminResult = ($output | Select-String "ADMIN_RESULT:") -replace "ADMIN_RESULT:", "" | ConvertFrom-Json
Write-Host "Admin results: $($adminResult | ConvertTo-Json)" -ForegroundColor Cyan

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "SCENARIO 3 RESULTS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nCashier:" -ForegroundColor Yellow
Write-Host "  Deposit visible: $($cashierResult.depositVisible)" -ForegroundColor $(if($cashierResult.depositVisible){'Green'}else{'Red'})
Write-Host "  Withdraw visible: $($cashierResult.withdrawVisible)" -ForegroundColor $(if($cashierResult.withdrawVisible){'Green'}else{'Red'})
Write-Host "  Transfer visible: $($cashierResult.transferVisible)" -ForegroundColor $(if(-not $cashierResult.transferVisible){'Green'}else{'Red'})
Write-Host "  Reconcile visible: $($cashierResult.reconcileVisible)" -ForegroundColor $(if(-not $cashierResult.reconcileVisible){'Green'}else{'Red'})

$cashierPass = $cashierResult.depositVisible -and $cashierResult.withdrawVisible -and (-not $cashierResult.transferVisible) -and (-not $cashierResult.reconcileVisible)
Write-Host "  SCENARIO 3 (Cashier): $(if($cashierPass){'PASS'}else{'FAIL'})" -ForegroundColor $(if($cashierPass){'Green'}else{'Red'})

Write-Host "`nAdmin:" -ForegroundColor Yellow
Write-Host "  Deposit visible: $($adminResult.depositVisible)" -ForegroundColor $(if($adminResult.depositVisible){'Green'}else{'Red'})
Write-Host "  Withdraw visible: $($adminResult.withdrawVisible)" -ForegroundColor $(if($adminResult.withdrawVisible){'Green'}else{'Red'})
Write-Host "  Transfer visible: $($adminResult.transferVisible)" -ForegroundColor $(if($adminResult.transferVisible){'Green'}else{'Red'})
Write-Host "  Reconcile visible: $($adminResult.reconcileVisible)" -ForegroundColor $(if($adminResult.reconcileVisible){'Green'}else{'Red'})

$adminPass = $adminResult.depositVisible -and $adminResult.withdrawVisible -and $adminResult.transferVisible -and $adminResult.reconcileVisible
Write-Host "  Admin verification: $(if($adminPass){'PASS'}else{'FAIL'})" -ForegroundColor $(if($adminPass){'Green'}else{'Red'})
