namespace KasserPro.Infrastructure.Data;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Multi-Tenant Seeder - Creates multiple demo tenants for different business types
/// </summary>
public static class MultiTenantSeeder
{
    private static readonly Random _random = new(42);

    public static async Task SeedAsync(AppDbContext context)
    {
        // Check if we already have multiple tenants
        var tenantCount = await context.Tenants.CountAsync();
        if (tenantCount >= 4)
        {
            Console.WriteLine("✅ Multiple tenants already exist - skipping multi-tenant seed");
            return;
        }

        Console.WriteLine("🔄 بدء تحميل بيانات متعددة للمحلات المختلفة...");

        // Create System Owner first
        await EnsureSystemOwnerAsync(context);

        // Seed different business types (basic structure only)
        await SeedHomeAppliancesStoreAsync(context);
        await SeedSupermarketAsync(context);
        await SeedRestaurantAsync(context);

        Console.WriteLine("✅ تم تحميل بيانات المحلات المتعددة بنجاح!");
        
        // Now seed complete data for each tenant
        Console.WriteLine("\n🔄 تحميل البيانات الكاملة للمحلات...");
        await HomeAppliancesSeeder.SeedAsync(context);
        await SupermarketSeeder.SeedAsync(context);
        await RestaurantSeeder.SeedAsync(context);
        Console.WriteLine("✅ تم تحميل البيانات الكاملة بنجاح!");
    }

    private static async Task EnsureSystemOwnerAsync(AppDbContext context)
    {
        if (!await context.Users.AnyAsync(u => u.Role == UserRole.SystemOwner))
        {
            var systemOwner = new User
            {
                TenantId = null,
                BranchId = null,
                Name = "System Owner",
                Email = "owner@kasserpro.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Owner@123"),
                Role = UserRole.SystemOwner,
                IsActive = true
            };
            context.Users.Add(systemOwner);
            await context.SaveChangesAsync();
        }
    }

    // ============================================
    // TENANT 2: محل أدوات منزلية
    // ============================================
    private static async Task SeedHomeAppliancesStoreAsync(AppDbContext context)
    {
        // Check if already exists
        if (await context.Tenants.AnyAsync(t => t.Slug == "home-appliances"))
            return;

        Console.WriteLine("\n📦 محل أدوات منزلية...");

        // 1. Create Tenant
        var tenant = new Tenant
        {
            Name = "محل الأمل للأدوات المنزلية",
            NameEn = "Al-Amal Home Appliances",
            Slug = "home-appliances",
            Currency = "EGP",
            Timezone = "Africa/Cairo",
            TaxRate = 14,
            IsTaxEnabled = true,
            IsActive = true,
            AllowNegativeStock = false,
            ReceiptFooterMessage = "شكراً لزيارتكم - جودة وأسعار مميزة",
            ReceiptShowLogo = true,
            ReceiptPaperSize = "80mm"
        };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        // 2. Create Branch
        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "الفرع الرئيسي",
            Code = "BR001",
            Address = "شارع فيصل، الجيزة",
            Phone = "0233556677",
            DefaultTaxRate = 14,
            DefaultTaxInclusive = false,
            CurrencyCode = "EGP",
            IsActive = true
        };
        context.Branches.Add(branch);
        await context.SaveChangesAsync();

        // 3. Create Users
        var admin = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "سامي المدير",
            Email = "samy@homeappliances.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = UserRole.Admin,
            IsActive = true
        };
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        // 4. Create Categories
        var categories = new List<Category>
        {
            new() { TenantId = tenant.Id, Name = "أدوات مطبخ", NameEn = "Kitchen Tools", SortOrder = 1, IsActive = true },
            new() { TenantId = tenant.Id, Name = "أجهزة كهربائية", NameEn = "Electrical Appliances", SortOrder = 2, IsActive = true },
            new() { TenantId = tenant.Id, Name = "أواني وأطباق", NameEn = "Cookware & Dishes", SortOrder = 3, IsActive = true }
        };
        context.Categories.AddRange(categories);
        await context.SaveChangesAsync();

        // 5. Create Products
        var products = new List<Product>
        {
            // أدوات مطبخ
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "طقم سكاكين", NameEn = "Knife Set", Sku = "KIT001", Price = 150, Cost = 80, AverageCost = 80, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 50, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "مقشرة خضار", NameEn = "Vegetable Peeler", Sku = "KIT002", Price = 25, Cost = 12, AverageCost = 12, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 100, IsActive = true },
            
            // أجهزة كهربائية
            new() { TenantId = tenant.Id, CategoryId = categories[1].Id, Name = "خلاط كهربائي", NameEn = "Electric Blender", Sku = "ELC001", Price = 450, Cost = 280, AverageCost = 280, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 20, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[1].Id, Name = "محمصة خبز", NameEn = "Toaster", Sku = "ELC002", Price = 320, Cost = 200, AverageCost = 200, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 15, IsActive = true },
            
            // أواني وأطباق
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "طقم أطباق", NameEn = "Dish Set", Sku = "DSH001", Price = 280, Cost = 150, AverageCost = 150, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 30, IsActive = true }
        };
        context.Products.AddRange(products);
        await context.SaveChangesAsync();

        Console.WriteLine("   ✓ محل أدوات منزلية: تم");
    }

    // ============================================
    // TENANT 3: سوبر ماركت
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
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "أرز", NameEn = "Rice", Sku = "GRC001", Price = 45, Cost = 35, AverageCost = 35, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 200, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "سكر", NameEn = "Sugar", Sku = "GRC002", Price = 35, Cost = 28, AverageCost = 28, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 150, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[1].Id, Name = "عصير", NameEn = "Juice", Sku = "BEV001", Price = 15, Cost = 10, AverageCost = 10, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 300, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "صابون", NameEn = "Soap", Sku = "CLN001", Price = 20, Cost = 12, AverageCost = 12, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 180, IsActive = true }
        };
        context.Products.AddRange(products);
        await context.SaveChangesAsync();

        Console.WriteLine("   ✓ سوبر ماركت: تم");
    }

    // ============================================
    // TENANT 4: مطعم
    // ============================================
    private static async Task SeedRestaurantAsync(AppDbContext context)
    {
        if (await context.Tenants.AnyAsync(t => t.Slug == "restaurant"))
            return;

        Console.WriteLine("\n🍽️ مطعم...");

        var tenant = new Tenant
        {
            Name = "مطعم الأمير",
            NameEn = "Al-Amir Restaurant",
            Slug = "restaurant",
            Currency = "EGP",
            Timezone = "Africa/Cairo",
            TaxRate = 14,
            IsTaxEnabled = true,
            IsActive = true,
            AllowNegativeStock = false,
            ReceiptFooterMessage = "شكراً لزيارتكم - بالهناء والشفاء",
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
            Address = "وسط البلد، القاهرة",
            Phone = "0255778899",
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
            Name = "طارق المدير",
            Email = "tarek@restaurant.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = UserRole.Admin,
            IsActive = true
        };
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var categories = new List<Category>
        {
            new() { TenantId = tenant.Id, Name = "مشويات", NameEn = "Grills", SortOrder = 1, IsActive = true },
            new() { TenantId = tenant.Id, Name = "مقبلات", NameEn = "Appetizers", SortOrder = 2, IsActive = true },
            new() { TenantId = tenant.Id, Name = "مشروبات", NameEn = "Drinks", SortOrder = 3, IsActive = true }
        };
        context.Categories.AddRange(categories);
        await context.SaveChangesAsync();

        var products = new List<Product>
        {
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "كباب", NameEn = "Kebab", Sku = "GRL001", Price = 80, Cost = 45, AverageCost = 45, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 100, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "كفتة", NameEn = "Kofta", Sku = "GRL002", Price = 70, Cost = 40, AverageCost = 40, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 120, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[1].Id, Name = "سلطة", NameEn = "Salad", Sku = "APP001", Price = 25, Cost = 10, AverageCost = 10, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 80, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "عصير طازج", NameEn = "Fresh Juice", Sku = "DRK001", Price = 30, Cost = 15, AverageCost = 15, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 150, IsActive = true }
        };
        context.Products.AddRange(products);
        await context.SaveChangesAsync();

        Console.WriteLine("   ✓ مطعم: تم");
    }
}
