namespace KasserPro.Infrastructure.Data;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Realistic Data Seeder - يولد بيانات واقعية لآخر 120 يوم (مبسط)
/// </summary>
public static class RealisticDataSeeder
{
    private static readonly Random _random = new(2026);
    private static readonly DateTime StartDate = DateTime.UtcNow.AddDays(-120);
    private static readonly DateTime EndDate = DateTime.UtcNow;

    public static async Task SeedAsync(AppDbContext context)
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  🚀 بدء تحميل البيانات الواقعية - 120 يوم كاملة          ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Slug == "supermarket");
        if (tenant == null)
        {
            Console.WriteLine("❌ لم يتم العثور على سوبر ماركت");
            return;
        }

        var ordersCount = await context.Orders.CountAsync(o => o.TenantId == tenant.Id);
        if (ordersCount > 500)
        {
            Console.WriteLine($"✅ البيانات الواقعية موجودة مسبقاً ({ordersCount} طلب)");
            return;
        }

        Console.WriteLine("🔄 تحميل البيانات...");

        var branch = await context.Branches.FirstAsync(b => b.TenantId == tenant.Id);
        var admin = await context.Users.FirstAsync(u => u.TenantId == tenant.Id && u.Role == UserRole.Admin);

        // Enhance categories and products
        await EnhanceCategoriesAndProductsAsync(context, tenant);

        var products = await context.Products.Where(p => p.TenantId == tenant.Id).ToListAsync();
        var customers = await context.Customers.Where(c => c.TenantId == tenant.Id).ToListAsync();

        if (products.Count == 0)
        {
            Console.WriteLine("⚠ لا توجد منتجات متاحة - تخطي تحميل البيانات الواقعية");
            return;
        }

        // Create suppliers
        var suppliers = await CreateSuppliersAsync(context, tenant, branch);

        // Create purchase invoices and update inventory
        await CreatePurchaseInvoicesAsync(context, tenant, branch, admin, products, suppliers);

        // Generate shifts and orders for 120 days
        await GenerateShiftsAndOrdersAsync(context, tenant, branch, admin, products, customers);

        // Generate branch transfers (if multiple branches exist)
        await GenerateBranchTransfersAsync(context, tenant, branch, admin, products);

        Console.WriteLine("\n✅ تم تحميل البيانات بنجاح!");
        await PrintStatisticsAsync(context, tenant.Id);
    }

    private static async Task EnhanceCategoriesAndProductsAsync(AppDbContext context, Tenant tenant)
    {
        // Check if already enhanced
        var productCount = await context.Products.CountAsync(p => p.TenantId == tenant.Id);
        if (productCount > 20)
        {
            Console.WriteLine("   ✓ المنتجات موجودة مسبقاً");
            return;
        }

        Console.WriteLine("📦 إنشاء الأصناف والمنتجات...");

        // Clear existing categories and products
        var existingCategories = await context.Categories.Where(c => c.TenantId == tenant.Id).ToListAsync();
        var existingProducts = await context.Products.Where(p => p.TenantId == tenant.Id).ToListAsync();
        context.Products.RemoveRange(existingProducts);
        context.Categories.RemoveRange(existingCategories);
        await context.SaveChangesAsync();

        // Create comprehensive categories
        var categories = new List<Category>
        {
            new() { TenantId = tenant.Id, Name = "بقالة", NameEn = "Grocery", SortOrder = 1, IsActive = true },
            new() { TenantId = tenant.Id, Name = "مشروبات", NameEn = "Beverages", SortOrder = 2, IsActive = true },
            new() { TenantId = tenant.Id, Name = "ألبان ومنتجات الحليب", NameEn = "Dairy Products", SortOrder = 3, IsActive = true },
            new() { TenantId = tenant.Id, Name = "خبز ومخبوزات", NameEn = "Bakery", SortOrder = 4, IsActive = true },
            new() { TenantId = tenant.Id, Name = "لحوم ودواجن", NameEn = "Meat & Poultry", SortOrder = 5, IsActive = true },
            new() { TenantId = tenant.Id, Name = "خضروات وفواكه", NameEn = "Fruits & Vegetables", SortOrder = 6, IsActive = true },
            new() { TenantId = tenant.Id, Name = "حلويات وشوكولاتة", NameEn = "Sweets & Chocolate", SortOrder = 7, IsActive = true },
            new() { TenantId = tenant.Id, Name = "منظفات", NameEn = "Cleaning Products", SortOrder = 8, IsActive = true },
            new() { TenantId = tenant.Id, Name = "عناية شخصية", NameEn = "Personal Care", SortOrder = 9, IsActive = true },
            new() { TenantId = tenant.Id, Name = "أدوات منزلية", NameEn = "Household Items", SortOrder = 10, IsActive = true }
        };

        context.Categories.AddRange(categories);
        await context.SaveChangesAsync();

        // Create products for each category
        var products = new List<Product>();
        int skuCounter = 1000;

        // بقالة (Grocery)
        var groceryCategory = categories[0];
        products.AddRange(new[]
        {
            CreateProduct(tenant.Id, groceryCategory.Id, "أرز أبيض 1 كجم", "White Rice 1kg", $"GRC{skuCounter++}", 45, 35, 500),
            CreateProduct(tenant.Id, groceryCategory.Id, "أرز بسمتي 1 كجم", "Basmati Rice 1kg", $"GRC{skuCounter++}", 65, 52, 300),
            CreateProduct(tenant.Id, groceryCategory.Id, "سكر 1 كجم", "Sugar 1kg", $"GRC{skuCounter++}", 35, 28, 400),
            CreateProduct(tenant.Id, groceryCategory.Id, "زيت طعام 1 لتر", "Cooking Oil 1L", $"GRC{skuCounter++}", 65, 52, 300),
            CreateProduct(tenant.Id, groceryCategory.Id, "زيت زيتون 500 مل", "Olive Oil 500ml", $"GRC{skuCounter++}", 120, 95, 100),
            CreateProduct(tenant.Id, groceryCategory.Id, "معكرونة", "Pasta", $"GRC{skuCounter++}", 18, 12, 600),
            CreateProduct(tenant.Id, groceryCategory.Id, "طحين 1 كجم", "Flour 1kg", $"GRC{skuCounter++}", 22, 16, 350),
            CreateProduct(tenant.Id, groceryCategory.Id, "ملح", "Salt", $"GRC{skuCounter++}", 8, 5, 800),
            CreateProduct(tenant.Id, groceryCategory.Id, "شاي أحمر", "Black Tea", $"GRC{skuCounter++}", 42, 32, 250),
            CreateProduct(tenant.Id, groceryCategory.Id, "شاي أخضر", "Green Tea", $"GRC{skuCounter++}", 55, 42, 180),
            CreateProduct(tenant.Id, groceryCategory.Id, "قهوة تركي", "Turkish Coffee", $"GRC{skuCounter++}", 85, 65, 180),
            CreateProduct(tenant.Id, groceryCategory.Id, "نسكافيه", "Nescafe", $"GRC{skuCounter++}", 95, 75, 200),
            CreateProduct(tenant.Id, groceryCategory.Id, "عدس أصفر", "Yellow Lentils", $"GRC{skuCounter++}", 28, 20, 220),
            CreateProduct(tenant.Id, groceryCategory.Id, "فول مدمس", "Fava Beans", $"GRC{skuCounter++}", 25, 18, 280),
            CreateProduct(tenant.Id, groceryCategory.Id, "حمص", "Chickpeas", $"GRC{skuCounter++}", 32, 24, 200),
            CreateProduct(tenant.Id, groceryCategory.Id, "فاصوليا بيضاء", "White Beans", $"GRC{skuCounter++}", 30, 22, 190),
            CreateProduct(tenant.Id, groceryCategory.Id, "عسل نحل 500 جم", "Honey 500g", $"GRC{skuCounter++}", 150, 120, 80),
            CreateProduct(tenant.Id, groceryCategory.Id, "مربى فراولة", "Strawberry Jam", $"GRC{skuCounter++}", 45, 35, 150),
            CreateProduct(tenant.Id, groceryCategory.Id, "طحينة", "Tahini", $"GRC{skuCounter++}", 38, 28, 120),
            CreateProduct(tenant.Id, groceryCategory.Id, "حلاوة طحينية", "Halva", $"GRC{skuCounter++}", 42, 32, 140)
        });

        // مشروبات (Beverages)
        var beverageCategory = categories[1];
        products.AddRange(new[]
        {
            CreateProduct(tenant.Id, beverageCategory.Id, "عصير برتقال", "Orange Juice", $"BEV{skuCounter++}", 15, 10, 450),
            CreateProduct(tenant.Id, beverageCategory.Id, "عصير مانجو", "Mango Juice", $"BEV{skuCounter++}", 18, 12, 380),
            CreateProduct(tenant.Id, beverageCategory.Id, "عصير تفاح", "Apple Juice", $"BEV{skuCounter++}", 16, 11, 400),
            CreateProduct(tenant.Id, beverageCategory.Id, "عصير جوافة", "Guava Juice", $"BEV{skuCounter++}", 14, 9, 350),
            CreateProduct(tenant.Id, beverageCategory.Id, "مياه معدنية صغيرة", "Small Water Bottle", $"BEV{skuCounter++}", 5, 3, 1000),
            CreateProduct(tenant.Id, beverageCategory.Id, "مياه معدنية كبيرة", "Large Water Bottle", $"BEV{skuCounter++}", 8, 5, 800),
            CreateProduct(tenant.Id, beverageCategory.Id, "مشروب غازي كولا", "Cola Soft Drink", $"BEV{skuCounter++}", 12, 8, 600),
            CreateProduct(tenant.Id, beverageCategory.Id, "مشروب غازي برتقال", "Orange Soda", $"BEV{skuCounter++}", 12, 8, 550),
            CreateProduct(tenant.Id, beverageCategory.Id, "مشروب طاقة", "Energy Drink", $"BEV{skuCounter++}", 25, 18, 250),
            CreateProduct(tenant.Id, beverageCategory.Id, "شاي مثلج", "Iced Tea", $"BEV{skuCounter++}", 18, 13, 300)
        });

        // ألبان ومنتجات الحليب (Dairy)
        var dairyCategory = categories[2];
        products.AddRange(new[]
        {
            CreateProduct(tenant.Id, dairyCategory.Id, "حليب طازج 1 لتر", "Fresh Milk 1L", $"DRY{skuCounter++}", 28, 22, 200),
            CreateProduct(tenant.Id, dairyCategory.Id, "حليب كامل الدسم", "Full Cream Milk", $"DRY{skuCounter++}", 32, 25, 180),
            CreateProduct(tenant.Id, dairyCategory.Id, "زبادي طبيعي", "Natural Yogurt", $"DRY{skuCounter++}", 12, 8, 350),
            CreateProduct(tenant.Id, dairyCategory.Id, "زبادي بالفواكه", "Fruit Yogurt", $"DRY{skuCounter++}", 15, 10, 300),
            CreateProduct(tenant.Id, dairyCategory.Id, "لبن رايب", "Laban", $"DRY{skuCounter++}", 10, 7, 300),
            CreateProduct(tenant.Id, groceryCategory.Id, "جبنة بيضاء", "White Cheese", $"DRY{skuCounter++}", 55, 42, 150),
            CreateProduct(tenant.Id, dairyCategory.Id, "جبنة شيدر", "Cheddar Cheese", $"DRY{skuCounter++}", 65, 50, 120),
            CreateProduct(tenant.Id, dairyCategory.Id, "جبنة موتزاريلا", "Mozzarella Cheese", $"DRY{skuCounter++}", 75, 58, 100),
            CreateProduct(tenant.Id, dairyCategory.Id, "قشطة", "Cream", $"DRY{skuCounter++}", 35, 27, 180),
            CreateProduct(tenant.Id, dairyCategory.Id, "زبدة 250 جم", "Butter 250g", $"DRY{skuCounter++}", 48, 38, 200)
        });

        // خبز ومخبوزات (Bakery)
        var bakeryCategory = categories[3];
        products.AddRange(new[]
        {
            CreateProduct(tenant.Id, bakeryCategory.Id, "خبز بلدي", "Baladi Bread", $"BKR{skuCounter++}", 5, 3, 500),
            CreateProduct(tenant.Id, bakeryCategory.Id, "خبز فينو", "Fino Bread", $"BKR{skuCounter++}", 8, 5, 400),
            CreateProduct(tenant.Id, bakeryCategory.Id, "خبز توست", "Toast Bread", $"BKR{skuCounter++}", 18, 13, 250),
            CreateProduct(tenant.Id, bakeryCategory.Id, "كرواسون", "Croissant", $"BKR{skuCounter++}", 12, 8, 200),
            CreateProduct(tenant.Id, bakeryCategory.Id, "كعك", "Kahk", $"BKR{skuCounter++}", 25, 18, 150)
        });

        // لحوم ودواجن (Meat & Poultry)
        var meatCategory = categories[4];
        products.AddRange(new[]
        {
            CreateProduct(tenant.Id, meatCategory.Id, "دجاج مجمد 1 كجم", "Frozen Chicken 1kg", $"MET{skuCounter++}", 85, 68, 100),
            CreateProduct(tenant.Id, meatCategory.Id, "لحم بقري 1 كجم", "Beef 1kg", $"MET{skuCounter++}", 250, 200, 50),
            CreateProduct(tenant.Id, meatCategory.Id, "لحم ضاني 1 كجم", "Lamb 1kg", $"MET{skuCounter++}", 280, 225, 40),
            CreateProduct(tenant.Id, meatCategory.Id, "سجق", "Sausages", $"MET{skuCounter++}", 65, 50, 120),
            CreateProduct(tenant.Id, meatCategory.Id, "برجر مجمد", "Frozen Burger", $"MET{skuCounter++}", 55, 42, 150)
        });

        // خضروات وفواكه (Fruits & Vegetables)
        var produceCategory = categories[5];
        products.AddRange(new[]
        {
            CreateProduct(tenant.Id, produceCategory.Id, "طماطم 1 كجم", "Tomatoes 1kg", $"PRD{skuCounter++}", 15, 10, 200),
            CreateProduct(tenant.Id, produceCategory.Id, "خيار 1 كجم", "Cucumber 1kg", $"PRD{skuCounter++}", 12, 8, 180),
            CreateProduct(tenant.Id, produceCategory.Id, "بطاطس 1 كجم", "Potatoes 1kg", $"PRD{skuCounter++}", 18, 13, 250),
            CreateProduct(tenant.Id, produceCategory.Id, "بصل 1 كجم", "Onions 1kg", $"PRD{skuCounter++}", 14, 10, 220),
            CreateProduct(tenant.Id, produceCategory.Id, "موز 1 كجم", "Bananas 1kg", $"PRD{skuCounter++}", 25, 18, 150),
            CreateProduct(tenant.Id, produceCategory.Id, "تفاح 1 كجم", "Apples 1kg", $"PRD{skuCounter++}", 35, 27, 120),
            CreateProduct(tenant.Id, produceCategory.Id, "برتقال 1 كجم", "Oranges 1kg", $"PRD{skuCounter++}", 22, 16, 180)
        });

        // حلويات وشوكولاتة (Sweets)
        var sweetsCategory = categories[6];
        products.AddRange(new[]
        {
            CreateProduct(tenant.Id, sweetsCategory.Id, "شوكولاتة", "Chocolate Bar", $"SWT{skuCounter++}", 15, 10, 300),
            CreateProduct(tenant.Id, sweetsCategory.Id, "بسكويت", "Biscuits", $"SWT{skuCounter++}", 12, 8, 400),
            CreateProduct(tenant.Id, sweetsCategory.Id, "ويفر", "Wafer", $"SWT{skuCounter++}", 10, 7, 350),
            CreateProduct(tenant.Id, sweetsCategory.Id, "حلوى جيلي", "Jelly Candy", $"SWT{skuCounter++}", 8, 5, 500),
            CreateProduct(tenant.Id, sweetsCategory.Id, "علكة", "Chewing Gum", $"SWT{skuCounter++}", 5, 3, 600)
        });

        // منظفات (Cleaning)
        var cleaningCategory = categories[7];
        products.AddRange(new[]
        {
            CreateProduct(tenant.Id, cleaningCategory.Id, "صابون سائل", "Liquid Soap", $"CLN{skuCounter++}", 35, 25, 280),
            CreateProduct(tenant.Id, cleaningCategory.Id, "مسحوق غسيل", "Washing Powder", $"CLN{skuCounter++}", 55, 42, 220),
            CreateProduct(tenant.Id, cleaningCategory.Id, "منظف أرضيات", "Floor Cleaner", $"CLN{skuCounter++}", 28, 20, 180),
            CreateProduct(tenant.Id, cleaningCategory.Id, "منظف زجاج", "Glass Cleaner", $"CLN{skuCounter++}", 22, 16, 150),
            CreateProduct(tenant.Id, cleaningCategory.Id, "إسفنجة", "Sponge", $"CLN{skuCounter++}", 8, 5, 400),
            CreateProduct(tenant.Id, cleaningCategory.Id, "فوط مطبخ", "Kitchen Towels", $"CLN{skuCounter++}", 18, 12, 320),
            CreateProduct(tenant.Id, cleaningCategory.Id, "كلور", "Bleach", $"CLN{skuCounter++}", 15, 10, 250),
            CreateProduct(tenant.Id, cleaningCategory.Id, "معطر جو", "Air Freshener", $"CLN{skuCounter++}", 32, 24, 180)
        });

        // عناية شخصية (Personal Care)
        var personalCareCategory = categories[8];
        products.AddRange(new[]
        {
            CreateProduct(tenant.Id, personalCareCategory.Id, "شامبو", "Shampoo", $"PRC{skuCounter++}", 45, 35, 200),
            CreateProduct(tenant.Id, personalCareCategory.Id, "صابون استحمام", "Bath Soap", $"PRC{skuCounter++}", 18, 13, 300),
            CreateProduct(tenant.Id, personalCareCategory.Id, "معجون أسنان", "Toothpaste", $"PRC{skuCounter++}", 28, 21, 250),
            CreateProduct(tenant.Id, personalCareCategory.Id, "فرشاة أسنان", "Toothbrush", $"PRC{skuCounter++}", 15, 10, 350),
            CreateProduct(tenant.Id, personalCareCategory.Id, "مزيل عرق", "Deodorant", $"PRC{skuCounter++}", 38, 28, 180),
            CreateProduct(tenant.Id, personalCareCategory.Id, "كريم مرطب", "Moisturizer", $"PRC{skuCounter++}", 55, 42, 150)
        });

        // أدوات منزلية (Household)
        var householdCategory = categories[9];
        products.AddRange(new[]
        {
            CreateProduct(tenant.Id, householdCategory.Id, "أكياس قمامة", "Garbage Bags", $"HHS{skuCounter++}", 25, 18, 300),
            CreateProduct(tenant.Id, householdCategory.Id, "ورق مطبخ", "Kitchen Paper", $"HHS{skuCounter++}", 22, 16, 250),
            CreateProduct(tenant.Id, householdCategory.Id, "مناديل ورقية", "Tissues", $"HHS{skuCounter++}", 18, 13, 400),
            CreateProduct(tenant.Id, householdCategory.Id, "أطباق بلاستيك", "Plastic Plates", $"HHS{skuCounter++}", 15, 10, 200),
            CreateProduct(tenant.Id, householdCategory.Id, "أكواب بلاستيك", "Plastic Cups", $"HHS{skuCounter++}", 12, 8, 250)
        });

        context.Products.AddRange(products);
        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ تم إنشاء {categories.Count} صنف و {products.Count} منتج");
    }

    private static Product CreateProduct(int tenantId, int categoryId, string name, string nameEn, string sku, decimal price, decimal cost, int stock)
    {
        return new Product
        {
            TenantId = tenantId,
            CategoryId = categoryId,
            Name = name,
            NameEn = nameEn,
            Sku = sku,
            Barcode = $"BAR{sku}",
            Price = price,
            Cost = cost,
            TaxRate = 14,
            TaxInclusive = false,
            TrackInventory = true,
            StockQuantity = 0, // Start with 0, will be updated by purchase invoices
            LowStockThreshold = (int)(stock * 0.1m),
            ReorderPoint = (int)(stock * 0.15m),
            IsActive = true,
            Type = ProductType.Physical
        };
    }

    private static async Task GenerateShiftsAndOrdersAsync(
        AppDbContext context, Tenant tenant, Branch branch, User admin,
        List<Product> products, List<Customer> customers)
    {
        var shifts = new List<Shift>();
        var orders = new List<Order>();
        int orderNumber = 10000;

        // Create a pool of returning customers (30% of customers will be frequent buyers)
        var returningCustomers = customers.Take((int)(customers.Count * 0.3)).ToList();

        // Generate shifts for 120 days (1 shift per day for simplicity)
        for (int day = 120; day >= 0; day--)
        {
            var shiftDate = StartDate.AddDays(day);
            var isWeekend = shiftDate.DayOfWeek == DayOfWeek.Friday || shiftDate.DayOfWeek == DayOfWeek.Saturday;

            var shift = new Shift
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                UserId = admin.Id,
                OpeningBalance = 2000,
                OpenedAt = shiftDate.AddHours(8),
                LastActivityAt = shiftDate.AddHours(8),
                IsClosed = true, // All historical shifts are closed
                IsForceClosed = false,
                IsHandedOver = false,
                HandoverBalance = 0,
                ClosedAt = shiftDate.AddHours(22)
            };

            shift.LastActivityAt = shift.ClosedAt.Value;

            context.Shifts.Add(shift);
            await context.SaveChangesAsync();

            // Generate orders for this shift
            var orderCount = isWeekend ? _random.Next(15, 25) : _random.Next(10, 18);

            decimal totalCash = 0;
            decimal totalCard = 0;
            int completedCount = 0;

            for (int i = 0; i < orderCount; i++)
            {
                var orderTime = shiftDate.AddMinutes(_random.Next(480, 840)); // 8am to 10pm

                // All historical orders are completed (85%), cancelled (10%), or refunded (5%)
                OrderStatus status;
                var statusRoll = _random.Next(100);
                if (statusRoll < 85) status = OrderStatus.Completed;
                else if (statusRoll < 95) status = OrderStatus.Cancelled;
                else status = OrderStatus.Refunded;

                // 60% chance of returning customer, 30% new customer, 10% no customer
                Customer? customer = null;
                var customerRoll = _random.Next(100);
                if (customerRoll < 60 && returningCustomers.Count > 0)
                {
                    customer = returningCustomers[_random.Next(returningCustomers.Count)];
                }
                else if (customerRoll < 90 && customers.Count > 0)
                {
                    customer = customers[_random.Next(customers.Count)];
                }

                var order = CreateOrder(
                    tenant.Id, branch.Id, admin.Id, shift.Id, admin.Name,
                    products, customer, orderTime, orderNumber++, status, branch
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

            // Update shift totals
            shift.TotalOrders = completedCount;
            shift.TotalCash = totalCash;
            shift.TotalCard = totalCard;
            shift.ExpectedBalance = shift.OpeningBalance + totalCash;
            shift.ClosingBalance = shift.ExpectedBalance + _random.Next(-100, 150);
            shift.Difference = shift.ClosingBalance - shift.ExpectedBalance;
            await context.SaveChangesAsync();
        }

        // Create TODAY's open shift
        var todayShift = new Shift
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            UserId = admin.Id,
            OpeningBalance = 2000,
            OpenedAt = DateTime.UtcNow.Date.AddHours(8),
            LastActivityAt = DateTime.UtcNow,
            IsClosed = false,
            IsForceClosed = false,
            IsHandedOver = false,
            HandoverBalance = 0
        };
        context.Shifts.Add(todayShift);
        await context.SaveChangesAsync();

        // Add 2-3 orders for today (some draft/pending)
        for (int i = 0; i < 3; i++)
        {
            var orderTime = DateTime.UtcNow.AddHours(-_random.Next(1, 4));
            OrderStatus status = i == 0 ? OrderStatus.Draft : (i == 1 ? OrderStatus.Pending : OrderStatus.Completed);

            Customer? customer = null;
            if (customers.Count > 0 && _random.Next(100) < 50)
            {
                customer = customers[_random.Next(customers.Count)];
            }

            var order = CreateOrder(
                tenant.Id, branch.Id, admin.Id, todayShift.Id, admin.Name,
                products, customer, orderTime, orderNumber++, status, branch
            );

            context.Orders.Add(order);
        }
        await context.SaveChangesAsync();

        Console.WriteLine($"   ✓ تم إنشاء {shifts.Count} وردية");
        Console.WriteLine($"   ✓ تم إنشاء ~{orderNumber - 10000} طلب");
    }

    private static async Task GenerateBranchTransfersAsync(
        AppDbContext context, Tenant tenant, Branch branch, User admin, List<Product> products)
    {
        var branches = await context.Branches.Where(b => b.TenantId == tenant.Id).ToListAsync();
        if (branches.Count < 2)
        {
            Console.WriteLine("   ⚠ فرع واحد فقط - لا يمكن إنشاء تحويلات");
            return;
        }

        Console.WriteLine("🔄 إنشاء تحويلات المخزون بين الفروع...");

        var transfers = new List<InventoryTransfer>();
        int transferNumber = 1000;

        // Create 8-12 transfers over the last 120 days
        var transferCount = _random.Next(8, 13);
        for (int i = 0; i < transferCount; i++)
        {
            var transferDate = StartDate.AddDays(_random.Next(0, 120));
            var fromBranch = branches[_random.Next(branches.Count)];
            Branch toBranch;
            do
            {
                toBranch = branches[_random.Next(branches.Count)];
            } while (toBranch.Id == fromBranch.Id);

            var product = products[_random.Next(products.Count)];
            var quantity = _random.Next(10, 100);

            var transfer = new InventoryTransfer
            {
                TenantId = tenant.Id,
                TransferNumber = $"IT-{transferDate:yyyyMMdd}-{transferNumber++:D4}",
                FromBranchId = fromBranch.Id,
                ToBranchId = toBranch.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                ProductSku = product.Sku,
                Quantity = quantity,
                Status = InventoryTransferStatus.Completed,
                Reason = "إعادة توزيع المخزون",
                Notes = $"تحويل من {fromBranch.Name} إلى {toBranch.Name}",
                CreatedAt = transferDate,
                CreatedByUserId = admin.Id,
                CreatedByUserName = admin.Name,
                ApprovedByUserId = admin.Id,
                ApprovedByUserName = admin.Name,
                ApprovedAt = transferDate.AddHours(1),
                ReceivedByUserId = admin.Id,
                ReceivedByUserName = admin.Name,
                ReceivedAt = transferDate.AddHours(2)
            };

            transfers.Add(transfer);
        }

        context.Set<InventoryTransfer>().AddRange(transfers);
        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ تم إنشاء {transfers.Count} تحويل مخزون");
    }

    private static Order CreateOrder(
        int tenantId, int branchId, int userId, int shiftId, string userName,
        List<Product> products, Customer? customer, DateTime orderTime, int orderNum,
        OrderStatus status, Branch branch)
    {
        var order = new Order
        {
            TenantId = tenantId,
            BranchId = branchId,
            ShiftId = shiftId,
            OrderNumber = $"ORD-{orderTime:yyyyMMdd}-{orderNum:D5}",
            UserId = userId,
            UserName = userName,
            Status = status,
            OrderType = OrderType.Takeaway,
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

        // 2-6 items per order
        var itemCount = _random.Next(2, 7);
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
            var qty = _random.Next(1, 6);

            // Tax Exclusive calculation
            var netPrice = product.Price * qty;
            var itemTax = netPrice * 0.14m;
            var grossPrice = netPrice + itemTax;

            var orderItem = new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductNameEn = product.NameEn,
                ProductSku = product.Sku,
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

        // Apply discount to 15% of orders
        var hasDiscount = _random.Next(100) < 15;
        if (hasDiscount)
        {
            var discountPercent = _random.Next(5, 31); // 5-30% discount
            order.DiscountType = "Percentage";
            order.DiscountValue = discountPercent;
            order.DiscountAmount = Math.Round(subtotal * (discountPercent / 100m), 2);
            order.DiscountCode = $"DISC{discountPercent}";
        }

        order.Total = Math.Round(subtotal + taxAmount - order.DiscountAmount, 2);

        if (status == OrderStatus.Completed)
        {
            // 20% of orders have debt
            var hasDebt = _random.Next(100) < 20 && customer != null;

            if (hasDebt)
            {
                var paidPercentage = _random.Next(50, 90) / 100m;
                order.AmountPaid = Math.Round(order.Total * paidPercentage, 2);
                order.AmountDue = order.Total - order.AmountPaid;

                // Update customer debt
                if (customer != null)
                {
                    customer.TotalDue += order.AmountDue;
                    customer.TotalSpent += order.Total;
                    customer.TotalOrders++;
                    customer.LastOrderAt = orderTime;
                }
            }
            else
            {
                order.AmountPaid = order.Total;
                order.AmountDue = 0;

                // Update customer purchases even if no debt
                if (customer != null)
                {
                    customer.TotalSpent += order.Total;
                    customer.TotalOrders++;
                    customer.LastOrderAt = orderTime;
                }
            }

            order.CompletedAt = orderTime.AddMinutes(_random.Next(2, 10));
            order.CompletedByUserId = userId;

            // Payment method distribution
            var paymentRoll = _random.Next(100);
            PaymentMethod paymentMethod;
            if (paymentRoll < 50) paymentMethod = PaymentMethod.Cash;
            else if (paymentRoll < 85) paymentMethod = PaymentMethod.Card;
            else if (paymentRoll < 95) paymentMethod = PaymentMethod.Fawry;
            else paymentMethod = PaymentMethod.BankTransfer;

            order.Payments.Add(new Payment
            {
                TenantId = tenantId,
                BranchId = branchId,
                Method = paymentMethod,
                Amount = order.AmountPaid,
                CreatedAt = order.CompletedAt.Value
            });
        }

        return order;
    }

    private static async Task<List<Supplier>> CreateSuppliersAsync(AppDbContext context, Tenant tenant, Branch branch)
    {
        var existingSuppliers = await context.Suppliers.Where(s => s.TenantId == tenant.Id).ToListAsync();
        if (existingSuppliers.Count >= 5)
        {
            Console.WriteLine("   ✓ الموردين موجودين مسبقاً");
            return existingSuppliers;
        }

        Console.WriteLine("🏭 إنشاء الموردين...");

        var suppliers = new List<Supplier>
        {
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "شركة الأهرام للتوريدات",
                NameEn = "Al Ahram Supplies",
                Phone = "01012345678",
                Email = "info@ahram-supplies.com",
                Address = "القاهرة، مصر",
                TaxNumber = "123-456-789",
                IsActive = true,
                TotalDue = 0,
                TotalPaid = 0,
                TotalPurchases = 0
            },
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "مؤسسة النيل للمواد الغذائية",
                NameEn = "Nile Food Materials",
                Phone = "01098765432",
                Email = "sales@nile-food.com",
                Address = "الجيزة، مصر",
                TaxNumber = "987-654-321",
                IsActive = true,
                TotalDue = 0,
                TotalPaid = 0,
                TotalPurchases = 0
            },
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "شركة الدلتا للألبان",
                NameEn = "Delta Dairy Co.",
                Phone = "01123456789",
                Email = "contact@delta-dairy.com",
                Address = "المنصورة، مصر",
                TaxNumber = "456-789-123",
                IsActive = true,
                TotalDue = 0,
                TotalPaid = 0,
                TotalPurchases = 0
            },
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "مصنع الشروق للمنظفات",
                NameEn = "Al Shorouk Cleaning Factory",
                Phone = "01156789012",
                Email = "info@shorouk-clean.com",
                Address = "العاشر من رمضان، مصر",
                TaxNumber = "789-123-456",
                IsActive = true,
                TotalDue = 0,
                TotalPaid = 0,
                TotalPurchases = 0
            },
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "توكيلات الخير للمشروبات",
                NameEn = "Al Khair Beverages",
                Phone = "01187654321",
                Email = "orders@khair-beverages.com",
                Address = "الإسكندرية، مصر",
                TaxNumber = "321-654-987",
                IsActive = true,
                TotalDue = 0,
                TotalPaid = 0,
                TotalPurchases = 0
            }
        };

        context.Suppliers.AddRange(suppliers);
        await context.SaveChangesAsync();
        Console.WriteLine($"   ✓ تم إنشاء {suppliers.Count} مورد");
        return suppliers;
    }

    private static async Task CreatePurchaseInvoicesAsync(
        AppDbContext context, Tenant tenant, Branch branch, User admin,
        List<Product> products, List<Supplier> suppliers)
    {
        if (products.Count == 0 || suppliers.Count == 0)
        {
            Console.WriteLine("   ⚠ لا توجد منتجات/موردين كافية لإنشاء فواتير شراء");
            return;
        }

        var existingInvoices = await context.PurchaseInvoices.CountAsync(p => p.TenantId == tenant.Id);
        if (existingInvoices > 10)
        {
            Console.WriteLine("   ✓ فواتير الشراء موجودة مسبقاً");
            return;
        }

        Console.WriteLine("📦 إنشاء فواتير الشراء وتحديث المخزون...");

        var invoices = new List<PurchaseInvoice>();
        int invoiceNumber = 1000;

        // Create 20-30 purchase invoices over the last 120 days (increased from 15-20)
        var invoiceCount = _random.Next(20, 31);
        for (int i = 0; i < invoiceCount; i++)
        {
            var invoiceDate = StartDate.AddDays(_random.Next(0, 120));
            var supplier = suppliers[_random.Next(suppliers.Count)];

            var invoice = new PurchaseInvoice
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                SupplierPhone = supplier.Phone,
                SupplierAddress = supplier.Address,
                InvoiceNumber = $"PI-{invoiceDate:yyyyMMdd}-{invoiceNumber++:D4}",
                InvoiceDate = invoiceDate,
                // All invoices must be Confirmed to accept payments
                Status = PurchaseInvoiceStatus.Confirmed,
                TaxRate = 14,
                Notes = $"فاتورة شراء من {supplier.Name}",
                CreatedAt = invoiceDate,
                CreatedByUserId = admin.Id,
                CreatedByUserName = admin.Name
            };

            // Confirm the invoice (required for payments)
            invoice.ConfirmedByUserId = admin.Id;
            invoice.ConfirmedByUserName = admin.Name;
            invoice.ConfirmedAt = invoiceDate.AddHours(1);

            // Add 8-20 items per invoice (increased from 5-15)
            var itemCount = _random.Next(8, 21);
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
                var qty = _random.Next(100, 800); // Increased from 50-500 to 100-800

                var netPrice = (product.Cost ?? 0m) * qty;
                var itemTax = netPrice * 0.14m;
                var grossPrice = netPrice + itemTax;

                var invoiceItem = new PurchaseInvoiceItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductNameEn = product.NameEn,
                    ProductSku = product.Sku,
                    Quantity = qty,
                    PurchasePrice = product.Cost ?? 0m,
                    SellingPrice = product.Price,
                    Total = Math.Round(grossPrice, 2)
                };

                invoice.Items.Add(invoiceItem);
                subtotal += netPrice;
                taxAmount += itemTax;

                // Update product stock and average cost
                var oldStock = product.StockQuantity ?? 0;
                var oldAvgCost = product.AverageCost ?? product.Cost ?? 0m;
                var newStock = oldStock + qty;

                // Calculate weighted average cost
                var totalOldValue = oldStock * oldAvgCost;
                var totalNewValue = qty * (product.Cost ?? 0m);
                product.AverageCost = newStock > 0 ? (totalOldValue + totalNewValue) / newStock : product.Cost;

                product.StockQuantity = newStock;
            }

            invoice.Subtotal = Math.Round(subtotal, 2);
            invoice.TaxAmount = Math.Round(taxAmount, 2);
            invoice.Total = Math.Round(subtotal + taxAmount, 2);

            // Update supplier totals
            supplier.TotalPurchases += invoice.Total;
            supplier.LastPurchaseDate = invoiceDate;

            // Randomize payment status (60% fully paid, 30% partially paid, 10% unpaid)
            var paymentRandom = _random.Next(100);
            if (paymentRandom < 60) // Fully paid
            {
                // Create a single payment for the full amount
                var payment = new PurchaseInvoicePayment
                {
                    Amount = invoice.Total,
                    PaymentDate = invoiceDate.AddDays(_random.Next(1, 7)),
                    Method = _random.Next(2) == 0 ? PaymentMethod.Cash : PaymentMethod.BankTransfer,
                    ReferenceNumber = _random.Next(2) == 0 ? $"REF-{_random.Next(10000, 99999)}" : null,
                    Notes = "دفعة كاملة",
                    CreatedByUserId = admin.Id,
                    CreatedByUserName = admin.Name,
                    CreatedAt = invoiceDate.AddDays(_random.Next(1, 7))
                };
                invoice.Payments.Add(payment);

                invoice.AmountPaid = invoice.Total;
                invoice.AmountDue = 0;
                supplier.TotalPaid += invoice.Total;
            }
            else if (paymentRandom < 90) // Partially paid (30%)
            {
                var paidPercentage = _random.Next(30, 70) / 100m;
                var paidAmount = Math.Round(invoice.Total * paidPercentage, 2);

                // Create 1-3 partial payments
                var paymentCount = _random.Next(1, 4);
                var remainingAmount = paidAmount;

                for (int p = 0; p < paymentCount && remainingAmount > 0; p++)
                {
                    var paymentAmount = p == paymentCount - 1
                        ? remainingAmount // Last payment takes the remaining
                        : Math.Round(remainingAmount / (paymentCount - p) * _random.Next(50, 150) / 100m, 2);

                    if (paymentAmount > remainingAmount)
                        paymentAmount = remainingAmount;

                    var payment = new PurchaseInvoicePayment
                    {
                        Amount = paymentAmount,
                        PaymentDate = invoiceDate.AddDays(_random.Next(1, 30)),
                        Method = _random.Next(3) switch
                        {
                            0 => PaymentMethod.Cash,
                            1 => PaymentMethod.BankTransfer,
                            _ => PaymentMethod.Card
                        },
                        ReferenceNumber = _random.Next(2) == 0 ? $"REF-{_random.Next(10000, 99999)}" : null,
                        Notes = $"دفعة جزئية {p + 1}",
                        CreatedByUserId = admin.Id,
                        CreatedByUserName = admin.Name,
                        CreatedAt = invoiceDate.AddDays(_random.Next(1, 30))
                    };
                    invoice.Payments.Add(payment);
                    remainingAmount -= paymentAmount;
                }

                invoice.AmountPaid = paidAmount;
                invoice.AmountDue = invoice.Total - paidAmount;
                supplier.TotalPaid += paidAmount;
                supplier.TotalDue += invoice.AmountDue;
            }
            else // Unpaid (10%)
            {
                invoice.AmountPaid = 0;
                invoice.AmountDue = invoice.Total;
                supplier.TotalDue += invoice.Total;
            }

            invoices.Add(invoice);
        }

        context.PurchaseInvoices.AddRange(invoices);
        await context.SaveChangesAsync();

        // Create BranchInventory records for all products
        var existingInventory = await context.Set<BranchInventory>()
            .Where(bi => bi.BranchId == branch.Id)
            .ToListAsync();

        if (existingInventory.Count == 0)
        {
            var branchInventories = products.Select(p => new BranchInventory
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                ProductId = p.Id,
                Quantity = p.StockQuantity ?? 0,
                ReorderLevel = (int)((p.StockQuantity ?? 0) * 0.15m),
                LastUpdatedAt = DateTime.UtcNow.AddDays(-_random.Next(1, 30))
            }).ToList();

            context.Set<BranchInventory>().AddRange(branchInventories);
            await context.SaveChangesAsync();
        }

        Console.WriteLine($"   ✓ تم إنشاء {invoices.Count} فاتورة شراء");
        Console.WriteLine($"   ✓ تم تحديث المخزون لـ {products.Count} منتج");
    }

    private static async Task PrintStatisticsAsync(AppDbContext context, int tenantId)
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    📊 إحصائيات البيانات                   ║");
        Console.WriteLine("╠════════════════════════════════════════════════════════════╣");

        var ordersWithDebt = await context.Orders
            .Where(o => o.TenantId == tenantId && o.AmountDue > 0)
            .ToListAsync();
        var totalDebt = ordersWithDebt.Sum(o => o.AmountDue);

        var ordersWithDiscount = await context.Orders
            .Where(o => o.TenantId == tenantId && o.DiscountAmount > 0)
            .ToListAsync();
        var totalDiscounts = ordersWithDiscount.Sum(o => o.DiscountAmount);

        var suppliers = await context.Suppliers
            .Where(s => s.TenantId == tenantId)
            .ToListAsync();
        var supplierDebt = suppliers.Sum(s => s.TotalDue);

        var transfersCount = await context.Set<InventoryTransfer>()
            .CountAsync(t => t.TenantId == tenantId);

        var openShifts = await context.Shifts
            .Where(s => s.TenantId == tenantId && !s.IsClosed)
            .CountAsync();

        var stats = new[]
        {
            ("الأصناف", await context.Categories.CountAsync(c => c.TenantId == tenantId)),
            ("المنتجات", await context.Products.CountAsync(p => p.TenantId == tenantId)),
            ("الموردين", await context.Suppliers.CountAsync(s => s.TenantId == tenantId)),
            ("فواتير الشراء", await context.PurchaseInvoices.CountAsync(pi => pi.TenantId == tenantId)),
            ("الورديات", await context.Shifts.CountAsync(s => s.TenantId == tenantId)),
            ("الورديات المفتوحة", openShifts),
            ("الطلبات", await context.Orders.CountAsync(o => o.TenantId == tenantId)),
            ("طلبات بخصم", ordersWithDiscount.Count),
            ("العملاء", await context.Customers.CountAsync(c => c.TenantId == tenantId)),
            ("تحويلات المخزون", transfersCount)
        };

        foreach (var (name, count) in stats)
        {
            Console.WriteLine($"║  {name,-20} : {count,8:N0}                      ║");
        }

        Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  ديون العملاء        : {totalDebt,8:N2} جنيه              ║");
        Console.WriteLine($"║  ديون الموردين       : {supplierDebt,8:N2} جنيه              ║");
        Console.WriteLine($"║  إجمالي الخصومات     : {totalDiscounts,8:N2} جنيه              ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");
    }
}
