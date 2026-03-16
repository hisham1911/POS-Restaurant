using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Expenses;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasserPro.Application.Services.Implementations;

/// <summary>
/// Service implementation for Expense Category management
/// </summary>
public class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ExpenseCategoryService> _logger;

    public ExpenseCategoryService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<ExpenseCategoryService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ApiResponse<List<ExpenseCategoryDto>>> GetAllAsync(bool includeInactive = false)
    {
        try
        {
            var query = _unitOfWork.ExpenseCategories.Query()
                .Where(c => c.TenantId == _currentUserService.TenantId);

            if (!includeInactive)
                query = query.Where(c => c.IsActive);

            var categories = await query
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            var dtos = categories.Select(MapToDto).ToList();

            return ApiResponse<List<ExpenseCategoryDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense categories");
            return ApiResponse<List<ExpenseCategoryDto>>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<ExpenseCategoryDto>> GetByIdAsync(int id)
    {
        try
        {
            var category = await _unitOfWork.ExpenseCategories.Query()
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _currentUserService.TenantId);

            if (category == null)
                return ApiResponse<ExpenseCategoryDto>.Fail(ErrorCodes.EXPENSE_CATEGORY_NOT_FOUND);

            return ApiResponse<ExpenseCategoryDto>.Ok(MapToDto(category));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense category {Id}", id);
            return ApiResponse<ExpenseCategoryDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<ExpenseCategoryDto>> CreateAsync(CreateExpenseCategoryRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Check for duplicate name
            var exists = await _unitOfWork.ExpenseCategories.Query()
                .AnyAsync(c => c.TenantId == _currentUserService.TenantId && 
                              c.Name == request.Name);

            if (exists)
                return ApiResponse<ExpenseCategoryDto>.Fail(ErrorCodes.EXPENSE_CATEGORY_ALREADY_EXISTS);

            var category = new ExpenseCategory
            {
                TenantId = _currentUserService.TenantId,
                Name = request.Name,
                NameEn = request.NameEn,
                Description = request.Description,
                Icon = request.Icon,
                Color = request.Color,
                IsActive = request.IsActive,
                IsSystem = false,
                SortOrder = request.SortOrder
            };

            await _unitOfWork.ExpenseCategories.AddAsync(category);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<ExpenseCategoryDto>.Ok(MapToDto(category));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error creating expense category");
            return ApiResponse<ExpenseCategoryDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<ExpenseCategoryDto>> UpdateAsync(int id, UpdateExpenseCategoryRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var category = await _unitOfWork.ExpenseCategories.Query()
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _currentUserService.TenantId);

            if (category == null)
                return ApiResponse<ExpenseCategoryDto>.Fail(ErrorCodes.EXPENSE_CATEGORY_NOT_FOUND);

            // Cannot edit system categories
            if (category.IsSystem)
                return ApiResponse<ExpenseCategoryDto>.Fail(ErrorCodes.EXPENSE_CATEGORY_SYSTEM);

            // Check for duplicate name (excluding current category)
            var exists = await _unitOfWork.ExpenseCategories.Query()
                .AnyAsync(c => c.TenantId == _currentUserService.TenantId && 
                              c.Name == request.Name &&
                              c.Id != id);

            if (exists)
                return ApiResponse<ExpenseCategoryDto>.Fail(ErrorCodes.EXPENSE_CATEGORY_ALREADY_EXISTS);

            // Update category
            category.Name = request.Name;
            category.NameEn = request.NameEn;
            category.Description = request.Description;
            category.Icon = request.Icon;
            category.Color = request.Color;
            category.IsActive = request.IsActive;
            category.SortOrder = request.SortOrder;

            _unitOfWork.ExpenseCategories.Update(category);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<ExpenseCategoryDto>.Ok(MapToDto(category));
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error updating expense category {Id}", id);
            return ApiResponse<ExpenseCategoryDto>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var category = await _unitOfWork.ExpenseCategories.Query()
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _currentUserService.TenantId);

            if (category == null)
                return ApiResponse<bool>.Fail(ErrorCodes.EXPENSE_CATEGORY_NOT_FOUND);

            // Cannot delete system categories
            if (category.IsSystem)
                return ApiResponse<bool>.Fail(ErrorCodes.EXPENSE_CATEGORY_SYSTEM);

            // Check if category has expenses
            var hasExpenses = await _unitOfWork.Expenses.Query()
                .AnyAsync(e => e.CategoryId == id);

            if (hasExpenses)
                return ApiResponse<bool>.Fail(ErrorCodes.EXPENSE_CATEGORY_HAS_EXPENSES);

            _unitOfWork.ExpenseCategories.Delete(category);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Error deleting expense category {Id}", id);
            return ApiResponse<bool>.Fail(ErrorCodes.INTERNAL_ERROR);
        }
    }

    public async Task SeedDefaultCategoriesAsync()
    {
        try
        {
            var tenants = await _unitOfWork.Tenants.Query().ToListAsync();

            foreach (var tenant in tenants)
            {
                // Check if categories already exist
                var exists = await _unitOfWork.ExpenseCategories.Query()
                    .AnyAsync(c => c.TenantId == tenant.Id && c.IsSystem);

                if (exists)
                    continue;

                var defaultCategories = new List<ExpenseCategory>
                {
                    new() { TenantId = tenant.Id, Name = "Ø±ÙˆØ§ØªØ¨", NameEn = "Salaries", Icon = "ðŸ’°", Color = "#3B82F6", IsActive = true, IsSystem = true, SortOrder = 1 },
                    new() { TenantId = tenant.Id, Name = "Ø¥ÙŠØ¬Ø§Ø±", NameEn = "Rent", Icon = "ðŸ¢", Color = "#8B5CF6", IsActive = true, IsSystem = true, SortOrder = 2 },
                    new() { TenantId = tenant.Id, Name = "ÙƒÙ‡Ø±Ø¨Ø§Ø¡", NameEn = "Electricity", Icon = "âš¡", Color = "#F59E0B", IsActive = true, IsSystem = true, SortOrder = 3 },
                    new() { TenantId = tenant.Id, Name = "Ù…ÙŠØ§Ù‡", NameEn = "Water", Icon = "ðŸ’§", Color = "#06B6D4", IsActive = true, IsSystem = true, SortOrder = 4 },
                    new() { TenantId = tenant.Id, Name = "ØµÙŠØ§Ù†Ø©", NameEn = "Maintenance", Icon = "ðŸ”§", Color = "#10B981", IsActive = true, IsSystem = true, SortOrder = 5 },
                    new() { TenantId = tenant.Id, Name = "ØªØ³ÙˆÙŠÙ‚", NameEn = "Marketing", Icon = "ðŸ“¢", Color = "#EC4899", IsActive = true, IsSystem = true, SortOrder = 6 },
                    new() { TenantId = tenant.Id, Name = "Ù…ÙˆØ§ØµÙ„Ø§Øª", NameEn = "Transportation", Icon = "ðŸš—", Color = "#6366F1", IsActive = true, IsSystem = true, SortOrder = 7 },
                    new() { TenantId = tenant.Id, Name = "Ø§ØªØµØ§Ù„Ø§Øª", NameEn = "Communications", Icon = "ðŸ“ž", Color = "#14B8A6", IsActive = true, IsSystem = true, SortOrder = 8 },
                    new() { TenantId = tenant.Id, Name = "Ù…Ø³ØªÙ„Ø²Ù…Ø§Øª Ù…ÙƒØªØ¨ÙŠØ©", NameEn = "Office Supplies", Icon = "ðŸ“", Color = "#F97316", IsActive = true, IsSystem = true, SortOrder = 9 },
                    new() { TenantId = tenant.Id, Name = "Ø£Ø®Ø±Ù‰", NameEn = "Other", Icon = "ðŸ“¦", Color = "#64748B", IsActive = true, IsSystem = true, SortOrder = 10 }
                };

                foreach (var category in defaultCategories)
                {
                    await _unitOfWork.ExpenseCategories.AddAsync(category);
                }

                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Default expense categories seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default expense categories");
        }
    }

    private ExpenseCategoryDto MapToDto(ExpenseCategory category)
    {
        return new ExpenseCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            NameEn = category.NameEn,
            Description = category.Description,
            Icon = category.Icon,
            Color = category.Color,
            IsActive = category.IsActive,
            IsSystem = category.IsSystem,
            SortOrder = category.SortOrder,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }
}
