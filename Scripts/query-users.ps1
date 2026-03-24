$db = "kasserpro.db"
$query = "SELECT Id, Name, Email, Role, IsActive FROM Users ORDER BY Role, Id"

$connection = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$db")
$connection.Open()

$command = $connection.CreateCommand()
$command.CommandText = $query

$reader = $command.ExecuteReader()

Write-Host "`nUsers in Database:"
Write-Host "=" * 80

while ($reader.Read()) {
    $id = $reader["Id"]
    $name = $reader["Name"]
    $email = $reader["Email"]
    $role = $reader["Role"]
    $isActive = $reader["IsActive"]
    
    Write-Host "ID: $id | Name: $name | Email: $email | Role: $role | Active: $isActive"
}

$reader.Close()
$connection.Close()
