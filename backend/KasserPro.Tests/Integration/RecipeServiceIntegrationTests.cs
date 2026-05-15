namespace KasserPro.Tests.Integration;

using FluentAssertions;
using KasserPro.Application.Common;
using KasserPro.Application.DTOs.Recipes;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;
using KasserPro.Infrastructure.Data;
using KasserPro.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class RecipeServiceIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RecipeServiceIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateAsync_ShouldRejectNonRawMaterialIngredient()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedRecipeContextAsync(db, "recipe-update-invalid");
        var service = CreateService(db, data.Tenant.Id, data.Branch.Id, data.User.Id);

        var recipe = new Recipe
        {
            TenantId = data.Tenant.Id,
            ProductId = data.ManufacturedProduct.Id,
            YieldQuantity = 1,
            IsActive = true,
            AutoDeductIngredients = true
        };
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();

        var result = await service.UpdateAsync(recipe.Id, new UpdateRecipeRequest
        {
            YieldQuantity = 1,
            IsActive = true,
            AutoDeductIngredients = true,
            Ingredients =
            [
                new()
                {
                    RawMaterialProductId = data.ServiceProduct.Id,
                    Quantity = 1,
                    Unit = UnitOfMeasure.Piece
                }
            ]
        }, data.Tenant.Id);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VALIDATION_ERROR);
    }

    [Fact]
    public async Task DeductIngredientsAsync_ShouldConvertIngredientQuantityToRawMaterialUnit()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedRecipeContextAsync(db, "recipe-unit-conversion");
        var service = CreateService(db, data.Tenant.Id, data.Branch.Id, data.User.Id);

        var createResult = await service.CreateAsync(new CreateRecipeRequest
        {
            ProductId = data.ManufacturedProduct.Id,
            YieldQuantity = 1,
            AutoDeductIngredients = true,
            Ingredients =
            [
                new()
                {
                    RawMaterialProductId = data.RawMaterial.Id,
                    Quantity = 500,
                    Unit = UnitOfMeasure.Gram
                }
            ]
        }, data.Tenant.Id);

        createResult.Success.Should().BeTrue(createResult.Message);
        createResult.Data!.TotalCost.Should().Be(5m);

        db.BranchInventories.Add(new BranchInventory
        {
            TenantId = data.Tenant.Id,
            BranchId = data.Branch.Id,
            ProductId = data.RawMaterial.Id,
            Quantity = 2m,
            ReorderLevel = 0
        });
        await db.SaveChangesAsync();

        var deductResult = await service.DeductIngredientsAsync(
            createResult.Data.Id,
            multiplier: 2,
            branchId: data.Branch.Id,
            tenantId: data.Tenant.Id);

        deductResult.Success.Should().BeTrue(deductResult.Message);

        var inventory = await db.BranchInventories.SingleAsync(i =>
            i.ProductId == data.RawMaterial.Id &&
            i.BranchId == data.Branch.Id &&
            i.TenantId == data.Tenant.Id);

        inventory.Quantity.Should().Be(1m);
    }

    [Fact]
    public async Task CreateAsync_ShouldUseRawMaterialAverageCostWhenManualCostIsZero()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedRecipeContextAsync(db, "recipe-average-cost");
        var service = CreateService(db, data.Tenant.Id, data.Branch.Id, data.User.Id);

        data.RawMaterial.Cost = 0m;
        data.RawMaterial.AverageCost = 12m;
        await db.SaveChangesAsync();

        var createResult = await service.CreateAsync(new CreateRecipeRequest
        {
            ProductId = data.ManufacturedProduct.Id,
            YieldQuantity = 1,
            AutoDeductIngredients = true,
            Ingredients =
            [
                new()
                {
                    RawMaterialProductId = data.RawMaterial.Id,
                    Quantity = 500,
                    Unit = UnitOfMeasure.Gram
                }
            ]
        }, data.Tenant.Id);

        createResult.Success.Should().BeTrue(createResult.Message);
        createResult.Data!.TotalCost.Should().Be(6m);

        var productRecipe = await service.GetByProductIdAsync(
            data.ManufacturedProduct.Id,
            data.Tenant.Id);

        productRecipe.Success.Should().BeTrue(productRecipe.Message);
        productRecipe.Data!.TotalCost.Should().Be(6m);
        productRecipe.Data.Ingredients.Single().Cost.Should().Be(6m);
    }

    [Fact]
    public async Task CreateAsync_ShouldUseRawMaterialManualCostWhenAverageCostIsZero()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var data = await SeedRecipeContextAsync(db, "recipe-zero-average-cost");
        var service = CreateService(db, data.Tenant.Id, data.Branch.Id, data.User.Id);

        data.RawMaterial.Cost = 10m;
        data.RawMaterial.AverageCost = 0m;
        await db.SaveChangesAsync();

        var createResult = await service.CreateAsync(new CreateRecipeRequest
        {
            ProductId = data.ManufacturedProduct.Id,
            YieldQuantity = 1,
            AutoDeductIngredients = true,
            Ingredients =
            [
                new()
                {
                    RawMaterialProductId = data.RawMaterial.Id,
                    Quantity = 500,
                    Unit = UnitOfMeasure.Gram
                }
            ]
        }, data.Tenant.Id);

        createResult.Success.Should().BeTrue(createResult.Message);
        createResult.Data!.TotalCost.Should().Be(5m);
        createResult.Data.Ingredients.Single().Cost.Should().Be(5m);
    }

    private static RecipeService CreateService(AppDbContext db, int tenantId, int branchId, int userId)
        => new(
            db,
            new TestCurrentUserService
            {
                TenantId = tenantId,
                BranchId = branchId,
                UserId = userId,
                Email = "recipe-tests@test.com"
            });

    private static async Task<RecipeSeedData> SeedRecipeContextAsync(AppDbContext db, string slug)
    {
        var tenant = new Tenant
        {
            Name = $"Recipe Tenant {slug}",
            Slug = $"{slug}-{Guid.NewGuid():N}",
            IsActive = true,
            IsTaxEnabled = true,
            TaxRate = 14m
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            TenantId = tenant.Id,
            Name = $"Recipe Branch {slug}",
            Code = $"RC-{Guid.NewGuid().ToString("N")[..4]}",
            CurrencyCode = "EGP",
            DefaultTaxRate = 14m,
            IsActive = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var user = new User
        {
            TenantId = tenant.Id,
            BranchId = branch.Id,
            Name = "Recipe Admin",
            Email = $"recipe-{Guid.NewGuid():N}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = UserRole.Admin,
            IsActive = true
        };

        var category = new Category
        {
            TenantId = tenant.Id,
            Name = $"Recipe Category {slug}",
            IsActive = true
        };
        db.Users.Add(user);
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var manufacturedProduct = new Product
        {
            TenantId = tenant.Id,
            CategoryId = category.Id,
            Name = $"Pizza {slug}",
            Price = 50m,
            Cost = 0m,
            Type = ProductType.Manufactured,
            Unit = UnitOfMeasure.Piece,
            TrackInventory = false,
            IsActive = true
        };

        var rawMaterial = new Product
        {
            TenantId = tenant.Id,
            CategoryId = category.Id,
            Name = $"Flour {slug}",
            Price = 0m,
            Cost = 10m,
            Type = ProductType.RawMaterial,
            Unit = UnitOfMeasure.Kilogram,
            TrackInventory = true,
            IsActive = true
        };

        var serviceProduct = new Product
        {
            TenantId = tenant.Id,
            CategoryId = category.Id,
            Name = $"Service {slug}",
            Price = 10m,
            Cost = 0m,
            Type = ProductType.Service,
            Unit = UnitOfMeasure.Piece,
            TrackInventory = false,
            IsActive = true
        };

        db.Products.AddRange(manufacturedProduct, rawMaterial, serviceProduct);
        await db.SaveChangesAsync();

        return new RecipeSeedData(
            tenant,
            branch,
            user,
            category,
            manufacturedProduct,
            rawMaterial,
            serviceProduct);
    }

    private sealed record RecipeSeedData(
        Tenant Tenant,
        Branch Branch,
        User User,
        Category Category,
        Product ManufacturedProduct,
        Product RawMaterial,
        Product ServiceProduct);
}
