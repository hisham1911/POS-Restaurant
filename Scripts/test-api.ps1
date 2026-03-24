# KasserPro API Stress Test Script
# Tests all hardened financial validation rules

$baseUrl = "http://localhost:5243/api"
$token = ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "KasserPro API Hardening Test Suite" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Helper function to make API calls
function Invoke-ApiCall {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null,
        [string]$Description
    )
    
    Write-Host "Testing: $Description" -ForegroundColor Yellow
    
    $headers = @{
        "Content-Type" = "application/json"
    }
    
    if ($token) {
        $headers["Authorization"] = "Bearer $token"
    }
    
    try {
        $params = @{
            Uri = "$baseUrl$Endpoint"
            Method = $Method
            Headers = $headers
        }
        
        if ($Body) {
            $params["Body"] = ($Body | ConvertTo-Json -Depth 10)
        }
        
        $response = Invoke-RestMethod @params
        
        Write-Host "   OK - Status: Success" -ForegroundColor Green
        Write-Host "   Response: $($response | ConvertTo-Json -Compress)" -ForegroundColor Gray
        return $response
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $errorBody = $_.ErrorDetails.Message
        
        if ($statusCode -eq 400) {
            Write-Host "   EXPECTED - 400 Bad Request" -ForegroundColor Yellow
            Write-Host "   Error: $errorBody" -ForegroundColor Gray
        }
        elseif ($statusCode -eq 409) {
            Write-Host "   EXPECTED - 409 Conflict (Concurrency)" -ForegroundColor Yellow
            Write-Host "   Error: $errorBody" -ForegroundColor Gray
        }
        else {
            Write-Host "   FAIL - Status: $statusCode" -ForegroundColor Red
            Write-Host "   Error: $errorBody" -ForegroundColor Gray
        }
        
        return $null
    }
}

# Test 1: Login
Write-Host ""
Write-Host "Test 1: Authentication" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan

$loginResponse = Invoke-ApiCall -Method POST -Endpoint "/auth/login" `
    -Body @{
        email = "admin@kasserpro.com"
        password = "Admin@123"
    } `
    -Description "Login as admin"

if ($loginResponse -and $loginResponse.data.token) {
    $token = $loginResponse.data.token
    Write-Host "   Token acquired successfully" -ForegroundColor Green
}
else {
    Write-Host "   FAIL - Could not acquire token. Exiting." -ForegroundColor Red
    exit 1
}

# Test 2: Create Customer with Credit Limit
Write-Host ""
Write-Host "Test 2: Customer Management" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan

$customer = Invoke-ApiCall -Method POST -Endpoint "/customers" `
    -Body @{
        phone = "01234567890"
        name = "Test Customer"
        creditLimit = 1000
        isActive = $true
    } `
    -Description "Create customer with 1000 EGP credit limit"

$customerId = $customer.data.id

# Test 3: Open Shift
Write-Host ""
Write-Host "Test 3: Shift Management" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan

$shift = Invoke-ApiCall -Method POST -Endpoint "/shifts" `
    -Body @{
        openingBalance = 100
    } `
    -Description "Open new shift"

# Test 4: Valid Credit Sale (Within Limit)
Write-Host ""
Write-Host "Test 4: Valid Credit Sale" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan

$order1 = Invoke-ApiCall -Method POST -Endpoint "/orders" `
    -Body @{
        customerId = $customerId
        orderType = "DineIn"
        items = @(
            @{
                productId = 1
                quantity = 2
            }
        )
    } `
    -Description "Create order for customer"

if ($order1) {
    $completeResponse = Invoke-ApiCall -Method POST -Endpoint "/orders/$($order1.data.id)/complete" `
        -Body @{
            payments = @(
                @{
                    method = "Cash"
                    amount = 50
                }
            )
        } `
        -Description "Complete order with partial payment (credit sale)"
    
    if ($completeResponse -and $completeResponse.data.status -eq "Completed") {
        Write-Host "   SUCCESS - Order completed with credit" -ForegroundColor Green
    }
    else {
        Write-Host "   FAIL - Order should be completed" -ForegroundColor Red
    }
}

# Test 5: Over-Limit Credit Sale
Write-Host ""
Write-Host "Test 5: Over-Limit Credit Sale" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan

$order2 = Invoke-ApiCall -Method POST -Endpoint "/orders" `
    -Body @{
        customerId = $customerId
        orderType = "DineIn"
        items = @(
            @{
                productId = 1
                quantity = 50
            }
        )
    } `
    -Description "Create large order"

if ($order2) {
    $completeResponse2 = Invoke-ApiCall -Method POST -Endpoint "/orders/$($order2.data.id)/complete" `
        -Body @{
            payments = @(
                @{
                    method = "Cash"
                    amount = 10
                }
            )
        } `
        -Description "Try to complete with insufficient payment (exceeds credit limit)"
    
    if (!$completeResponse2) {
        Write-Host "   SUCCESS - Order rejected due to credit limit" -ForegroundColor Green
    }
    else {
        Write-Host "   FAIL - Order should be rejected" -ForegroundColor Red
    }
}

# Test 6: Overpayment Limit
Write-Host ""
Write-Host "Test 6: Overpayment Protection" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan

$order3 = Invoke-ApiCall -Method POST -Endpoint "/orders" `
    -Body @{
        orderType = "DineIn"
        items = @(
            @{
                productId = 1
                quantity = 1
            }
        )
    } `
    -Description "Create small order"

if ($order3) {
    $completeResponse3 = Invoke-ApiCall -Method POST -Endpoint "/orders/$($order3.data.id)/complete" `
        -Body @{
            payments = @(
                @{
                    method = "Cash"
                    amount = 10000
                }
            )
        } `
        -Description "Try to pay 10000 EGP for small order (overpayment)"
    
    if (!$completeResponse3) {
        Write-Host "   SUCCESS - Overpayment rejected (anti-money laundering)" -ForegroundColor Green
    }
    else {
        Write-Host "   FAIL - Overpayment should be rejected" -ForegroundColor Red
    }
}

# Test 7: Discount Validation
Write-Host ""
Write-Host "Test 7: Discount Validation" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan

$order4 = Invoke-ApiCall -Method POST -Endpoint "/orders" `
    -Body @{
        orderType = "DineIn"
        discountType = "percentage"
        discountValue = 150
        items = @(
            @{
                productId = 1
                quantity = 1
            }
        )
    } `
    -Description "Create order with 150% discount (should be clamped to 100%)"

if ($order4) {
    Write-Host "   INFO - Check if discount was clamped to 100%" -ForegroundColor Cyan
    Write-Host "   Discount Amount: $($order4.data.discountAmount)" -ForegroundColor Gray
}

# Test 8: Stock Validation
Write-Host ""
Write-Host "Test 8: Stock Validation" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan

$order5 = Invoke-ApiCall -Method POST -Endpoint "/orders" `
    -Body @{
        orderType = "DineIn"
        items = @(
            @{
                productId = 1
                quantity = 999999
            }
        )
    } `
    -Description "Try to order 999999 units (insufficient stock)"

if (!$order5) {
    Write-Host "   SUCCESS - Order rejected due to insufficient stock" -ForegroundColor Green
}
else {
    Write-Host "   WARNING - Order created (check if AllowNegativeStock is enabled)" -ForegroundColor Yellow
}

# Test 9: Concurrency Test
Write-Host ""
Write-Host "Test 9: Concurrency Protection" -ForegroundColor Cyan
Write-Host "----------------------------------------" -ForegroundColor Cyan

$customerData = Invoke-ApiCall -Method GET -Endpoint "/customers/$customerId" `
    -Description "Fetch customer data"

if ($customerData) {
    $oldRowVersion = $customerData.data.rowVersion
    
    # Update customer
    $update1 = Invoke-ApiCall -Method PUT -Endpoint "/customers/$customerId" `
        -Body @{
            phone = $customerData.data.phone
            name = "Updated Name 1"
            creditLimit = $customerData.data.creditLimit
            rowVersion = $oldRowVersion
        } `
        -Description "Update customer (first update)"
    
    # Try to update with old RowVersion
    $update2 = Invoke-ApiCall -Method PUT -Endpoint "/customers/$customerId" `
        -Body @{
            phone = $customerData.data.phone
            name = "Updated Name 2"
            creditLimit = $customerData.data.creditLimit
            rowVersion = $oldRowVersion
        } `
        -Description "Update customer with OLD RowVersion (should fail)"
    
    if (!$update2) {
        Write-Host "   SUCCESS - Concurrency conflict detected" -ForegroundColor Green
    }
    else {
        Write-Host "   FAIL - Concurrency check not working" -ForegroundColor Red
    }
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Suite Complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "   - Authentication: Tested" -ForegroundColor White
Write-Host "   - Credit Limit Validation: Tested" -ForegroundColor White
Write-Host "   - Overpayment Protection: Tested" -ForegroundColor White
Write-Host "   - Discount Clamping: Tested" -ForegroundColor White
Write-Host "   - Stock Validation: Tested" -ForegroundColor White
Write-Host "   - Concurrency Protection: Tested" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Review test results above" -ForegroundColor Gray
Write-Host "   2. Update Frontend to handle new validation rules" -ForegroundColor Gray
Write-Host "   3. Implement RowVersion handling in Frontend" -ForegroundColor Gray
Write-Host "   4. Add client-side validation for credit limits" -ForegroundColor Gray
Write-Host ""
