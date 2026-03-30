namespace KasserPro.Infrastructure.Services;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

/// <summary>
/// P2: Validates data integrity after restore
/// Checks for schema/data mismatches that could cause runtime errors
/// </summary>
public class DataValidationService
{
    private readonly ILogger<DataValidationService> _logger;

    public DataValidationService(ILogger<DataValidationService> _logger)
    {
        this._logger = _logger;
    }

    /// <summary>
    /// Validates critical columns after restore
    /// Returns list of issues found (empty = all good)
    /// </summary>
    public async Task<List<DataValidationIssue>> ValidateRestoredDataAsync(string dbPath)
    {
        var issues = new List<DataValidationIssue>();

        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            // Check 1: Products.Price should be numeric
            issues.AddRange(await ValidateNumericColumnAsync(
                connection,
                "Products",
                "Price",
                "Product prices must be numeric"));

            // StockQuantity column removed - now using BranchInventory table

            // Check 2: Orders.Total should be numeric
            issues.AddRange(await ValidateNumericColumnAsync(
                connection,
                "Orders",
                "Total",
                "Order totals must be numeric"));

            // Check 4: Delete marked but might be confusing
            issues.AddRange(await CheckSoftDeleteColumnsAsync(connection));

            // Additional checks can be added here as needed

            if (issues.Count > 0)
            {
                _logger.LogWarning(
                    "Found {Count} data validation issues after restore: {Issues}",
                    issues.Count,
                    string.Join("; ", issues.Select(i => i.Message)));
            }

            return issues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating restored database");
            // Don't throw - let restore continue, but log the warning
            return issues;
        }
    }

    /// <summary>
    /// Checks if a column contains non-numeric values
    /// </summary>
    private async Task<List<DataValidationIssue>> ValidateNumericColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string errorMessage)
    {
        var issues = new List<DataValidationIssue>();

        try
        {
            using var command = connection.CreateCommand();

            // Check if column exists first
            command.CommandText = $"PRAGMA table_info({tableName})";
            using var reader = await command.ExecuteReaderAsync();

            bool columnExists = false;
            while (await reader.ReadAsync())
            {
                if (reader["name"].ToString() == columnName)
                {
                    columnExists = true;
                    break;
                }
            }

            if (!columnExists)
            {
                return issues; // Column doesn't exist, skip check
            }

            // Count non-numeric values in the column
            // In SQLite, CAST('abc' AS REAL) returns 0.0 (never NULL), so we can't use IS NOT NULL.
            // Instead: if the value is text, check if it matches a numeric pattern.
            // A text value that CAST to REAL gives 0.0 AND doesn't start with '0' (or is just '0') is non-numeric.
            command.CommandText = $@"
                SELECT COUNT(*) 
                FROM {tableName} 
                WHERE {columnName} IS NOT NULL 
                  AND typeof({columnName}) != 'real' 
                  AND typeof({columnName}) != 'integer'
                  AND typeof({columnName}) != 'null'
                  AND (
                    typeof({columnName}) != 'text'
                    OR (
                      typeof({columnName}) = 'text'
                      AND TRIM({columnName}) != ''
                      AND {columnName} NOT GLOB '[0-9]*'
                      AND {columnName} NOT GLOB '-[0-9]*'
                      AND {columnName} NOT GLOB '.[0-9]*'
                      AND {columnName} NOT GLOB '-.[0-9]*'
                    )
                  )";

            var result = await command.ExecuteScalarAsync();
            if (result is long count && count > 0)
            {
                issues.Add(new DataValidationIssue
                {
                    Severity = "WARNING",
                    Table = tableName,
                    Column = columnName,
                    Count = (int)count,
                    Message = $"{errorMessage} - found {count} non-numeric values in {tableName}.{columnName}"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating {Table}.{Column}", tableName, columnName);
        }

        return issues;
    }

    /// <summary>
    /// Checks for soft-deleted records that might cause issues
    /// </summary>
    private async Task<List<DataValidationIssue>> CheckSoftDeleteColumnsAsync(
        SqliteConnection connection)
    {
        var issues = new List<DataValidationIssue>();

        try
        {
            using var command = connection.CreateCommand();

            // Get list of tables with IsDeleted columns
            command.CommandText = @"
                SELECT DISTINCT m.tbl_name
                FROM sqlite_master m
                WHERE EXISTS (
                    SELECT 1 FROM pragma_table_info(m.tbl_name)
                    WHERE name = 'IsDeleted'
                )";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var tableName = reader[0].ToString();
                
                // Count deleted records
                using var countCmd = connection.CreateCommand();
                countCmd.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE IsDeleted = 1";
                var deletedCount = await countCmd.ExecuteScalarAsync();

                if (deletedCount is long count && count > 0)
                {
                    _logger.LogInformation(
                        "Found {Count} soft-deleted records in {Table}",
                        count, tableName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking soft-delete columns");
        }

        return issues;
    }
}

/// <summary>
/// Represents a data validation issue found after restore
/// </summary>
public class DataValidationIssue
{
    public string Severity { get; set; } = "WARNING"; // WARNING, ERROR
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Message { get; set; } = string.Empty;
}
