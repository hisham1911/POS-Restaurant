namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Suppliers;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

public class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SupplierService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<List<SupplierDto>>> GetAllAsync()
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var suppliers = await _unitOfWork.Suppliers.Query()
            .Where(s => s.TenantId == tenantId && s.BranchId == branchId && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto
            {
                Id = s.Id,
                Name = s.Name,
                NameEn = s.NameEn,
                Phone = s.Phone,
                Email = s.Email,
                Address = s.Address,
                TaxNumber = s.TaxNumber,
                ContactPerson = s.ContactPerson,
                Notes = s.Notes,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                TotalDue = s.TotalDue,
                TotalPaid = s.TotalPaid,
                TotalPurchases = s.TotalPurchases,
                LastPurchaseDate = s.LastPurchaseDate
            })
            .ToListAsync();

        return ApiResponse<List<SupplierDto>>.Ok(suppliers);
    }

    public async Task<ApiResponse<SupplierDto>> GetByIdAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var supplier = await _unitOfWork.Suppliers.Query()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && s.BranchId == branchId && !s.IsDeleted);
        
        if (supplier == null)
            return ApiResponse<SupplierDto>.Fail(ErrorCodes.SUPPLIER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.SUPPLIER_NOT_FOUND));

        return ApiResponse<SupplierDto>.Ok(new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            NameEn = supplier.NameEn,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            TaxNumber = supplier.TaxNumber,
            ContactPerson = supplier.ContactPerson,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive,
            CreatedAt = supplier.CreatedAt,
            TotalDue = supplier.TotalDue,
            TotalPaid = supplier.TotalPaid,
            TotalPurchases = supplier.TotalPurchases,
            LastPurchaseDate = supplier.LastPurchaseDate
        });
    }

    public async Task<ApiResponse<SupplierDto>> CreateAsync(CreateSupplierRequest request)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<SupplierDto>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        var supplier = new Supplier
        {
            TenantId = _currentUser.TenantId,
            BranchId = _currentUser.BranchId,
            Name = request.Name.Trim(),
            NameEn = request.NameEn?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            TaxNumber = request.TaxNumber?.Trim(),
            ContactPerson = request.ContactPerson?.Trim(),
            Notes = request.Notes?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Suppliers.AddAsync(supplier);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<SupplierDto>.Ok(new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            NameEn = supplier.NameEn,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            TaxNumber = supplier.TaxNumber,
            ContactPerson = supplier.ContactPerson,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive,
            CreatedAt = supplier.CreatedAt,
            TotalDue = supplier.TotalDue,
            TotalPaid = supplier.TotalPaid,
            TotalPurchases = supplier.TotalPurchases,
            LastPurchaseDate = supplier.LastPurchaseDate
        }, "تم إضافة المورد بنجاح");
    }

    public async Task<ApiResponse<SupplierDto>> UpdateAsync(int id, UpdateSupplierRequest request)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<SupplierDto>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var supplier = await _unitOfWork.Suppliers.Query()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && s.BranchId == branchId && !s.IsDeleted);
        
        if (supplier == null)
            return ApiResponse<SupplierDto>.Fail(ErrorCodes.SUPPLIER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.SUPPLIER_NOT_FOUND));

        if (!request.IsActive && supplier.TotalDue > 0)
        {
            return ApiResponse<SupplierDto>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "لا يمكن تعطيل مورد لديه رصيد متبقي غير مسدد");
        }

        supplier.Name = request.Name.Trim();
        supplier.NameEn = request.NameEn?.Trim();
        supplier.Phone = request.Phone?.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.Address = request.Address?.Trim();
        supplier.TaxNumber = request.TaxNumber?.Trim();
        supplier.ContactPerson = request.ContactPerson?.Trim();
        supplier.Notes = request.Notes?.Trim();
        supplier.IsActive = request.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Suppliers.Update(supplier);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<SupplierDto>.Ok(new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            NameEn = supplier.NameEn,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            TaxNumber = supplier.TaxNumber,
            ContactPerson = supplier.ContactPerson,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive,
            CreatedAt = supplier.CreatedAt,
            TotalDue = supplier.TotalDue,
            TotalPaid = supplier.TotalPaid,
            TotalPurchases = supplier.TotalPurchases,
            LastPurchaseDate = supplier.LastPurchaseDate
        }, "تم تحديث المورد بنجاح");
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var supplier = await _unitOfWork.Suppliers.Query()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && s.BranchId == branchId && !s.IsDeleted);
        
        if (supplier == null)
            return ApiResponse<bool>.Fail(ErrorCodes.SUPPLIER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.SUPPLIER_NOT_FOUND));

        var hasOutstandingInvoices = await _unitOfWork.PurchaseInvoices.Query()
            .AnyAsync(pi => pi.TenantId == tenantId
                         && !pi.IsDeleted
                         && pi.SupplierId == supplier.Id
                         && pi.Status != PurchaseInvoiceStatus.Draft
                         && pi.Status != PurchaseInvoiceStatus.Cancelled);

        if (supplier.TotalDue > 0 || hasOutstandingInvoices)
        {
            return ApiResponse<bool>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "لا يمكن حذف هذا المورد. لديه فواتير غير مسددة أو رصيد متبقٍ. يرجى تسوية جميع الفواتير أولاً.");
        }

        // Soft delete
        supplier.IsDeleted = true;
        supplier.UpdatedAt = DateTime.UtcNow;
        
        _unitOfWork.Suppliers.Update(supplier);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف المورد بنجاح");
    }
}
