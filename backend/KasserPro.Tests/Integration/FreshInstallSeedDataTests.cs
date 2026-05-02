namespace KasserPro.Tests.Integration;

using FluentAssertions;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class FreshInstallSeedDataTests
{
    [Fact]
    public async Task FreshInstallSeedPipeline_ShouldPopulateDemoCatalogIconsAndInventory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"kasserpro-seed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var dbPath = Path.Combine(tempDirectory, "kasserpro.db");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await ButcherDataSeeder.SeedAsync(context);
            context.ChangeTracker.Clear();

            await MultiTenantSeeder.SeedAsync(context);
            context.ChangeTracker.Clear();

            await RealisticDataSeeder.SeedAsync(context);
            context.ChangeTracker.Clear();

            await SeedCatalogIconSynchronizer.SynchronizeAsync(context);
            context.ChangeTracker.Clear();

            await SeedInventorySynchronizer.SynchronizeAsync(context);
            context.ChangeTracker.Clear();

            await MultiTenantSeeder.CloseOpenShiftsForTargetTenantsAsync(context);
            context.ChangeTracker.Clear();

            var seededTenants = await context.Tenants
                .AsNoTracking()
                .Where(t => SeedTenantRegistry.Slugs.Contains(t.Slug))
                .OrderBy(t => t.Id)
                .Select(t => new { t.Id, t.Slug })
                .ToListAsync();

            seededTenants.Select(t => t.Slug).Should().BeEquivalentTo(new[]
            {
                "al-amana-butcher",
                "supermarket"
            });

            var seedTenantIds = seededTenants.Select(t => t.Id).ToList();
            var butcherTenantId = seededTenants.Single(t => t.Slug == "al-amana-butcher").Id;
            var supermarketTenantId = seededTenants.Single(t => t.Slug == "supermarket").Id;

            var supermarketCategoryCount = await context.Categories
                .AsNoTracking()
                .CountAsync(c => c.TenantId == supermarketTenantId);
            supermarketCategoryCount.Should().BeGreaterOrEqualTo(10);

            var supermarketProductCount = await context.Products
                .AsNoTracking()
                .CountAsync(p => p.TenantId == supermarketTenantId);
            supermarketProductCount.Should().BeGreaterOrEqualTo(80);

            var categoriesWithoutIcons = await context.Categories
                .AsNoTracking()
                .CountAsync(c => seedTenantIds.Contains(c.TenantId)
                    && (c.ImageUrl == null || c.ImageUrl.Trim() == string.Empty));
            categoriesWithoutIcons.Should().Be(0);

            var productsWithoutIcons = await context.Products
                .AsNoTracking()
                .CountAsync(p => seedTenantIds.Contains(p.TenantId)
                    && (p.ImageUrl == null || p.ImageUrl.Trim() == string.Empty));
            productsWithoutIcons.Should().Be(0);

            var butcherOffalCategoryIcon = await context.Categories
                .AsNoTracking()
                .Where(c => c.TenantId == butcherTenantId && c.Name == "أحشاء ومنتجات ثانوية")
                .Select(c => c.ImageUrl)
                .SingleAsync();
            butcherOffalCategoryIcon.Should().Be("🥩");

            var supermarketMeatPoultryCategoryIcon = await context.Categories
                .AsNoTracking()
                .Where(c => c.TenantId == supermarketTenantId && c.Name == "لحوم ودواجن")
                .Select(c => c.ImageUrl)
                .SingleAsync();
            supermarketMeatPoultryCategoryIcon.Should().Be("🥩");

            var nonLogicalCategoryIcons = await context.Categories
                .AsNoTracking()
                .CountAsync(c => seedTenantIds.Contains(c.TenantId) && c.ImageUrl == "🫀");
            nonLogicalCategoryIcons.Should().Be(0, because: "seed categories should not use non-intuitive anatomical icons");

            var nonLogicalProductIcons = await context.Products
                .AsNoTracking()
                .CountAsync(p => seedTenantIds.Contains(p.TenantId) && p.ImageUrl == "🫀");
            nonLogicalProductIcons.Should().Be(0, because: "seed products should not use non-intuitive anatomical icons");

            var butcherProductIcons = await context.Products
                .AsNoTracking()
                .Where(p => p.TenantId == butcherTenantId)
                .Where(p => p.Name == "لحم موزه"
                    || p.Name == "كوارع"
                    || p.Name == "كوارع بالكيلو"
                    || p.Name == "طحال")
                .Select(p => new { p.Name, p.ImageUrl })
                .ToListAsync();

            butcherProductIcons.Single(p => p.Name == "لحم موزه").ImageUrl.Should().Be("🥩");
            butcherProductIcons.Single(p => p.Name == "كوارع").ImageUrl.Should().Be("🥩");
            butcherProductIcons.Single(p => p.Name == "كوارع بالكيلو").ImageUrl.Should().Be("🥩");
            butcherProductIcons.Single(p => p.Name == "طحال").ImageUrl.Should().Be("🥩");

            foreach (var tenant in seededTenants)
            {
                var expectedInventoryRows = await (
                    from product in context.Products.AsNoTracking()
                    join branch in context.Branches.AsNoTracking() on product.TenantId equals branch.TenantId
                    where product.TenantId == tenant.Id
                        && product.IsActive
                        && product.TrackInventory
                        && branch.IsActive
                    select 1
                ).CountAsync();

                var actualInventoryRows = await context.BranchInventories
                    .AsNoTracking()
                    .CountAsync(i => i.TenantId == tenant.Id);

                actualInventoryRows.Should().Be(
                    expectedInventoryRows,
                    because: $"{tenant.Slug} should have one inventory row per active tracked product/branch");

                var nonPositiveInventoryRows = await context.BranchInventories
                    .AsNoTracking()
                    .CountAsync(i => i.TenantId == tenant.Id && i.Quantity <= 0);

                nonPositiveInventoryRows.Should().Be(
                    0,
                    because: $"{tenant.Slug} seed inventory should stay positive on a fresh install");
            }

            var openShiftCount = await context.Shifts
                .AsNoTracking()
                .CountAsync(s => seedTenantIds.Contains(s.TenantId) && !s.IsClosed);

            openShiftCount.Should().Be(0, because: "seed tenants must not start with open shifts");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for SQLite temp files.
            }
        }
    }

    [Fact]
    public async Task MultiTenantSeeder_ShouldPruneObsoleteDemoTenants_AndCloseOpenShifts()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"kasserpro-prune-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var dbPath = Path.Combine(tempDirectory, "kasserpro.db");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            await ButcherDataSeeder.SeedAsync(context);
            context.ChangeTracker.Clear();

            await MultiTenantSeeder.SeedAsync(context);
            context.ChangeTracker.Clear();

            var supermarketTenant = await context.Tenants.SingleAsync(t => t.Slug == "supermarket");
            var supermarketBranch = await context.Branches.FirstAsync(b => b.TenantId == supermarketTenant.Id);
            var supermarketUser = await context.Users.FirstAsync(u => u.TenantId == supermarketTenant.Id && u.Role == UserRole.Admin);

            context.Shifts.Add(new Shift
            {
                TenantId = supermarketTenant.Id,
                BranchId = supermarketBranch.Id,
                UserId = supermarketUser.Id,
                OpeningBalance = 1000,
                OpenedAt = DateTime.UtcNow.AddHours(-3),
                LastActivityAt = DateTime.UtcNow.AddMinutes(-15),
                IsClosed = false,
                IsForceClosed = false,
                IsHandedOver = false,
                HandoverBalance = 0
            });

            var obsoleteTenant = new Tenant
            {
                Name = "مطعم قديم",
                NameEn = "Legacy Restaurant",
                Slug = "restaurant",
                Currency = "EGP",
                Timezone = "Africa/Cairo",
                TaxRate = 14,
                IsTaxEnabled = true,
                IsActive = true,
                AllowNegativeStock = false,
                ReceiptShowLogo = true,
                ReceiptPaperSize = "80mm"
            };
            context.Tenants.Add(obsoleteTenant);
            await context.SaveChangesAsync();

            var obsoleteTenantId = obsoleteTenant.Id;

            var obsoleteBranch = new Branch
            {
                TenantId = obsoleteTenantId,
                Name = "فرع قديم",
                Code = "OLD01",
                Address = "القاهرة",
                Phone = "0200000000",
                DefaultTaxRate = 14,
                DefaultTaxInclusive = false,
                CurrencyCode = "EGP",
                IsActive = true
            };
            context.Branches.Add(obsoleteBranch);
            await context.SaveChangesAsync();

            var obsoleteUser = new User
            {
                TenantId = obsoleteTenantId,
                BranchId = obsoleteBranch.Id,
                Name = "Legacy Admin",
                Email = $"legacy-{Guid.NewGuid():N}@example.com",
                PasswordHash = "seed-hash",
                Role = UserRole.Admin,
                IsActive = true
            };

            var obsoleteCategory = new Category
            {
                TenantId = obsoleteTenantId,
                Name = "مشويات",
                NameEn = "Grills",
                SortOrder = 1,
                IsActive = true
            };

            context.Users.Add(obsoleteUser);
            context.Categories.Add(obsoleteCategory);
            await context.SaveChangesAsync();

            context.Products.Add(new Product
            {
                TenantId = obsoleteTenantId,
                CategoryId = obsoleteCategory.Id,
                Name = "كباب",
                NameEn = "Kebab",
                Sku = "OLD-KEB-1",
                Price = 100,
                Cost = 70,
                TaxRate = 14,
                TaxInclusive = false,
                TrackInventory = true,
                IsActive = true
            });

            context.Customers.Add(new Customer
            {
                TenantId = obsoleteTenantId,
                Name = "عميل قديم",
                Phone = "0199999999",
                IsActive = true
            });

            context.Shifts.Add(new Shift
            {
                TenantId = obsoleteTenantId,
                BranchId = obsoleteBranch.Id,
                UserId = obsoleteUser.Id,
                OpeningBalance = 500,
                OpenedAt = DateTime.UtcNow.AddHours(-5),
                LastActivityAt = DateTime.UtcNow.AddHours(-1),
                IsClosed = false,
                IsForceClosed = false,
                IsHandedOver = false,
                HandoverBalance = 0
            });

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            await MultiTenantSeeder.SeedAsync(context);
            context.ChangeTracker.Clear();

            var seededSlugs = await context.Tenants
                .AsNoTracking()
                .Where(t => SeedTenantRegistry.Slugs.Contains(t.Slug))
                .Select(t => t.Slug)
                .ToListAsync();

            seededSlugs.Should().BeEquivalentTo(new[]
            {
                "al-amana-butcher",
                "supermarket"
            });

            (await context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == obsoleteTenantId)).Should().BeFalse();
            (await context.Branches.IgnoreQueryFilters().AnyAsync(b => b.TenantId == obsoleteTenantId)).Should().BeFalse();
            (await context.Users.IgnoreQueryFilters().AnyAsync(u => u.TenantId == obsoleteTenantId)).Should().BeFalse();
            (await context.Categories.IgnoreQueryFilters().AnyAsync(c => c.TenantId == obsoleteTenantId)).Should().BeFalse();
            (await context.Products.IgnoreQueryFilters().AnyAsync(p => p.TenantId == obsoleteTenantId)).Should().BeFalse();
            (await context.Customers.IgnoreQueryFilters().AnyAsync(c => c.TenantId == obsoleteTenantId)).Should().BeFalse();
            (await context.Shifts.IgnoreQueryFilters().AnyAsync(s => s.TenantId == obsoleteTenantId)).Should().BeFalse();

            var openTargetShifts = await (
                from shift in context.Shifts.AsNoTracking()
                join tenant in context.Tenants.AsNoTracking() on shift.TenantId equals tenant.Id
                where SeedTenantRegistry.Slugs.Contains(tenant.Slug)
                      && !shift.IsClosed
                select shift.Id
            ).CountAsync();

            openTargetShifts.Should().Be(0, because: "target seed tenants must never keep open shifts");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for SQLite temp files.
            }
        }
    }
}
