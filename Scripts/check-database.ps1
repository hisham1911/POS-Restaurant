# Check if database exists and has users
$dbPath = "backend/KasserPro.API/kasserpro.db"

if (Test-Path $dbPath) {
    Write-Host "✅ Database exists at: $dbPath"
    $dbSize = (Get-Item $dbPath).Length / 1KB
    Write-Host "   Size: $([math]::Round($dbSize, 2)) KB"
    Write-Host ""
    
    # Try to read using .NET SQLite
    Add-Type -Path "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Data.dll"
    
    try {
        $connectionString = "Data Source=$dbPath;Version=3;Read Only=True;"
        $connection = New-Object System.Data.SQLite.SQLiteConnection($connectionString)
        $connection.Open()
        
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT Id, Name, Email, Role, IsActive FROM Users LIMIT 5"
        
        $reader = $command.ExecuteReader()
        
        Write-Host "Users in database:"
        Write-Host "=" * 80
        
        while ($reader.Read()) {
            Write-Host "ID: $($reader['Id']) | Name: $($reader['Name']) | Email: $($reader['Email']) | Role: $($reader['Role']) | Active: $($reader['IsActive'])"
        }
        
        $reader.Close()
        $connection.Close()
    }
    catch {
        Write-Host "❌ Could not read database with .NET SQLite"
        Write-Host "Error: $($_.Exception.Message)"
        Write-Host ""
        Write-Host "Trying alternative method..."
        
        # Alternative: Check if backend is running and query via API
        Write-Host "Please check the backend logs or use the test-get-users.ps1 script after logging in"
    }
}
else {
    Write-Host "❌ Database NOT found at: $dbPath"
    Write-Host "   The backend needs to run at least once to create the database"
}
