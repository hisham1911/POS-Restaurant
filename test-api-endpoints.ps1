$port = 5244
$base = "http://localhost:$port"
$body = '{"email":"admin@kasserpro.com","password":"Admin@123"}'

try {
    $login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body $body -ErrorAction Stop
    $token = $login.data.accessToken
    Write-Host "LOGIN: OK"
} catch {
    Write-Host "LOGIN FAILED: $($_.Exception.Message)"
    exit 1
}

$h = @{Authorization="Bearer $token"}
$endpoints = @('api/products', 'api/orders', 'api/orders/today', 'api/inventory/low-stock', 'api/categories', 'api/shifts/current')

foreach ($ep in $endpoints) {
    try {
        $r = Invoke-RestMethod -Uri "$base/$ep" -Headers $h -ErrorAction Stop
        $status = if ($r.success) { "OK" } else { "FAIL: $($r.message)" }
        Write-Host "${ep}: $status"
    } catch {
        $code = [int]$_.Exception.Response.StatusCode
        $errMsg = ""
        try {
            $sr = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($sr)
            $errMsg = $reader.ReadToEnd()
            if ($errMsg.Length -gt 300) { $errMsg = $errMsg.Substring(0,300) + "..." }
        } catch {}
        Write-Host "${ep}: ERROR $code - $errMsg"
    }
}
