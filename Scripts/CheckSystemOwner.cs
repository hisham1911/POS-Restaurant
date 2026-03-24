using System;
using Microsoft.Data.Sqlite;

var dbPath = "backend/KasserPro.API/kasserpro.db";
var connectionString = $"Data Source={dbPath}";

using var connection = new SqliteConnection(connectionString);
connection.Open();

var command = connection.CreateCommand();
command.CommandText = @"
    SELECT Id, Name, Email, Role, TenantId, BranchId, IsActive 
    FROM Users 
    WHERE Role = 2
    ORDER BY Id";

Console.WriteLine("\n=== System Owner Users ===\n");

using var reader = command.ExecuteReader();
var found = false;

while (reader.Read())
{
    found = true;
    Console.WriteLine($"ID: {reader.GetInt32(0)}");
    Console.WriteLine($"Name: {reader.GetString(1)}");
    Console.WriteLine($"Email: {reader.GetString(2)}");
    Console.WriteLine($"Role: {reader.GetInt32(3)} (SystemOwner)");
    Console.WriteLine($"TenantId: {(reader.IsDBNull(4) ? "NULL" : reader.GetInt32(4).ToString())}");
    Console.WriteLine($"BranchId: {(reader.IsDBNull(5) ? "NULL" : reader.GetInt32(5).ToString())}");
    Console.WriteLine($"IsActive: {reader.GetBoolean(6)}");
    Console.WriteLine("---");
}

if (!found)
{
    Console.WriteLine("❌ No SystemOwner users found!");
}
else
{
    Console.WriteLine("\n✅ SystemOwner check complete");
}
