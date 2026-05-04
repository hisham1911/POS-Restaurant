namespace KasserPro.Application.DTOs.Wallets;

public class WalletDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateWalletRequest
{
    public string Name { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; } = 0;
    public string? Notes { get; set; }
}

public class UpdateWalletRequest
{
    public string Name { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class WalletTransactionDto
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public string WalletName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WalletDepositWithdrawRequest
{
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public class WalletTransactionFilters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Type { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

public class PagedWalletTransactions
{
    public List<WalletTransactionDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
