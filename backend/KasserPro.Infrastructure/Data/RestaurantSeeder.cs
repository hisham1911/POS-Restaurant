namespace KasserPro.Infrastructure.Data;

using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Data Seeder for Restaurant - مطعم الأمير
/// </summary>
public static class RestaurantSeeder
{
    private static readonly Random _random = new(45);

    public static async Task SeedAsync(AppDbContext context)
    {
        // Check if already seeded
        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Slug == "restaurant");
        if (tenant == null) return;

        // Check if already has complete data
        if (await context.Orders.AnyAsync(o => o.TenantId == tenant.Id))
        {
            Console.WriteLine("   ✓ مطعم: البيانات موجودة مسبقاً");
            return;
        }

        Console.WriteLine("   🔄 تحميل بيانات مطعم...");

        var branch = await context.Branches.FirstAsync(b => b.TenantId == tenant.Id);
        var admin = await context.Users.FirstAsync(u => u.TenantId == tenant.Id && u.Role == UserRole.Admin);
        
        // Add Cashiers and Waiters
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

        Console.WriteLine("   ✅ مطعم: تم التحميل الكامل");
    }

    private static async Task<List<User>> SeedCashiersAsync(AppDbContext context, Tenant tenant, Branch branch)
    {
        var cashiers = new List<User>
        {
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "عمر الكاشير",
                Email = "omar@restaurant.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Role = UserRole.Cashier,
                IsActive = true
            },
            new()
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                Name = "يوسف الكاشير",
                Email = "youssef@restaurant.com",
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
            // عملاء VIP (زبائن دائمين)
            new() { TenantId = tenant.Id, Name = "حسام الدين محمد", Phone = "01001234567", Email = "hossam@email.com", Address = "المعادي، القاهرة", LoyaltyPoints = 650, TotalOrders = 95, TotalSpent = 18500, LastOrderAt = DateTime.UtcNow.AddHours(-4), IsActive = true },
            new() { TenantId = tenant.Id, Name = "وليد أحمد", Phone = "01112345678", Email = "walid@email.com", Address = "الزمالك، القاهرة", LoyaltyPoints = 580, TotalOrders = 82, TotalSpent = 16200, LastOrderAt = DateTime.UtcNow.AddDays(-1), IsActive = true },
            new() { TenantId = tenant.Id, Name = "طارق سعيد", Phone = "01223456789", Email = "tarek.s@email.com", Address = "مدينة نصر، القاهرة", LoyaltyPoints = 520, TotalOrders = 75, TotalSpent = 14800, LastOrderAt = DateTime.UtcNow.AddDays(-2), IsActive = true },
            
            // عملاء منتظمين
            new() { TenantId = tenant.Id, Name = "كريم عبدالله", Phone = "01098765432", Email = "karim@email.com", Address = "المهندسين، الجيزة", LoyaltyPoints = 380, TotalOrders = 52, TotalSpent = 9800, LastOrderAt = DateTime.UtcNow.AddDays(-3), IsActive = true },
            new() { TenantId = tenant.Id, Name = "ياسر محمود", Phone = "01198765432", Email = "yasser@email.com", Address = "حلوان، القاهرة", LoyaltyPoints = 320, TotalOrders = 45, TotalSpent = 8200, LastOrderAt = DateTime.UtcNow.AddDays(-4), IsActive = true },
            new() { TenantId = tenant.Id, Name = "سامي حسن", Phone = "01287654321", Email = "samy@email.com", Address = "الدقي، الجيزة", LoyaltyPoints = 280, TotalOrders = 38, TotalSpent = 7100, LastOrderAt = DateTime.UtcNow.AddDays(-5), IsActive = true },
            new() { TenantId = tenant.Id, Name = "عادل علي", Phone = "01156789012", Email = "adel@email.com", Address = "العباسية، القاهرة", LoyaltyPoints = 240, TotalOrders = 32, TotalSpent = 6200, LastOrderAt = DateTime.UtcNow.AddDays(-6), IsActive = true },
            new() { TenantId = tenant.Id, Name = "مصطفى محمد", Phone = "01267890123", Email = "mostafa@email.com", Address = "الهرم، الجيزة", LoyaltyPoints = 210, TotalOrders = 28, TotalSpent = 5400, LastOrderAt = DateTime.UtcNow.AddDays(-7), IsActive = true },
            
            // عملاء جدد
            new() { TenantId = tenant.Id, Name = "بلال أحمد", Phone = "01078901234", Email = null, Address = "شبرا، القاهرة", LoyaltyPoints = 55, TotalOrders = 6, TotalSpent = 1200, LastOrderAt = DateTime.UtcNow.AddDays(-8), IsActive = true },
            new() { TenantId = tenant.Id, Name = "فادي سعيد", Phone = "01189012345", Email = null, Address = "المطرية، القاهرة", LoyaltyPoints = 40, TotalOrders = 4, TotalSpent = 850, LastOrderAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            
            // عملاء شركات (طلبات جماعية)
            new() { TenantId = tenant.Id, Name = "شركة النور - قسم المشتريات", Phone = "01090123456", Email = "alnour.company@email.com", Address = "وسط البلد، القاهرة", LoyaltyPoints = 920, TotalOrders = 65, TotalSpent = 28500, LastOrderAt = DateTime.UtcNow.AddHours(-10), IsActive = true },
            new() { TenantId = tenant.Id, Name = "مكتب الأمل - إدارة", Phone = "01201234567", Email = "alamal.office@email.com", Address = "مصر الجديدة، القاهرة", LoyaltyPoints = 780, TotalOrders = 52, TotalSpent = 22800, LastOrderAt = DateTime.UtcNow.AddDays(-1), IsActive = true }
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

        // Create 14 days of closed shifts + 1 open shift today
        for (int day = 14; day >= 0; day--)
        {
            var shiftDate = DateTime.UtcNow.Date.AddDays(-day);
            var isClosed = day > 0;
            var cashier = cashiers[day % cashiers.Count];

            var shift = new Shift
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                UserId = cashier.Id,
                OpeningBalance = 1500,
                OpenedAt = shiftDate.AddHours(10), // Restaurants open at 10 AM
                LastActivityAt = shiftDate.AddHours(10),
                IsClosed = isClosed,
                IsForceClosed = false,
                IsHandedOver = false,
                HandoverBalance = 0
            };

            if (isClosed)
            {
                shift.ClosedAt = shiftDate.AddHours(23); // Close at 11 PM
                shift.LastActivityAt = shiftDate.AddHours(23);
                shift.Notes = $"وردية {shiftDate:yyyy-MM-dd}";
            }

            context.Shifts.Add(shift);
            await context.SaveChangesAsync();

            // Create orders for this shift
            var isWeekend = shiftDate.DayOfWeek == DayOfWeek.Friday || shiftDate.DayOfWeek == DayOfWeek.Saturday;
            var orderCount = day == 0 ? _random.Next(2, 4) : (isWeekend ? _random.Next(12, 20) : _random.Next(8, 15));

            decimal totalCash = 0;
            decimal totalCard = 0;
            int completedCount = 0;

            for (int i = 0; i < orderCount; i++)
            {
                var orderTime = shift.OpenedAt.AddMinutes(_random.Next(60, 750));
                var status = day == 0 && i >= orderCount - 2
                    ? (i == orderCount - 1 ? OrderStatus.Draft : OrderStatus.Pending)
                    : OrderStatus.Completed;

                var customer = _random.Next(4) == 0 ? customers[_random.Next(customers.Count)] : null;

                var order = CreateRestaurantOrder(
                    tenant.Id, branch.Id, cashier.Id, shift.Id, cashier.Name,
                    products, customer, orderTime, (day * 1000) + (i + 1) + 4000, status, branch
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
                shift.ClosingBalance = shift.ExpectedBalance + _random.Next(-40, 60);
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

    private static Order CreateRestaurantOrder(
        int tenantId, int branchId, int userId, int shiftId, string userName,
        List<Product> products, Customer? customer, DateTime orderTime, int orderNum,
        OrderStatus status, Branch branch)
    {
        // Restaurant orders have variety: 50% DineIn, 30% Takeaway, 20% Delivery
        var orderTypes = new[] { OrderType.DineIn, OrderType.DineIn, OrderType.DineIn, OrderType.DineIn, OrderType.DineIn, 
                                 OrderType.Takeaway, OrderType.Takeaway, OrderType.Takeaway, 
                                 OrderType.Delivery, OrderType.Delivery };
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

        // Restaurant orders have 2-4 items (meals + drinks)
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
            order.CompletedAt = orderTime.AddMinutes(_random.Next(15, 45)); // Restaurant orders take longer
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
            (CategoryName: "رواتب", Amount: 18000m, Days: 5, Description: "رواتب الموظفين - شهر مارس"),
            (CategoryName: "رواتب", Amount: 18000m, Days: 35, Description: "رواتب الموظفين - شهر فبراير"),
            (CategoryName: "إيجار", Amount: 12000m, Days: 3, Description: "إيجار المطعم - شهر مارس"),
            (CategoryName: "إيجار", Amount: 12000m, Days: 33, Description: "إيجار المطعم - شهر فبراير"),
            (CategoryName: "كهرباء", Amount: 2800m, Days: 8, Description: "فاتورة الكهرباء - شهر فبراير"),
            (CategoryName: "كهرباء", Amount: 2500m, Days: 38, Description: "فاتورة الكهرباء - شهر يناير"),
            (CategoryName: "صيانة", Amount: 850m, Days: 12, Description: "صيانة المطبخ والمعدات"),
            (CategoryName: "صيانة", Amount: 620m, Days: 25, Description: "إصلاح التكييف"),
            (CategoryName: "مواصلات", Amount: 420m, Days: 6, Description: "مواصلات التوصيل"),
            (CategoryName: "أخرى", Amount: 780m, Days: 10, Description: "مستلزمات تغليف ونظافة")
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
                ExpenseNumber = $"EXP-{expenseDate:yyyyMMdd}-{expenses.Count + 4001:D4}",
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
            var transDate = DateTime.UtcNow.AddDays(-_random.Next(1, 14));
            var isDeposit = _random.Next(2) == 0;
            var amount = _random.Next(800, 2500);

            var transaction = new CashRegisterTransaction
            {
                TenantId = tenant.Id,
                BranchId = branch.Id,
                TransactionNumber = $"CRT-{transDate:yyyyMMdd}-{i + 4001:D4}",
                Type = isDeposit ? CashRegisterTransactionType.Deposit : CashRegisterTransactionType.Withdrawal,
                Amount = amount,
                BalanceBefore = 4000,
                BalanceAfter = isDeposit ? 4000 + amount : 4000 - amount,
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
