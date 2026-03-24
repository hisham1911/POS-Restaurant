# Test login directly with proper JSON
$body = @{
    email = "admin@kasserpro.com"
    password = "Admin@123"
} | ConvertTo-Json

Write-Host "Testing login with:"
Write-Host $body
Write-Host ""

try {
    $response = Invoke-WebRequest -Uri "http://localhost:5243/api/auth/login" `
        -Method Post `
        -Body $body `
        -ContentType "application/json" `
        -UseBasicParsing

    Write-Host "✅ Success! Status: $($response.StatusCode)"
    Write-Host $response.Content
}
catch {
    Write-Host "❌ Error: $($_.Exception.Message)"
    Write-Host "Status Code: $($_.Exception.Response.StatusCode.value__)"
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body:"
        Write-Host $responseBody
    }
}
