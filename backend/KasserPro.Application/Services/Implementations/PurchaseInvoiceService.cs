namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.PurchaseInvoices;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

public class PurchaseInvoiceService : IPurchaseInvoiceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public PurchaseInvoiceService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<PurchaseInvoiceDto>>> GetAllAsync(
        int? supplierId = null,
        PurchaseInvoiceStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var tenantId = _currentUser.TenantId;
        var query = _unitOfWork.PurchaseInvoices.Query()
            .Where(pi => pi.TenantId == tenantId && !pi.IsDeleted);

        if (supplierId.HasValue)
            query = query.Where(pi => pi.SupplierId == supplierId.Value);

        if (status.HasValue)
            query = query.Where(pi => pi.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(pi => pi.InvoiceDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(pi => pi.InvoiceDate <= toDate.Value);

        var totalCount = await query.CountAsync();
        
        var invoices = await query
            .OrderByDescending(pi => pi.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(pi => new PurchaseInvoiceDto
            {
                Id = pi.Id,
                InvoiceNumber = pi.InvoiceNumber,
                SupplierId = pi.SupplierId,
                SupplierName = pi.SupplierName,
                SupplierPhone = pi.SupplierPhone,
                InvoiceDate = pi.InvoiceDate,
                Status = pi.Status.ToString(),
                Subtotal = pi.Subtotal,
                TaxRate = pi.TaxRate,
                TaxAmount = pi.TaxAmount,
                Total = pi.Total,
                AmountPaid = pi.AmountPaid,
                AmountDue = pi.AmountDue,
                Notes = pi.Notes,
                CreatedByUserName = pi.CreatedByUserName ?? "",
                ConfirmedByUserName = pi.ConfirmedByUserName,
                ConfirmedAt = pi.ConfirmedAt,
                CreatedAt = pi.CreatedAt
            })
            .ToListAsync();

        var result = new PagedResult<PurchaseInvoiceDto>(invoices, totalCount, pageNumber, pageSize);

        return ApiResponse<PagedResult<PurchaseInvoiceDto>>.Ok(result);
    }

    public async Task<ApiResponse<PurchaseInvoiceDto>> GetByIdAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var invoice = await _unitOfWork.PurchaseInvoices.Query()
            .Include(pi => pi.Items)
            .Include(pi => pi.Payments)
            .FirstOrDefaultAsync(pi => pi.Id == id && pi.TenantId == tenantId && !pi.IsDeleted);

        if (invoice == null)
            return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND));

        var dto = new PurchaseInvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            SupplierId = invoice.SupplierId,
            SupplierName = invoice.SupplierName,
            SupplierPhone = invoice.SupplierPhone,
            InvoiceDate = invoice.InvoiceDate,
            Status = invoice.Status.ToString(),
            Subtotal = invoice.Subtotal,
            TaxRate = invoice.TaxRate,
            TaxAmount = invoice.TaxAmount,
            Total = invoice.Total,
            AmountPaid = invoice.AmountPaid,
            AmountDue = invoice.AmountDue,
            Notes = invoice.Notes,
            CreatedByUserName = invoice.CreatedByUserName ?? "",
            ConfirmedByUserName = invoice.ConfirmedByUserName,
            ConfirmedAt = invoice.ConfirmedAt,
            CreatedAt = invoice.CreatedAt,
            Items = invoice.Items.Select(item => new PurchaseInvoiceItemDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                ProductSku = item.ProductSku,
                Quantity = item.Quantity,
                PurchasePrice = item.PurchasePrice,
                SellingPrice = item.SellingPrice,
                Total = item.Total,
                Notes = item.Notes
            }).ToList(),
            Payments = invoice.Payments.Select(p => new PurchaseInvoicePaymentDto
            {
                Id = p.Id,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                Method = p.Method.ToString(),
                ReferenceNumber = p.ReferenceNumber,
                Notes = p.Notes,
                CreatedByUserName = p.CreatedByUserName ?? "",
                CreatedAt = p.CreatedAt
            }).ToList()
        };

        return ApiResponse<PurchaseInvoiceDto>.Ok(dto);
    }

    public async Task<ApiResponse<PurchaseInvoiceDto>> CreateAsync(CreatePurchaseInvoiceRequest request)
    {
        // Validation
        if (request.Items == null || !request.Items.Any())
            return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_EMPTY, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_EMPTY));

        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var userId = _currentUser.UserId;

        // Get supplier
        var supplier = await _unitOfWork.Suppliers.Query()
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId && s.TenantId == tenantId && !s.IsDeleted);

        if (supplier == null)
            return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.SUPPLIER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.SUPPLIER_NOT_FOUND));

        // Get user
        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == userId);

        // Get tenant for tax rate
        var tenant = await _unitOfWork.Tenants.Query()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        var taxRate = tenant?.TaxRate ?? 14m;

        // Generate invoice number
        var lastInvoice = await _unitOfWork.PurchaseInvoices.Query()
            .Where(pi => pi.TenantId == tenantId)
            .OrderByDescending(pi => pi.Id)
            .FirstOrDefaultAsync();

        var invoiceNumber = GenerateInvoiceNumber(lastInvoice?.InvoiceNumber);

        // Create invoice
        var invoice = new PurchaseInvoice
        {
            TenantId = tenantId,
            BranchId = branchId,
            InvoiceNumber = invoiceNumber,
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            SupplierPhone = supplier.Phone,
            SupplierAddress = supplier.Address,
            InvoiceDate = request.InvoiceDate,
            Status = PurchaseInvoiceStatus.Draft,
            TaxRate = taxRate,
            Notes = request.Notes,
            CreatedByUserId = userId,
            CreatedByUserName = user?.Name,
            CreatedAt = DateTime.UtcNow
        };

        // Add items
        decimal subtotal = 0;
        foreach (var itemRequest in request.Items)
        {
            if (itemRequest.Quantity <= 0)
                return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_INVALID_QUANTITY, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_INVALID_QUANTITY));

            if (itemRequest.PurchasePrice < 0)
                return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_INVALID_PRICE, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_INVALID_PRICE));

            var product = await _unitOfWork.Products.Query()
                .FirstOrDefaultAsync(p => p.Id == itemRequest.ProductId && p.TenantId == tenantId && !p.IsDeleted);

            if (product == null)
                return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PRODUCT_NOT_FOUND));

            if (product.Type == ProductType.Service)
                return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PRODUCT_SERVICE_NOT_PURCHASABLE, ErrorMessages.Get(ErrorCodes.PRODUCT_SERVICE_NOT_PURCHASABLE));

            var itemTotal = itemRequest.Quantity * itemRequest.PurchasePrice;
            subtotal += itemTotal;

            invoice.Items.Add(new PurchaseInvoiceItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductSku = product.Sku,
                Quantity = itemRequest.Quantity,
                PurchasePrice = itemRequest.PurchasePrice,
                SellingPrice = itemRequest.SellingPrice,
                Total = itemTotal,
                Notes = itemRequest.Notes
            });
        }

        // Calculate totals
        invoice.Subtotal = subtotal;
        invoice.TaxAmount = subtotal * (taxRate / 100);
        invoice.Total = invoice.Subtotal + invoice.TaxAmount;
        invoice.AmountDue = invoice.Total;

        await _unitOfWork.PurchaseInvoices.AddAsync(invoice);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(invoice.Id);
    }

    public async Task<ApiResponse<PurchaseInvoiceDto>> UpdateAsync(int id, UpdatePurchaseInvoiceRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var invoice = await _unitOfWork.PurchaseInvoices.Query()
            .Include(pi => pi.Items)
            .FirstOrDefaultAsync(pi => pi.Id == id && pi.TenantId == tenantId && !pi.IsDeleted);

        if (invoice == null)
            return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND));

        if (invoice.Status != PurchaseInvoiceStatus.Draft)
            return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE));

        // Validation
        if (request.Items == null || !request.Items.Any())
            return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_EMPTY, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_EMPTY));

        // Get supplier
        var supplier = await _unitOfWork.Suppliers.Query()
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId && s.TenantId == tenantId && !s.IsDeleted);

        if (supplier == null)
            return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.SUPPLIER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.SUPPLIER_NOT_FOUND));

        // Update invoice header
        invoice.SupplierId = supplier.Id;
        invoice.SupplierName = supplier.Name;
        invoice.SupplierPhone = supplier.Phone;
        invoice.SupplierAddress = supplier.Address;
        invoice.InvoiceDate = request.InvoiceDate;
        invoice.Notes = request.Notes;
        invoice.UpdatedAt = DateTime.UtcNow;

        // Remove old items
        foreach (var oldItem in invoice.Items.ToList())
        {
            _unitOfWork.PurchaseInvoiceItems.Delete(oldItem);
        }
        invoice.Items.Clear();

        // Add new items
        decimal subtotal = 0;
        foreach (var itemRequest in request.Items)
        {
            if (itemRequest.Quantity <= 0)
                return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_INVALID_QUANTITY, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_INVALID_QUANTITY));

            if (itemRequest.PurchasePrice < 0)
                return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_INVALID_PRICE, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_INVALID_PRICE));

            var product = await _unitOfWork.Products.Query()
                .FirstOrDefaultAsync(p => p.Id == itemRequest.ProductId && p.TenantId == tenantId && !p.IsDeleted);

            if (product == null)
                return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PRODUCT_NOT_FOUND));

            if (product.Type == ProductType.Service)
                return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PRODUCT_SERVICE_NOT_PURCHASABLE, ErrorMessages.Get(ErrorCodes.PRODUCT_SERVICE_NOT_PURCHASABLE));

            var itemTotal = itemRequest.Quantity * itemRequest.PurchasePrice;
            subtotal += itemTotal;

            invoice.Items.Add(new PurchaseInvoiceItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductSku = product.Sku,
                Quantity = itemRequest.Quantity,
                PurchasePrice = itemRequest.PurchasePrice,
                SellingPrice = itemRequest.SellingPrice,
                Total = itemTotal,
                Notes = itemRequest.Notes
            });
        }

        // Recalculate totals
        invoice.Subtotal = subtotal;
        invoice.TaxAmount = subtotal * (invoice.TaxRate / 100);
        invoice.Total = invoice.Subtotal + invoice.TaxAmount;
        invoice.AmountDue = invoice.Total - invoice.AmountPaid;

        _unitOfWork.PurchaseInvoices.Update(invoice);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(invoice.Id);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var invoice = await _unitOfWork.PurchaseInvoices.Query()
            .FirstOrDefaultAsync(pi => pi.Id == id && pi.TenantId == tenantId && !pi.IsDeleted);

        if (invoice == null)
            return ApiResponse<bool>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND));

        if (invoice.Status == PurchaseInvoiceStatus.Confirmed)
            return ApiResponse<bool>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_DELETABLE, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_DELETABLE));

        // Soft delete
        invoice.IsDeleted = true;
        invoice.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.PurchaseInvoices.Update(invoice);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف الفاتورة بنجاح");
    }

    public async Task<ApiResponse<PurchaseInvoiceDto>> ConfirmAsync(int id)
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.UserId;

        var invoice = await _unitOfWork.PurchaseInvoices.Query()
            .Include(pi => pi.Items)
            .FirstOrDefaultAsync(pi => pi.Id == id && pi.TenantId == tenantId && !pi.IsDeleted);

        if (invoice == null)
            return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND));

        if (invoice.Status != PurchaseInvoiceStatus.Draft)
            return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_ALREADY_CONFIRMED, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_ALREADY_CONFIRMED));

        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == userId);

        // Update invoice status
        invoice.Status = PurchaseInvoiceStatus.Confirmed;
        invoice.ConfirmedByUserId = userId;
        invoice.ConfirmedByUserName = user?.Name;
        invoice.ConfirmedAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;

        // Update inventory for each item
        foreach (var item in invoice.Items)
        {
            var product = await _unitOfWork.Products.Query()
                .FirstOrDefaultAsync(p => p.Id == item.ProductId);

            if (product != null)
            {
                // Update BranchInventory (the authoritative stock location)
                var branchInventory = await _unitOfWork.BranchInventories.Query()
                    .FirstOrDefaultAsync(bi => bi.ProductId == product.Id 
                                            && bi.BranchId == invoice.BranchId 
                                            && bi.TenantId == tenantId);

                int balanceBefore;
                
                if (branchInventory == null)
                {
                    // Create BranchInventory if it doesn't exist
                    branchInventory = new BranchInventory
                    {
                        TenantId = tenantId,
                        BranchId = invoice.BranchId,
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        ReorderLevel = product.LowStockThreshold ?? 10,
                        LastUpdatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.BranchInventories.AddAsync(branchInventory);
                    balanceBefore = 0;
                }
                else
                {
                    balanceBefore = branchInventory.Quantity;
                    branchInventory.Quantity += item.Quantity;
                    branchInventory.LastUpdatedAt = DateTime.UtcNow;
                    _unitOfWork.BranchInventories.Update(branchInventory);
                }

                // Create stock movement (for both new and existing inventory)
                var stockMovement = new StockMovement
                {
                    TenantId = tenantId,
                    BranchId = invoice.BranchId,
                    ProductId = product.Id,
                    Type = StockMovementType.Receiving,
                    Quantity = item.Quantity,
                    ReferenceId = invoice.Id,
                    ReferenceType = "PurchaseInvoice",
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceBefore + item.Quantity,
                    Reason = $"شراء من {invoice.SupplierName}",
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.StockMovements.AddAsync(stockMovement);

                // Update product metadata (not stock quantity)
                product.LastPurchasePrice = item.PurchasePrice;
                product.LastPurchaseDate = DateTime.UtcNow;
                product.LastStockUpdate = DateTime.UtcNow;
                
                // Update average cost using weighted average
                var oldStock = balanceBefore;
                var oldAvgCost = product.AverageCost ?? product.Cost ?? 0m;
                var newStock = balanceBefore + item.Quantity;
                if (newStock > 0)
                {
                    var totalOldValue = oldStock * oldAvgCost;
                    var totalNewValue = item.Quantity * item.PurchasePrice;
                    product.AverageCost = (totalOldValue + totalNewValue) / newStock;
                }
                
                product.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Products.Update(product);
            }
        }

        _unitOfWork.PurchaseInvoices.Update(invoice);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(invoice.Id);
    }

    public async Task<ApiResponse<PurchaseInvoiceDto>> CancelAsync(int id, CancelInvoiceRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.UserId;

        var invoice = await _unitOfWork.PurchaseInvoices.Query()
            .Include(pi => pi.Items)
            .FirstOrDefaultAsync(pi => pi.Id == id && pi.TenantId == tenantId && !pi.IsDeleted);

        if (invoice == null)
            return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND));

        if (invoice.Status == PurchaseInvoiceStatus.Cancelled)
            return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.PURCHASE_INVOICE_ALREADY_CANCELLED, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_ALREADY_CANCELLED));

        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == userId);

        var wasConfirmed = invoice.Status == PurchaseInvoiceStatus.Confirmed;

        // Update invoice status
        invoice.Status = PurchaseInvoiceStatus.Cancelled;
        invoice.CancelledByUserId = userId;
        invoice.CancelledByUserName = user?.Name;
        invoice.CancelledAt = DateTime.UtcNow;
        invoice.CancellationReason = request.Reason;
        invoice.InventoryAdjustedOnCancellation = request.AdjustInventory;
        invoice.UpdatedAt = DateTime.UtcNow;

        // Adjust inventory if requested and invoice was confirmed
        if (request.AdjustInventory && wasConfirmed)
        {
            foreach (var item in invoice.Items)
            {
                var product = await _unitOfWork.Products.Query()
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                if (product != null)
                {
                    // Update BranchInventory (not Product.StockQuantity)
                    var branchInventory = await _unitOfWork.BranchInventories.Query()
                        .FirstOrDefaultAsync(bi => bi.ProductId == product.Id 
                                                && bi.BranchId == invoice.BranchId 
                                                && bi.TenantId == tenantId);

                    if (branchInventory != null)
                    {
                        var balanceBefore = branchInventory.Quantity;
                        branchInventory.Quantity -= item.Quantity;
                        branchInventory.LastUpdatedAt = DateTime.UtcNow;
                        _unitOfWork.BranchInventories.Update(branchInventory);

                        // Create stock movement
                        var stockMovement = new StockMovement
                        {
                            TenantId = tenantId,
                            BranchId = invoice.BranchId,
                            ProductId = product.Id,
                            Type = StockMovementType.Adjustment,
                            Quantity = -item.Quantity,
                            ReferenceId = invoice.Id,
                            ReferenceType = "PurchaseInvoice",
                            BalanceBefore = balanceBefore,
                            BalanceAfter = branchInventory.Quantity,
                            Reason = $"إلغاء فاتورة شراء: {request.Reason}",
                            UserId = userId,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _unitOfWork.StockMovements.AddAsync(stockMovement);
                    }
                }
            }
        }

        _unitOfWork.PurchaseInvoices.Update(invoice);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(invoice.Id);
    }

    public async Task<ApiResponse<PurchaseInvoicePaymentDto>> AddPaymentAsync(int invoiceId, AddPaymentRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.UserId;

        var invoice = await _unitOfWork.PurchaseInvoices.Query()
            .FirstOrDefaultAsync(pi => pi.Id == invoiceId && pi.TenantId == tenantId && !pi.IsDeleted);

        if (invoice == null)
            return ApiResponse<PurchaseInvoicePaymentDto>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND));

        if (invoice.Status != PurchaseInvoiceStatus.Confirmed)
            return ApiResponse<PurchaseInvoicePaymentDto>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE));

        if (request.Amount <= 0)
            return ApiResponse<PurchaseInvoicePaymentDto>.Fail(ErrorCodes.PAYMENT_INVALID_AMOUNT, ErrorMessages.Get(ErrorCodes.PAYMENT_INVALID_AMOUNT));

        if (request.Amount > invoice.AmountDue)
            return ApiResponse<PurchaseInvoicePaymentDto>.Fail(ErrorCodes.PAYMENT_EXCEEDS_DUE, ErrorMessages.Get(ErrorCodes.PAYMENT_EXCEEDS_DUE));

        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == userId);

        var payment = new PurchaseInvoicePayment
        {
            PurchaseInvoiceId = invoiceId,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate,
            Method = request.Method,
            ReferenceNumber = request.ReferenceNumber,
            Notes = request.Notes,
            CreatedByUserId = userId,
            CreatedByUserName = user?.Name,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.PurchaseInvoicePayments.AddAsync(payment);

        // Update invoice amounts
        invoice.AmountPaid += request.Amount;
        invoice.AmountDue = invoice.Total - invoice.AmountPaid;
        invoice.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.PurchaseInvoices.Update(invoice);
        await _unitOfWork.SaveChangesAsync();

        var dto = new PurchaseInvoicePaymentDto
        {
            Id = payment.Id,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            Method = payment.Method.ToString(),
            ReferenceNumber = payment.ReferenceNumber,
            Notes = payment.Notes,
            CreatedByUserName = payment.CreatedByUserName ?? "",
            CreatedAt = payment.CreatedAt
        };

        return ApiResponse<PurchaseInvoicePaymentDto>.Ok(dto, "تم إضافة الدفعة بنجاح");
    }

    public async Task<ApiResponse<bool>> DeletePaymentAsync(int invoiceId, int paymentId)
    {
        var tenantId = _currentUser.TenantId;

        var invoice = await _unitOfWork.PurchaseInvoices.Query()
            .FirstOrDefaultAsync(pi => pi.Id == invoiceId && pi.TenantId == tenantId && !pi.IsDeleted);

        if (invoice == null)
            return ApiResponse<bool>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND));

        var payment = await _unitOfWork.PurchaseInvoicePayments.Query()
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.PurchaseInvoiceId == invoiceId);

        if (payment == null)
            return ApiResponse<bool>.Fail(ErrorCodes.PAYMENT_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PAYMENT_NOT_FOUND));

        // Update invoice amounts
        invoice.AmountPaid -= payment.Amount;
        invoice.AmountDue = invoice.Total - invoice.AmountPaid;
        invoice.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.PurchaseInvoices.Update(invoice);
        _unitOfWork.PurchaseInvoicePayments.Delete(payment);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "تم حذف الدفعة بنجاح");
    }

    private string GenerateInvoiceNumber(string? lastInvoiceNumber)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PI-{year}-";

        if (string.IsNullOrEmpty(lastInvoiceNumber) || !lastInvoiceNumber.StartsWith(prefix))
        {
            return $"{prefix}0001";
        }

        var lastNumber = int.Parse(lastInvoiceNumber.Substring(prefix.Length));
        var newNumber = lastNumber + 1;
        return $"{prefix}{newNumber:D4}";
    }
}

