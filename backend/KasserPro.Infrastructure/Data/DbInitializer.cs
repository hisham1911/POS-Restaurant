namespace KasserPro.Infrastructure.Data;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        await context.Database.MigrateAsync();

        // Seed Default Tenant
        if (!await context.Tenants.AnyAsync())
        {
            var tenant = new Tenant
            {
                Name = "شركة كاشير برو للتجارة",
                NameEn = "KasserPro Trading Company",
                Slug = "kasserpro",
                Currency = "EGP",
                Timezone = "Africa/Cairo",
                IsActive = true,
                AllowNegativeStock = false, // منع البيع السالب
                // Receipt Settings
                ReceiptFooterMessage = "شكراً لزيارتكم - نتمنى لكم يوماً سعيداً",
                ReceiptShowLogo = true,
                ReceiptShowCustomerName = true,
                ReceiptPaperSize = "80mm",
                ReceiptPhoneNumber = "01000000001"
            };
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();
        }

        var defaultTenant = await context.Tenants.FirstAsync();

        // Seed Branches
        if (!await context.Branches.AnyAsync())
        {
            var branches = new List<Branch>
            {
                new()
                {
                    TenantId = defaultTenant.Id,
                    Name = "الفرع الرئيسي",
                    Code = "BR001",
                    Address = "شارع التحرير، وسط البلد، القاهرة",
                    Phone = "01000000001",
                    DefaultTaxRate = 14,
                    DefaultTaxInclusive = true,
                    CurrencyCode = "EGP",
                    IsActive = true
                },
                new()
                {
                    TenantId = defaultTenant.Id,
                    Name = "فرع المعادي",
                    Code = "BR002",
                    Address = "شارع 9، المعادي، القاهرة",
                    Phone = "01000000002",
                    DefaultTaxRate = 14,
                    DefaultTaxInclusive = true,
                    CurrencyCode = "EGP",
                    IsActive = true
                }
            };
            context.Branches.AddRange(branches);
            await context.SaveChangesAsync();
        }

        var defaultBranch = await context.Branches.FirstAsync();

        // Seed Users
        if (!await context.Users.AnyAsync())
        {
            var users = new List<User>
            {
                new()
                {
                    TenantId = defaultTenant.Id,
                    BranchId = defaultBranch.Id,
                    Name = "مدير النظام",
                    Email = "admin@kasserpro.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    Role = UserRole.Admin,
                    IsActive = true
                },
                new()
                {
                    TenantId = defaultTenant.Id,
                    BranchId = defaultBranch.Id,
                    Name = "أحمد محمد",
                    Email = "ahmed@kasserpro.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    Role = UserRole.Cashier,
                    IsActive = true
                },
                new()
                {
                    TenantId = defaultTenant.Id,
                    BranchId = defaultBranch.Id,
                    Name = "فاطمة علي",
                    Email = "fatima@kasserpro.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    Role = UserRole.Cashier,
                    IsActive = true
                },
                new()
                {
                    TenantId = defaultTenant.Id,
                    BranchId = defaultBranch.Id,
                    Name = "محمود حسن",
                    Email = "mahmoud@kasserpro.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    Role = UserRole.Cashier,
                    IsActive = true
                },
                new()
                {
                    TenantId = null,
                    BranchId = null,
                    Name = "System Owner",
                    Email = "owner@kasserpro.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Owner@123"),
                    Role = UserRole.SystemOwner,
                    IsActive = true
                }
            };
            context.Users.AddRange(users);
            await context.SaveChangesAsync();
        }

        // Seed Categories
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new() { TenantId = defaultTenant.Id, Name = "مشروبات ساخنة", NameEn = "Hot Drinks", SortOrder = 1, ImageUrl = "☕" },
                new() { TenantId = defaultTenant.Id, Name = "مشروبات باردة", NameEn = "Cold Drinks", SortOrder = 2, ImageUrl = "🥤" },
                new() { TenantId = defaultTenant.Id, Name = "مأكولات", NameEn = "Food", SortOrder = 3, ImageUrl = "🍔" },
                new() { TenantId = defaultTenant.Id, Name = "حلويات", NameEn = "Desserts", SortOrder = 4, ImageUrl = "🍰" },
                new() { TenantId = defaultTenant.Id, Name = "وجبات خفيفة", NameEn = "Snacks", SortOrder = 5, ImageUrl = "🍿" }
            };
            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
        }

        // Seed Products with Stock
        if (!await context.Products.AnyAsync())
        {
            var categories = await context.Categories.ToListAsync();

            var products = new List<Product>
            {
                // مشروبات ساخنة (Hot Drinks)
                new() { TenantId = defaultTenant.Id, Name = "قهوة إسبريسو", NameEn = "Espresso", Sku = "HOT001", Barcode = "6291041500213", Price = 25, Cost = 8, TaxRate = 14, TaxInclusive = true, CategoryId = categories[0].Id, ImageUrl = "☕", TrackInventory = true, LowStockThreshold = 30 },
                new() { TenantId = defaultTenant.Id, Name = "كابتشينو", NameEn = "Cappuccino", Sku = "HOT002", Barcode = "6291041500220", Price = 30, Cost = 10, TaxRate = 14, TaxInclusive = true, CategoryId = categories[0].Id, ImageUrl = "☕", TrackInventory = true, LowStockThreshold = 30 },
                new() { TenantId = defaultTenant.Id, Name = "لاتيه", NameEn = "Latte", Sku = "HOT003", Barcode = "6291041500237", Price = 32, Cost = 11, TaxRate = 14, TaxInclusive = true, CategoryId = categories[0].Id, ImageUrl = "☕", TrackInventory = true, LowStockThreshold = 30 },
                new() { TenantId = defaultTenant.Id, Name = "موكا", NameEn = "Mocha", Sku = "HOT004", Barcode = "6291041500244", Price = 35, Cost = 12, TaxRate = 14, TaxInclusive = true, CategoryId = categories[0].Id, ImageUrl = "☕", TrackInventory = true, LowStockThreshold = 25 },
                new() { TenantId = defaultTenant.Id, Name = "شاي أخضر", NameEn = "Green Tea", Sku = "HOT005", Barcode = "6291041500251", Price = 20, Cost = 5, TaxRate = 14, TaxInclusive = true, CategoryId = categories[0].Id, ImageUrl = "🍵", TrackInventory = true, LowStockThreshold = 40 },
                new() { TenantId = defaultTenant.Id, Name = "شاي أسود", NameEn = "Black Tea", Sku = "HOT006", Barcode = "6291041500268", Price = 18, Cost = 4, TaxRate = 14, TaxInclusive = true, CategoryId = categories[0].Id, ImageUrl = "🍵", TrackInventory = true, LowStockThreshold = 40 },
                new() { TenantId = defaultTenant.Id, Name = "شوكولاتة ساخنة", NameEn = "Hot Chocolate", Sku = "HOT007", Barcode = "6291041500275", Price = 28, Cost = 9, TaxRate = 14, TaxInclusive = true, CategoryId = categories[0].Id, ImageUrl = "🍫", TrackInventory = true, LowStockThreshold = 20 },
                new() { TenantId = defaultTenant.Id, Name = "قهوة تركية", NameEn = "Turkish Coffee", Sku = "HOT008", Barcode = "6291041500282", Price = 22, Cost = 7, TaxRate = 14, TaxInclusive = true, CategoryId = categories[0].Id, ImageUrl = "☕", TrackInventory = true, LowStockThreshold = 30 },

                // مشروبات باردة (Cold Drinks)
                new() { TenantId = defaultTenant.Id, Name = "عصير برتقال طازج", NameEn = "Fresh Orange Juice", Sku = "COLD001", Barcode = "6291041500299", Price = 25, Cost = 10, TaxRate = 14, TaxInclusive = true, CategoryId = categories[1].Id, ImageUrl = "🍊", TrackInventory = true, LowStockThreshold = 15 },
                new() { TenantId = defaultTenant.Id, Name = "عصير مانجو", NameEn = "Mango Juice", Sku = "COLD002", Barcode = "6291041500306", Price = 28, Cost = 12, TaxRate = 14, TaxInclusive = true, CategoryId = categories[1].Id, ImageUrl = "🥭", TrackInventory = true, LowStockThreshold = 15 },
                new() { TenantId = defaultTenant.Id, Name = "عصير فراولة", NameEn = "Strawberry Juice", Sku = "COLD003", Barcode = "6291041500313", Price = 30, Cost = 13, TaxRate = 14, TaxInclusive = true, CategoryId = categories[1].Id, ImageUrl = "🍓", TrackInventory = true, LowStockThreshold = 15 },
                new() { TenantId = defaultTenant.Id, Name = "سموذي موز", NameEn = "Banana Smoothie", Sku = "COLD004", Barcode = "6291041500320", Price = 32, Cost = 14, TaxRate = 14, TaxInclusive = true, CategoryId = categories[1].Id, ImageUrl = "🍌", TrackInventory = true, LowStockThreshold = 12 },
                new() { TenantId = defaultTenant.Id, Name = "مياه معدنية", NameEn = "Mineral Water", Sku = "COLD005", Barcode = "6291041500337", Price = 10, Cost = 3, TaxRate = 14, TaxInclusive = true, CategoryId = categories[1].Id, ImageUrl = "💧", TrackInventory = true, LowStockThreshold = 50 },
                new() { TenantId = defaultTenant.Id, Name = "مشروب غازي", NameEn = "Soft Drink", Sku = "COLD006", Barcode = "6291041500344", Price = 15, Cost = 5, TaxRate = 14, TaxInclusive = true, CategoryId = categories[1].Id, ImageUrl = "🥤", TrackInventory = true, LowStockThreshold = 40 },
                new() { TenantId = defaultTenant.Id, Name = "آيس كوفي", NameEn = "Iced Coffee", Sku = "COLD007", Barcode = "6291041500351", Price = 35, Cost = 12, TaxRate = 14, TaxInclusive = true, CategoryId = categories[1].Id, ImageUrl = "🧊", TrackInventory = true, LowStockThreshold = 20 },
                new() { TenantId = defaultTenant.Id, Name = "ليموناضة", NameEn = "Lemonade", Sku = "COLD008", Barcode = "6291041500368", Price = 22, Cost = 8, TaxRate = 14, TaxInclusive = true, CategoryId = categories[1].Id, ImageUrl = "🍋", TrackInventory = true, LowStockThreshold = 25 },

                // مأكولات (Food)
                new() { TenantId = defaultTenant.Id, Name = "برجر لحم", NameEn = "Beef Burger", Sku = "FOOD001", Barcode = "6291041500375", Price = 55, Cost = 25, TaxRate = 14, TaxInclusive = true, CategoryId = categories[2].Id, ImageUrl = "🍔", TrackInventory = true, LowStockThreshold = 15 },
                new() { TenantId = defaultTenant.Id, Name = "برجر دجاج", NameEn = "Chicken Burger", Sku = "FOOD002", Barcode = "6291041500382", Price = 50, Cost = 22, TaxRate = 14, TaxInclusive = true, CategoryId = categories[2].Id, ImageUrl = "🍔", TrackInventory = true, LowStockThreshold = 15 },
                new() { TenantId = defaultTenant.Id, Name = "ساندويتش كلوب", NameEn = "Club Sandwich", Sku = "FOOD003", Barcode = "6291041500399", Price = 45, Cost = 20, TaxRate = 14, TaxInclusive = true, CategoryId = categories[2].Id, ImageUrl = "🥪", TrackInventory = true, LowStockThreshold = 12 },
                new() { TenantId = defaultTenant.Id, Name = "بيتزا مارجريتا", NameEn = "Margherita Pizza", Sku = "FOOD004", Barcode = "6291041500406", Price = 65, Cost = 28, TaxRate = 14, TaxInclusive = true, CategoryId = categories[2].Id, ImageUrl = "🍕", TrackInventory = true, LowStockThreshold = 10 },
                new() { TenantId = defaultTenant.Id, Name = "باستا ألفريدو", NameEn = "Alfredo Pasta", Sku = "FOOD005", Barcode = "6291041500413", Price = 60, Cost = 26, TaxRate = 14, TaxInclusive = true, CategoryId = categories[2].Id, ImageUrl = "🍝", TrackInventory = true, LowStockThreshold = 10 },
                new() { TenantId = defaultTenant.Id, Name = "سلطة سيزر", NameEn = "Caesar Salad", Sku = "FOOD006", Barcode = "6291041500420", Price = 40, Cost = 18, TaxRate = 14, TaxInclusive = true, CategoryId = categories[2].Id, ImageUrl = "🥗", TrackInventory = true, LowStockThreshold = 12 },
                new() { TenantId = defaultTenant.Id, Name = "فطيرة جبن", NameEn = "Cheese Pie", Sku = "FOOD007", Barcode = "6291041500437", Price = 35, Cost = 15, TaxRate = 14, TaxInclusive = true, CategoryId = categories[2].Id, ImageUrl = "🥧", TrackInventory = true, LowStockThreshold = 15 },

                // حلويات (Desserts)
                new() { TenantId = defaultTenant.Id, Name = "كيك شوكولاتة", NameEn = "Chocolate Cake", Sku = "DES001", Barcode = "6291041500444", Price = 40, Cost = 16, TaxRate = 14, TaxInclusive = true, CategoryId = categories[3].Id, ImageUrl = "🍰", TrackInventory = true, LowStockThreshold = 8 },
                new() { TenantId = defaultTenant.Id, Name = "تشيز كيك", NameEn = "Cheesecake", Sku = "DES002", Barcode = "6291041500451", Price = 45, Cost = 18, TaxRate = 14, TaxInclusive = true, CategoryId = categories[3].Id, ImageUrl = "🍰", TrackInventory = true, LowStockThreshold = 8 },
                new() { TenantId = defaultTenant.Id, Name = "كوكيز", NameEn = "Cookies", Sku = "DES003", Barcode = "6291041500468", Price = 25, Cost = 8, TaxRate = 14, TaxInclusive = true, CategoryId = categories[3].Id, ImageUrl = "🍪", TrackInventory = true, LowStockThreshold = 20 },
                new() { TenantId = defaultTenant.Id, Name = "براونيز", NameEn = "Brownies", Sku = "DES004", Barcode = "6291041500475", Price = 30, Cost = 12, TaxRate = 14, TaxInclusive = true, CategoryId = categories[3].Id, ImageUrl = "🍫", TrackInventory = true, LowStockThreshold = 15 },
                new() { TenantId = defaultTenant.Id, Name = "آيس كريم", NameEn = "Ice Cream", Sku = "DES005", Barcode = "6291041500482", Price = 28, Cost = 10, TaxRate = 14, TaxInclusive = true, CategoryId = categories[3].Id, ImageUrl = "🍨", TrackInventory = true, LowStockThreshold = 18 },
                new() { TenantId = defaultTenant.Id, Name = "دونات", NameEn = "Donuts", Sku = "DES006", Barcode = "6291041500499", Price = 22, Cost = 7, TaxRate = 14, TaxInclusive = true, CategoryId = categories[3].Id, ImageUrl = "🍩", TrackInventory = true, LowStockThreshold = 15 },
                new() { TenantId = defaultTenant.Id, Name = "كرواسون", NameEn = "Croissant", Sku = "DES007", Barcode = "6291041500506", Price = 20, Cost = 6, TaxRate = 14, TaxInclusive = true, CategoryId = categories[3].Id, ImageUrl = "🥐", TrackInventory = true, LowStockThreshold = 20 },

                // وجبات خفيفة (Snacks)
                new() { TenantId = defaultTenant.Id, Name = "شيبس", NameEn = "Chips", Sku = "SNK001", Barcode = "6291041500513", Price = 15, Cost = 5, TaxRate = 14, TaxInclusive = true, CategoryId = categories[4].Id, ImageUrl = "🥔", TrackInventory = true, LowStockThreshold = 35 },
                new() { TenantId = defaultTenant.Id, Name = "فشار", NameEn = "Popcorn", Sku = "SNK002", Barcode = "6291041500520", Price = 18, Cost = 6, TaxRate = 14, TaxInclusive = true, CategoryId = categories[4].Id, ImageUrl = "🍿", TrackInventory = true, LowStockThreshold = 25 },
                new() { TenantId = defaultTenant.Id, Name = "مكسرات", NameEn = "Mixed Nuts", Sku = "SNK003", Barcode = "6291041500537", Price = 35, Cost = 15, TaxRate = 14, TaxInclusive = true, CategoryId = categories[4].Id, ImageUrl = "🥜", TrackInventory = true, LowStockThreshold = 18 },
                new() { TenantId = defaultTenant.Id, Name = "بسكويت", NameEn = "Biscuits", Sku = "SNK004", Barcode = "6291041500544", Price = 12, Cost = 4, TaxRate = 14, TaxInclusive = true, CategoryId = categories[4].Id, ImageUrl = "🍪", TrackInventory = true, LowStockThreshold = 30 },
                new() { TenantId = defaultTenant.Id, Name = "شوكولاتة", NameEn = "Chocolate Bar", Sku = "SNK005", Barcode = "6291041500551", Price = 20, Cost = 8, TaxRate = 14, TaxInclusive = true, CategoryId = categories[4].Id, ImageUrl = "🍫", TrackInventory = true, LowStockThreshold = 28 },
                new() { TenantId = defaultTenant.Id, Name = "علكة", NameEn = "Chewing Gum", Sku = "SNK006", Barcode = "6291041500568", Price = 8, Cost = 2, TaxRate = 14, TaxInclusive = true, CategoryId = categories[4].Id, ImageUrl = "🍬", TrackInventory = true, LowStockThreshold = 40 },
                new() { TenantId = defaultTenant.Id, Name = "حلوى", NameEn = "Candy", Sku = "SNK007", Barcode = "6291041500575", Price = 10, Cost = 3, TaxRate = 14, TaxInclusive = true, CategoryId = categories[4].Id, ImageUrl = "🍭", TrackInventory = true, LowStockThreshold = 38 }
            };

            // Set LastStockUpdate for all products
            foreach (var p in products)
            {
                p.LastStockUpdate = DateTime.UtcNow;
            }

            context.Products.AddRange(products);
            await context.SaveChangesAsync();
        }

        // Seed Customers
        if (!await context.Customers.AnyAsync())
        {
            var customers = new List<Customer>
            {
                new() { TenantId = defaultTenant.Id, Name = "محمد أحمد علي", Phone = "01001234567", Email = "mohamed@email.com", Address = "شارع المعز، القاهرة", LoyaltyPoints = 150, TotalOrders = 12, TotalSpent = 2500, LastOrderAt = DateTime.UtcNow.AddDays(-2), IsActive = true },
                new() { TenantId = defaultTenant.Id, Name = "فاطمة حسن", Phone = "01112345678", Email = "fatma@email.com", Address = "المهندسين، الجيزة", LoyaltyPoints = 280, TotalOrders = 25, TotalSpent = 4800, LastOrderAt = DateTime.UtcNow.AddDays(-1), IsActive = true },
                new() { TenantId = defaultTenant.Id, Name = "أحمد محمود", Phone = "01223456789", Email = "ahmed.m@email.com", Address = "مدينة نصر، القاهرة", LoyaltyPoints = 75, TotalOrders = 5, TotalSpent = 980, LastOrderAt = DateTime.UtcNow.AddDays(-5), IsActive = true },
                new() { TenantId = defaultTenant.Id, Name = "سارة عبدالله", Phone = "01098765432", Email = "sara.a@email.com", Address = "الدقي، الجيزة", LoyaltyPoints = 420, TotalOrders = 35, TotalSpent = 7200, LastOrderAt = DateTime.UtcNow.AddDays(-3), IsActive = true },
                new() { TenantId = defaultTenant.Id, Name = "علي حسين", Phone = "01198765432", Email = null, Address = "حلوان، القاهرة", LoyaltyPoints = 50, TotalOrders = 3, TotalSpent = 650, LastOrderAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
                new() { TenantId = defaultTenant.Id, Name = "نور الدين", Phone = "01287654321", Email = "nour@email.com", Address = null, LoyaltyPoints = 180, TotalOrders = 15, TotalSpent = 3100, LastOrderAt = DateTime.UtcNow.AddDays(-4), IsActive = true },
                new() { TenantId = defaultTenant.Id, Name = "ياسمين خالد", Phone = "01087654321", Email = "yasmine@email.com", Address = "الزمالك، القاهرة", LoyaltyPoints = 320, TotalOrders = 28, TotalSpent = 5500, LastOrderAt = DateTime.UtcNow.AddDays(-1), IsActive = true },
                new() { TenantId = defaultTenant.Id, Name = "كريم سعيد", Phone = "01187654321", Email = null, Address = "العباسية", LoyaltyPoints = 90, TotalOrders = 8, TotalSpent = 1200, LastOrderAt = DateTime.UtcNow.AddDays(-7), IsActive = true }
            };
            context.Customers.AddRange(customers);
            await context.SaveChangesAsync();
        }

        // Seed Suppliers
        if (!await context.Suppliers.AnyAsync())
        {
            var suppliers = new List<Supplier>
            {
                new() { TenantId = defaultTenant.Id, BranchId = defaultBranch.Id, Name = "شركة البن العربي", Phone = "0233334444", Email = "info@arabcoffee.com", Address = "شارع الجمهورية، القاهرة", ContactPerson = "أحمد محمود", TaxNumber = "123-456-789", Notes = "مورد رئيسي للقهوة والشاي", IsActive = true },
                new() { TenantId = defaultTenant.Id, BranchId = defaultBranch.Id, Name = "مؤسسة الألبان الطازجة", Phone = "0244445555", Email = "sales@fresh-dairy.com", Address = "المنطقة الصناعية، العاشر من رمضان", ContactPerson = "سارة علي", TaxNumber = "234-567-890", Notes = "متخصصون في منتجات الألبان والحليب", IsActive = true },
                new() { TenantId = defaultTenant.Id, BranchId = defaultBranch.Id, Name = "شركة الفواكه والعصائر", Phone = "0255556666", Email = "contact@fruits-juice.com", Address = "سوق العبور، القاهرة", ContactPerson = "محمد حسن", TaxNumber = "345-678-901", Notes = "فواكه طازجة ومواد خام للعصائر", IsActive = true },
                new() { TenantId = defaultTenant.Id, BranchId = defaultBranch.Id, Name = "مخبز الأمل", Phone = "0266667777", Email = "orders@amal-bakery.com", Address = "وسط البلد، القاهرة", ContactPerson = "فاطمة أحمد", TaxNumber = "456-789-012", Notes = "مخبوزات وحلويات طازجة", IsActive = true },
                new() { TenantId = defaultTenant.Id, BranchId = defaultBranch.Id, Name = "شركة المواد الغذائية المتحدة", Phone = "0277778888", Email = "info@united-foods.com", Address = "مدينة نصر، القاهرة", ContactPerson = "خالد سعيد", TaxNumber = "567-890-123", Notes = "مواد غذائية ومشروبات متنوعة", IsActive = true }
            };
            context.Suppliers.AddRange(suppliers);
            await context.SaveChangesAsync();
        }

        // Seed Shifts and Orders with varied dates
        if (!await context.Shifts.AnyAsync())
        {
            var admin = await context.Users.FirstAsync(u => u.Role == UserRole.Admin);
            var products = await context.Products.ToListAsync();
            var customers = await context.Customers.ToListAsync();
            var random = new Random(42);

            // Create shifts for the past 14 days
            for (int day = 14; day >= 0; day--)
            {
                var shiftDate = DateTime.UtcNow.Date.AddDays(-day);
                var isClosed = day > 0; // Only today's shift is open

                var shift = new Shift
                {
                    TenantId = defaultTenant.Id,
                    BranchId = defaultBranch.Id,
                    UserId = admin.Id,
                    OpeningBalance = 500,
                    OpenedAt = shiftDate.AddHours(9),
                    LastActivityAt = shiftDate.AddHours(9), // Initialize LastActivityAt
                    IsClosed = isClosed,
                    IsForceClosed = false,
                    IsHandedOver = false,
                    HandoverBalance = 0
                };

                if (isClosed)
                {
                    shift.ClosedAt = shiftDate.AddHours(21);
                    shift.LastActivityAt = shiftDate.AddHours(21); // Update to close time
                    shift.Notes = $"وردية يوم {shiftDate:yyyy-MM-dd}";
                }

                context.Shifts.Add(shift);
                await context.SaveChangesAsync();

                // Create orders for this shift (more orders on weekends)
                var isWeekend = shiftDate.DayOfWeek == DayOfWeek.Friday || shiftDate.DayOfWeek == DayOfWeek.Saturday;
                var orderCount = day == 0 ? random.Next(3, 6) : (isWeekend ? random.Next(10, 18) : random.Next(5, 12));

                var orders = new List<Order>();
                decimal totalCash = 0;
                decimal totalCard = 0;

                for (int i = 0; i < orderCount; i++)
                {
                    var orderTime = shift.OpenedAt.AddMinutes(random.Next(30, 700));
                    var status = OrderStatus.Completed;

                    // Assign customer to some orders
                    Customer? customer = random.Next(3) == 0 ? customers[random.Next(customers.Count)] : null;

                    var order = CreateSampleOrder(
                        defaultTenant.Id, defaultBranch.Id, admin.Id, shift.Id,
                        products, random, orderTime, (day * 100) + i + 1, status, customer
                    );
                    orders.Add(order);

                    if (status == OrderStatus.Completed)
                    {
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

                context.Orders.AddRange(orders);
                await context.SaveChangesAsync();

                // Update shift totals
                if (isClosed)
                {
                    var completedCount = orders.Count(o => o.Status == OrderStatus.Completed);
                    shift.TotalOrders = completedCount;
                    shift.TotalCash = totalCash;
                    shift.TotalCard = totalCard;
                    shift.ExpectedBalance = shift.OpeningBalance + totalCash;
                    shift.ClosingBalance = shift.ExpectedBalance + random.Next(-20, 50);
                    shift.Difference = shift.ClosingBalance - shift.ExpectedBalance;
                    await context.SaveChangesAsync();
                }
            }

            // Deduct stock for completed orders
            var completedOrders = await context.Orders
                .Include(o => o.Items)
                .Where(o => o.Status == OrderStatus.Completed)
                .ToListAsync();

            // Stock is now managed through BranchInventory and StockMovements
            // No need to manually update product stock here
            await context.SaveChangesAsync();
        }
    }

    private static Order CreateSampleOrder(
        int tenantId, int branchId, int userId, int shiftId,
        List<Product> products, Random random, DateTime orderTime, int orderNum,
        OrderStatus status, Customer? customer = null)
    {
        var order = new Order
        {
            TenantId = tenantId,
            BranchId = branchId,
            ShiftId = shiftId,
            OrderNumber = $"ORD-{orderTime:yyyyMMdd}-{orderNum:D4}",
            UserId = userId,
            Status = status,
            OrderType = random.Next(10) < 8 ? OrderType.DineIn : OrderType.Delivery,
            CreatedAt = orderTime,
            BranchName = "الفرع الرئيسي",
            BranchAddress = "شارع التحرير، وسط البلد، القاهرة",
            BranchPhone = "01000000001",
            UserName = "مدير النظام",
            CurrencyCode = "EGP",
            TaxRate = 14
        };

        // Add customer if provided
        if (customer != null)
        {
            order.CustomerId = customer.Id;
            order.CustomerName = customer.Name;
            order.CustomerPhone = customer.Phone;
        }

        var itemCount = random.Next(1, 5);
        decimal subtotal = 0;
        decimal taxAmount = 0;

        var usedProducts = new HashSet<int>();
        for (int j = 0; j < itemCount; j++)
        {
            Product product;
            do
            {
                product = products[random.Next(products.Count)];
            } while (usedProducts.Contains(product.Id) && usedProducts.Count < products.Count);

            usedProducts.Add(product.Id);
            var qty = random.Next(1, 4);

            var grossPrice = product.Price * qty;
            var netPrice = grossPrice / 1.14m;
            var itemTax = grossPrice - netPrice;

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
                TaxInclusive = true,
                TaxAmount = Math.Round(itemTax, 2),
                Subtotal = grossPrice,
                Total = grossPrice
            };

            order.Items.Add(orderItem);
            subtotal += grossPrice;
            taxAmount += itemTax;
        }

        order.Subtotal = subtotal;
        order.TaxAmount = Math.Round(taxAmount, 2);
        order.Total = subtotal;

        if (status == OrderStatus.Completed)
        {
            order.AmountPaid = order.Total;
            order.AmountDue = 0;
            order.CompletedAt = orderTime.AddMinutes(random.Next(5, 20));
            order.CompletedByUserId = userId;

            var paymentMethod = random.Next(10) < 7 ? PaymentMethod.Cash :
                (random.Next(2) == 0 ? PaymentMethod.Card : PaymentMethod.Fawry);

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
            order.CancelledAt = orderTime.AddMinutes(random.Next(10, 30));
            order.CancellationReason = "طلب العميل الإلغاء";
        }

        return order;
    }
}
