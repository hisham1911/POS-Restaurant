using Microsoft.Data.Sqlite;

var connectionString = "Data Source=KasserPro.API/kasserpro.db";
using var connection = new SqliteConnection(connectionString);
connection.Open();

var command = connection.CreateCommand();
command.CommandText = @"
    SELECT Id, Name, Email, Role, IsActive, PasswordHash 
    FROM Users 
    ORDER BY Role, Id";

using var reader = command.ExecuteReader();

Console.WriteLine("\n" + new string('=', 100));
Console.WriteLine("Users in Database:");
Console.WriteLine(new string('=', 100));

while (reader.Read())
{
    var id = reader.GetInt32(0);
    var name = reader.GetString(1);
    var email = reader.GetString(2);
    var role = reader.GetInt32(3);
    var isActive = reader.GetBoolean(4);
    var passwordHash = reader.GetString(5);
    
    var roleName = role switch
    {
        0 => "Cashier",
        1 => "Admin",
        2 => "SystemOwner",
        _ => "Unknown"
    };
    
    Console.WriteLine($"ID: {id,-3} | Name: {name,-25} | Email: {email,-30} | Role: {roleName,-12} | Active: {isActive}");
    Console.WriteLine($"      PasswordHash: {passwordHash.Substring(0, Math.Min(50, passwordHash.Length))}...");
    Console.WriteLine();
}

Console.WriteLine(new string('=', 100));
