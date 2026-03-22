namespace KasserPro.Infrastructure.Data;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Data Seeder for Supermarket - سوبر ماركت الخير
/// </summary>
public static class SupermarketSeeder
{
    private static readonly Random _random = new(44);

    public static async Task SeedAsync(AppDbContext context)
    {
        // Check if already seeded
        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Slug == "supermarket");
        if (tenant == null) return;

        // Check if already has complete data
        if (await context.Orders.AnyAsync(o => o.TenantId == tenant.Id))
        {
            Console.WriteLine("   ✓ سوبر ماركت: البيانات موجودة مسبقاً");
            return;
        }

        Console.WriteLine("   🔄 تحميل بيانات سوبر ماركت...");

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

        Console.WriteLine("   ✅ سوبر ماركت: تم التحميل الكامل");
    }

    private static async Task<List<User>> SeedCashiersAsync(AppDbContext context, Tenant tenant, Branch branch)
    {
        var cashiers = new List<User>
        {
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "فاطمة الكاشير",
                Email = "fatma@supermarket.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Role = UserRole.Cashier,
                IsActive = true
            },
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "زينب الكاشير",
                Email = "zainab@supermarket.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Role = UserRole.Cashier,
                IsActive = true
            },
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "مريم الكاشير",
                Email = "mariam@supermarket.com",
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
            // عملاء VIP (عائلات كبيرة)
            new() { TenantId = tenant.Id, Name = "عائلة أحمد محمود", Phone = "01001234567", Email = "ahmed.family@email.com", Address = "المعادي، القاهرة", LoyaltyPoints = 520, TotalOrders = 85, TotalSpent = 15800, LastOrderAt = DateTime.UtcNow.AddHours(-6), IsActive = true },
            new() { TenantId = tenant.Id, Name = "عائلة محمد حسن", Phone = "01112345678", Email = "mohamed.family@email.com", Address = "الزمالك، القاهرة", LoyaltyPoints = 480, TotalOrders = 78, TotalSpent = 14200, LastOrderAt = DateTime.UtcNow.AddDays(-1), IsActive = true },
            new() { TenantId = tenant.Id, Name = "عائلة خالد سعيد", Phone = "01223456789", Email = "khaled.family@email.com", Address = "مدينة نصر، القاهرة", LoyaltyPoints = 420, TotalOrders = 68, TotalSpent = 12500, LastOrderAt = DateTime.UtcNow.AddDays(-2), IsActive = true },
            
            // عملاء منتظمين
            new() { TenantId = tenant.Id, Name = "إيمان علي", Phone = "01098765432", Email = "eman@email.com", Address = "المهندسين، الجيزة", LoyaltyPoints = 280, TotalOrders = 45, TotalSpent = 8200, LastOrderAt = DateTime.UtcNow.AddDays(-3), IsActive = true },
            new() { TenantId = tenant.Id, Name = "سمية محمد", Phone = "01198765432", Email = "somaya@email.com", Address = "حلوان، القاهرة", LoyaltyPoints = 240, TotalOrders = 38, TotalSpent = 6900, LastOrderAt = DateTime.UtcNow.AddDays(-4), IsActive = true },
            new() { TenantId = tenant.Id, Name = "نهى حسن", Phone = "01287654321", Email = "noha@email.com", Address = "الدقي، الجيزة", LoyaltyPoints = 210, TotalOrders = 32, TotalSpent = 5800, LastOrderAt = DateTime.UtcNow.AddDays(-5), IsActive = true },
            new() { TenantId = tenant.Id, Name = "أمل سعيد", Phone = "01156789012", Email = "amal@email.com", Address = "العباسية، القاهرة", LoyaltyPoints = 180, TotalOrders = 28, TotalSpent = 4900, LastOrderAt = DateTime.UtcNow.AddDays(-6), IsActive = true },
            new() { TenantId = tenant.Id, Name = "رشا أحمد", Phone = "01267890123", Email = "rasha@email.com", Address = "الهرم، الجيزة", LoyaltyPoints = 150, TotalOrders = 22, TotalSpent = 3800, LastOrderAt = DateTime.UtcNow.AddDays(-7), IsActive = true },
            
            // عملاء جدد
            new() { TenantId = tenant.Id, Name = "غادة محمود", Phone = "01078901234", Email = null, Address = "شبرا، القاهرة", LoyaltyPoints = 45, TotalOrders = 5, TotalSpent = 980, LastOrderAt = DateTime.UtcNow.AddDays(-8), IsActive = true },
            new() { TenantId = tenant.Id, Name = "ياسمين علي", Phone = "01189012345", Email = null, Address = "المطرية، القاهرة", LoyaltyPoints = 30, TotalOrders = 3, TotalSpent = 650, LastOrderAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            
            // عملاء محلات صغيرة (جملة)
            new() { TenantId = tenant.Id, Name = "بقالة الأمل - سامي حسن", Phone = "01090123456", Email = "alamal.grocery@email.com", Address = "وسط البلد، القاهرة", LoyaltyPoints = 850, TotalOrders = 120, TotalSpent = 32500, LastOrderAt = DateTime.UtcNow.AddHours(-12), IsActive = true },
            new() { TenantId = tenant.Id, Name = "كافتيريا النور - أحمد محمد", Phone = "01201234567", Email = "alnour.cafe@email.com", Address = "مصر الجديدة، القاهرة", LoyaltyPoints = 720, TotalOrders = 95, TotalSpent = 28800, LastOrderAt = DateTime.UtcNow.AddDays(-1), IsActive = true },
            new() { TenantId = tenant.Id, Name = "مطعم الفردوس - محمود علي", Phone = "01012345678", Email = "alferdous@email.com", Address = "المعادي، القاهرة", LoyaltyPoints = 680, TotalOrders = 88, TotalSpent = 25200, LastOrderAt = DateTime.UtcNow.AddDays(-2), IsActive = true }
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

        // Create 12 days of closed shifts + 1 open shift today
        for (int day = 12; day >= 0; day--)
        {
            var shiftDate = DateTime.UtcNow.Date.AddDays(-day);
            var isClosed = day > 0;
            var cashier = cashiers[day % cashiers.Count];

            var shift = new Shift
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                UserId = cashier.Id,
                OpeningBalance = 2000,
                OpenedAt = shiftDate.AddHours(8),
                LastActivityAt = shiftDate.AddHours(8),
                IsClosed = isClosed,
                IsForceClosed = false,
                IsHandedOver = false,
                HandoverBalance = 0
            };

            if (isClosed)
            {
                shift.ClosedAt = shiftDate.AddHours(22);
                shift.LastActivityAt = shiftDate.AddHours(22);
                shift.Notes = $"وردية {shiftDate:yyyy-MM-dd}";
            }

            context.Shifts.Add(shift);
            await context.SaveChangesAsync();

            // Create orders for this shift (supermarkets have many orders)
            var isWeekend = shiftDate.DayOfWeek == DayOfWeek.Friday || shiftDate.DayOfWeek == DayOfWeek.Saturday;
            var orderCount = day == 0 ? _random.Next(2, 4) : (isWeekend ? _random.Next(15, 25) : _random.Next(10, 18));

            decimal totalCash = 0;
            decimal totalCard = 0;
            int completedCount = 0;

            for (int i = 0; i < orderCount; i++)
            {
                var orderTime = shift.OpenedAt.AddMinutes(_random.Next(30, 800));
                var status = day == 0 && i >= orderCount - 2
                    ? (i == orderCount - 1 ? OrderStatus.Draft : OrderStatus.Pending)
                    : OrderStatus.Completed;

                var customer = _random.Next(5) == 0 ? customers[_random.Next(customers.Count)] : null;

                var order = CreateSupermarketOrder(
                    tenant.Id, branch.Id, cashier.Id, shift.Id, cashier.Name,
                    products, customer, orderTime, (day * 1000) + (i + 1) + 3000, status, branch
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
                shift.ClosingBalance = shift.ExpectedBalance + _random.Next(-50, 80);
                shift.Difference = shift.ClosingBalance - shift.ExpectedBalance;
                await context.SaveChangesAsync();
            }
        }

        // Deduct stock for completed orders
        var completedOrders = await context.Orders
            .Include(o => o.Items)
            .Where(o => o.TenantId == tenant.Id && o.Status == OrderStatus.Completed)
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

        // Create return orders (2-3% of completed orders)
        await CreateReturnOrdersAsync(context, tenant, branch, cashiers, completedOrders);
    }

    private static Order CreateSupermarketOrder(
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
            OrderType = OrderType.Takeaway, // Supermarket orders are always takeaway
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

        // Supermarket orders have more items (2-4 items)
        var itemCount = _random.Next(2, 5);
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
            var qty = _random.Next(1, 5); // Higher quantities for grocery items

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
            order.CompletedAt = orderTime.AddMinutes(_random.Next(2, 8));
            order.CompletedByUserId = userId;

            var paymentMethod = _random.Next(10) < 5 ? PaymentMethod.Cash : PaymentMethod.Card;

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
            (CategoryName: "رواتب", Amount: 15000m, Days: 5, Description: "رواتب الموظفين - شهر مارس"),
            (CategoryName: "رواتب", Amount: 15000m, Days: 35, Description: "رواتب الموظفين - شهر فبراير"),
            (CategoryName: "إيجار", Amount: 10000m, Days: 3, Description: "إيجار المحل - شهر مارس"),
            (CategoryName: "إيجار", Amount: 10000m, Days: 33, Description: "إيجار المحل - شهر فبراير"),
            (CategoryName: "كهرباء", Amount: 2200m, Days: 8, Description: "فاتورة الكهرباء - شهر فبراير"),
            (CategoryName: "كهرباء", Amount: 1950m, Days: 38, Description: "فاتورة الكهرباء - شهر يناير"),
            (CategoryName: "صيانة", Amount: 580m, Days: 12, Description: "صيانة الثلاجات والفريزرات"),
            (CategoryName: "صيانة", Amount: 420m, Days: 25, Description: "صيانة دورية للمعدات"),
            (CategoryName: "مواصلات", Amount: 350m, Days: 6, Description: "مواصلات التوصيل"),
            (CategoryName: "أخرى", Amount: 680m, Days: 10, Description: "مستلزمات تغليف ونظافة")
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
                ExpenseNumber = $"EXP-{expenseDate:yyyyMMdd}-{expenses.Count + 3001:D4}",
                Amount = amount,
                Description = description,
                ExpenseDate = expenseDate,
                Status = ExpenseStatus.Paid,
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

    private static async Task CreateReturnOrdersAsync(AppDbContext context, Tenant tenant, Branch branch, List<User> cashiers, List<Order> completedOrders)
    {
        // Create returns for 2-3% of completed orders
        var returnCount = Math.Max(1, (int)(completedOrders.Count * 0.025));
        var ordersToReturn = completedOrders
            .OrderBy(o => Guid.NewGuid())
            .Take(returnCount)
            .ToList();

        foreach (var originalOrder in ordersToReturn)
        {
            var returnDate = originalOrder.CompletedAt!.Value.AddHours(_random.Next(2, 48));
            var cashier = cashiers[_random.Next(cashiers.Count)];

            // Get shift for return date
            var shift = await context.Shifts
                .Where(s => s.TenantId == tenant.Id 
                         && s.BranchId == branch.Id
                         && s.OpenedAt.Date == returnDate.Date)
                .FirstOrDefaultAsync();

            if (shift == null) continue;

            var returnOrder = new Order
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                ShiftId = shift.Id,
                OrderNumber = $"RET-{returnDate:yyyyMMdd}-{_random.Next(1000, 9999)}",
                UserId = cashier.Id,
                UserName = cashier.Name,
                Status = OrderStatus.Completed,
                OrderType = OrderType.Return,
                CreatedAt = returnDate,
                CompletedAt = returnDate.AddMinutes(5),
                CompletedByUserId = cashier.Id,
                BranchName = branch.Name,
                BranchAddress = branch.Address,
                BranchPhone = branch.Phone,
                CurrencyCode = "EGP",
                TaxRate = 14,
                CustomerId = originalOrder.CustomerId,
                CustomerName = originalOrder.CustomerName,
                CustomerPhone = originalOrder.CustomerPhone
            };

            // Return 1-2 items from original order
            var itemsToReturn = originalOrder.Items
                .OrderBy(i => Guid.NewGuid())
                .Take(_random.Next(1, Math.Min(3, originalOrder.Items.Count + 1)))
                .ToList();

            decimal subtotal = 0;
            decimal taxAmount = 0;

            foreach (var originalItem in itemsToReturn)
            {
                var returnQty = -Math.Abs(originalItem.Quantity); // Negative quantity for returns
                var netPrice = originalItem.UnitPrice * Math.Abs(returnQty);
                var itemTax = netPrice * (14m / 100m);

                var returnItem = new OrderItem
                {
                    ProductId = originalItem.ProductId,
                    ProductName = originalItem.ProductName,
                    ProductNameEn = originalItem.ProductNameEn,
                    ProductSku = originalItem.ProductSku,
                    UnitPrice = originalItem.UnitPrice,
                    UnitCost = originalItem.UnitCost,
                    OriginalPrice = originalItem.OriginalPrice,
                    Quantity = returnQty,
                    TaxRate = 14,
                    TaxInclusive = false,
                    TaxAmount = -Math.Round(itemTax, 2),
                    Subtotal = -Math.Round(netPrice, 2),
                    Total = -Math.Round(netPrice + itemTax, 2)
                };

                returnOrder.Items.Add(returnItem);
                subtotal -= netPrice;
                taxAmount -= itemTax;
            }

            returnOrder.Subtotal = Math.Round(subtotal, 2);
            returnOrder.TaxAmount = Math.Round(taxAmount, 2);
            returnOrder.Total = Math.Round(subtotal + taxAmount, 2);
            returnOrder.AmountPaid = returnOrder.Total;
            returnOrder.AmountDue = 0;

            // Refund payment
            var refundMethod = originalOrder.Payments.FirstOrDefault()?.Method ?? PaymentMethod.Cash;
            returnOrder.Payments.Add(new Payment
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Method = refundMethod,
                Amount = returnOrder.Total,
                CreatedAt = returnDate.AddMinutes(5)
            });

            context.Orders.Add(returnOrder);

            // Restore stock for returned items
            foreach (var item in returnOrder.Items)
            {
                var product = await context.Products.FindAsync(item.ProductId);
                if (product != null && product.StockQuantity.HasValue)
                {
                    product.StockQuantity += Math.Abs(item.Quantity);
                    product.LastStockUpdate = returnDate;
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedCashRegisterTransactionsAsync(AppDbContext context, Tenant tenant, Branch branch, User admin)
    {
        var transactions = new List<CashRegisterTransaction>();

        for (int i = 0; i < 5; i++)
        {
            var transDate = DateTime.UtcNow.AddDays(-_random.Next(1, 12));
            var isDeposit = _random.Next(2) == 0;
            var amount = _random.Next(1000, 3000);

            var transaction = new CashRegisterTransaction
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                TransactionNumber = $"CRT-{transDate:yyyyMMdd}-{i + 3001:D4}",
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
    }
}
