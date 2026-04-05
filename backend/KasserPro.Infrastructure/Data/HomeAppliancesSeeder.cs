namespace KasserPro.Infrastructure.Data;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Data Seeder for Home Appliances Store - محل الأمل للأدوات المنزلية
/// </summary>
public static class HomeAppliancesSeeder
{
    private static readonly Random _random = new(43);

    public static async Task SeedAsync(AppDbContext context)
    {
        // Check if already seeded
        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Slug == "home-appliances");
        if (tenant == null) return;

        // Check if already has complete data
        if (await context.Orders.AnyAsync(o => o.TenantId == tenant.Id))
        {
            Console.WriteLine("   ✓ محل أدوات منزلية: البيانات موجودة مسبقاً");
            return;
        }

        Console.WriteLine("   🔄 تحميل بيانات محل أدوات منزلية...");

        var branch = await context.Branches.FirstAsync(b => b.TenantId == tenant.Id);
        var admin = await context.Users.FirstAsync(u => u.TenantId == tenant.Id && u.Role == UserRole.Admin);

        // Add Cashiers
        var cashiers = await SeedCashiersAsync(context, tenant, branch);
        var allUsers = new List<User> { admin };
        allUsers.AddRange(cashiers);

        var categories = await context.Categories.Where(c => c.TenantId == tenant.Id).ToListAsync();
        var products = await context.Products.Where(p => p.TenantId == tenant.Id).ToListAsync();

        var customers = await SeedCustomersAsync(context, tenant);
        await SeedExpenseCategoriesAsync(context, tenant);
        await SeedShiftsAndOrdersAsync(context, tenant, branch, allUsers, products, customers);
        await SeedExpensesAsync(context, tenant, branch, admin);
        await SeedCashRegisterTransactionsAsync(context, tenant, branch, admin);

        Console.WriteLine("   ✅ محل أدوات منزلية: تم التحميل الكامل");
    }

    private static async Task<List<User>> SeedCashiersAsync(AppDbContext context, Tenant tenant, Branch branch)
    {
        var cashiers = new List<User>
        {
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "نور الكاشير",
                Email = "nour@homeappliances.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Role = UserRole.Cashier,
                IsActive = true
            },
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "هدى الكاشير",
                Email = "hoda@homeappliances.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Role = UserRole.Cashier,
                IsActive = true
            }
        };

        context.Users.AddRange(cashiers);
        await context.SaveChangesAsync();
        return cashiers;
    }

    private static async Task<List<Customer>> SeedCustomersAsync(AppDbContext context, Tenant tenant)
    {
        var customers = new List<Customer>
        {
            // عملاء VIP
            new() { TenantId = tenant.Id, Name = "منى أحمد", Phone = "01001111111", Email = "mona@email.com", Address = "المعادي، القاهرة", LoyaltyPoints = 380, TotalOrders = 28, TotalSpent = 8500, LastOrderAt = DateTime.UtcNow.AddDays(-1), IsActive = true },
            new() { TenantId = tenant.Id, Name = "سارة محمود", Phone = "01112222222", Email = "sara@email.com", Address = "الزمالك، القاهرة", LoyaltyPoints = 420, TotalOrders = 32, TotalSpent = 9800, LastOrderAt = DateTime.UtcNow.AddHours(-8), IsActive = true },

            // عملاء منتظمين
            new() { TenantId = tenant.Id, Name = "ليلى حسن", Phone = "01223333333", Email = "laila@email.com", Address = "مدينة نصر، القاهرة", LoyaltyPoints = 220, TotalOrders = 18, TotalSpent = 5200, LastOrderAt = DateTime.UtcNow.AddDays(-3), IsActive = true },
            new() { TenantId = tenant.Id, Name = "نادية سعيد", Phone = "01098888888", Email = "nadia@email.com", Address = "المهندسين، الجيزة", LoyaltyPoints = 180, TotalOrders = 14, TotalSpent = 4100, LastOrderAt = DateTime.UtcNow.AddDays(-5), IsActive = true },
            new() { TenantId = tenant.Id, Name = "هالة علي", Phone = "01187777777", Email = "hala@email.com", Address = "الدقي، الجيزة", LoyaltyPoints = 150, TotalOrders = 11, TotalSpent = 3400, LastOrderAt = DateTime.UtcNow.AddDays(-7), IsActive = true },

            // عملاء جدد
            new() { TenantId = tenant.Id, Name = "رانيا محمد", Phone = "01276666666", Email = null, Address = "حلوان، القاهرة", LoyaltyPoints = 35, TotalOrders = 2, TotalSpent = 850, LastOrderAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new() { TenantId = tenant.Id, Name = "دينا أحمد", Phone = "01055555555", Email = null, Address = "العباسية، القاهرة", LoyaltyPoints = 25, TotalOrders = 1, TotalSpent = 450, LastOrderAt = DateTime.UtcNow.AddDays(-12), IsActive = true },

            // عملاء محلات (جملة)
            new() { TenantId = tenant.Id, Name = "محل الأمل - أحمد صلاح", Phone = "01144444444", Email = "alamal.shop@email.com", Address = "شبرا، القاهرة", LoyaltyPoints = 650, TotalOrders = 45, TotalSpent = 18500, LastOrderAt = DateTime.UtcNow.AddHours(-10), IsActive = true },
            new() { TenantId = tenant.Id, Name = "سوبر ماركت النور - محمد حسن", Phone = "01233333333", Email = "alnour.market@email.com", Address = "فيصل، الجيزة", LoyaltyPoints = 520, TotalOrders = 38, TotalSpent = 14200, LastOrderAt = DateTime.UtcNow.AddDays(-2), IsActive = true }
        };

        context.Customers.AddRange(customers);
        await context.SaveChangesAsync();
        return customers;
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
    }

    private static async Task SeedShiftsAndOrdersAsync(AppDbContext context, Tenant tenant, Branch branch, List<User> users, List<Product> products, List<Customer> customers)
    {
        var cashiers = users.Where(u => u.Role == UserRole.Cashier).ToList();
        if (cashiers.Count == 0) cashiers = users;

        // Create 10 days of closed shifts + 1 open shift today
        for (int day = 10; day >= 0; day--)
        {
            var shiftDate = DateTime.UtcNow.Date.AddDays(-day);
            var isClosed = day > 0;
            var cashier = cashiers[day % cashiers.Count];

            var shift = new Shift
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                UserId = cashier.Id,
                OpeningBalance = 1000,
                OpenedAt = shiftDate.AddHours(9),
                LastActivityAt = shiftDate.AddHours(9),
                IsClosed = isClosed,
                IsForceClosed = false,
                IsHandedOver = false,
                HandoverBalance = 0
            };

            if (isClosed)
            {
                shift.ClosedAt = shiftDate.AddHours(21);
                shift.LastActivityAt = shiftDate.AddHours(21);
                shift.Notes = $"وردية {shiftDate:yyyy-MM-dd}";
            }

            context.Shifts.Add(shift);
            await context.SaveChangesAsync();

            // Create orders for this shift
            var isWeekend = shiftDate.DayOfWeek == DayOfWeek.Friday || shiftDate.DayOfWeek == DayOfWeek.Saturday;
            var orderCount = day == 0 ? _random.Next(1, 3) : (isWeekend ? _random.Next(6, 12) : _random.Next(3, 8));

            decimal totalCash = 0;
            decimal totalCard = 0;
            int completedCount = 0;

            for (int i = 0; i < orderCount; i++)
            {
                var orderTime = shift.OpenedAt.AddMinutes(_random.Next(30, 700));
                var status = OrderStatus.Completed;

                var customer = _random.Next(4) == 0 ? customers[_random.Next(customers.Count)] : null;

                var order = CreateHomeAppliancesOrder(
                    tenant.Id, branch.Id, cashier.Id, shift.Id, cashier.Name,
                    products, customer, orderTime, (day * 1000) + (i + 1) + 2000, status, branch
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
                shift.ClosingBalance = shift.ExpectedBalance + _random.Next(-30, 50);
                shift.Difference = shift.ClosingBalance - shift.ExpectedBalance;
                await context.SaveChangesAsync();
            }
        }

        // Deduct stock for completed orders
        var completedOrders = await context.Orders
            .Include(o => o.Items)
            .Where(o => o.TenantId == tenant.Id && o.Status == OrderStatus.Completed)
            .ToListAsync();

        // Stock is now managed through BranchInventory and StockMovements
        // No need to manually update product stock here
        await context.SaveChangesAsync();

    }

    private static Order CreateHomeAppliancesOrder(
        int tenantId, int branchId, int userId, int shiftId, string userName,
        List<Product> products, Customer? customer, DateTime orderTime, int orderNum,
        OrderStatus status, Branch branch)
    {
        var order = new Order
        {
            TenantId = tenantId,
            BranchId = branchId,
            ShiftId = shiftId,
            OrderNumber = $"ORD-{orderTime:yyyyMMdd}-{orderNum:D4}",
            UserId = userId,
            UserName = userName,
            Status = status,
            OrderType = OrderType.Takeaway, // Home appliances are always takeaway
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

        // Add 1-3 items per order
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
            var qty = _random.Next(1, 3);

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

            var paymentMethod = _random.Next(10) < 6 ? PaymentMethod.Card : PaymentMethod.Cash;

            order.Payments.Add(new Payment
            {
                TenantId = tenantId,
                BranchId = branchId,
                Method = paymentMethod,
                Amount = order.Total,
                CreatedAt = order.CompletedAt.Value
            });
        }

        return order;
    }

    private static async Task SeedExpensesAsync(AppDbContext context, Tenant tenant, Branch branch, User admin)
    {
        var categories = await context.ExpenseCategories.Where(c => c.TenantId == tenant.Id).ToListAsync();
        var expenses = new List<Expense>();

        var expenseData = new[]
        {
            (CategoryName: "رواتب", Amount: 9000m, Days: 5, Description: "رواتب الموظفين - شهر مارس"),
            (CategoryName: "رواتب", Amount: 9000m, Days: 35, Description: "رواتب الموظفين - شهر فبراير"),
            (CategoryName: "إيجار", Amount: 6500m, Days: 3, Description: "إيجار المحل - شهر مارس"),
            (CategoryName: "إيجار", Amount: 6500m, Days: 33, Description: "إيجار المحل - شهر فبراير"),
            (CategoryName: "كهرباء", Amount: 1200m, Days: 8, Description: "فاتورة الكهرباء - شهر فبراير"),
            (CategoryName: "صيانة", Amount: 380m, Days: 12, Description: "صيانة التكييف"),
            (CategoryName: "مواصلات", Amount: 220m, Days: 6, Description: "مواصلات التوصيل"),
            (CategoryName: "أخرى", Amount: 450m, Days: 10, Description: "مستلزمات تغليف ونظافة")
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
                ExpenseNumber = $"EXP-{expenseDate:yyyyMMdd}-{expenses.Count + 2001:D4}",
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
    }

    private static async Task SeedCashRegisterTransactionsAsync(AppDbContext context, Tenant tenant, Branch branch, User admin)
    {
        var transactions = new List<CashRegisterTransaction>();

        for (int i = 0; i < 4; i++)
        {
            var transDate = DateTime.UtcNow.AddDays(-_random.Next(1, 10));
            var isDeposit = _random.Next(2) == 0;
            var amount = _random.Next(500, 1500);

            var transaction = new CashRegisterTransaction
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                TransactionNumber = $"CRT-{transDate:yyyyMMdd}-{i + 2001:D4}",
                Type = isDeposit ? CashRegisterTransactionType.Deposit : CashRegisterTransactionType.Withdrawal,
                Amount = amount,
                BalanceBefore = 3000,
                BalanceAfter = isDeposit ? 3000 + amount : 3000 - amount,
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
    }
}
