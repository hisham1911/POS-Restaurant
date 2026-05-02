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
    private readonly ICashRegisterService _cashRegisterService;

    public PurchaseInvoiceService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, ICashRegisterService cashRegisterService)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _cashRegisterService = cashRegisterService;
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
        var branchId = _currentUser.BranchId;
        var query = _unitOfWork.PurchaseInvoices.Query()
            .Where(pi => pi.TenantId == tenantId && pi.BranchId == branchId && !pi.IsDeleted);

        if (supplierId.HasValue)
            query = query.Where(pi => pi.SupplierId == supplierId.Value);

        if (status.HasValue)
            query = query.Where(pi => pi.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(pi => pi.InvoiceDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(pi => pi.InvoiceDate <= toDate.Value);

        var totalCount = await query.CountAsync();
        // SQLite doesn't support Sum on decimal - cast to double first
        var totalAmount = Math.Round(
            (decimal)(await query.SumAsync(pi => (double?)pi.Total) ?? 0.0),
            2);
        var totalPaidAmount = Math.Round(
            (decimal)(await query.SumAsync(pi => (double?)pi.AmountPaid) ?? 0.0),
            2);
        var totalDueAmount = Math.Round(
            (decimal)(await query.SumAsync(pi => (double?)pi.AmountDue) ?? 0.0),
            2);

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

        var result = new PagedResult<PurchaseInvoiceDto>(
            invoices,
            totalCount,
            pageNumber,
            pageSize,
            totalAmount,
            totalPaidAmount,
            totalDueAmount);

        return ApiResponse<PagedResult<PurchaseInvoiceDto>>.Ok(result);
    }

    public async Task<ApiResponse<PurchaseInvoicePreviewDto>> PrepareAsync(CreatePurchaseInvoiceRequest request)
    {
        if (request.Items == null || !request.Items.Any())
        {
            return ApiResponse<PurchaseInvoicePreviewDto>.Fail(
                ErrorCodes.PURCHASE_INVOICE_EMPTY,
                ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_EMPTY));
        }

        var tenantId = _currentUser.TenantId;

        var supplierExists = await _unitOfWork.Suppliers.Query()
            .AnyAsync(s => s.Id == request.SupplierId && s.TenantId == tenantId && !s.IsDeleted);

        if (!supplierExists)
        {
            return ApiResponse<PurchaseInvoicePreviewDto>.Fail(
                ErrorCodes.SUPPLIER_NOT_FOUND,
                ErrorMessages.Get(ErrorCodes.SUPPLIER_NOT_FOUND));
        }

        decimal subtotal = 0m;

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                return ApiResponse<PurchaseInvoicePreviewDto>.Fail(
                    ErrorCodes.PURCHASE_INVOICE_INVALID_QUANTITY,
                    ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_INVALID_QUANTITY));
            }

            if (item.PurchasePrice < 0)
            {
                return ApiResponse<PurchaseInvoicePreviewDto>.Fail(
                    ErrorCodes.PURCHASE_INVOICE_INVALID_PRICE,
                    ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_INVALID_PRICE));
            }

            var product = await _unitOfWork.Products.Query()
                .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.TenantId == tenantId && !p.IsDeleted);

            if (product == null)
            {
                return ApiResponse<PurchaseInvoicePreviewDto>.Fail(
                    ErrorCodes.PRODUCT_NOT_FOUND,
                    ErrorMessages.Get(ErrorCodes.PRODUCT_NOT_FOUND));
            }

            if (product.Type == ProductType.Service)
            {
                return ApiResponse<PurchaseInvoicePreviewDto>.Fail(
                    ErrorCodes.PRODUCT_SERVICE_NOT_PURCHASABLE,
                    ErrorMessages.Get(ErrorCodes.PRODUCT_SERVICE_NOT_PURCHASABLE));
            }

            var batchRequested = IsBatchRequested(item);
            if ((product.IsBatchTracked || batchRequested) && item.SellingPrice <= 0)
            {
                return ApiResponse<PurchaseInvoicePreviewDto>.Fail(
                    ErrorCodes.VALIDATION_ERROR,
                    "سعر بيع الدفعة مطلوب");
            }

            subtotal += item.Quantity * item.PurchasePrice;
        }

        var tenant = await _unitOfWork.Tenants.Query()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        var taxRate = tenant?.TaxRate ?? 14m;
        subtotal = Math.Round(subtotal, 2);
        var taxAmount = Math.Round(subtotal * (taxRate / 100m), 2);
        var total = Math.Round(subtotal + taxAmount, 2);

        return ApiResponse<PurchaseInvoicePreviewDto>.Ok(new PurchaseInvoicePreviewDto
        {
            Subtotal = subtotal,
            TaxRate = taxRate,
            TaxAmount = taxAmount,
            Total = total
        });
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
                Notes = item.Notes,
                BatchNumber = item.BatchNumber,
                ExpiryDate = item.ExpiryDate,
                ProductionDate = item.ProductionDate
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

        // Generate invoice number per-tenant for the current year.
        var invoiceNumber = await GenerateInvoiceNumberAsync(tenantId);

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

            var batchRequested = IsBatchRequested(itemRequest);
            if ((product.IsBatchTracked || batchRequested) && itemRequest.SellingPrice <= 0)
                return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.VALIDATION_ERROR, "سعر بيع الدفعة مطلوب");

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
                Notes = itemRequest.Notes,
                BatchNumber = itemRequest.BatchNumber,
                ExpiryDate = itemRequest.ExpiryDate,
                ProductionDate = itemRequest.ProductionDate
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

            var batchRequested = IsBatchRequested(itemRequest);
            if ((product.IsBatchTracked || batchRequested) && itemRequest.SellingPrice <= 0)
                return ApiResponse<PurchaseInvoiceDto>.Fail(ErrorCodes.VALIDATION_ERROR, "سعر بيع الدفعة مطلوب");

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
                Notes = itemRequest.Notes,
                BatchNumber = itemRequest.BatchNumber,
                ExpiryDate = itemRequest.ExpiryDate,
                ProductionDate = itemRequest.ProductionDate
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

        if (invoice.Status != PurchaseInvoiceStatus.Draft)
        {
            return ApiResponse<bool>.Fail(
                ErrorCodes.PURCHASE_INVOICE_NOT_DELETABLE,
                "لا يمكن حذف إلا الفواتير في حالة مسودة");
        }

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

        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Update invoice status
            invoice.Status = PurchaseInvoiceStatus.Confirmed;
            invoice.ConfirmedByUserId = userId;
            invoice.ConfirmedByUserName = user?.Name;
            invoice.ConfirmedAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;

            var supplier = await GetSupplierForInvoiceAsync(invoice.SupplierId, invoice.BranchId);
            if (supplier != null)
            {
                supplier.TotalDue = Math.Round(supplier.TotalDue + invoice.AmountDue, 2);
                supplier.TotalPurchases = Math.Round(supplier.TotalPurchases + invoice.Total, 2);
                supplier.LastPurchaseDate = invoice.InvoiceDate;
            }

            // Update inventory for each item
            foreach (var item in invoice.Items)
            {
                var product = await _unitOfWork.Products.Query()
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId
                                           && p.TenantId == tenantId
                                           && !p.IsDeleted);

                if (product != null)
                {
                    var batchRequested = IsBatchRequested(item);
                    var shouldCreateBatch = product.IsBatchTracked || batchRequested;
                    if (shouldCreateBatch && item.SellingPrice <= 0)
                    {
                        return ApiResponse<PurchaseInvoiceDto>.Fail(
                            ErrorCodes.VALIDATION_ERROR,
                            "سعر بيع الدفعة مطلوب");
                    }

                    if (batchRequested && !product.IsBatchTracked)
                    {
                        product.IsBatchTracked = true;
                    }

                    // Update BranchInventory (the authoritative stock location)
                    var branchInventory = await _unitOfWork.BranchInventories.Query()
                        .FirstOrDefaultAsync(bi => bi.ProductId == product.Id
                                                && bi.BranchId == invoice.BranchId
                                                && bi.TenantId == tenantId);

                    int balanceBefore;

                    if (shouldCreateBatch)
                    {
                        await EnsureExistingStockHasOpeningBatchAsync(
                            product,
                            invoice.BranchId,
                            tenantId,
                            branchInventory?.Quantity ?? 0,
                            DateTime.UtcNow,
                            invoice.InvoiceDate.AddTicks(-1));
                    }

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

                    // Create ProductBatch when the product tracks batches or the invoice line explicitly provides batch data.
                    ProductBatch? batch = null;
                    if (shouldCreateBatch)
                    {
                        // Generate BatchNumber if not provided
                        var batchNumber = string.IsNullOrWhiteSpace(item.BatchNumber)
                            ? null
                            : item.BatchNumber.Trim();

                        if (!string.IsNullOrWhiteSpace(batchNumber))
                        {
                            var batchExists = await _unitOfWork.ProductBatches.Query()
                                .AnyAsync(b => b.BatchNumber == batchNumber
                                            && b.ProductId == item.ProductId
                                            && b.BranchId == invoice.BranchId
                                            && b.TenantId == tenantId
                                            && !b.IsDeleted
                                            && b.Status != BatchStatus.Depleted);
                            if (batchExists)
                            {
                                return ApiResponse<PurchaseInvoiceDto>.Fail(
                                    ErrorCodes.BATCH_NUMBER_DUPLICATE,
                                    ErrorMessages.Get(ErrorCodes.BATCH_NUMBER_DUPLICATE));
                            }
                        }

                        batch = new ProductBatch
                        {
                            TenantId = tenantId,
                            BranchId = invoice.BranchId,
                            ProductId = product.Id,
                            BatchNumber = batchNumber,
                            PurchaseDate = invoice.InvoiceDate,
                            ExpiryDate = item.ExpiryDate,
                            ProductionDate = item.ProductionDate,
                            Quantity = item.Quantity,
                            InitialQuantity = item.Quantity,
                            CostPrice = item.PurchasePrice,
                            // Save SellingPrice if provided and > 0
                            SellingPrice = item.SellingPrice > 0 ? item.SellingPrice : null,
                            SupplierName = invoice.SupplierName,
                            PurchaseInvoiceId = invoice.Id,
                            Status = BatchStatus.Active,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _unitOfWork.ProductBatches.AddAsync(batch);
                        stockMovement.Batch = batch;
                        item.Batch = batch;
                    }

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
                        product.AverageCost = Math.Round((totalOldValue + totalNewValue) / newStock, 4);
                    }

                    product.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.Products.Update(product);

                    // Update SupplierProduct link
                    var supplierProduct = await _unitOfWork.SupplierProducts.Query()
                        .FirstOrDefaultAsync(sp => sp.SupplierId == invoice.SupplierId
                                                 && sp.ProductId == item.ProductId
                                                 && sp.TenantId == tenantId);

                    if (supplierProduct == null)
                    {
                        supplierProduct = new SupplierProduct
                        {
                            TenantId = tenantId,
                            SupplierId = invoice.SupplierId,
                            ProductId = item.ProductId,
                            IsPreferred = false,
                            LastPurchasePrice = item.PurchasePrice,
                            LastPurchaseDate = invoice.InvoiceDate,
                            TotalQuantityPurchased = item.Quantity,
                            TotalAmountSpent = Math.Round(item.Quantity * item.PurchasePrice, 2),
                            CreatedAt = DateTime.UtcNow
                        };
                        await _unitOfWork.SupplierProducts.AddAsync(supplierProduct);
                    }
                    else
                    {
                        supplierProduct.LastPurchasePrice = item.PurchasePrice;
                        supplierProduct.LastPurchaseDate = invoice.InvoiceDate;
                        supplierProduct.TotalQuantityPurchased += item.Quantity;
                        supplierProduct.TotalAmountSpent = Math.Round(
                            supplierProduct.TotalAmountSpent + (item.Quantity * item.PurchasePrice), 2);
                        supplierProduct.UpdatedAt = DateTime.UtcNow;
                        _unitOfWork.SupplierProducts.Update(supplierProduct);
                    }
                }
            }

            _unitOfWork.PurchaseInvoices.Update(invoice);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

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

        if (invoice.Status == PurchaseInvoiceStatus.Paid)
        {
            return ApiResponse<PurchaseInvoiceDto>.Fail(
                ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE,
                "لا يمكن إلغاء فاتورة مدفوعة بالكامل، يجب إنشاء مسترد أولاً");
        }

        if (invoice.Status != PurchaseInvoiceStatus.Confirmed
            && invoice.Status != PurchaseInvoiceStatus.PartiallyPaid)
        {
            return ApiResponse<PurchaseInvoiceDto>.Fail(
                ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE,
                ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE));
        }

        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == userId);

        var wasConfirmed = invoice.Status == PurchaseInvoiceStatus.Confirmed
            || invoice.Status == PurchaseInvoiceStatus.PartiallyPaid;

        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Update invoice status
            invoice.Status = PurchaseInvoiceStatus.Cancelled;
            invoice.CancelledByUserId = userId;
            invoice.CancelledByUserName = user?.Name;
            invoice.CancelledAt = DateTime.UtcNow;
            invoice.CancellationReason = request.Reason;
            invoice.InventoryAdjustedOnCancellation = request.AdjustInventory;
            invoice.UpdatedAt = DateTime.UtcNow;

            if (wasConfirmed)
            {
                var supplier = await GetSupplierForInvoiceAsync(invoice.SupplierId, invoice.BranchId);
                if (supplier != null)
                {
                    supplier.TotalDue = Math.Round(Math.Max(0m, supplier.TotalDue - invoice.AmountDue), 2);
                    supplier.TotalPurchases = Math.Round(Math.Max(0m, supplier.TotalPurchases - invoice.Total), 2);
                }
            }

            // Adjust inventory if requested and invoice was confirmed
            if (request.AdjustInventory && wasConfirmed)
            {
                foreach (var item in invoice.Items)
                {
                    var product = await _unitOfWork.Products.Query()
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                    if (product != null)
                    {
                        // Update branch-level inventory
                        var branchInventory = await _unitOfWork.BranchInventories.Query()
                            .FirstOrDefaultAsync(bi => bi.ProductId == product.Id
                                                    && bi.BranchId == invoice.BranchId
                                                    && bi.TenantId == tenantId);

                        if (branchInventory != null)
                        {
                            var balanceBefore = branchInventory.Quantity;
                            branchInventory.Quantity -= item.Quantity;
                            if (branchInventory.Quantity < 0) branchInventory.Quantity = 0;
                            branchInventory.LastUpdatedAt = DateTime.UtcNow;
                            _unitOfWork.BranchInventories.Update(branchInventory);

                            // Find and adjust associated product batches for this invoice item
                            var batch = await _unitOfWork.ProductBatches.Query()
                                .FirstOrDefaultAsync(pb => pb.PurchaseInvoiceId == invoice.Id
                                    && pb.ProductId == product.Id
                                    && pb.TenantId == tenantId
                                    && pb.BranchId == invoice.BranchId
                                    && !pb.IsDeleted);
                            if (batch != null)
                            {
                                batch.Quantity -= item.Quantity;
                                if (batch.Quantity <= 0)
                                {
                                    batch.Quantity = 0;
                                    batch.Status = BatchStatus.Depleted;
                                }
                                _unitOfWork.ProductBatches.Update(batch);
                            }

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
                                BatchId = batch?.Id,
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
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

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

        if (invoice.Status != PurchaseInvoiceStatus.Confirmed
            && invoice.Status != PurchaseInvoiceStatus.PartiallyPaid)
            return ApiResponse<PurchaseInvoicePaymentDto>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE));

        if (request.Amount <= 0)
            return ApiResponse<PurchaseInvoicePaymentDto>.Fail(ErrorCodes.PAYMENT_INVALID_AMOUNT, ErrorMessages.Get(ErrorCodes.PAYMENT_INVALID_AMOUNT));

        if (request.Amount > invoice.AmountDue)
            return ApiResponse<PurchaseInvoicePaymentDto>.Fail(ErrorCodes.PAYMENT_EXCEEDS_DUE, ErrorMessages.Get(ErrorCodes.PAYMENT_EXCEEDS_DUE));

        var referenceValidation = ValidateReferenceForNonCashPayment(
            request.Method,
            request.ReferenceNumber);
        if (!referenceValidation.Success)
            return ApiResponse<PurchaseInvoicePaymentDto>.Fail(
                ErrorCodes.PAYMENT_REFERENCE_REQUIRED,
                referenceValidation.Message!);

        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == userId);

        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
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
            invoice.AmountPaid = Math.Round(invoice.AmountPaid + request.Amount, 2);
            invoice.AmountDue = Math.Round(invoice.Total - invoice.AmountPaid, 2);
            RecalculateInvoiceStatus(invoice);
            invoice.UpdatedAt = DateTime.UtcNow;

            var supplier = await GetSupplierForInvoiceAsync(invoice.SupplierId, invoice.BranchId);
            if (supplier != null)
            {
                supplier.TotalDue = Math.Round(Math.Max(0m, supplier.TotalDue - request.Amount), 2);
                supplier.TotalPaid = Math.Round(supplier.TotalPaid + request.Amount, 2);
            }

            _unitOfWork.PurchaseInvoices.Update(invoice);
            await _unitOfWork.SaveChangesAsync();

            // Record cash register transaction for cash payments only
            if (request.Method == PaymentMethod.Cash)
            {
                await _cashRegisterService.RecordTransactionAsync(
                    CashRegisterTransactionType.SupplierPayment,
                    request.Amount,
                    $"دفع فاتورة مورد: {invoice.SupplierName}",
                    referenceType: "PurchaseInvoicePayment",
                    referenceId: payment.Id,
                    shiftId: null,
                    branchId: invoice.BranchId);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

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
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse<bool>> DeletePaymentAsync(int invoiceId, int paymentId)
    {
        var tenantId = _currentUser.TenantId;

        var invoice = await _unitOfWork.PurchaseInvoices.Query()
            .FirstOrDefaultAsync(pi => pi.Id == invoiceId && pi.TenantId == tenantId && !pi.IsDeleted);

        if (invoice == null)
            return ApiResponse<bool>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_FOUND));

        if (invoice.Status == PurchaseInvoiceStatus.Cancelled)
        {
            return ApiResponse<bool>.Fail(
                ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE,
                "لا يمكن حذف دفعة من فاتورة ملغاة");
        }

        if (invoice.Status == PurchaseInvoiceStatus.Returned
            || invoice.Status == PurchaseInvoiceStatus.PartiallyReturned
            || invoice.Status == PurchaseInvoiceStatus.Draft)
        {
            return ApiResponse<bool>.Fail(ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE, ErrorMessages.Get(ErrorCodes.PURCHASE_INVOICE_NOT_EDITABLE));
        }

        var payment = await _unitOfWork.PurchaseInvoicePayments.Query()
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.PurchaseInvoiceId == invoiceId);

        if (payment == null)
            return ApiResponse<bool>.Fail(ErrorCodes.PAYMENT_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PAYMENT_NOT_FOUND));

        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Update invoice amounts
            invoice.AmountPaid = Math.Round(Math.Max(0m, invoice.AmountPaid - payment.Amount), 2);
            invoice.AmountDue = Math.Round(invoice.Total - invoice.AmountPaid, 2);
            RecalculateInvoiceStatus(invoice);
            invoice.UpdatedAt = DateTime.UtcNow;

            var supplier = await GetSupplierForInvoiceAsync(invoice.SupplierId, invoice.BranchId);
            if (supplier != null)
            {
                supplier.TotalDue = Math.Round(supplier.TotalDue + payment.Amount, 2);
                supplier.TotalPaid = Math.Round(Math.Max(0m, supplier.TotalPaid - payment.Amount), 2);
            }

            _unitOfWork.PurchaseInvoices.Update(invoice);
            _unitOfWork.PurchaseInvoicePayments.Delete(payment);
            await _unitOfWork.SaveChangesAsync();

            // Reverse CashRegister for cash payments
            if (payment.Method == PaymentMethod.Cash)
            {
                await _cashRegisterService.RecordTransactionAsync(
                    CashRegisterTransactionType.SupplierPaymentReversal,
                    payment.Amount,
                    $"عكس دفعة مورد محذوفة - فاتورة {invoice.InvoiceNumber}",
                    referenceType: "PurchaseInvoicePayment",
                    referenceId: payment.Id,
                    shiftId: null,
                    branchId: invoice.BranchId);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<bool>.Ok(true, "تم حذف الدفعة بنجاح");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private static bool IsBatchRequested(CreatePurchaseInvoiceItemRequest item)
    {
        return !string.IsNullOrWhiteSpace(item.BatchNumber)
            || item.ExpiryDate.HasValue
            || item.ProductionDate.HasValue;
    }

    private static bool IsBatchRequested(UpdatePurchaseInvoiceItemRequest item)
    {
        return !string.IsNullOrWhiteSpace(item.BatchNumber)
            || item.ExpiryDate.HasValue
            || item.ProductionDate.HasValue;
    }

    private static bool IsBatchRequested(PurchaseInvoiceItem item)
    {
        return !string.IsNullOrWhiteSpace(item.BatchNumber)
            || item.ExpiryDate.HasValue
            || item.ProductionDate.HasValue;
    }

    private async Task EnsureExistingStockHasOpeningBatchAsync(
        Product product,
        int branchId,
        int tenantId,
        int existingBranchStock,
        DateTime now,
        DateTime purchaseDate)
    {
        if (existingBranchStock <= 0)
        {
            return;
        }

        var activeBatchQuantity = await _unitOfWork.ProductBatches.Query()
            .Where(pb => pb.TenantId == tenantId
                      && pb.BranchId == branchId
                      && pb.ProductId == product.Id
                      && !pb.IsDeleted
                      && pb.Status == BatchStatus.Active)
            .SumAsync(pb => (int?)pb.Quantity) ?? 0;

        var missingBatchQuantity = existingBranchStock - activeBatchQuantity;
        if (missingBatchQuantity <= 0)
        {
            return;
        }

        var branchSellingPrice = await _unitOfWork.BranchProductPrices.Query()
            .Where(bp => bp.TenantId == tenantId
                      && bp.ProductId == product.Id
                      && bp.BranchId == branchId
                      && bp.IsActive
                      && !bp.IsDeleted
                      && bp.EffectiveFrom <= now
                      && (bp.EffectiveTo == null || bp.EffectiveTo >= now))
            .OrderByDescending(bp => bp.EffectiveFrom)
            .Select(bp => (decimal?)bp.Price)
            .FirstOrDefaultAsync();

        var openingBatch = new ProductBatch
        {
            TenantId = tenantId,
            BranchId = branchId,
            ProductId = product.Id,
            BatchNumber = $"رصيد افتتاحي - {purchaseDate:yyyyMMddHHmmss}-{now:HHmmssfff}",
            Quantity = missingBatchQuantity,
            InitialQuantity = missingBatchQuantity,
            CostPrice = product.AverageCost ?? product.Cost,
            SellingPrice = branchSellingPrice ?? product.Price,
            PurchaseDate = purchaseDate,
            Status = BatchStatus.Active,
            Notes = "Opening batch generated from existing branch inventory before purchase invoice batch receiving.",
            CreatedAt = now
        };

        await _unitOfWork.ProductBatches.AddAsync(openingBatch);
    }

    private static void RecalculateInvoiceStatus(PurchaseInvoice invoice)
    {
        if (invoice.AmountDue <= 0)
        {
            invoice.Status = PurchaseInvoiceStatus.Paid;
        }
        else if (invoice.AmountPaid > 0)
        {
            invoice.Status = PurchaseInvoiceStatus.PartiallyPaid;
        }
        else
        {
            invoice.Status = PurchaseInvoiceStatus.Confirmed;
        }
    }

    private static (bool Success, string? Message) ValidateReferenceForNonCashPayment(
        PaymentMethod method,
        string? reference)
    {
        if (method == PaymentMethod.Cash)
        {
            return (true, null);
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            return (false, "رقم المعاملة مطلوب لطرق الدفع غير النقدية");
        }

        return (true, null);
    }

    private Task<Supplier?> GetSupplierForInvoiceAsync(int supplierId, int branchId)
    {
        var tenantId = _currentUser.TenantId;

        return _unitOfWork.Suppliers.Query()
            .FirstOrDefaultAsync(s => s.Id == supplierId
                                   && s.TenantId == tenantId
                                   && s.BranchId == branchId);
    }

    private async Task<string> GenerateInvoiceNumberAsync(int tenantId)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PI-{year}-";

        var existingNumbers = await _unitOfWork.PurchaseInvoices.Query()
            // InvoiceNumber has a unique index that includes soft-deleted rows.
            // Ignore global filters here to avoid reusing numbers from deleted drafts.
            .IgnoreQueryFilters()
            .Where(pi => pi.TenantId == tenantId && pi.InvoiceNumber.StartsWith(prefix))
            .Select(pi => pi.InvoiceNumber)
            .ToListAsync();

        if (existingNumbers.Count == 0)
        {
            return $"{prefix}0001";
        }

        var maxSequence = 0;
        foreach (var invoiceNumber in existingNumbers)
        {
            if (invoiceNumber.Length <= prefix.Length)
            {
                continue;
            }

            var suffix = invoiceNumber.Substring(prefix.Length);
            if (int.TryParse(suffix, out var sequence) && sequence > maxSequence)
            {
                maxSequence = sequence;
            }
        }

        return $"{prefix}{maxSequence + 1:D4}";
    }
}
