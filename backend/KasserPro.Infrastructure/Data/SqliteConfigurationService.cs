namespace KasserPro.Infrastructure.Data;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Data;

/// <summary>
/// P1 PRODUCTION: Configures SQLite for production use with WAL mode and proper timeouts
/// </summary>
public class SqliteConfigurationService
{
    private readonly ILogger<SqliteConfigurationService> _logger;

    public SqliteConfigurationService(ILogger<SqliteConfigurationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Applies production SQLite configuration
    /// </summary>
    public async Task ConfigureAsync(IDbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
        {
            _logger.LogWarning("Connection is not SqliteConnection, skipping SQLite configuration");
            return;
        }

        if (sqliteConnection.State != ConnectionState.Open)
        {
            await sqliteConnection.OpenAsync();
        }

        using var cmd = sqliteConnection.CreateCommand();

        var connectionStringBuilder = new SqliteConnectionStringBuilder(sqliteConnection.ConnectionString);
        var isEncryptedDatabase = !string.IsNullOrWhiteSpace(connectionStringBuilder.Password);

        // WAL offers better concurrency on plaintext SQLite.
        // SQLCipher deployments are more stable on DELETE journaling.
        cmd.CommandText = isEncryptedDatabase
            ? "PRAGMA journal_mode=DELETE;"
            : "PRAGMA journal_mode=WAL;";
        var journalMode = await cmd.ExecuteScalarAsync();

        if (!isEncryptedDatabase && journalMode?.ToString()?.ToUpper() == "WAL")
        {
            _logger.LogInformation("✓ SQLite journal_mode: WAL (Write-Ahead Logging enabled)");
        }
        else if (isEncryptedDatabase && journalMode?.ToString()?.ToUpper() == "DELETE")
        {
            _logger.LogInformation("✓ SQLite journal_mode: DELETE (SQLCipher compatibility mode)");
        }
        else
        {
            _logger.LogWarning("⚠ Failed to set expected journal mode, current mode: {Mode}", journalMode);
        }

        // P1: Set per-connection PRAGMAs
        cmd.CommandText = "PRAGMA busy_timeout=5000;";
        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("✓ SQLite busy_timeout: 5000ms");

        cmd.CommandText = "PRAGMA synchronous=NORMAL;";
        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("✓ SQLite synchronous: NORMAL");

        cmd.CommandText = "PRAGMA foreign_keys=ON;";
        await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("✓ SQLite foreign_keys: ON");

        // Verify configuration
        await VerifyConfigurationAsync(sqliteConnection);
    }

    /// <summary>
    /// Verifies SQLite configuration is applied correctly
    /// </summary>
    private async Task VerifyConfigurationAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();

        // Verify journal_mode
        cmd.CommandText = "PRAGMA journal_mode;";
        var journalMode = await cmd.ExecuteScalarAsync();

        // Verify foreign_keys
        cmd.CommandText = "PRAGMA foreign_keys;";
        var foreignKeys = await cmd.ExecuteScalarAsync();

        _logger.LogInformation(
            "SQLite Configuration Verified - journal_mode={JournalMode}, foreign_keys={ForeignKeys}",
            journalMode, foreignKeys);

        var isEncryptedDatabase = !string.IsNullOrWhiteSpace(new SqliteConnectionStringBuilder(connection.ConnectionString).Password);
        var expectedJournalMode = isEncryptedDatabase ? "DELETE" : "WAL";

        if (!string.Equals(journalMode?.ToString(), expectedJournalMode, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "⚠ PRODUCTION WARNING: journal_mode is {CurrentMode}, expected {ExpectedMode}",
                journalMode,
                expectedJournalMode);
        }
    }
}
