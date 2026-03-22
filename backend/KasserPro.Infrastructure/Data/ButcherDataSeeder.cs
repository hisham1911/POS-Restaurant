namespace KasserPro.Infrastructure.Data;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Data Seeder for Butcher Shop - مجزر الأمانة
/// </summary>
public static class ButcherDataSeeder
{
    private static readonly Random _random = new(42);

    public static async Task SeedAsync(AppDbContext context)
    {
        // Check if data already exists - skip seeding if so
        if (await context.Tenants.AnyAsync())
        {
            Console.WriteLine("✅ البيانات موجودة مسبقاً - تخطي التحميل");
            return;
        }

        Console.WriteLine("🔄 بدء تحميل بيانات المجزر (أول تشغيل)...");

        var tenant = await SeedTenantAsync(context);
        var branch = await SeedBranchAsync(context, tenant);
        var users = await SeedUsersAsync(context, tenant, branch);
        var categories = await SeedCategoriesAsync(context, tenant);
        var products = await SeedProductsAsync(context, tenant, categories);
        var customers = await SeedCustomersAsync(context, tenant);
        var suppliers = await SeedSuppliersAsync(context, tenant, branch);
        await SeedExpenseCategoriesAsync(context, tenant);
        await SeedShiftsAndOrdersAsync(context, tenant, branch, users, products, customers);
        await SeedPurchaseInvoicesAsync(context, tenant, branch, users[0], suppliers, products);
        await SeedExpensesAsync(context, tenant, branch, users[0]);
        await SeedCashRegisterTransactionsAsync(context, tenant, branch, users[0]);

        Console.WriteLine("✅ تم تحميل بيانات المجزر بنجاح!");
    }

    private static async Task ClearBusinessDataAsync(AppDbContext context)
    {
        Console.WriteLine("🗑️  مسح البيانات القديمة...");

        context.Payments.RemoveRange(context.Payments);
        context.OrderItems.RemoveRange(context.OrderItems);
        context.Orders.RemoveRange(context.Orders);
        context.CashRegisterTransactions.RemoveRange(context.CashRegisterTransactions);
        context.ExpenseAttachments.RemoveRange(context.ExpenseAttachments);
        context.Expenses.RemoveRange(context.Expenses);
        context.ExpenseCategories.RemoveRange(context.ExpenseCategories);
        context.PurchaseInvoicePayments.RemoveRange(context.PurchaseInvoicePayments);
        context.PurchaseInvoiceItems.RemoveRange(context.PurchaseInvoiceItems);
        context.PurchaseInvoices.RemoveRange(context.PurchaseInvoices);
        context.SupplierProducts.RemoveRange(context.SupplierProducts);
        context.Suppliers.RemoveRange(context.Suppliers);
        context.StockMovements.RemoveRange(context.StockMovements);
        context.Products.RemoveRange(context.Products);
        context.Categories.RemoveRange(context.Categories);
        context.Customers.RemoveRange(context.Customers);
        context.Shifts.RemoveRange(context.Shifts);
        context.AuditLogs.RemoveRange(context.AuditLogs);

        await context.SaveChangesAsync();
        Console.WriteLine("   ✓ تم مسح البيانات القديمة");
    }

    private static async Task<Tenant> SeedTenantAsync(AppDbContext context)
    {
        var existing = await context.Tenants.FirstOrDefaultAsync();
        if (existing != null)
        {
            Console.WriteLine("   ✓ المتجر موجود مسبقاً");
            return existing;
        }

        var tenant = new Tenant
        {
            Name = "مجزر الأمانة",
            NameEn = "Al-Amana Butcher",
            Slug = "al-amana-butcher",
            Currency = "EGP",
            Timezone = "Africa/Cairo",
            TaxRate = 14,
            IsTaxEnabled = true,
            IsActive = true,
            AllowNegativeStock = false,
            ReceiptFooterMessage = "شكراً لثقتكم - لحوم طازجة يومياً",
            ReceiptShowLogo = true,
            ReceiptShowCustomerName = true,
            ReceiptPaperSize = "80mm",
            ReceiptPhoneNumber = "0233445566"
        };

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        Console.WriteLine("   ✓ المتجر: مجزر الأمانة");
        return tenant;
    }

    private static async Task<Branch> SeedBranchAsync(AppDbContext context, Tenant tenant)
    {
        var existing = await context.Branches.FirstOrDefaultAsync();
        if (existing != null) return existing;

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = "الفرع الرئيسي",
            Code = "BR001",
            Address = "شارع الجمهورية، وسط البلد، القاهرة",
            Phone = "0233445566",
            DefaultTaxRate = 14,
            DefaultTaxInclusive = false,
            CurrencyCode = "EGP",
            IsActive = true
        };

        context.Branches.Add(branch);
        await context.SaveChangesAsync();
        Console.WriteLine("   ✓ الفرع: الفرع الرئيسي");
        return branch;
    }

    private static async Task<List<User>> SeedUsersAsync(AppDbContext context, Tenant tenant, Branch branch)
    {
        if (await context.Users.AnyAsync(u => u.TenantId == tenant.Id))
        {
            Console.WriteLine("   ✓ المستخدمين موجودين مسبقاً");
            return await context.Users.Where(u => u.TenantId == tenant.Id).ToListAsync();
        }

        // Create System Owner first (if not exists)
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
            Console.WriteLine("   ✓ System Owner: owner@kasserpro.com (Password: Owner@123)");
        }

        var users = new List<User>
        {
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "أحمد المدير",
                Email = "admin@kasserpro.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Role = UserRole.Admin,
                IsActive = true
            },
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "محمد الكاشير",
                Email = "mohamed@kasserpro.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Role = UserRole.Cashier,
                IsActive = true
            },
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "علي الكاشير",
                Email = "ali@kasserpro.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Role = UserRole.Cashier,
                IsActive = true
            }
        };

        context.Users.AddRange(users);
        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ المستخدمين: {users.Count} (1 مدير + 2 كاشير)");
        return users;
    }

    private static async Task<List<Category>> SeedCategoriesAsync(AppDbContext context, Tenant tenant)
    {
        var categories = new List<Category>
        {
            new() { TenantId = tenant.Id, Name = "لحوم بقري", NameEn = "Beef", SortOrder = 1, ImageUrl = "🥩", IsActive = true },
            new() { TenantId = tenant.Id, Name = "لحوم مفرومة ومصنعة", NameEn = "Minced & Processed", SortOrder = 2, ImageUrl = "🍖", IsActive = true },
            new() { TenantId = tenant.Id, Name = "أحشاء ومنتجات ثانوية", NameEn = "Offal & By-products", SortOrder = 3, ImageUrl = "🫀", IsActive = true }
        };

        context.Categories.AddRange(categories);
        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ الفئات: {categories.Count}");
        return categories;
    }

    private static async Task<List<Product>> SeedProductsAsync(AppDbContext context, Tenant tenant, List<Category> categories)
    {
        var products = new List<Product>
        {
            // لحوم بقري (Beef)
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "قراقيش", NameEn = "Qaraqish", Sku = "BEEF001", Barcode = "6291001001", Price = 25, Cost = 18, AverageCost = 18, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 50, LowStockThreshold = 10, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "لحم قطع", NameEn = "Meat Cuts", Sku = "BEEF002", Barcode = "6291001002", Price = 380, Cost = 320, AverageCost = 320, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 80, LowStockThreshold = 15, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "لحم ضلعه", NameEn = "Ribs", Sku = "BEEF003", Barcode = "6291001003", Price = 320, Cost = 270, AverageCost = 270, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 60, LowStockThreshold = 12, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "لحم مميز", NameEn = "Premium Meat", Sku = "BEEF004", Barcode = "6291001004", Price = 400, Cost = 340, AverageCost = 340, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 70, LowStockThreshold = 15, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "لحم دوش", NameEn = "Doush Meat", Sku = "BEEF005", Barcode = "6291001005", Price = 340, Cost = 290, AverageCost = 290, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 65, LowStockThreshold = 12, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "لحم موزه", NameEn = "Mouza Meat", Sku = "BEEF006", Barcode = "6291001006", Price = 400, Cost = 340, AverageCost = 340, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 75, LowStockThreshold = 15, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "لحم راس", NameEn = "Head Meat", Sku = "BEEF007", Barcode = "6291001007", Price = 225, Cost = 180, AverageCost = 180, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 45, LowStockThreshold = 10, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "مكعبات لحم احمر", NameEn = "Red Meat Cubes", Sku = "BEEF008", Barcode = "6291001008", Price = 380, Cost = 320, AverageCost = 320, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 55, LowStockThreshold = 12, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "استيك", NameEn = "Steak", Sku = "BEEF009", Barcode = "6291001009", Price = 450, Cost = 380, AverageCost = 380, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 40, LowStockThreshold = 8, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[0].Id, Name = "بوفتيك", NameEn = "Beefsteak", Sku = "BEEF010", Barcode = "6291001010", Price = 420, Cost = 360, AverageCost = 360, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 45, LowStockThreshold = 10, IsActive = true },

            // لحوم مفرومة ومصنعة (Minced & Processed)
            new() { TenantId = tenant.Id, CategoryId = categories[1].Id, Name = "كباب حله", NameEn = "Kebab Halla", Sku = "PROC001", Barcode = "6291002001", Price = 380, Cost = 320, AverageCost = 320, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 60, LowStockThreshold = 12, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[1].Id, Name = "كباب حله احمر", NameEn = "Red Kebab Halla", Sku = "PROC002", Barcode = "6291002002", Price = 380, Cost = 320, AverageCost = 320, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 55, LowStockThreshold = 12, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[1].Id, Name = "برجر", NameEn = "Burger", Sku = "PROC003", Barcode = "6291002003", Price = 250, Cost = 200, AverageCost = 200, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 80, LowStockThreshold = 15, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[1].Id, Name = "سجق مخصوص", NameEn = "Special Sausage", Sku = "PROC004", Barcode = "6291002004", Price = 300, Cost = 240, AverageCost = 240, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 70, LowStockThreshold = 15, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[1].Id, Name = "مفروم مخصوص", NameEn = "Special Minced", Sku = "PROC005", Barcode = "6291002005", Price = 300, Cost = 250, AverageCost = 250, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 75, LowStockThreshold = 15, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[1].Id, Name = "مزاليكا", NameEn = "Mazalika", Sku = "PROC006", Barcode = "6291002006", Price = 270, Cost = 220, AverageCost = 220, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 50, LowStockThreshold = 10, IsActive = true },

            // أحشاء ومنتجات ثانوية (Offal & By-products)
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "كوارع بالكيلو", NameEn = "Trotters per Kg", Sku = "OFFAL001", Barcode = "6291003001", Price = 180, Cost = 140, AverageCost = 140, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 40, LowStockThreshold = 8, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "كوارع", NameEn = "Trotters", Sku = "OFFAL002", Barcode = "6291003002", Price = 280, Cost = 220, AverageCost = 220, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 35, LowStockThreshold = 7, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "دهن طرب", NameEn = "Fat Tarab", Sku = "OFFAL003", Barcode = "6291003003", Price = 75, Cost = 50, AverageCost = 50, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 30, LowStockThreshold = 6, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "حلويات", NameEn = "Sweetbreads", Sku = "OFFAL004", Barcode = "6291003004", Price = 130, Cost = 100, AverageCost = 100, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 25, LowStockThreshold = 5, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "طحال", NameEn = "Spleen", Sku = "OFFAL005", Barcode = "6291003005", Price = 150, Cost = 120, AverageCost = 120, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 20, LowStockThreshold = 5, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "ممبار", NameEn = "Mumbar", Sku = "OFFAL006", Barcode = "6291003006", Price = 260, Cost = 210, AverageCost = 210, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 30, LowStockThreshold = 6, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "ام الشلاتيت", NameEn = "Um El-Shalatit", Sku = "OFFAL007", Barcode = "6291003007", Price = 140, Cost = 110, AverageCost = 110, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 22, LowStockThreshold = 5, IsActive = true },
            new() { TenantId = tenant.Id, CategoryId = categories[2].Id, Name = "دهن كلاوي", NameEn = "Kidney Fat", Sku = "OFFAL008", Barcode = "6291003008", Price = 75, Cost = 50, AverageCost = 50, TaxRate = 14, TaxInclusive = false, TrackInventory = true, StockQuantity = 28, LowStockThreshold = 6, IsActive = true }
        };

        foreach (var p in products)
        {
            p.LastStockUpdate = DateTime.UtcNow;
        }

        context.Products.AddRange(products);
        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ المنتجات: {products.Count} منتج");

        // Create BranchInventories for all products in all branches
        var branches = await context.Branches.Where(b => b.TenantId == tenant.Id).ToListAsync();
        var branchInventories = new List<BranchInventory>();

        foreach (var product in products)
        {
            foreach (var branch in branches)
            {
                branchInventories.Add(new BranchInventory
                {
                    TenantId = tenant.Id,
                    BranchId = branch.Id,
                    ProductId = product.Id,
                    Quantity = product.StockQuantity ?? 0,
                    ReorderLevel = product.LowStockThreshold ?? 10,
                    LastUpdatedAt = DateTime.UtcNow
                });
            }
        }

        context.BranchInventories.AddRange(branchInventories);
        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ مخزون الفروع: {branchInventories.Count} سجل");

        return products;
    }

    private static async Task<List<Customer>> SeedCustomersAsync(AppDbContext context, Tenant tenant)
    {
        var customers = new List<Customer>
        {
            // عملاء VIP (كبار العملاء)
            new() { TenantId = tenant.Id, Name = "محمد أحمد السيد", Phone = "01001234567", Email = "mohamed.ahmed@email.com", Address = "المعادي، القاهرة", LoyaltyPoints = 450, TotalOrders = 35, TotalSpent = 12500, LastOrderAt = DateTime.UtcNow.AddDays(-1), IsActive = true },
            new() { TenantId = tenant.Id, Name = "أحمد حسن علي", Phone = "01112345678", Email = "ahmed.hassan@email.com", Address = "الزمالك، القاهرة", LoyaltyPoints = 520, TotalOrders = 42, TotalSpent = 15800, LastOrderAt = DateTime.UtcNow.AddHours(-6), IsActive = true },
            new() { TenantId = tenant.Id, Name = "خالد محمود فتحي", Phone = "01223456789", Email = "khaled.mahmoud@email.com", Address = "مدينة نصر، القاهرة", LoyaltyPoints = 380, TotalOrders = 28, TotalSpent = 9800, LastOrderAt = DateTime.UtcNow.AddDays(-2), IsActive = true },
            
            // عملاء منتظمين
            new() { TenantId = tenant.Id, Name = "عمر سعيد محمد", Phone = "01098765432", Email = "omar.saeed@email.com", Address = "المهندسين، الجيزة", LoyaltyPoints = 280, TotalOrders = 22, TotalSpent = 7100, LastOrderAt = DateTime.UtcNow.AddDays(-3), IsActive = true },
            new() { TenantId = tenant.Id, Name = "يوسف علي حسن", Phone = "01198765432", Email = "youssef.ali@email.com", Address = "حلوان، القاهرة", LoyaltyPoints = 190, TotalOrders = 15, TotalSpent = 4800, LastOrderAt = DateTime.UtcNow.AddDays(-5), IsActive = true },
            new() { TenantId = tenant.Id, Name = "حسام الدين إبراهيم", Phone = "01287654321", Email = "hossam.ibrahim@email.com", Address = "الدقي، الجيزة", LoyaltyPoints = 220, TotalOrders = 18, TotalSpent = 5900, LastOrderAt = DateTime.UtcNow.AddDays(-4), IsActive = true },
            new() { TenantId = tenant.Id, Name = "كريم عبدالله", Phone = "01156789012", Email = "karim.abdullah@email.com", Address = "العباسية، القاهرة", LoyaltyPoints = 160, TotalOrders = 12, TotalSpent = 3900, LastOrderAt = DateTime.UtcNow.AddDays(-6), IsActive = true },
            new() { TenantId = tenant.Id, Name = "طارق سمير", Phone = "01267890123", Email = "tarek.samir@email.com", Address = "الهرم، الجيزة", LoyaltyPoints = 140, TotalOrders = 10, TotalSpent = 3200, LastOrderAt = DateTime.UtcNow.AddDays(-8), IsActive = true },
            
            // عملاء جدد
            new() { TenantId = tenant.Id, Name = "ياسر محمود", Phone = "01078901234", Email = null, Address = "شبرا، القاهرة", LoyaltyPoints = 45, TotalOrders = 3, TotalSpent = 980, LastOrderAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new() { TenantId = tenant.Id, Name = "وليد أحمد", Phone = "01189012345", Email = null, Address = "المطرية، القاهرة", LoyaltyPoints = 30, TotalOrders = 2, TotalSpent = 650, LastOrderAt = DateTime.UtcNow.AddDays(-12), IsActive = true },
            
            // عملاء مطاعم (جملة)
            new() { TenantId = tenant.Id, Name = "مطعم الأمير - أحمد صلاح", Phone = "01090123456", Email = "alamir.restaurant@email.com", Address = "وسط البلد، القاهرة", LoyaltyPoints = 850, TotalOrders = 65, TotalSpent = 28500, LastOrderAt = DateTime.UtcNow.AddHours(-12), IsActive = true },
            new() { TenantId = tenant.Id, Name = "مطعم الفردوس - محمد عبدالرحمن", Phone = "01201234567", Email = "alferdous.rest@email.com", Address = "مصر الجديدة، القاهرة", LoyaltyPoints = 720, TotalOrders = 52, TotalSpent = 22800, LastOrderAt = DateTime.UtcNow.AddDays(-1), IsActive = true },
            new() { TenantId = tenant.Id, Name = "كافتيريا النخيل - سامي حسن", Phone = "01012345678", Email = null, Address = "المعادي، القاهرة", LoyaltyPoints = 480, TotalOrders = 38, TotalSpent = 15200, LastOrderAt = DateTime.UtcNow.AddDays(-2), IsActive = true },
            
            // عملاء محلات جزارة (منافسين/موزعين)
            new() { TenantId = tenant.Id, Name = "محل الرحمة - عبدالله محمد", Phone = "01123456789", Email = null, Address = "إمبابة، الجيزة", LoyaltyPoints = 620, TotalOrders = 45, TotalSpent = 19500, LastOrderAt = DateTime.UtcNow.AddDays(-3), IsActive = true },
            new() { TenantId = tenant.Id, Name = "سوبر ماركت الخير - حسن علي", Phone = "01234567890", Email = "alkheir.market@email.com", Address = "فيصل، الجيزة", LoyaltyPoints = 550, TotalOrders = 40, TotalSpent = 17800, LastOrderAt = DateTime.UtcNow.AddDays(-4), IsActive = true }
        };

        context.Customers.AddRange(customers);
        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ العملاء: {customers.Count} (VIP: 3, منتظمين: 5, جدد: 2, مطاعم: 3, محلات: 2)");
        return customers;
    }

    private static async Task<List<Supplier>> SeedSuppliersAsync(AppDbContext context, Tenant tenant, Branch branch)
    {
        var suppliers = new List<Supplier>
        {
            // موردين رئيسيين
            new() { TenantId = tenant.Id, BranchId = branch.Id, Name = "مزرعة الأمل للحوم", Phone = "0233334444", Email = "info@amal-farm.com", Address = "طريق مصر إسكندرية الصحراوي، كم 28", ContactPerson = "أحمد محمود السيد", TaxNumber = "123-456-789", Notes = "مورد رئيسي للحوم البقري الطازجة - توريد يومي", IsActive = true },
            new() { TenantId = tenant.Id, BranchId = branch.Id, Name = "شركة اللحوم الطازجة المتحدة", Phone = "0244445555", Email = "sales@fresh-meat.com", Address = "المنطقة الصناعية، العاشر من رمضان", ContactPerson = "محمد حسن علي", TaxNumber = "234-567-890", Notes = "لحوم مستوردة عالية الجودة - توريد أسبوعي", IsActive = true },
            new() { TenantId = tenant.Id, BranchId = branch.Id, Name = "مجازر الصفوة", Phone = "0255556666", Email = "contact@safwa-meat.com", Address = "سوق العبور، القاهرة", ContactPerson = "خالد سعيد محمود", TaxNumber = "345-678-901", Notes = "لحوم محلية طازجة - أسعار منافسة", IsActive = true },
            
            // موردين إضافيين
            new() { TenantId = tenant.Id, BranchId = branch.Id, Name = "مزارع الوادي الجديد", Phone = "0266667777", Email = "newvalley@email.com", Address = "الوادي الجديد، الخارجة", ContactPerson = "عمر فتحي", TaxNumber = "456-789-012", Notes = "لحوم عضوية - توريد شهري", IsActive = true },
            new() { TenantId = tenant.Id, BranchId = branch.Id, Name = "شركة الذبائح المصرية", Phone = "0277778888", Email = "egy-meat@email.com", Address = "مدينة السادات، المنوفية", ContactPerson = "ياسر محمد", TaxNumber = "567-890-123", Notes = "متخصصون في اللحوم المفرومة والمصنعة", IsActive = true }
        };

        context.Suppliers.AddRange(suppliers);
        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ الموردين: {suppliers.Count} (رئيسيين: 3، إضافيين: 2)");
        return suppliers;
    }

    private static async Task SeedExpenseCategoriesAsync(AppDbContext context, Tenant tenant)
    {
        var categories = new List<ExpenseCategory>
        {
            new() { TenantId = tenant.Id, Name = "رواتب", NameEn = "Salaries", Icon = "💰", Color = "#3B82F6", IsActive = true, IsSystem = true, SortOrder = 1 },
            new() { TenantId = tenant.Id, Name = "إيجار", NameEn = "Rent", Icon = "🏢", Color = "#8B5CF6", IsActive = true, IsSystem = true, SortOrder = 2 },
            new() { TenantId = tenant.Id, Name = "كهرباء", NameEn = "Electricity", Icon = "⚡", Color = "#F59E0B", IsActive = true, IsSystem = true, SortOrder = 3 },
            new() { TenantId = tenant.Id, Name = "صيانة", NameEn = "Maintenance", Icon = "🔧", Color = "#10B981", IsActive = true, IsSystem = true, SortOrder = 4 },
            new() { TenantId = tenant.Id, Name = "مواصلات", NameEn = "Transportation", Icon = "🚗", Color = "#6366F1", IsActive = true, IsSystem = true, SortOrder = 5 },
            new() { TenantId = tenant.Id, Name = "أخرى", NameEn = "Other", Icon = "📦", Color = "#64748B", IsActive = true, IsSystem = true, SortOrder = 6 }
        };

        context.ExpenseCategories.AddRange(categories);
        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ فئات المصروفات: {categories.Count}");
    }

    private static async Task SeedShiftsAndOrdersAsync(AppDbContext context, Tenant tenant, Branch branch, List<User> users, List<Product> products, List<Customer> customers)
    {
        Console.WriteLine("   🔄 إنشاء الورديات والطلبات...");

        var cashier1 = users.First(u => u.Role == UserRole.Cashier);
        var cashier2 = users.Last(u => u.Role == UserRole.Cashier);

        // Create 14 days of closed shifts + 1 open shift today
        for (int day = 14; day >= 0; day--)
        {
            var shiftDate = DateTime.UtcNow.Date.AddDays(-day);
            var isClosed = day > 0;
            var cashier = day % 2 == 0 ? cashier1 : cashier2;

            var shift = new Shift
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                UserId = cashier.Id,
                OpeningBalance = 1000,
                OpenedAt = shiftDate.AddHours(8),
                LastActivityAt = shiftDate.AddHours(8),
                IsClosed = isClosed,
                IsForceClosed = false,
                IsHandedOver = false,
                HandoverBalance = 0
            };

            if (isClosed)
            {
                shift.ClosedAt = shiftDate.AddHours(20);
                shift.LastActivityAt = shiftDate.AddHours(20);
                shift.Notes = $"وردية {shiftDate:yyyy-MM-dd}";
            }

            context.Shifts.Add(shift);
            await context.SaveChangesAsync();

            // Create orders for this shift
            var isWeekend = shiftDate.DayOfWeek == DayOfWeek.Friday || shiftDate.DayOfWeek == DayOfWeek.Saturday;
            var orderCount = day == 0 ? _random.Next(2, 4) : (isWeekend ? _random.Next(8, 15) : _random.Next(4, 10));

            decimal totalCash = 0;
            decimal totalCard = 0;
            int completedCount = 0;

            for (int i = 0; i < orderCount; i++)
            {
                var orderTime = shift.OpenedAt.AddMinutes(_random.Next(30, 700));
                var status = day == 0 && i >= orderCount - 2
                    ? (i == orderCount - 1 ? OrderStatus.Draft : OrderStatus.Pending)
                    : OrderStatus.Completed;

                var customer = _random.Next(3) == 0 ? customers[_random.Next(customers.Count)] : null;

                var order = CreateButcherOrder(
                    tenant.Id, branch.Id, cashier.Id, shift.Id, cashier.Name,
                    products, customer, orderTime, (day * 100) + i + 1, status, branch
                );

                context.Orders.Add(order);

                if (status == OrderStatus.Completed)
                {
                    completedCount++;
                    var payment = order.Payments.FirstOrDefault();
                    if (payment != null)
                    {
                        if (payment.Method == PaymentMethod.Cash)
                            totalCash += payment.Amount;
                        else
                            totalCard += payment.Amount;
                    }
                }
            }

            await context.SaveChangesAsync();

            // Update shift totals for closed shifts
            if (isClosed)
            {
                shift.TotalOrders = completedCount;
                shift.TotalCash = totalCash;
                shift.TotalCard = totalCard;
                shift.ExpectedBalance = shift.OpeningBalance + totalCash;
                shift.ClosingBalance = shift.ExpectedBalance + _random.Next(-50, 100);
                shift.Difference = shift.ClosingBalance - shift.ExpectedBalance;
                await context.SaveChangesAsync();
            }
        }

        // Deduct stock for completed orders
        var completedOrders = await context.Orders
            .Include(o => o.Items)
            .Where(o => o.Status == OrderStatus.Completed)
            .ToListAsync();

        foreach (var order in completedOrders)
        {
            foreach (var item in order.Items)
            {
                var product = await context.Products.FindAsync(item.ProductId);
                if (product != null && product.StockQuantity.HasValue)
                {
                    product.StockQuantity -= item.Quantity;
                    product.LastStockUpdate = order.CompletedAt ?? order.CreatedAt;
                }
            }
        }
        await context.SaveChangesAsync();

        Console.WriteLine($"   ✓ الورديات: 15 (14 مغلقة + 1 مفتوحة)");
        Console.WriteLine($"   ✓ الطلبات: {completedOrders.Count} طلب مكتمل");
    }

    private static Order CreateButcherOrder(
        int tenantId, int branchId, int userId, int shiftId, string userName,
        List<Product> products, Customer? customer, DateTime orderTime, int orderNum,
        OrderStatus status, Branch branch)
    {
        // Add variety to order types
        var orderTypes = new[] { OrderType.Takeaway, OrderType.Takeaway, OrderType.Takeaway, OrderType.Delivery, OrderType.DineIn };
        var orderType = orderTypes[_random.Next(orderTypes.Length)];

        var order = new Order
        {
            TenantId = tenantId,
            BranchId = branchId,
            ShiftId = shiftId,
            OrderNumber = $"ORD-{orderTime:yyyyMMdd}-{orderNum:D4}",
            UserId = userId,
            UserName = userName,
            Status = status,
            OrderType = orderType,
            CreatedAt = orderTime,
            BranchName = branch.Name,
            BranchAddress = branch.Address,
            BranchPhone = branch.Phone,
            CurrencyCode = "EGP",
            TaxRate = 14
        };

        if (customer != null)
        {
            order.CustomerId = customer.Id;
            order.CustomerName = customer.Name;
            order.CustomerPhone = customer.Phone;
        }

        // Add 1-3 items per order (butcher orders usually have fewer items)
        var itemCount = _random.Next(1, 4);
        decimal subtotal = 0;
        decimal taxAmount = 0;

        var usedProducts = new HashSet<int>();
        for (int j = 0; j < itemCount; j++)
        {
            Product product;
            do
            {
                product = products[_random.Next(products.Count)];
            } while (usedProducts.Contains(product.Id) && usedProducts.Count < products.Count);

            usedProducts.Add(product.Id);
            var qty = _random.Next(1, 3); // Smaller quantities for meat

            // Tax Exclusive calculation
            var netPrice = product.Price * qty;
            var itemTax = netPrice * (14m / 100m);
            var grossPrice = netPrice + itemTax;

            var orderItem = new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductNameEn = product.NameEn,
                ProductSku = product.Sku,
                ProductBarcode = product.Barcode,
                UnitPrice = product.Price,
                UnitCost = product.Cost,
                OriginalPrice = product.Price,
                Quantity = qty,
                TaxRate = 14,
                TaxInclusive = false,
                TaxAmount = Math.Round(itemTax, 2),
                Subtotal = Math.Round(netPrice, 2),
                Total = Math.Round(grossPrice, 2)
            };

            order.Items.Add(orderItem);
            subtotal += netPrice;
            taxAmount += itemTax;
        }

        order.Subtotal = Math.Round(subtotal, 2);
        order.TaxAmount = Math.Round(taxAmount, 2);
        order.Total = Math.Round(subtotal + taxAmount, 2);

        if (status == OrderStatus.Completed)
        {
            order.AmountPaid = order.Total;
            order.AmountDue = 0;
            order.CompletedAt = orderTime.AddMinutes(_random.Next(5, 15));
            order.CompletedByUserId = userId;

            var paymentMethod = _random.Next(10) < 7 ? PaymentMethod.Cash : PaymentMethod.Card;

            order.Payments.Add(new Payment
            {
                TenantId = tenantId,
                BranchId = branchId,
                Method = paymentMethod,
                Amount = order.Total,
                CreatedAt = order.CompletedAt.Value
            });
        }
        else if (status == OrderStatus.Cancelled)
        {
            order.CancelledAt = orderTime.AddMinutes(_random.Next(10, 30));
            order.CancellationReason = "طلب العميل";
        }

        return order;
    }

    private static async Task SeedPurchaseInvoicesAsync(AppDbContext context, Tenant tenant, Branch branch, User admin, List<Supplier> suppliers, List<Product> products)
    {
        Console.WriteLine("   🔄 إنشاء فواتير الشراء...");

        var invoices = new List<PurchaseInvoice>();

        // Create 5 purchase invoices over the past 30 days
        for (int i = 0; i < 5; i++)
        {
            var supplier = suppliers[_random.Next(suppliers.Count)];
            var invoiceDate = DateTime.UtcNow.AddDays(-_random.Next(1, 30));

            var invoice = new PurchaseInvoice
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                InvoiceNumber = $"PI-{invoiceDate:yyyyMMdd}-{i + 1:D3}",
                InvoiceDate = invoiceDate,
                Status = PurchaseInvoiceStatus.Confirmed,
                TaxRate = 14,
                Notes = $"فاتورة شراء من {supplier.Name}",
                CreatedByUserId = admin.Id,
                CreatedByUserName = admin.Name,
                ConfirmedByUserId = admin.Id,
                ConfirmedByUserName = admin.Name,
                ConfirmedAt = invoiceDate,
                CreatedAt = invoiceDate
            };

            // Add 3-6 items per invoice
            var itemCount = _random.Next(3, 7);
            decimal subtotal = 0;
            decimal taxAmount = 0;

            for (int j = 0; j < itemCount; j++)
            {
                var product = products[_random.Next(products.Count)];
                var qty = _random.Next(10, 50); // Meat quantities in kg
                var purchasePrice = product.Cost ?? (product.Price * 0.85m);

                var itemTotal = purchasePrice * qty;

                var item = new PurchaseInvoiceItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductSku = product.Sku,
                    Quantity = qty,
                    PurchasePrice = purchasePrice,
                    Total = Math.Round(itemTotal, 2)
                };

                invoice.Items.Add(item);
                subtotal += itemTotal;
            }

            // Calculate tax on subtotal
            taxAmount = subtotal * (14m / 100m);

            invoice.Subtotal = Math.Round(subtotal, 2);
            invoice.TaxAmount = Math.Round(taxAmount, 2);
            invoice.Total = Math.Round(subtotal + taxAmount, 2);
            invoice.AmountPaid = invoice.Total;
            invoice.AmountDue = 0;

            // Add payment
            invoice.Payments.Add(new PurchaseInvoicePayment
            {
                Amount = invoice.Total,
                Method = PaymentMethod.Cash,
                PaymentDate = invoiceDate,
                Notes = "دفع كامل",
                CreatedByUserId = admin.Id,
                CreatedByUserName = admin.Name
            });

            invoices.Add(invoice);
        }

        context.PurchaseInvoices.AddRange(invoices);
        await context.SaveChangesAsync();

        // Update product stock from purchase invoices
        foreach (var invoice in invoices)
        {
            foreach (var item in invoice.Items)
            {
                var product = await context.Products.FindAsync(item.ProductId);
                if (product != null && product.TrackInventory)
                {
                    product.StockQuantity = (product.StockQuantity ?? 0) + item.Quantity;
                    product.LastPurchasePrice = item.PurchasePrice;
                    product.LastPurchaseDate = invoice.InvoiceDate;
                    product.LastStockUpdate = invoice.InvoiceDate;
                }
            }
        }
        await context.SaveChangesAsync();

        Console.WriteLine($"   ✓ فواتير الشراء: {invoices.Count}");
    }

    private static async Task SeedExpensesAsync(AppDbContext context, Tenant tenant, Branch branch, User admin)
    {
        Console.WriteLine("   🔄 إنشاء المصروفات...");

        var categories = await context.ExpenseCategories.Where(c => c.TenantId == tenant.Id).ToListAsync();
        var expenses = new List<Expense>();

        // Create 15 expenses over the past 60 days (more realistic)
        var expenseData = new[]
        {
            // رواتب (شهرية)
            (CategoryName: "رواتب", Amount: 12000m, Days: 5, Description: "رواتب الموظفين - شهر مارس"),
            (CategoryName: "رواتب", Amount: 12000m, Days: 35, Description: "رواتب الموظفين - شهر فبراير"),
            
            // إيجار (شهري)
            (CategoryName: "إيجار", Amount: 8000m, Days: 3, Description: "إيجار المحل - شهر مارس"),
            (CategoryName: "إيجار", Amount: 8000m, Days: 33, Description: "إيجار المحل - شهر فبراير"),
            
            // كهرباء (شهري)
            (CategoryName: "كهرباء", Amount: 1850m, Days: 8, Description: "فاتورة الكهرباء - شهر فبراير"),
            (CategoryName: "كهرباء", Amount: 1620m, Days: 38, Description: "فاتورة الكهرباء - شهر يناير"),
            
            // صيانة (متفرقة)
            (CategoryName: "صيانة", Amount: 450m, Days: 12, Description: "صيانة الثلاجات"),
            (CategoryName: "صيانة", Amount: 680m, Days: 25, Description: "إصلاح ماكينة الفرم"),
            (CategoryName: "صيانة", Amount: 320m, Days: 42, Description: "صيانة دورية للمعدات"),
            
            // مواصلات (أسبوعية)
            (CategoryName: "مواصلات", Amount: 280m, Days: 2, Description: "مواصلات التوصيل - أسبوع 1"),
            (CategoryName: "مواصلات", Amount: 310m, Days: 9, Description: "مواصلات التوصيل - أسبوع 2"),
            (CategoryName: "مواصلات", Amount: 265m, Days: 16, Description: "مواصلات التوصيل - أسبوع 3"),
            
            // أخرى (متفرقة)
            (CategoryName: "أخرى", Amount: 520m, Days: 6, Description: "مستلزمات تغليف ونظافة"),
            (CategoryName: "أخرى", Amount: 380m, Days: 18, Description: "رسوم حكومية وتراخيص"),
            (CategoryName: "أخرى", Amount: 450m, Days: 28, Description: "مصاريف إدارية متنوعة")
        };

        foreach (var (categoryName, amount, daysAgo, description) in expenseData)
        {
            var category = categories.First(c => c.Name == categoryName);
            var expenseDate = DateTime.UtcNow.AddDays(-daysAgo);

            var expense = new Expense
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                CategoryId = category.Id,
                ExpenseNumber = $"EXP-{expenseDate:yyyyMMdd}-{expenses.Count + 1:D3}",
                Amount = amount,
                Description = description,
                ExpenseDate = expenseDate,
                Status = ExpenseStatus.Approved,
                PaymentMethod = amount > 5000 ? PaymentMethod.Card : (_random.Next(2) == 0 ? PaymentMethod.Cash : PaymentMethod.Card),
                PaymentDate = expenseDate,
                CreatedByUserId = admin.Id,
                CreatedByUserName = admin.Name,
                ApprovedByUserId = admin.Id,
                ApprovedByUserName = admin.Name,
                ApprovedAt = expenseDate.AddHours(1),
                PaidByUserId = admin.Id,
                PaidByUserName = admin.Name,
                PaidAt = expenseDate,
                CreatedAt = expenseDate
            };

            expenses.Add(expense);
        }

        context.Expenses.AddRange(expenses);
        await context.SaveChangesAsync();

        Console.WriteLine($"   ✓ المصروفات: {expenses.Count} (رواتب: 2، إيجار: 2، كهرباء: 2، صيانة: 3، مواصلات: 3، أخرى: 3)");
    }

    private static async Task SeedCashRegisterTransactionsAsync(AppDbContext context, Tenant tenant, Branch branch, User admin)
    {
        Console.WriteLine("   🔄 إنشاء حركات الخزينة...");

        var transactions = new List<CashRegisterTransaction>();

        // Create 6 transactions over the past 14 days
        for (int i = 0; i < 6; i++)
        {
            var transDate = DateTime.UtcNow.AddDays(-_random.Next(1, 14));
            var isDeposit = _random.Next(2) == 0;
            var amount = _random.Next(500, 2000);

            var transaction = new CashRegisterTransaction
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                TransactionNumber = $"CRT-{transDate:yyyyMMdd}-{i + 1:D3}",
                Type = isDeposit ? CashRegisterTransactionType.Deposit : CashRegisterTransactionType.Withdrawal,
                Amount = amount,
                BalanceBefore = 5000,
                BalanceAfter = isDeposit ? 5000 + amount : 5000 - amount,
                TransactionDate = transDate,
                Description = isDeposit ? "إيداع نقدي" : "سحب نقدي",
                UserId = admin.Id,
                UserName = admin.Name,
                CreatedAt = transDate
            };

            transactions.Add(transaction);
        }

        context.CashRegisterTransactions.AddRange(transactions);
        await context.SaveChangesAsync();

        Console.WriteLine($"   ✓ حركات الخزينة: {transactions.Count}");
    }
}
