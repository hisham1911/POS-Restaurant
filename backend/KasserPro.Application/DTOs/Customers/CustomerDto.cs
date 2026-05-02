namespace KasserPro.Application.DTOs.Customers;

/// <summary>
/// Customer information DTO
/// </summary>
public class CustomerDto
{
    public int Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public int LoyaltyPoints { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastOrderAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Credit Sales Fields
    /// <summary>
    /// إجمالي مديونية العميل عبر جميع فروع المنشأة — وليس الفرع الحالي فقط.
    /// Tenant-wide, not branch-scoped.
    /// </summary>
    public decimal TotalDue { get; set; }

    /// <summary>
    /// مديونية العميل في الفرع الحالي فقط.
    /// Branch-scoped.
    /// </summary>
    public decimal BranchAmountDue { get; set; }

    public decimal CreditLimit { get; set; }
}

/// <summary>
/// Request to create a new customer
/// </summary>
public class CreateCustomerRequest
{
    /// <summary>
    /// Phone number (required, must be unique per tenant)
    /// </summary>
    public string Phone { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer name (optional)
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Email address (optional)
    /// </summary>
    public string? Email { get; set; }
    
    /// <summary>
    /// Address (optional)
    /// </summary>
    public string? Address { get; set; }
    
    /// <summary>
    /// Notes about the customer (optional)
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Credit limit for the customer (optional, defaults to 0 for unlimited)
    /// </summary>
    public decimal? CreditLimit { get; set; }
}

/// <summary>
/// Request to update an existing customer
/// </summary>
public class UpdateCustomerRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// Concurrency token for optimistic locking
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Minimal customer info for order attachment
/// </summary>
public class CustomerSummaryDto
{
    public int Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int LoyaltyPoints { get; set; }
    /// <summary>
    /// إجمالي مديونية العميل عبر جميع فروع المنشأة — وليس الفرع الحالي فقط.
    /// Tenant-wide, not branch-scoped.
    /// </summary>
    public decimal TotalDue { get; set; }
    public decimal CreditLimit { get; set; }
}
