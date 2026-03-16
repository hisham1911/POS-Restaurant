namespace KasserPro.Domain.Entities;

using System.ComponentModel.DataAnnotations;
using KasserPro.Domain.Common;

/// <summary>
/// Customer entity for tracking customer information and purchase history.
/// Primary lookup is by Phone number (unique per tenant).
/// </summary>
public class Customer : BaseEntity
{
    public int TenantId { get; set; }

    /// <summary>
    /// Primary identifier - phone number (unique per tenant)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Whether customer is active (soft delete alternative)
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Loyalty points balance (for future loyalty program)
    /// </summary>
    public int LoyaltyPoints { get; set; } = 0;

    /// <summary>
    /// Denormalized: Total number of completed orders
    /// </summary>
    public int TotalOrders { get; set; } = 0;

    /// <summary>
    /// Denormalized: Total amount spent across all orders
    /// </summary>
    public decimal TotalSpent { get; set; } = 0;

    /// <summary>
    /// Last order timestamp for customer activity tracking
    /// </summary>
    public DateTime? LastOrderAt { get; set; }

    /// <summary>
    /// Denormalized: Total outstanding balance (credit/debt)
    /// Positive value = customer owes money
    /// </summary>
    public decimal TotalDue { get; set; } = 0;

    /// <summary>
    /// Maximum credit limit allowed for this customer (0 = no limit)
    /// </summary>
    public decimal CreditLimit { get; set; } = 0;

    /// <summary>
    /// Concurrency token for optimistic locking.
    /// Prevents lost updates on TotalDue, TotalSpent, LoyaltyPoints from concurrent operations.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
