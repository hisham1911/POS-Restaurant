namespace KasserPro.Infrastructure.Data;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public static class RestaurantDemoSeeder
{
    public const string TenantSlug = "am-salama";
    public const string DemoEmail = "demo@amsalama.com";
    public const string DemoPassword = "Demo@12345";

    private const string BranchCode = "SALAMA-001";

    public static async Task SeedAmSalamaAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == TenantSlug, cancellationToken);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = "مطعم عم سلامة",
                NameEn = "Am Salama Restaurant",
                Slug = TenantSlug
            };
            context.Tenants.Add(tenant);
        }

        tenant.Name = "مطعم عم سلامة";
        tenant.NameEn = "Am Salama Restaurant";
        tenant.Currency = "EGP";
        tenant.Timezone = "Africa/Cairo";
        tenant.IsActive = true;
        tenant.IsDeleted = false;
        tenant.TaxRate = 0;
        tenant.IsTaxEnabled = false;
        tenant.ServiceChargeRate = 0;
        tenant.AllowNegativeStock = false;
        tenant.ReceiptFooterMessage = "شكراً لزيارتكم مطعم عم سلامة";
        tenant.ReceiptPhoneNumber = "01020804678";
        tenant.ReceiptShowLogo = true;
        tenant.ReceiptPaperSize = "80mm";

        await context.SaveChangesAsync(cancellationToken);

        var branch = await context.Branches
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                b => b.TenantId == tenant.Id && b.Code == BranchCode,
                cancellationToken);

        if (branch is null)
        {
            branch = new Branch
            {
                TenantId = tenant.Id,
                Code = BranchCode
            };
            context.Branches.Add(branch);
        }

        branch.Name = "الفرع الرئيسي";
        branch.Address = "الفرع الرئيسي";
        branch.Phone = "01020804678";
        branch.DefaultTaxRate = 0;
        branch.DefaultTaxInclusive = false;
        branch.CurrencyCode = "EGP";
        branch.IsActive = true;
        branch.IsDeleted = false;

        await context.SaveChangesAsync(cancellationToken);

        await UpsertDemoAdminAsync(context, tenant.Id, branch.Id, cancellationToken);
        await UpsertRestaurantTablesAsync(context, tenant.Id, branch.Id, cancellationToken);

        var categoryIds = await UpsertCategoriesAsync(context, tenant.Id, cancellationToken);
        await UpsertProductsAsync(context, tenant.Id, categoryIds, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task UpsertDemoAdminAsync(
        AppDbContext context,
        int tenantId,
        int branchId,
        CancellationToken cancellationToken)
    {
        var admin = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == DemoEmail, cancellationToken);

        if (admin is not null && admin.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Demo email {DemoEmail} is already assigned to another tenant.");
        }

        if (admin is null)
        {
            admin = new User
            {
                Email = DemoEmail
            };
            context.Users.Add(admin);
        }

        admin.TenantId = tenantId;
        admin.BranchId = branchId;
        admin.Name = "حساب عرض عم سلامة";
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);
        admin.Role = UserRole.Admin;
        admin.IsActive = true;
        admin.IsDeleted = false;
        admin.UpdateSecurityStamp();
    }

    private static async Task UpsertRestaurantTablesAsync(
        AppDbContext context,
        int tenantId,
        int branchId,
        CancellationToken cancellationToken)
    {
        var existingTables = await context.RestaurantTables
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && t.BranchId == branchId)
            .ToDictionaryAsync(t => t.Number, cancellationToken);

        for (var tableNumber = 1; tableNumber <= 12; tableNumber++)
        {
            var number = tableNumber.ToString();
            if (!existingTables.TryGetValue(number, out var table))
            {
                table = new RestaurantTable
                {
                    TenantId = tenantId,
                    BranchId = branchId,
                    Number = number
                };
                context.RestaurantTables.Add(table);
            }

            table.SortOrder = tableNumber;
            table.Status = RestaurantTableStatus.Available;
            table.IsActive = true;
            table.IsDeleted = false;
        }
    }

    private static async Task<Dictionary<string, int>> UpsertCategoriesAsync(
        AppDbContext context,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var specs = new[]
        {
            new CategorySpec("pizza", "بيتزا إيطالي", "Italian Pizza", 1),
            new CategorySpec("crepe-regular", "كريب عادي", "Regular Crepe", 2),
            new CategorySpec("crepe-vip", "كريب VIP", "VIP Crepe", 3),
            new CategorySpec("crepe-mix", "كريب ميكس", "Mix Crepe", 4),
            new CategorySpec("crepe-meat", "كريب اللحمة", "Meat Crepe", 5),
            new CategorySpec("appetizers", "المقبلات", "Appetizers", 6)
        };

        var existingCategories = await context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ToDictionaryAsync(c => c.Name, cancellationToken);

        foreach (var spec in specs)
        {
            if (!existingCategories.TryGetValue(spec.Name, out var category))
            {
                category = new Category
                {
                    TenantId = tenantId,
                    Name = spec.Name
                };
                context.Categories.Add(category);
                existingCategories[spec.Name] = category;
            }

            category.NameEn = spec.NameEn;
            category.SortOrder = spec.SortOrder;
            category.IsActive = true;
            category.IsDeleted = false;
        }

        await context.SaveChangesAsync(cancellationToken);

        return specs.ToDictionary(
            spec => spec.Key,
            spec => existingCategories[spec.Name].Id);
    }

    private static async Task UpsertProductsAsync(
        AppDbContext context,
        int tenantId,
        IReadOnlyDictionary<string, int> categoryIds,
        CancellationToken cancellationToken)
    {
        var specs = BuildMenuProducts();
        var skus = specs.Select(spec => spec.Sku).ToList();
        var existingProducts = await context.Products
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.Sku != null && skus.Contains(p.Sku))
            .ToDictionaryAsync(p => p.Sku!, cancellationToken);

        foreach (var spec in specs)
        {
            if (!existingProducts.TryGetValue(spec.Sku, out var product))
            {
                product = new Product
                {
                    TenantId = tenantId,
                    Sku = spec.Sku
                };
                context.Products.Add(product);
            }

            product.CategoryId = categoryIds[spec.CategoryKey];
            product.Name = spec.Name;
            product.NameEn = spec.NameEn;
            product.Description = "صنف تجريبي من منيو مطعم عم سلامة";
            product.Price = spec.Price;
            product.Cost = 0;
            product.AverageCost = 0;
            product.TaxRate = 0;
            product.TaxInclusive = false;
            product.Type = ProductType.Service;
            product.Unit = UnitOfMeasure.Piece;
            product.TrackInventory = false;
            product.IsBatchTracked = false;
            product.LowStockThreshold = null;
            product.ReorderPoint = null;
            product.LastPurchasePrice = null;
            product.LastPurchaseDate = null;
            product.LastStockUpdate = null;
            product.Barcode = null;
            product.IsActive = true;
            product.IsDeleted = false;
        }
    }

    private static ProductSpec[] BuildMenuProducts() =>
    [
        new("AS-PIZ-001", "pizza", "بيتزا مارجرينا صغير", "Small Margherita Pizza", 80),
        new("AS-PIZ-002", "pizza", "بيتزا مارجرينا وسط", "Medium Margherita Pizza", 120),
        new("AS-PIZ-003", "pizza", "بيتزا مارجرينا كبير", "Large Margherita Pizza", 160),
        new("AS-PIZ-004", "pizza", "بيتزا خضروات صغير", "Small Vegetables Pizza", 95),
        new("AS-PIZ-005", "pizza", "بيتزا خضروات وسط", "Medium Vegetables Pizza", 135),
        new("AS-PIZ-006", "pizza", "بيتزا خضروات كبير", "Large Vegetables Pizza", 175),
        new("AS-PIZ-007", "pizza", "بيتزا رومي صغير", "Small Roumy Pizza", 100),
        new("AS-PIZ-008", "pizza", "بيتزا رومي وسط", "Medium Roumy Pizza", 145),
        new("AS-PIZ-009", "pizza", "بيتزا رومي كبير", "Large Roumy Pizza", 180),
        new("AS-PIZ-010", "pizza", "بيتزا سلامي صغير", "Small Salami Pizza", 100),
        new("AS-PIZ-011", "pizza", "بيتزا سلامي وسط", "Medium Salami Pizza", 145),
        new("AS-PIZ-012", "pizza", "بيتزا سلامي كبير", "Large Salami Pizza", 180),

        new("AS-CRR-001", "crepe-regular", "كريب بيبروني", "Pepperoni Crepe", 70),
        new("AS-CRR-002", "crepe-regular", "كريب ميكسات", "Mixes Crepe", 70),
        new("AS-CRR-003", "crepe-regular", "كريب تيربو هوا", "Turbo Hawa Crepe", 85),
        new("AS-CRR-004", "crepe-regular", "كريب تيربو مكسيكان", "Turbo Mexican Crepe", 85),
        new("AS-CRR-005", "crepe-regular", "كريب بومبون كراميل", "Bonbon Caramel Crepe", 80),
        new("AS-CRR-006", "crepe-regular", "كريب بطاطس", "Fries Crepe", 70),
        new("AS-CRR-007", "crepe-regular", "كريب كاتشب", "Ketchup Crepe", 50),
        new("AS-CRR-008", "crepe-regular", "كريب شيكولاتة", "Chocolate Crepe", 50),
        new("AS-CRR-009", "crepe-regular", "كريب كاتشب حار", "Spicy Ketchup Crepe", 55),
        new("AS-CRR-010", "crepe-regular", "كريب شيدر", "Cheddar Crepe", 70),
        new("AS-CRR-011", "crepe-regular", "كريب قطعتين جبنة", "Double Cheese Crepe", 70),
        new("AS-CRR-012", "crepe-regular", "إضافة موتزاريلا", "Mozzarella Extra", 15),

        new("AS-CRV-001", "crepe-vip", "كريب سجق اسكندراني", "Alexandrian Sausage Crepe", 85),
        new("AS-CRV-002", "crepe-vip", "كريب سجق بلدي", "Baladi Sausage Crepe", 95),
        new("AS-CRV-003", "crepe-vip", "كريب مدخن", "Smoked Crepe", 85),
        new("AS-CRV-004", "crepe-vip", "كريب ميجا", "Mega Crepe", 90),
        new("AS-CRV-005", "crepe-vip", "كريب بطاطمة", "Batata Crepe", 90),
        new("AS-CRV-006", "crepe-vip", "كريب سوبر ميكس", "Super Mix Crepe", 125),
        new("AS-CRV-007", "crepe-vip", "كريب سوبر كرانشي", "Super Crunchy Crepe", 110),
        new("AS-CRV-008", "crepe-vip", "كريب ميكس فارغ", "Empty Mix Crepe", 100),
        new("AS-CRV-009", "crepe-vip", "كريب ميكس حادقوقة", "Hadkouka Mix Crepe", 105),

        new("AS-CRM-001", "crepe-mix", "كريب استربس حار", "Spicy Strips Crepe", 95),
        new("AS-CRM-002", "crepe-mix", "كريب استربس بارد", "Regular Strips Crepe", 95),
        new("AS-CRM-003", "crepe-mix", "كريب كرسبي حار", "Spicy Crispy Crepe", 70),
        new("AS-CRM-004", "crepe-mix", "كريب كرسبي بارد", "Regular Crispy Crepe", 70),
        new("AS-CRM-005", "crepe-mix", "كريب ميكس لحوم", "Meat Mix Crepe", 125),

        new("AS-CMT-001", "crepe-meat", "كريب شيش طاووق", "Shish Tawook Crepe", 100),
        new("AS-CMT-002", "crepe-meat", "كريب فاهيتا فراخ", "Chicken Fajita Crepe", 100),
        new("AS-CMT-003", "crepe-meat", "كريب كريسبي رانش", "Crispy Ranch Crepe", 110),
        new("AS-CMT-004", "crepe-meat", "كريب زنجر رانش", "Zinger Ranch Crepe", 100),
        new("AS-CMT-005", "crepe-meat", "كريب بطاطس بس", "Fries Only Crepe", 55),

        new("AS-APP-001", "appetizers", "بطاطس مثلثة", "Triangle Fries", 100),
        new("AS-APP-002", "appetizers", "تومية", "Garlic Sauce", 15),
        new("AS-APP-003", "appetizers", "صوص جبنة", "Cheese Sauce", 15),
        new("AS-APP-004", "appetizers", "بطاطس بالجبنة", "Cheese Fries", 30),
        new("AS-APP-005", "appetizers", "صوص كاتشب", "Ketchup Sauce", 10),
        new("AS-APP-006", "appetizers", "صوص مايونيز", "Mayonnaise Sauce", 10),
        new("AS-APP-007", "appetizers", "صوص باربكيو", "Barbecue Sauce", 10),
        new("AS-APP-008", "appetizers", "صوص رانش", "Ranch Sauce", 10)
    ];

    private sealed record CategorySpec(string Key, string Name, string NameEn, int SortOrder);

    private sealed record ProductSpec(
        string Sku,
        string CategoryKey,
        string Name,
        string NameEn,
        decimal Price);
}
