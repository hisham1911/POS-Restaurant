$dbPath = "backend/KasserPro.API/kasserpro.db"

Add-Type -Path "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Data.SQLite\v4.0_1.0.118.0__db937bc2d44ff139\System.Data.SQLite.dll" -ErrorAction SilentlyContinue

try {
    $connection = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$dbPath")
    $connection.Open()
    
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT Id, Name, Email, Role, TenantId, BranchId, IsActive FROM Users WHERE Role = 2"
    
    $reader = $command.ExecuteReader()
    
    Write-Host "`n=== System Owner Users ===" -ForegroundColor Cyan
    Write-Host ""
    
    $found = $false
    while ($reader.Read()) {
        $found = $true
        Write-Host "ID: $($reader['Id'])" -ForegroundColor Yellow
        Write-Host "Name: $($reader['Name'])"
        Write-Host "Email: $($reader['Email'])"
        Write-Host "Role: $($reader['Role']) (SystemOwner)"
        Write-Host "TenantId: $($reader['TenantId'])"
        Write-Host "BranchId: $($reader['BranchId'])"
        Write-Host "IsActive: $($reader['IsActive'])"
        Write-Host "---"
    }
    
    if (-not $found) {
        Write-Host "No SystemOwner users found!" -ForegroundColor Red
    }
    
    $reader.Close()
    $connection.Close()
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "Trying alternative method..." -ForegroundColor Yellow
    
    # Alternative: Use sqlite3.exe if available
    if (Get-Command sqlite3 -ErrorAction SilentlyContinue) {
        sqlite3 $dbPath "SELECT Id, Name, Email, Role, TenantId, BranchId, IsActive FROM Users WHERE Role = 2"
    }
}
