namespace KasserPro.Infrastructure.Data;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Multi-Tenant Seeder - Creates multiple demo tenants for different business types
/// </summary>
public static class MultiTenantSeeder
{
    private static readonly string[] TargetTenantSlugs =
    {
        "al-amana-butcher",
        "supermarket"
    };

    private static readonly string[] ObsoleteDemoTenantSlugs =
    {
        "kasserpro",
        "home-appliances",
        "restaurant"
    };

    public static async Task SeedAsync(AppDbContext context)
    {
        Console.WriteLine("🔄 مزامنة بيانات المتاجر التجريبية...");

        await EnsureSystemOwnerAsync(context);
        await RemoveObsoleteDemoTenantsAsync(context);

        await SeedSupermarketAsync(context);
        await SupermarketSeeder.SeedAsync(context);
        await CloseOpenShiftsAsync(context, TargetTenantSlugs);

        Console.WriteLine("✅ تمت مزامنة بيانات المتاجر التجريبية.");
    }

    public static Task CloseOpenShiftsForTargetTenantsAsync(AppDbContext context)
    {
        return CloseOpenShiftsAsync(context, TargetTenantSlugs);
    }

    private static async Task RemoveObsoleteDemoTenantsAsync(AppDbContext context)
    {
        var obsoleteTenants = await context.Tenants
            .IgnoreQueryFilters()
            .Where(t => ObsoleteDemoTenantSlugs.Contains(t.Slug))
            .Select(t => new { t.Id, t.Slug })
            .ToListAsync();

        if (obsoleteTenants.Count == 0)
        {
            Console.WriteLine("   ✓ لا توجد متاجر تجريبية قديمة للحذف");
            return;
        }

        var obsoleteTenantIds = obsoleteTenants.Select(t => t.Id).ToList();
        var obsoleteSlugs = string.Join(", ", obsoleteTenants.Select(t => t.Slug));

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var obsoleteUserIds = await context.Users
                .IgnoreQueryFilters()
                .Where(u => u.TenantId.HasValue && obsoleteTenantIds.Contains(u.TenantId.Value))
                .Select(u => u.Id)
                .ToListAsync();

            var obsoleteOrderIds = await context.Orders
                .IgnoreQueryFilters()
                .Where(o => obsoleteTenantIds.Contains(o.TenantId))
                .Select(o => o.Id)
                .ToListAsync();

            var obsoleteInvoiceIds = await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(i => obsoleteTenantIds.Contains(i.TenantId))
                .Select(i => i.Id)
                .ToListAsync();

            var obsoleteExpenseIds = await context.Expenses
                .IgnoreQueryFilters()
                .Where(e => obsoleteTenantIds.Contains(e.TenantId))
                .Select(e => e.Id)
                .ToListAsync();

            var obsoleteSupplierIds = await context.Suppliers
                .IgnoreQueryFilters()
                .Where(s => obsoleteTenantIds.Contains(s.TenantId))
                .Select(s => s.Id)
                .ToListAsync();

            var obsoleteProductIds = await context.Products
                .IgnoreQueryFilters()
                .Where(p => obsoleteTenantIds.Contains(p.TenantId))
                .Select(p => p.Id)
                .ToListAsync();

            if (obsoleteOrderIds.Count > 0)
            {
                await context.OrderItems
                    .IgnoreQueryFilters()
                    .Where(i => obsoleteOrderIds.Contains(i.OrderId))
                    .ExecuteDeleteAsync();
            }

            await context.Payments
                .IgnoreQueryFilters()
                .Where(p => obsoleteTenantIds.Contains(p.TenantId))
                .ExecuteDeleteAsync();

            await context.RefundLogs
                .IgnoreQueryFilters()
                .Where(r => obsoleteTenantIds.Contains(r.TenantId))
                .ExecuteDeleteAsync();

            if (obsoleteInvoiceIds.Count > 0)
            {
                await context.PurchaseInvoicePayments
                    .IgnoreQueryFilters()
                    .Where(p => obsoleteInvoiceIds.Contains(p.PurchaseInvoiceId))
                    .ExecuteDeleteAsync();

                await context.PurchaseInvoiceItems
                    .IgnoreQueryFilters()
                    .Where(i => obsoleteInvoiceIds.Contains(i.PurchaseInvoiceId))
                    .ExecuteDeleteAsync();
            }

            if (obsoleteExpenseIds.Count > 0)
            {
                await context.ExpenseAttachments
                    .IgnoreQueryFilters()
                    .Where(a => obsoleteExpenseIds.Contains(a.ExpenseId))
                    .ExecuteDeleteAsync();
            }

            if (obsoleteSupplierIds.Count > 0 || obsoleteProductIds.Count > 0)
            {
                await context.SupplierProducts
                    .IgnoreQueryFilters()
                    .Where(sp => obsoleteSupplierIds.Contains(sp.SupplierId) || obsoleteProductIds.Contains(sp.ProductId))
                    .ExecuteDeleteAsync();
            }

            if (obsoleteUserIds.Count > 0)
            {
                await context.UserPermissions
                    .IgnoreQueryFilters()
                    .Where(up => obsoleteUserIds.Contains(up.UserId))
                    .ExecuteDeleteAsync();
            }

            await context.CashRegisterTransactions
                .IgnoreQueryFilters()
                .Where(t => obsoleteTenantIds.Contains(t.TenantId))
                .ExecuteDeleteAsync();

            await context.DebtPayments
                .IgnoreQueryFilters()
                .Where(dp => obsoleteTenantIds.Contains(dp.TenantId))
                .ExecuteDeleteAsync();

            await context.StockMovements
                .IgnoreQueryFilters()
                .Where(sm => obsoleteTenantIds.Contains(sm.TenantId))
                .ExecuteDeleteAsync();

            await context.InventoryTransfers
                .IgnoreQueryFilters()
                .Where(it => obsoleteTenantIds.Contains(it.TenantId))
                .ExecuteDeleteAsync();

            await context.BranchInventories
                .IgnoreQueryFilters()
                .Where(i => obsoleteTenantIds.Contains(i.TenantId))
                .ExecuteDeleteAsync();

            await context.BranchProductPrices
                .IgnoreQueryFilters()
                .Where(p => obsoleteTenantIds.Contains(p.TenantId))
                .ExecuteDeleteAsync();

            await context.Expenses
                .IgnoreQueryFilters()
                .Where(e => obsoleteTenantIds.Contains(e.TenantId))
                .ExecuteDeleteAsync();

            await context.ExpenseCategories
                .IgnoreQueryFilters()
                .Where(c => obsoleteTenantIds.Contains(c.TenantId))
                .ExecuteDeleteAsync();

            await context.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(i => obsoleteTenantIds.Contains(i.TenantId))
                .ExecuteDeleteAsync();

            if (obsoleteOrderIds.Count > 0)
            {
                await context.Orders
                    .IgnoreQueryFilters()
                    .Where(o => obsoleteOrderIds.Contains(o.Id) && o.OriginalOrderId.HasValue)
                    .ExecuteDeleteAsync();
            }

            await context.Orders
                .IgnoreQueryFilters()
                .Where(o => obsoleteTenantIds.Contains(o.TenantId))
                .ExecuteDeleteAsync();

            await context.Shifts
                .IgnoreQueryFilters()
                .Where(s => obsoleteTenantIds.Contains(s.TenantId))
                .ExecuteDeleteAsync();

            await context.Suppliers
                .IgnoreQueryFilters()
                .Where(s => obsoleteTenantIds.Contains(s.TenantId))
                .ExecuteDeleteAsync();

            await context.Customers
                .IgnoreQueryFilters()
                .Where(c => obsoleteTenantIds.Contains(c.TenantId))
                .ExecuteDeleteAsync();

            await context.Products
                .IgnoreQueryFilters()
                .Where(p => obsoleteTenantIds.Contains(p.TenantId))
                .ExecuteDeleteAsync();

            await context.Categories
                .IgnoreQueryFilters()
                .Where(c => obsoleteTenantIds.Contains(c.TenantId))
                .ExecuteDeleteAsync();

            await context.AuditLogs
                .IgnoreQueryFilters()
                .Where(a => obsoleteTenantIds.Contains(a.TenantId))
                .ExecuteDeleteAsync();

            await context.Users
                .IgnoreQueryFilters()
                .Where(u => u.TenantId.HasValue && obsoleteTenantIds.Contains(u.TenantId.Value))
                .ExecuteDeleteAsync();

            await context.Branches
                .IgnoreQueryFilters()
                .Where(b => obsoleteTenantIds.Contains(b.TenantId))
                .ExecuteDeleteAsync();

            await context.Tenants
                .IgnoreQueryFilters()
                .Where(t => obsoleteTenantIds.Contains(t.Id))
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();
            Console.WriteLine($"   ✓ تم حذف المتاجر غير المطلوبة: {obsoleteSlugs}");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CloseOpenShiftsAsync(AppDbContext context, string[] tenantSlugs)
    {
        var targetTenantIds = await context.Tenants
            .AsNoTracking()
            .Where(t => tenantSlugs.Contains(t.Slug))
            .Select(t => t.Id)
            .ToListAsync();

        if (targetTenantIds.Count == 0)
        {
            return;
        }

        var openShifts = await context.Shifts
            .Where(s => targetTenantIds.Contains(s.TenantId) && !s.IsClosed)
            .ToListAsync();

        if (openShifts.Count == 0)
        {
            Console.WriteLine("   ✓ لا توجد ورديات مفتوحة في المتاجر المستهدفة");
            return;
        }

        foreach (var shift in openShifts)
        {
            var closeAt = shift.ClosedAt ?? shift.LastActivityAt;
            if (closeAt <= shift.OpenedAt)
            {
                var fallbackCloseAt = shift.OpenedAt.AddHours(8);
                closeAt = fallbackCloseAt;
            }

            shift.IsClosed = true;
            shift.ClosedAt = closeAt;
            shift.LastActivityAt = closeAt;
            shift.ExpectedBalance = shift.OpeningBalance + shift.TotalCash;

            if (shift.ClosingBalance == 0)
            {
                shift.ClosingBalance = shift.ExpectedBalance;
            }

            shift.Difference = shift.ClosingBalance - shift.ExpectedBalance;
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ تم إغلاق {openShifts.Count} وردية مفتوحة");
    }

    private static async Task EnsureSystemOwnerAsync(AppDbContext context)
    {
        if (!await context.Users.AnyAsync(u => u.Role == UserRole.SystemOwner))
        {
            var systemOwnerPassword = SeedSystemOwnerPasswordResolver.Resolve(out var passwordFromEnvironment);

            var systemOwner = new User
            {
                TenantId = null,
                BranchId = null,
                Name = "System Owner",
                Email = "owner@kasserpro.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(systemOwnerPassword),
                Role = UserRole.SystemOwner,
                IsActive = true
            };
            context.Users.Add(systemOwner);
            await context.SaveChangesAsync();
            Console.WriteLine(passwordFromEnvironment
                ? $"   ✓ System Owner password source: env {SeedSystemOwnerPasswordResolver.EnvironmentVariableName}"
                : "   ✓ System Owner password source: auto-generated for this seed run");
        }
    }

    // ============================================
    // TENANT 2: سوبر ماركت
    // ============================================
    private static async Task SeedSupermarketAsync(AppDbContext context)
    {
        if (await context.Tenants.AnyAsync(t => t.Slug == "supermarket"))
            return;

        Console.WriteLine("\n🛒 سوبر ماركت...");

        var tenant = new Tenant
        {
            Name = "سوبر ماركت الخير",
            NameEn = "Al-Kheir Supermarket",
            Slug = "supermarket",
            Currency = "EGP",
            Timezone = "Africa/Cairo",
            TaxRate = 14,
            IsTaxEnabled = true,
            IsActive = true,
            AllowNegativeStock = false,
            ReceiptFooterMessage = "شكراً لتسوقكم معنا",
            ReceiptShowLogo = true,
            ReceiptPaperSize = "80mm"
        };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "الفرع الرئيسي",
            Code = "BR001",
            Address = "شارع الهرم، الجيزة",
            Phone = "0244667788",
            DefaultTaxRate = 14,
            DefaultTaxInclusive = false,
            CurrencyCode = "EGP",
            IsActive = true
        };
        context.Branches.Add(branch);
        await context.SaveChangesAsync();

        var admin = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "كريم المدير",
            Email = "karim@supermarket.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = UserRole.Admin,
            IsActive = true
        };
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var categories = new List<Category>
        {
            new() { TenantId = tenant.Id, Name = "بقالة", NameEn = "Grocery", SortOrder = 1, IsActive = true },
            new() { TenantId = tenant.Id, Name = "مشروبات", NameEn = "Beverages", SortOrder = 2, IsActive = true },
            new() { TenantId = tenant.Id, Name = "منظفات", NameEn = "Cleaning", SortOrder = 3, IsActive = true }
        };
        context.Categories.AddRange(categories);
        await context.SaveChangesAsync();

        var products = new List<Product>
        {
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "أرز", NameEn = "Rice", Sku = "GRC001", Price = 45, Cost = 35, AverageCost = 35, TaxRate = 14, TaxInclusive = false, TrackInventory = true, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "سكر", NameEn = "Sugar", Sku = "GRC002", Price = 35, Cost = 28, AverageCost = 28, TaxRate = 14, TaxInclusive = false, TrackInventory = true, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[1].Id, Name = "عصير", NameEn = "Juice", Sku = "BEV001", Price = 15, Cost = 10, AverageCost = 10, TaxRate = 14, TaxInclusive = false, TrackInventory = true, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "صابون", NameEn = "Soap", Sku = "CLN001", Price = 20, Cost = 12, AverageCost = 12, TaxRate = 14, TaxInclusive = false, TrackInventory = true, IsActive = true }
        };
        context.Products.AddRange(products);
        await context.SaveChangesAsync();

        Console.WriteLine("   ✓ سوبر ماركت: تم");
    }

}
