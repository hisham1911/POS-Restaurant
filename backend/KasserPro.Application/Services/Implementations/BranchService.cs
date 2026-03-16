namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Branches;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;

public class BranchService : IBranchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public BranchService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<List<BranchDto>>> GetAllAsync()
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.UserId;

        // Get user to check their assigned branch
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        
        var query = _unitOfWork.Branches.Query()
            .Where(b => b.TenantId == tenantId);

        // Cashiers can only see their assigned branch
        if (user?.Role == Domain.Enums.UserRole.Cashier && user.BranchId.HasValue)
        {
            query = query.Where(b => b.Id == user.BranchId.Value);
        }

        var branches = await query
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto
            {
                Id = b.Id,
                TenantId = b.TenantId,
                Name = b.Name,
                Code = b.Code,
                Address = b.Address,
                Phone = b.Phone,
                DefaultTaxRate = b.DefaultTaxRate,
                DefaultTaxInclusive = b.DefaultTaxInclusive,
                CurrencyCode = b.CurrencyCode,
                IsActive = b.IsActive,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<BranchDto>>.Ok(branches);
    }

    public async Task<ApiResponse<BranchDto>> GetByIdAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var branch = await _unitOfWork.Branches.Query()
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId);

        if (branch == null)
            return ApiResponse<BranchDto>.Fail("الفرع غير موجود");

        return ApiResponse<BranchDto>.Ok(new BranchDto
        {
            Id = branch.Id,
            TenantId = branch.TenantId,
            Name = branch.Name,
            Code = branch.Code,
            Address = branch.Address,
            Phone = branch.Phone,
            DefaultTaxRate = branch.DefaultTaxRate,
            DefaultTaxInclusive = branch.DefaultTaxInclusive,
            CurrencyCode = branch.CurrencyCode,
            IsActive = branch.IsActive,
            CreatedAt = branch.CreatedAt
        });
    }

    public async Task<ApiResponse<BranchDto>> CreateAsync(CreateBranchDto dto)
    {
        var tenantId = _currentUser.TenantId;
        
        // Check if code already exists
        var exists = await _unitOfWork.Branches.Query()
            .AnyAsync(b => b.TenantId == tenantId && b.Code == dto.Code);

        if (exists)
            return ApiResponse<BranchDto>.Fail("كود الفرع موجود مسبقاً");

        var branch = new Branch
        {
            TenantId = tenantId,
            Name = dto.Name,
            Code = dto.Code,
            Address = dto.Address,
            Phone = dto.Phone,
            IsActive = true
        };

        await _unitOfWork.Branches.AddAsync(branch);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<BranchDto>.Ok(new BranchDto
        {
            Id = branch.Id,
            TenantId = branch.TenantId,
            Name = branch.Name,
            Code = branch.Code,
            Address = branch.Address,
            Phone = branch.Phone,
            DefaultTaxRate = branch.DefaultTaxRate,
            DefaultTaxInclusive = branch.DefaultTaxInclusive,
            CurrencyCode = branch.CurrencyCode,
            IsActive = branch.IsActive,
            CreatedAt = branch.CreatedAt
        }, "تم إنشاء الفرع بنجاح");
    }

    public async Task<ApiResponse<BranchDto>> UpdateAsync(int id, UpdateBranchDto dto)
    {
        var tenantId = _currentUser.TenantId;
        var branch = await _unitOfWork.Branches.Query()
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId);

        if (branch == null)
            return ApiResponse<BranchDto>.Fail("الفرع غير موجود");

        // Check if code already exists for another branch
        var codeExists = await _unitOfWork.Branches.Query()
            .AnyAsync(b => b.TenantId == tenantId && b.Code == dto.Code && b.Id != id);

        if (codeExists)
            return ApiResponse<BranchDto>.Fail("كود الفرع موجود مسبقاً");

        branch.Name = dto.Name;
        branch.Code = dto.Code;
        branch.Address = dto.Address;
        branch.Phone = dto.Phone;
        branch.IsActive = dto.IsActive;

        _unitOfWork.Branches.Update(branch);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<BranchDto>.Ok(new BranchDto
        {
            Id = branch.Id,
            TenantId = branch.TenantId,
            Name = branch.Name,
            Code = branch.Code,
            Address = branch.Address,
            Phone = branch.Phone,
            DefaultTaxRate = branch.DefaultTaxRate,
            DefaultTaxInclusive = branch.DefaultTaxInclusive,
            CurrencyCode = branch.CurrencyCode,
            IsActive = branch.IsActive,
            CreatedAt = branch.CreatedAt
        }, "تم تحديث الفرع بنجاح");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var branch = await _unitOfWork.Branches.Query()
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId);

        if (branch == null)
            return ApiResponse<bool>.Fail("الفرع غير موجود");

        branch.IsDeleted = true;
        _unitOfWork.Branches.Update(branch);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف الفرع بنجاح");
    }
}
