namespace KasserPro.Application.Services.Implementations;

using Microsoft.EntityFrameworkCore;
using KasserPro.Application.Common;
using KasserPro.Application.Common.Interfaces;
using KasserPro.Application.DTOs;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.DTOs.Orders;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Entities;
using KasserPro.Domain.Enums;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IInventoryService _inventoryService;
    private readonly ICustomerService _customerService;
    private readonly ICashRegisterService _cashRegisterService;
    private readonly IWalletService _walletService;
    private readonly IPermissionService _permissionService;
    private readonly IRecipeService _recipeService;
    private sealed record RefundPaymentAllocation(PaymentMethod Method, int? WalletId, decimal Amount, string? Reference);

    // Valid state transitions
    private static readonly Dictionary<OrderStatus, OrderStatus[]> ValidTransitions = new()
    {
        { OrderStatus.Draft, new[] { OrderStatus.Pending, OrderStatus.Preparing, OrderStatus.Completed, OrderStatus.Cancelled } },
        { OrderStatus.Pending, new[] { OrderStatus.Preparing, OrderStatus.Completed, OrderStatus.Cancelled } },
        { OrderStatus.Preparing, new[] { OrderStatus.Prepared, OrderStatus.Completed, OrderStatus.Cancelled } },
        { OrderStatus.Prepared, new[] { OrderStatus.Delivered, OrderStatus.Completed, OrderStatus.Cancelled } },
        { OrderStatus.Delivered, new[] { OrderStatus.Completed, OrderStatus.Cancelled } },
        { OrderStatus.Completed, new[] { OrderStatus.Refunded } },
        { OrderStatus.PartiallyRefunded, new[] { OrderStatus.Refunded } },
        { OrderStatus.Cancelled, Array.Empty<OrderStatus>() },
        { OrderStatus.Refunded, Array.Empty<OrderStatus>() }
    };

    private static readonly OrderStatus[] OpenTableStatuses =
    [
        OrderStatus.Draft,
        OrderStatus.Pending,
        OrderStatus.Preparing,
        OrderStatus.Prepared,
        OrderStatus.Delivered
    ];

    private static readonly OrderStatus[] EditableOrderStatuses =
    [
        OrderStatus.Draft,
        OrderStatus.Pending,
        OrderStatus.Preparing,
        OrderStatus.Prepared
    ];

    private static bool CanSellProduct(Product product)
        => product.Type != ProductType.RawMaterial;

    private static bool RequiresDirectStockTracking(Product product)
        => product.Type != ProductType.Manufactured && product.TrackInventory;

    private static bool CanModifyOpenOrder(OrderStatus status)
        => EditableOrderStatuses.Contains(status);

    private async Task<decimal?> ResolveUnitCostAsync(Product product, decimal? batchCost, int tenantId)
    {
        var recipeResponse = await _recipeService.GetByProductIdAsync(product.Id, tenantId);
        var recipe = recipeResponse.Data;
        if (recipe != null && recipe.YieldQuantity > 0)
            return Math.Round(recipe.TotalCost / recipe.YieldQuantity, 4);

        if (batchCost.HasValue)
            return batchCost.Value;

        return product.AverageCost ?? product.Cost;
    }

    public OrderService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, IInventoryService inventoryService, ICustomerService customerService, ICashRegisterService cashRegisterService, IWalletService walletService, IPermissionService permissionService, IRecipeService recipeService)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _inventoryService = inventoryService;
        _customerService = customerService;
        _cashRegisterService = cashRegisterService;
        _walletService = walletService;
        _permissionService = permissionService;
        _recipeService = recipeService;
    }

    public async Task<ApiResponse<OrderDto>> CreateAsync(CreateOrderRequest request, int userId)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        if (request.DeliveryFee < 0)
            return ApiResponse<OrderDto>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                "Ø±Ø³ÙˆÙ… Ø§Ù„ØªÙˆØµÙŠÙ„ Ù„Ø§ ÙŠÙ…ÙƒÙ† Ø£Ù† ØªÙƒÙˆÙ† Ø£Ù‚Ù„ Ù…Ù† ØµÙØ±");

        // VALIDATION: Order must have at least one item
        if (request.Items == null || request.Items.Count == 0)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_EMPTY, "Ù„Ø§ ÙŠÙ…ÙƒÙ† Ø¥Ù†Ø´Ø§Ø¡ Ø·Ù„Ø¨ ÙØ§Ø±Øº");

        // Get Tenant for dynamic tax settings
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
        if (tenant == null)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.TENANT_NOT_FOUND, "Ø§Ù„Ø´Ø±ÙƒØ© ØºÙŠØ± Ù…ÙˆØ¬ÙˆØ¯Ø©");

        // Get Branch for snapshot
        var branch = await _unitOfWork.Branches.GetByIdAsync(branchId);
        if (branch == null)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.BRANCH_NOT_FOUND, ErrorMessages.Get(ErrorCodes.BRANCH_NOT_FOUND));

        // Get User for snapshot
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.USER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

        if (RequestContainsAnyDiscount(request))
        {
            var canApplyDiscount = await _permissionService.HasPermissionAsync(userId, Permission.PosApplyDiscount);
            if (!canApplyDiscount)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_PRIVILEGES, ErrorMessages.Get(ErrorCodes.INSUFFICIENT_PRIVILEGES));
        }

        // SHIFT LINKING (optional): attach an open shift if available for this branch and user.
        var currentShift = await _unitOfWork.Shifts.Query()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId
                                   && s.BranchId == branchId
                                   && s.UserId == userId
                                   && !s.IsClosed);

        // Dynamic Tax Rate from Tenant Settings
        var tenantTaxRate = tenant.IsTaxEnabled ? tenant.TaxRate : 0m;

        // CUSTOMER LOOKUP: If CustomerId is provided, fetch customer details for snapshot
        string? customerName = request.CustomerName;
        string? customerPhone = request.CustomerPhone;
        string? deliveryAddress = request.DeliveryAddress;
        RestaurantTable? table = null;

        if (request.CustomerId.HasValue)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(request.CustomerId.Value);
            if (customer != null && customer.TenantId == tenantId)
            {
                // Use customer data as fallback if not provided in request
                customerName ??= customer.Name;
                customerPhone ??= customer.Phone;
                if (request.OrderType == OrderType.Delivery)
                    deliveryAddress = string.IsNullOrWhiteSpace(deliveryAddress) ? customer.Address : deliveryAddress;
            }
        }

        if (request.OrderType == OrderType.DineIn)
        {
            if (!request.TableId.HasValue)
                return ApiResponse<OrderDto>.Fail(
                    ErrorCodes.TABLE_REQUIRED_FOR_DINEIN,
                    ErrorMessages.Get(ErrorCodes.TABLE_REQUIRED_FOR_DINEIN));

            table = await _unitOfWork.RestaurantTables.Query()
                .FirstOrDefaultAsync(t => t.Id == request.TableId.Value
                                       && t.TenantId == tenantId
                                       && t.BranchId == branchId
                                       && t.IsActive);

            if (table == null)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.TABLE_NOT_FOUND, ErrorMessages.Get(ErrorCodes.TABLE_NOT_FOUND));

            var tableHasOpenOrder = await _unitOfWork.Orders.Query()
                .AnyAsync(o => o.TenantId == tenantId
                            && o.BranchId == branchId
                            && o.TableId == table.Id
                            && o.OrderType == OrderType.DineIn
                            && OpenTableStatuses.Contains(o.Status));

            if (table.Status != RestaurantTableStatus.Available || tableHasOpenOrder)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.TABLE_NOT_AVAILABLE, ErrorMessages.Get(ErrorCodes.TABLE_NOT_AVAILABLE));
        }
        else if (request.TableId.HasValue)
        {
            return ApiResponse<OrderDto>.Fail(ErrorCodes.TABLE_NOT_ALLOWED, ErrorMessages.Get(ErrorCodes.TABLE_NOT_ALLOWED));
        }

        if (request.OrderType == OrderType.Delivery && string.IsNullOrWhiteSpace(deliveryAddress))
            return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, "Ø¹Ù†ÙˆØ§Ù† Ø§Ù„ØªÙˆØµÙŠÙ„ Ù…Ø·Ù„ÙˆØ¨ Ù„Ø·Ù„Ø¨Ø§Øª Ø§Ù„Ø¯Ù„ÙŠÙØ±ÙŠ");

        var externalOrderNumber = request.OrderSource == OrderSource.POS
            ? null
            : request.ExternalOrderNumber?.Trim();

        if (request.OrderSource != OrderSource.POS && !string.IsNullOrWhiteSpace(externalOrderNumber))
        {
            var externalOrderExists = await _unitOfWork.Orders.Query()
                .AnyAsync(o => o.TenantId == tenantId
                            && o.BranchId == branchId
                            && o.OrderSource == request.OrderSource
                            && o.ExternalOrderNumber == externalOrderNumber
                            && o.Status != OrderStatus.Cancelled);

            if (externalOrderExists)
                return ApiResponse<OrderDto>.Fail(
                    ErrorCodes.EXTERNAL_ORDER_NUMBER_EXISTS,
                    ErrorMessages.Get(ErrorCodes.EXTERNAL_ORDER_NUMBER_EXISTS));
        }

        var order = new Order
        {
            TenantId = tenantId,
            BranchId = branchId,
            // SHIFT LINKING: Link order to current shift if found.
            ShiftId = currentShift?.Id,
            OrderNumber = GenerateOrderNumber(),
            UserId = userId,
            // CUSTOMER LINKING: Link order to customer if provided
            CustomerId = request.CustomerId,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            Notes = request.Notes,
            Status = OrderStatus.Pending,
            OrderType = request.OrderType,
            TableId = table?.Id,
            TableNumberSnapshot = table?.Number,
            OrderSource = request.OrderSource,
            ExternalOrderNumber = externalOrderNumber,
            // Delivery fields
            DeliveryAddress = deliveryAddress,
            DeliveryFee = request.DeliveryFee,
            DeliveryNotes = request.DeliveryNotes,
            DeliveryStatus = request.OrderType == OrderType.Delivery ? DeliveryStatus.PendingAssignment : null,
            // Branch Snapshot (on Order entity)
            BranchName = branch.Name,
            BranchAddress = branch.Address,
            BranchPhone = branch.Phone,
            // User Snapshot (on Order entity)
            UserName = user.Name,
            // Currency from Branch
            CurrencyCode = branch.CurrencyCode,
            // Tax Rate from Tenant (dynamic)
            TaxRate = tenantTaxRate,
            ServiceChargePercent = tenant.ServiceChargeRate,
            // Order-level discount
            DiscountType = NormalizeDiscountType(request.DiscountType),
            DiscountValue = request.DiscountValue
        };

        foreach (var item in request.Items)
        {
            // VALIDATION: Quantity must be positive
            if (item.Quantity <= 0)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_INVALID_QUANTITY, ErrorMessages.Get(ErrorCodes.ORDER_INVALID_QUANTITY));

            var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);

            // VALIDATION: Product must exist AND be active
            if (product == null)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, $"Ø§Ù„Ù…Ù†ØªØ¬ ØºÙŠØ± Ù…ÙˆØ¬ÙˆØ¯: {item.ProductId}");

            if (!product.IsActive)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.PRODUCT_INACTIVE, $"Ø§Ù„Ù…Ù†ØªØ¬ ØºÙŠØ± Ù…ØªØ§Ø­ Ù„Ù„Ø¨ÙŠØ¹: {product.Name}");

            if (!CanSellProduct(product))
                return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, $"Ù„Ø§ ÙŠÙ…ÙƒÙ† Ø¨ÙŠØ¹ Ø§Ù„Ù…Ø§Ø¯Ø© Ø§Ù„Ø®Ø§Ù… Ù…Ø¨Ø§Ø´Ø±Ø©: {product.Name}");

            // STOCK VALIDATION: Check if sufficient stock is available (only for products with direct stock)
            if (RequiresDirectStockTracking(product))
            {
                // P0-3: Read from BranchInventory (same table that gets decremented).
                // This is a soft check (UX hint). The hard check is inside CompleteAsync.
                var hasBranchInventory = await _unitOfWork.BranchInventories.Query()
                    .AnyAsync(bi => bi.TenantId == tenantId
                                 && bi.BranchId == branchId
                                 && bi.ProductId == product.Id);

                if (hasBranchInventory)
                {
                    var currentStock = await _inventoryService.GetAvailableQuantityAsync(product.Id, branchId);
                    if (currentStock < item.Quantity && !tenant.AllowNegativeStock)
                    {
                        return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_STOCK,
                            $"Ø§Ù„Ù…Ø®Ø²ÙˆÙ† ØºÙŠØ± ÙƒØ§ÙÙ Ù„Ù„Ù…Ù†ØªØ¬: {product.Name}. Ø§Ù„Ù…ØªØ§Ø­: {currentStock}ØŒ Ø§Ù„Ù…Ø·Ù„ÙˆØ¨: {item.Quantity}");
                    }
                }
            }

            // Dynamic Tax Rate Priority: Product â†’ Tenant
            // If product has specific tax rate, use it; otherwise use tenant's rate
            var taxRate = product.TaxRate ?? tenantTaxRate;

            // If tax is disabled at tenant level, override to 0
            if (!tenant.IsTaxEnabled)
                taxRate = 0m;

            if (taxRate < 0 || taxRate > 100)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

            // Price Priority: 1) ProductBatch.SellingPrice, 2) BranchProductPrice.Price, 3) Product.Price
            var (sellingPrice, batchId, batchNumber, expiryDate, batchCost, priceError) = await ResolveBatchSaleSnapshotAsync(
                product.Id, branchId, tenantId, product.Price, item.BatchId);
            if (priceError != null)
                return ApiResponse<OrderDto>.Fail(priceError.Value.ErrorCode, priceError.Value.Message);

            var unitPrice = ResolveNetUnitPrice(sellingPrice);

            // Validate batch quantity if batch is selected
            if (batchId.HasValue)
            {
                var batch = await _unitOfWork.ProductBatches.GetByIdAsync(batchId.Value);
                if (batch == null || batch.Quantity < item.Quantity)
                {
                    return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_STOCK,
                        $"Ø§Ù„ÙƒÙ…ÙŠØ© Ø§Ù„Ù…ØªØ§Ø­Ø© ÙÙŠ Ø§Ù„Ø¨Ø§ØªØ´ {batchNumber} ØºÙŠØ± ÙƒØ§ÙÙŠØ©. Ø§Ù„Ù…ØªØ§Ø­: {batch?.Quantity ?? 0}ØŒ Ø§Ù„Ù…Ø·Ù„ÙˆØ¨: {item.Quantity}");
                }
            }

            var unitCost = await ResolveUnitCostAsync(product, batchCost, tenantId);

            var orderItem = new OrderItem
            {
                ProductId = product.Id,
                // Product Snapshot (on OrderItem entity)
                ProductName = product.Name,
                ProductNameEn = product.NameEn,
                ProductSku = product.Sku,
                ProductBarcode = product.Barcode,
                // Price Snapshot - UnitPrice is NET (excluding tax)
                UnitPrice = unitPrice,
                UnitCost = unitCost,
                OriginalPrice = sellingPrice, // Use resolved selling price
                Quantity = item.Quantity,
                DiscountType = NormalizeDiscountType(item.DiscountType),
                DiscountValue = item.DiscountValue,
                DiscountReason = item.DiscountReason,
                // Tax Snapshot - Dynamic from Product or Tenant
                TaxRate = taxRate,
                TaxInclusive = false,
                Notes = item.Notes,
                // Batch Info
                BatchId = batchId,
                BatchNumber = batchNumber,
                ExpiryDate = expiryDate
            };

            var itemDiscountValidation = ValidateDiscount(orderItem.DiscountType, orderItem.DiscountValue, unitPrice * item.Quantity);
            if (!itemDiscountValidation.Success)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, itemDiscountValidation.Message!);

            // Calculate tax amount and totals with proper rounding
            CalculateItemTotals(orderItem);
            order.Items.Add(orderItem);
        }

        var orderDiscountValidation = ValidateDiscount(
            order.DiscountType,
            order.DiscountValue,
            order.Items.Sum(i => i.Subtotal - i.DiscountAmount));
        if (!orderDiscountValidation.Success)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, orderDiscountValidation.Message!);

        CalculateOrderTotals(order);

        var recipeStockValidation = await ValidateRecipeIngredientStockAsync(
            order.Items.Where(i => i.ProductId.HasValue && !i.IsCustomItem)
                .Select(i => (ProductId: i.ProductId!.Value, i.Quantity, i.ProductName)),
            tenantId,
            branchId,
            tenant.AllowNegativeStock);
        if (!recipeStockValidation.Success)
            return ApiResponse<OrderDto>.Fail(recipeStockValidation.ErrorCode!, recipeStockValidation.Message!);

        if (table != null)
            table.Status = RestaurantTableStatus.Occupied;

        await _unitOfWork.Orders.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(MapToDto(order), "ØªÙ… Ø¥Ù†Ø´Ø§Ø¡ Ø§Ù„Ø·Ù„Ø¨ Ø¨Ù†Ø¬Ø§Ø­");
    }

    public async Task<ApiResponse<OrderDto>> GetByIdAsync(int id)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.DeliveryPerson)
            .FirstOrDefaultAsync(o => o.Id == id
                && o.TenantId == _currentUser.TenantId
                && o.BranchId == _currentUser.BranchId);

        if (order == null)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));

        return ApiResponse<OrderDto>.Ok(MapToDto(order));
    }

    public async Task<ApiResponse<List<OrderDto>>> GetTodayOrdersAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        var orders = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.DeliveryPerson)
            .Where(o => o.TenantId == tenantId && o.BranchId == branchId && o.CreatedAt.Date == today)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return ApiResponse<List<OrderDto>>.Ok(orders.Select(MapToDto).ToList());
    }

    public async Task<ApiResponse<PagedResult<OrderDto>>> GetAllAsync(string? status = null, string? orderType = null, DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 20)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        var query = _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.DeliveryPerson)
            .Where(o => o.TenantId == tenantId && o.BranchId == branchId);

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            {
                query = query.Where(o => o.Status == orderStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(orderType)
            && Enum.TryParse<OrderType>(orderType, true, out var parsedOrderType))
        {
            query = query.Where(o => o.OrderType == parsedOrderType);
        }

        // Apply date range filter
        if (fromDate.HasValue)
        {
            var fromDateUtc = fromDate.Value.Date;
            query = query.Where(o => o.CreatedAt >= fromDateUtc);
        }

        if (toDate.HasValue)
        {
            var toDateUtc = toDate.Value.Date.AddDays(1).AddTicks(-1); // End of day
            query = query.Where(o => o.CreatedAt <= toDateUtc);
        }

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply pagination
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResult<OrderDto>(
            orders.Select(MapToDto).ToList(),
            totalCount,
            page,
            pageSize
        );

        return ApiResponse<PagedResult<OrderDto>>.Ok(result);
    }

    public async Task<ApiResponse<PagedResult<OrderDto>>> GetByCustomerIdAsync(int customerId, int page = 1, int pageSize = 10)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;

        var query = _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.DeliveryPerson)
            .Where(o => o.TenantId == tenantId
                && o.BranchId == branchId
                && o.CustomerId == customerId);

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResult<OrderDto>(
            orders.Select(MapToDto).ToList(),
            totalCount,
            page,
            pageSize
        );

        return ApiResponse<PagedResult<OrderDto>>.Ok(result);
    }

    public async Task<ApiResponse<OrderDto>> AddItemAsync(int orderId, AddOrderItemRequest request)
    {
        // VALIDATION: Quantity must be positive
        if (request.Quantity <= 0)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_INVALID_QUANTITY, ErrorMessages.Get(ErrorCodes.ORDER_INVALID_QUANTITY));

        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId
                && o.TenantId == _currentUser.TenantId
                && o.BranchId == _currentUser.BranchId);

        if (order == null)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));

        if (!CanModifyOpenOrder(order.Status))
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_CANNOT_MODIFY, ErrorMessages.Get(ErrorCodes.ORDER_CANNOT_MODIFY));

        if (RequestContainsDiscount(request.DiscountType, request.DiscountValue))
        {
            var canApplyDiscount = await _permissionService.HasPermissionAsync(_currentUser.UserId, Permission.PosApplyDiscount);
            if (!canApplyDiscount)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_PRIVILEGES, ErrorMessages.Get(ErrorCodes.INSUFFICIENT_PRIVILEGES));
        }

        var product = await _unitOfWork.Products.GetByIdAsync(request.ProductId);
        if (product == null)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PRODUCT_NOT_FOUND));

        // VALIDATION: Product must be active
        if (!product.IsActive)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.PRODUCT_INACTIVE, $"Ø§Ù„Ù…Ù†ØªØ¬ ØºÙŠØ± Ù…ØªØ§Ø­ Ù„Ù„Ø¨ÙŠØ¹: {product.Name}");

        if (!CanSellProduct(product))
            return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, $"Ù„Ø§ ÙŠÙ…ÙƒÙ† Ø¨ÙŠØ¹ Ø§Ù„Ù…Ø§Ø¯Ø© Ø§Ù„Ø®Ø§Ù… Ù…Ø¨Ø§Ø´Ø±Ø©: {product.Name}");

        // Get Tenant for dynamic tax settings
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(order.TenantId);
        var tenantTaxRate = tenant?.IsTaxEnabled == true ? tenant.TaxRate : 0m;

        // STOCK VALIDATION: Check if sufficient stock is available
        if (RequiresDirectStockTracking(product))
        {
            // Get current stock from BranchInventory
            var branchInventory = await _unitOfWork.BranchInventories.Query()
                .FirstOrDefaultAsync(bi => bi.TenantId == order.TenantId
                    && bi.BranchId == order.BranchId
                    && bi.ProductId == product.Id);

            var currentStock = branchInventory?.Quantity ?? 0;
            // Calculate total quantity in order for this product
            var existingQty = order.Items.Where(i => i.ProductId == product.Id).Sum(i => i.Quantity);
            var totalRequested = existingQty + request.Quantity;

            if (currentStock < totalRequested && tenant?.AllowNegativeStock != true)
            {
                var available = Math.Max(0, currentStock - existingQty);
                return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_STOCK,
                    $"Ø§Ù„Ù…Ø®Ø²ÙˆÙ† ØºÙŠØ± ÙƒØ§ÙÙ Ù„Ù„Ù…Ù†ØªØ¬: {product.Name}. Ø§Ù„Ù…ØªØ§Ø­: {available}ØŒ Ø§Ù„Ù…Ø·Ù„ÙˆØ¨: {request.Quantity}");
            }
        }

        // Dynamic Tax Rate Priority: Product â†’ Tenant
        var taxRate = product.TaxRate ?? tenantTaxRate;

        // If tax is disabled at tenant level, override to 0
        if (tenant?.IsTaxEnabled != true)
            taxRate = 0m;

        if (taxRate < 0 || taxRate > 100)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        // Price Priority: 1) ProductBatch.SellingPrice, 2) BranchProductPrice.Price, 3) Product.Price
        var (sellingPrice, batchId, batchNumber, expiryDate, batchCost, priceError) = await ResolveBatchSaleSnapshotAsync(
            product.Id, order.BranchId, order.TenantId, product.Price, request.BatchId);
        if (priceError != null)
            return ApiResponse<OrderDto>.Fail(priceError.Value.ErrorCode, priceError.Value.Message);

        var unitPrice = ResolveNetUnitPrice(sellingPrice);

        // Validate batch quantity if batch is selected
        if (batchId.HasValue)
        {
            var batch = await _unitOfWork.ProductBatches.GetByIdAsync(batchId.Value);
            if (batch == null || batch.Quantity < request.Quantity)
            {
                return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_STOCK,
                    $"Ø§Ù„ÙƒÙ…ÙŠØ© Ø§Ù„Ù…ØªØ§Ø­Ø© ÙÙŠ Ø§Ù„Ø¨Ø§ØªØ´ {batchNumber} ØºÙŠØ± ÙƒØ§ÙÙŠØ©. Ø§Ù„Ù…ØªØ§Ø­: {batch?.Quantity ?? 0}ØŒ Ø§Ù„Ù…Ø·Ù„ÙˆØ¨: {request.Quantity}");
            }
        }

        var unitCost = await ResolveUnitCostAsync(product, batchCost, order.TenantId);

        var orderItem = new OrderItem
        {
            ProductId = product.Id,
            // Product Snapshot (on OrderItem entity)
            ProductName = product.Name,
            ProductNameEn = product.NameEn,
            ProductSku = product.Sku,
            ProductBarcode = product.Barcode,
            // Price Snapshot
            UnitPrice = unitPrice,
            UnitCost = unitCost,
            OriginalPrice = sellingPrice, // Use resolved selling price
            Quantity = request.Quantity,
            DiscountType = NormalizeDiscountType(request.DiscountType),
            DiscountValue = request.DiscountValue,
            DiscountReason = request.DiscountReason,
            // Tax Snapshot
            TaxRate = taxRate,
            TaxInclusive = false,
            Notes = request.Notes,
            // Batch Info
            BatchId = batchId,
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate
        };

        var itemDiscountValidation = ValidateDiscount(orderItem.DiscountType, orderItem.DiscountValue, unitPrice * request.Quantity);
        if (!itemDiscountValidation.Success)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, itemDiscountValidation.Message!);

        // Calculate tax amount and totals with proper rounding
        CalculateItemTotals(orderItem);

        order.Items.Add(orderItem);
        CalculateOrderTotals(order);

        var recipeStockValidation = await ValidateRecipeIngredientStockAsync(
            order.Items.Where(i => i.ProductId.HasValue && !i.IsCustomItem)
                .Select(i => (ProductId: i.ProductId!.Value, i.Quantity, i.ProductName)),
            order.TenantId,
            order.BranchId,
            tenant?.AllowNegativeStock == true);
        if (!recipeStockValidation.Success)
            return ApiResponse<OrderDto>.Fail(recipeStockValidation.ErrorCode!, recipeStockValidation.Message!);

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(MapToDto(order));
    }

    public async Task<ApiResponse<OrderDto>> AddCustomItemAsync(int orderId, AddCustomItemRequest request)
    {
        if (request.TaxRate.HasValue && (request.TaxRate.Value < 0 || request.TaxRate.Value > 100))
            return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        // VALIDATION: Quantity must be positive
        if (request.Quantity <= 0)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_INVALID_QUANTITY, "Ø§Ù„ÙƒÙ…ÙŠØ© ÙŠØ¬Ø¨ Ø£Ù† ØªÙƒÙˆÙ† Ø£ÙƒØ¨Ø± Ù…Ù† ØµÙØ±");

        // VALIDATION: Name is required
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, "Ø§Ø³Ù… Ø§Ù„Ù…Ù†ØªØ¬ Ù…Ø·Ù„ÙˆØ¨");

        // VALIDATION: Price must be non-negative
        if (request.UnitPrice < 0)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.PRODUCT_INVALID_PRICE, "Ø§Ù„Ø³Ø¹Ø± ÙŠØ¬Ø¨ Ø£Ù† ÙŠÙƒÙˆÙ† Ø£ÙƒØ¨Ø± Ù…Ù† Ø£Ùˆ ÙŠØ³Ø§ÙˆÙŠ ØµÙØ±");

        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .Include(o => o.DeliveryPerson)
            .FirstOrDefaultAsync(o => o.Id == orderId
                && o.TenantId == _currentUser.TenantId
                && o.BranchId == _currentUser.BranchId);

        if (order == null)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));

        if (!CanModifyOpenOrder(order.Status))
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_CANNOT_MODIFY, ErrorMessages.Get(ErrorCodes.ORDER_CANNOT_MODIFY));

        OrderItem? parentItem = null;
        if (request.ParentOrderItemId.HasValue)
        {
            parentItem = order.Items.FirstOrDefault(i => i.Id == request.ParentOrderItemId.Value);
            if (parentItem == null || parentItem.ParentOrderItemId.HasValue)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_ITEM_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_ITEM_NOT_FOUND));
        }

        // Get Tenant for dynamic tax settings
        var tenant = await _unitOfWork.Tenants.GetByIdAsync(order.TenantId);
        var tenantTaxRate = tenant?.IsTaxEnabled == true ? tenant.TaxRate : 0m;

        // Use custom tax rate if provided, otherwise use tenant default
        var taxRate = request.TaxRate ?? tenantTaxRate;

        // If tax is disabled at tenant level, override to 0
        if (tenant?.IsTaxEnabled != true)
            taxRate = 0m;

        if (taxRate < 0 || taxRate > 100)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        var unitPrice = ResolveNetUnitPrice(request.UnitPrice);

        // Create custom order item
        var orderItem = new OrderItem
        {
            // Custom item fields
            IsCustomItem = true,
            ProductId = null, // No product reference
            CustomName = request.Name,
            CustomUnitPrice = request.UnitPrice,
            CustomTaxRate = taxRate,

            // Snapshot fields (populated from custom data)
            ProductName = request.Name,
            ProductNameEn = null,
            ProductSku = null,
            ProductBarcode = null,

            // Price Snapshot
            UnitPrice = unitPrice,
            UnitCost = null,
            OriginalPrice = request.UnitPrice,
            Quantity = request.Quantity,
            ParentOrderItemId = parentItem?.Id,

            // Tax Snapshot
            TaxRate = taxRate,
            TaxInclusive = false,
            Notes = request.Notes
        };

        // Calculate tax amount and totals with proper rounding
        CalculateItemTotals(orderItem);

        order.Items.Add(orderItem);
        CalculateOrderTotals(order);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(MapToDto(order), "ØªÙ… Ø¥Ø¶Ø§ÙØ© Ø§Ù„Ù…Ù†ØªØ¬ Ø§Ù„Ù…Ø®ØµØµ Ø¨Ù†Ø¬Ø§Ø­");
    }

    public async Task<ApiResponse<OrderDto>> RemoveItemAsync(int orderId, int itemId)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .Include(o => o.DeliveryPerson)
            .FirstOrDefaultAsync(o => o.Id == orderId
                && o.TenantId == _currentUser.TenantId
                && o.BranchId == _currentUser.BranchId);

        if (order == null)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));

        // VALIDATION: Can only modify Draft orders
        if (!CanModifyOpenOrder(order.Status))
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_CANNOT_MODIFY, "Ù„Ø§ ÙŠÙ…ÙƒÙ† ØªØ¹Ø¯ÙŠÙ„ Ø·Ù„Ø¨ Ù…ÙƒØªÙ…Ù„ Ø£Ùˆ Ù…Ù„ØºÙŠ");

        var item = order.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_ITEM_NOT_FOUND, "Ø¹Ù†ØµØ± Ø§Ù„Ø·Ù„Ø¨ ØºÙŠØ± Ù…ÙˆØ¬ÙˆØ¯");

        order.Items.Remove(item);
        CalculateOrderTotals(order);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(MapToDto(order));
    }

    /// <summary>
    /// Complete an order with payments - uses database transaction for atomicity.
    /// If saving order succeeds but adding payment fails, the entire operation is rolled back.
    /// </summary>
    public async Task<ApiResponse<OrderDto>> CompleteAsync(int orderId, CompleteOrderRequest request)
    {
        var tenantId = _currentUser.TenantId;
        var branchId = _currentUser.BranchId;
        var actingUserId = _currentUser.UserId; // from current JWT claims

        var currentShift = await _unitOfWork.Shifts.Query()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId
                                   && s.BranchId == branchId
                                   && s.UserId == actingUserId
                                   && !s.IsClosed);

        if (currentShift == null)
        {
            return ApiResponse<OrderDto>.Fail(
                ErrorCodes.NO_OPEN_SHIFT,
                "Ù„Ø§ ÙŠÙ…ÙƒÙ† Ø¥ØªÙ…Ø§Ù… Ø§Ù„Ø¨ÙŠØ¹ Ø¨Ø¯ÙˆÙ† ÙˆØ±Ø¯ÙŠØ© Ù…ÙØªÙˆØ­Ø©");
        }

        // Use transaction for atomicity - Order + Payments must succeed together
        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            var order = await _unitOfWork.Orders.Query()
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .Include(o => o.DeliveryPerson)
                .Include(o => o.Table)
                .FirstOrDefaultAsync(o => o.Id == orderId
                    && o.TenantId == _currentUser.TenantId
                    && o.BranchId == _currentUser.BranchId);

            if (order == null)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));

            // Validate state transition
            var validationResult = ValidateStateTransition(order.Status, OrderStatus.Completed);
            if (!validationResult.Success)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_INVALID_STATE_TRANSITION, validationResult.Message!);

            // Validate payment amount against the authoritative order total.
            var orderTotal = Math.Round(order.Total, 2);
            if (orderTotal <= 0)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.PAYMENT_INVALID_AMOUNT, ErrorMessages.Get(ErrorCodes.PAYMENT_INVALID_AMOUNT));

            decimal totalPaymentAmount = Math.Round(
                request.Payments
                    .Where(p => p.Amount > 0)
                    .Sum(p => Math.Round(p.Amount, 2)),
                2);

            // âœ… Allow zero payment ONLY for credit sales (customer linked + permission)
            if (totalPaymentAmount <= 0)
            {
                // Check if this is a valid credit sale (zero payment)
                var hasCreditPermission = await _permissionService.HasPermissionAsync(_currentUser.UserId, Permission.PosCreditSale);
                if (!hasCreditPermission)
                {
                    return ApiResponse<OrderDto>.Fail(ErrorCodes.PAYMENT_CREDIT_NOT_ALLOWED,
                        ErrorMessages.Get(ErrorCodes.PAYMENT_CREDIT_NOT_ALLOWED));
                }

                if (!order.CustomerId.HasValue)
                {
                    return ApiResponse<OrderDto>.Fail(ErrorCodes.PAYMENT_INVALID_AMOUNT,
                        "Ø§Ù„Ø¨ÙŠØ¹ Ø§Ù„Ø¢Ø¬Ù„ Ø¨Ø¯ÙˆÙ† Ø¯ÙØ¹ ÙŠØªØ·Ù„Ø¨ Ø±Ø¨Ø· Ø¹Ù…ÙŠÙ„ Ø¨Ø§Ù„Ø·Ù„Ø¨.");
                }

                // Validate credit limit for full amount
                var canTakeCredit = await _customerService.ValidateCreditLimitAsync(order.CustomerId.Value, orderTotal);
                if (!canTakeCredit)
                {
                    var customer = await _unitOfWork.Customers.GetByIdAsync(order.CustomerId.Value);
                    return ApiResponse<OrderDto>.Fail(ErrorCodes.CUSTOMER_CREDIT_LIMIT_EXCEEDED,
                        $"تجاوز حد الائتمان. الحد المسموح: {customer?.CreditLimit:F2} ج.م، الرصيد الحالي: {customer?.TotalDue:F2} ج.م");
                }
            }

            // Allow partial payment ONLY if customer is linked AND user has credit sale permission
            if (totalPaymentAmount > 0 && totalPaymentAmount < orderTotal)
            {
                var hasCreditPermission = await _permissionService.HasPermissionAsync(_currentUser.UserId, Permission.PosCreditSale);
                if (!hasCreditPermission)
                {
                    return ApiResponse<OrderDto>.Fail(ErrorCodes.PAYMENT_CREDIT_NOT_ALLOWED,
                        ErrorMessages.Get(ErrorCodes.PAYMENT_CREDIT_NOT_ALLOWED));
                }

                if (!order.CustomerId.HasValue)
                {
                    return ApiResponse<OrderDto>.Fail(ErrorCodes.PAYMENT_INSUFFICIENT,
                        $"Ø§Ù„Ù…Ø¨Ù„Øº Ø§Ù„Ù…Ø¯ÙÙˆØ¹ ({totalPaymentAmount:F2}) Ø£Ù‚Ù„ Ù…Ù† Ø¥Ø¬Ù…Ø§Ù„ÙŠ Ø§Ù„Ø·Ù„Ø¨ ({order.Total:F2}). Ø§Ù„Ø¨ÙŠØ¹ Ø§Ù„Ø¢Ø¬Ù„ ÙŠØªØ·Ù„Ø¨ Ø±Ø¨Ø· Ø¹Ù…ÙŠÙ„ Ø¨Ø§Ù„Ø·Ù„Ø¨.");
                }

                // Validate credit limit
                var amountDue = orderTotal - totalPaymentAmount;
                var canTakeCredit = await _customerService.ValidateCreditLimitAsync(order.CustomerId.Value, amountDue);

                if (!canTakeCredit)
                {
                    var customer = await _unitOfWork.Customers.GetByIdAsync(order.CustomerId.Value);
                    return ApiResponse<OrderDto>.Fail(ErrorCodes.CUSTOMER_CREDIT_LIMIT_EXCEEDED,
                        $"تجاوز حد الائتمان. الحد المسموح: {customer?.CreditLimit:F2} ج.م، الرصيد الحالي: {customer?.TotalDue:F2} ج.م");
                }
            }

            // Cash overpayment is change; non-cash overpayment must not enter accounting.
            var hasNonCashOverpayment = totalPaymentAmount > orderTotal
                && request.Payments
                    .Where(p => p.Amount > 0)
                    .Any(p => !Enum.TryParse<PaymentMethod>(p.Method, true, out var method) || method != PaymentMethod.Cash);
            var maxAllowedPayment = Math.Round(orderTotal * 2m, 2);
            if (hasNonCashOverpayment || totalPaymentAmount > maxAllowedPayment)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.PAYMENT_EXCEEDS_DUE,
                    $"Ø§Ù„Ù…Ø¨Ù„Øº Ø§Ù„Ù…Ø¯ÙÙˆØ¹ ({totalPaymentAmount:F2}) ÙŠØªØ¬Ø§ÙˆØ² Ø§Ù„Ø­Ø¯ Ø§Ù„Ù…Ø³Ù…ÙˆØ­ ({maxAllowedPayment:F2})");

            // Add payments
            decimal totalPaid = 0;
            decimal cashPaymentAmount = 0;
            var recordedPayments = new List<Payment>();
            foreach (var paymentReq in request.Payments)
            {
                if (paymentReq.Amount <= 0)
                    continue;

                if (!Enum.TryParse<PaymentMethod>(paymentReq.Method, true, out var paymentMethod))
                    return ApiResponse<OrderDto>.Fail(ErrorCodes.PAYMENT_INVALID_METHOD, ErrorMessages.Get(ErrorCodes.PAYMENT_INVALID_METHOD));

                var referenceValidation = ValidateReferenceForNonCashPayment(
                    paymentMethod,
                    paymentReq.Reference);
                if (!referenceValidation.Success)
                {
                    return ApiResponse<OrderDto>.Fail(
                        ErrorCodes.PAYMENT_REFERENCE_REQUIRED,
                        referenceValidation.Message!);
                }

                if (paymentMethod == PaymentMethod.Wallet)
                {
                    if (!paymentReq.WalletId.HasValue)
                        return ApiResponse<OrderDto>.Fail(ErrorCodes.WALLET_NOT_FOUND, ErrorMessages.Get(ErrorCodes.WALLET_NOT_FOUND));

                    var walletExists = await _unitOfWork.Wallets.Query()
                        .AnyAsync(w => w.Id == paymentReq.WalletId.Value
                                    && w.TenantId == _currentUser.TenantId
                                    && w.BranchId == _currentUser.BranchId
                                    && w.IsActive
                                    && !w.IsDeleted);
                    if (!walletExists)
                        return ApiResponse<OrderDto>.Fail(ErrorCodes.WALLET_NOT_FOUND, ErrorMessages.Get(ErrorCodes.WALLET_NOT_FOUND));
                }

                var payment = new Payment
                {
                    TenantId = _currentUser.TenantId,
                    BranchId = _currentUser.BranchId,
                    OrderId = order.Id,
                    Amount = Math.Min(
                        Math.Round(paymentReq.Amount, 2),
                        Math.Max(0, orderTotal - totalPaid)),
                    Method = paymentMethod,
                    Reference = paymentReq.Reference,
                    WalletId = paymentReq.WalletId
                };
                if (payment.Amount <= 0)
                    continue;

                await _unitOfWork.Payments.AddAsync(payment);
                order.Payments.Add(payment);
                recordedPayments.Add(payment);
                totalPaid += payment.Amount;

                // Track cash payments for cash register
                if (payment.Method == PaymentMethod.Cash)
                    cashPaymentAmount += payment.Amount;
            }

            // Payment is the final cashier-controlled step; operational stages are not used.
            order.Status = OrderStatus.Completed;
            if (order.OrderType == OrderType.Delivery)
                order.DeliveryStatus = DeliveryStatus.PendingAssignment;
            
            order.AmountPaid = Math.Min(Math.Round(totalPaid, 2), orderTotal);
            order.AmountDue = Math.Max(0, Math.Round(orderTotal - totalPaid, 2));
            order.ChangeAmount = Math.Max(0, Math.Round(totalPaymentAmount - orderTotal, 2));
            order.CompletedAt = DateTime.UtcNow;

            if (order.Table != null)
            {
                order.Table.Status = RestaurantTableStatus.Available;
                order.KitchenPrintCount = 0;
                order.LastKitchenPrintedAt = null;
            }

            await _unitOfWork.SaveChangesAsync();

            // P0-3: Re-validate stock INSIDE the write transaction.
            // This is the authoritative check. The CreateAsync check is just a UX hint.
            // SQLite's write lock guarantees no other writer can change stock between
            // this read and the decrement below.
            // ONLY validate for items with ProductId (skip custom items)
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(_currentUser.TenantId);
            if (tenant != null && !tenant.AllowNegativeStock)
            {
                foreach (var item in order.Items.Where(i => i.ProductId.HasValue && !i.IsCustomItem))
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId!.Value);
                    if (product != null && RequiresDirectStockTracking(product))
                    {
                        var branchStock = await _inventoryService.GetAvailableQuantityAsync(
                            item.ProductId!.Value, _currentUser.BranchId);
                        if (branchStock < item.Quantity)
                        {
                            await transaction.RollbackAsync();
                            return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_STOCK,
                                $"Ø§Ù„Ù…Ø®Ø²ÙˆÙ† ØªØºÙŠØ± Ø£Ø«Ù†Ø§Ø¡ Ø¥ØªÙ…Ø§Ù… Ø§Ù„Ø·Ù„Ø¨. Ø§Ù„Ù…Ù†ØªØ¬: {item.ProductName}. " +
                                $"Ø§Ù„Ù…ØªØ§Ø­ Ø§Ù„Ø¢Ù†: {branchStock}ØŒ Ø§Ù„Ù…Ø·Ù„ÙˆØ¨: {item.Quantity}");
                        }
                    }
                }
            }

            // Decrement stock ONLY for products that track inventory (skip custom items)
            // Fetch all products in one query for performance
            var productIds = order.Items
                .Where(i => i.ProductId.HasValue && !i.IsCustomItem)
                .Select(i => i.ProductId!.Value)
                .Distinct()
                .ToList();

            var products = await _unitOfWork.Products.Query()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p);

            var stockItems = order.Items
                .Where(i => i.ProductId.HasValue
                         && !i.IsCustomItem
                         && products.ContainsKey(i.ProductId.Value)
                         && RequiresDirectStockTracking(products[i.ProductId.Value]))
                .Select(i => (i.Id, i.ProductId!.Value, i.Quantity, i.BatchId))
                .ToList();

            if (stockItems.Any())
            {
                await _inventoryService.BatchDecrementStockAsync(stockItems, order.Id);
            }

            // Deduct recipe ingredients if applicable
            foreach (var item in order.Items.Where(i => i.ProductId.HasValue && !i.IsCustomItem))
            {
                var recipeResponse = await _recipeService.GetByProductIdAsync(item.ProductId!.Value, order.TenantId);
                var recipe = recipeResponse.Data;
                if (recipe != null && recipe.AutoDeductIngredients)
                {
                    var result = await _recipeService.DeductIngredientsAsync(
                        recipe.Id,
                        multiplier: item.Quantity,
                        branchId: order.BranchId,
                        tenantId: order.TenantId);
                    if (!result.Success)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<OrderDto>.Fail(
                            ErrorCodes.INSUFFICIENT_STOCK,
                            result.Message ?? ErrorMessages.Get(ErrorCodes.INSUFFICIENT_STOCK));
                    }
                }
            }

            // Update customer statistics if order has a customer
            if (order.CustomerId.HasValue)
            {
                // Calculate loyalty points: 1 point per 1 currency unit
                int loyaltyPoints = (int)Math.Floor(order.Total);
                await _customerService.UpdateOrderStatsAsync(order.CustomerId.Value, order.Total, loyaltyPoints);

                // Update credit balance if there's unpaid amount
                if (order.AmountDue > 0)
                {
                    await _customerService.UpdateCreditBalanceAsync(order.CustomerId.Value, order.AmountDue);
                }
            }

            // INTEGRATION: Record cash register transaction for cash payments
            if (cashPaymentAmount > 0)
            {
                await _cashRegisterService.RecordTransactionAsync(
                    type: CashRegisterTransactionType.Sale,
                    amount: cashPaymentAmount,
                    description: $"Ù…Ø¨ÙŠØ¹Ø§Øª - Ø·Ù„Ø¨ #{order.OrderNumber}",
                    referenceType: "Order",
                    referenceId: order.Id,
                    shiftId: order.ShiftId ?? currentShift.Id
                );
            }

            // INTEGRATION: Record wallet transactions for non-cash wallet payments
            foreach (var payment in recordedPayments.Where(p => p.Method == PaymentMethod.Wallet && p.WalletId.HasValue && p.Amount > 0))
            {
                await _walletService.RecordOrderPaymentAsync(
                    walletId: payment.WalletId!.Value,
                    amount: payment.Amount,
                    orderId: order.Id,
                    orderNumber: order.OrderNumber,
                    referenceNumber: payment.Reference,
                    userId: _currentUser.UserId,
                    userName: _currentUser.Email ?? "Unknown",
                    ct: CancellationToken.None
                );
            }

            // Save all changes (customer stats, credit balance, cash register)
            await _unitOfWork.SaveChangesAsync();

            // Commit transaction - all operations succeeded
            await transaction.CommitAsync();

            return ApiResponse<OrderDto>.Ok(MapToDto(order), "ØªÙ… Ø¥ØªÙ…Ø§Ù… Ø§Ù„Ø¯ÙØ¹ ÙˆØ¥ØºÙ„Ø§Ù‚ Ø§Ù„Ø·Ù„Ø¨");
        }
        catch (Exception ex)
        {
            // Rollback transaction - something failed
            await transaction.RollbackAsync();
            return ApiResponse<OrderDto>.Fail(ErrorCodes.SYSTEM_INTERNAL_ERROR,
                $"Ø­Ø¯Ø« Ø®Ø·Ø£ Ø£Ø«Ù†Ø§Ø¡ Ø¥ØªÙ…Ø§Ù… Ø§Ù„Ø·Ù„Ø¨: {ex.Message}");
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
            return (false, "Ø±Ù‚Ù… Ø§Ù„Ù…Ø¹Ø§Ù…Ù„Ø© Ù…Ø·Ù„ÙˆØ¨ Ù„Ø·Ø±Ù‚ Ø§Ù„Ø¯ÙØ¹ ØºÙŠØ± Ø§Ù„Ù†Ù‚Ø¯ÙŠØ©");
        }

        return (true, null);
    }

    public async Task<ApiResponse<KitchenTicketDto>> SendToKitchenAsync(int orderId)
    {
        try
        {
            var order = await _unitOfWork.Orders.Query()
                .AsNoTracking()
                .Include(o => o.Items)
                .Include(o => o.Table)
                .FirstOrDefaultAsync(o => o.Id == orderId
                    && o.TenantId == _currentUser.TenantId
                    && o.BranchId == _currentUser.BranchId);

            if (order == null)
            {
                return ApiResponse<KitchenTicketDto>.Fail(ErrorCodes.ORDER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));
            }

            if (!CanModifyOpenOrder(order.Status))
            {
                return ApiResponse<KitchenTicketDto>.Fail(ErrorCodes.ORDER_CANNOT_MODIFY, ErrorMessages.Get(ErrorCodes.ORDER_CANNOT_MODIFY));
            }

            var printableItems = order.Items
                .Where(i => i.Quantity > i.KitchenPrintedQuantity)
                .OrderBy(i => i.ParentOrderItemId ?? i.Id)
                .ThenBy(i => i.ParentOrderItemId.HasValue ? 1 : 0)
                .ThenBy(i => i.Id)
                .ToList();

            if (printableItems.Count == 0)
            {
                return ApiResponse<KitchenTicketDto>.Fail(ErrorCodes.NOTHING_TO_PRINT, ErrorMessages.Get(ErrorCodes.NOTHING_TO_PRINT));
            }

            var printedAt = DateTime.UtcNow;
            var ticketType = order.KitchenPrintCount == 0 ? "Full" : "Additions";
            var destination = GetKitchenDestination(order);

            var ticket = new KitchenTicketDto
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                TicketType = ticketType,
                Header = ticketType == "Additions" ? $"Ø¥Ø¶Ø§ÙØ© â€” {destination}" : destination,
                Notes = order.Notes,
                KitchenPrintCount = order.KitchenPrintCount + 1,
                PrintedAt = printedAt,
                Items = BuildKitchenTicketItems(order.Items.ToList(), printableItems)
            };

            return ApiResponse<KitchenTicketDto>.Ok(ticket, "ØªÙ… Ø¥Ø±Ø³Ø§Ù„ Ø§Ù„Ø·Ù„Ø¨ Ù„Ù„Ù…Ø·Ø¨Ø®");
        }
        catch (Exception ex)
        {
            return ApiResponse<KitchenTicketDto>.Fail(ErrorCodes.SYSTEM_INTERNAL_ERROR,
                $"Ø­Ø¯Ø« Ø®Ø·Ø£ Ø£Ø«Ù†Ø§Ø¡ Ø¥Ø±Ø³Ø§Ù„ Ø§Ù„Ø·Ù„Ø¨ Ù„Ù„Ù…Ø·Ø¨Ø®: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> MarkKitchenTicketPrintedAsync(int orderId, KitchenTicketDto ticket)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            var order = await _unitOfWork.Orders.Query()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId
                    && o.TenantId == _currentUser.TenantId
                    && o.BranchId == _currentUser.BranchId);

            if (order == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return ApiResponse<bool>.Fail(ErrorCodes.ORDER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));
            }

            var itemsById = order.Items.ToDictionary(i => i.Id);
            foreach (var ticketItem in ticket.Items)
            {
                if (!itemsById.TryGetValue(ticketItem.OrderItemId, out var orderItem))
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return ApiResponse<bool>.Fail(ErrorCodes.ORDER_ITEM_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_ITEM_NOT_FOUND));
                }

                orderItem.KitchenPrintedQuantity = Math.Min(
                    orderItem.Quantity,
                    orderItem.KitchenPrintedQuantity + ticketItem.Quantity);
                orderItem.LastKitchenPrintedAt = ticket.PrintedAt;
            }

            order.KitchenPrintCount += 1;
            order.LastKitchenPrintedAt = ticket.PrintedAt;

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<bool>.Ok(true, "ØªÙ… ØªØ£ÙƒÙŠØ¯ Ø·Ø¨Ø§Ø¹Ø© ØªØ°ÙƒØ±Ø© Ø§Ù„Ù…Ø·Ø¨Ø®");
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return ApiResponse<bool>.Fail(ErrorCodes.SYSTEM_INTERNAL_ERROR,
                $"Ø­Ø¯Ø« Ø®Ø·Ø£ Ø£Ø«Ù†Ø§Ø¡ ØªØ£ÙƒÙŠØ¯ Ø·Ø¨Ø§Ø¹Ø© ØªØ°ÙƒØ±Ø© Ø§Ù„Ù…Ø·Ø¨Ø®: {ex.Message}");
        }
    }

    private static string GetKitchenDestination(Order order)
    {
        return order.OrderType switch
        {
            OrderType.DineIn => $"طاولة {order.TableNumberSnapshot ?? order.Table?.Number ?? order.TableId?.ToString() ?? "-"}",
            OrderType.Delivery => string.IsNullOrWhiteSpace(order.CustomerName)
                ? "Ø¯Ù„ÙŠÙØ±ÙŠ"
                : $"Ø¯Ù„ÙŠÙØ±ÙŠ - {order.CustomerName.Trim()}",
            OrderType.Takeaway => "ØªÙŠÙƒ Ø£ÙˆØ§ÙŠ",
            _ => order.OrderType.ToString()
        };
    }

    private static List<KitchenTicketItemDto> BuildKitchenTicketItems(
        List<OrderItem> allItems,
        List<OrderItem> printableItems)
    {
        var printableIds = printableItems.Select(i => i.Id).ToHashSet();
        var ticketItems = new List<KitchenTicketItemDto>();

        foreach (var parent in allItems.Where(i => !i.ParentOrderItemId.HasValue).OrderBy(i => i.Id))
        {
            if (printableIds.Contains(parent.Id))
                ticketItems.Add(MapKitchenTicketItem(parent, isAddOn: false));

            foreach (var addOn in allItems.Where(i => i.ParentOrderItemId == parent.Id && printableIds.Contains(i.Id)).OrderBy(i => i.Id))
            {
                ticketItems.Add(MapKitchenTicketItem(addOn, isAddOn: true));
            }
        }

        foreach (var orphanAddOn in printableItems
            .Where(i => i.ParentOrderItemId.HasValue && allItems.All(parent => parent.Id != i.ParentOrderItemId.Value))
            .OrderBy(i => i.Id))
        {
            ticketItems.Add(MapKitchenTicketItem(orphanAddOn, isAddOn: true));
        }

        return ticketItems;
    }

    private static KitchenTicketItemDto MapKitchenTicketItem(OrderItem item, bool isAddOn)
    {
        return new KitchenTicketItemDto
        {
            OrderItemId = item.Id,
            ParentOrderItemId = item.ParentOrderItemId,
            Name = isAddOn ? $"+ {item.ProductName}" : item.ProductName,
            Quantity = item.Quantity - item.KitchenPrintedQuantity,
            Notes = item.Notes,
            IsCustomItem = item.IsCustomItem,
            IsAddOn = isAddOn
        };
    }

    public async Task<ApiResponse<OrderDto>> UpdateStatusAsync(int orderId, UpdateOrderStatusRequest request)
    {
        if (request.Status == OrderStatus.Cancelled)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, "Ø³Ø¨Ø¨ Ø§Ù„Ø¥Ù„ØºØ§Ø¡ Ù…Ø·Ù„ÙˆØ¨");

            var canCancel = await _permissionService.HasPermissionAsync(_currentUser.UserId, Permission.PosCancelOrder);
            if (!canCancel)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.INSUFFICIENT_PRIVILEGES, ErrorMessages.Get(ErrorCodes.INSUFFICIENT_PRIVILEGES));

            var cancelResult = await CancelAsync(orderId, request.Reason.Trim());
            if (!cancelResult.Success)
                return ApiResponse<OrderDto>.Fail(cancelResult.ErrorCode!, cancelResult.Message!);

            return await GetByIdAsync(orderId);
        }

        return ApiResponse<OrderDto>.Fail(
            ErrorCodes.ORDER_INVALID_STATE_TRANSITION,
            "ØªØºÙŠÙŠØ± Ù…Ø±Ø§Ø­Ù„ Ø§Ù„Ø·Ù„Ø¨ ØºÙŠØ± Ù…ØªØ§Ø­. Ø§Ù„Ø·Ù„Ø¨ ÙŠØµØ¨Ø­ Ù…ÙƒØªÙ…Ù„Ø§ ÙÙˆØ± Ø¥ØªÙ…Ø§Ù… Ø§Ù„Ø¯ÙØ¹.");

    }

    public async Task<ApiResponse<bool>> CancelAsync(int orderId, string? reason)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == orderId
                && o.TenantId == _currentUser.TenantId
                && o.BranchId == _currentUser.BranchId);

        if (order == null)
            return ApiResponse<bool>.Fail(ErrorCodes.ORDER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));

        if (order.AmountPaid > 0 || order.Payments.Any(p => p.Amount > 0))
            return ApiResponse<bool>.Fail(
                ErrorCodes.ORDER_INVALID_STATE_TRANSITION,
                "Ù„Ø§ ÙŠÙ…ÙƒÙ† Ø¥Ù„ØºØ§Ø¡ Ø·Ù„Ø¨ ØªÙ… Ø§Ù„Ø¯ÙØ¹ Ø¹Ù„ÙŠÙ‡. Ø§Ø³ØªØ®Ø¯Ù… Ø§Ù„Ø§Ø³ØªØ±Ø¬Ø§Ø¹ Ø¨Ø¯Ù„ Ø§Ù„Ø¥Ù„ØºØ§Ø¡.");

        // Validate state transition
        var validationResult = ValidateStateTransition(order.Status, OrderStatus.Cancelled);
        if (!validationResult.Success)
            return ApiResponse<bool>.Fail(ErrorCodes.ORDER_INVALID_STATE_TRANSITION, validationResult.Message!);

        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = reason;

            if (order.Table != null)
            {
                order.Table.Status = RestaurantTableStatus.Available;
                order.KitchenPrintCount = 0;
                order.LastKitchenPrintedAt = null;
            }

            // âœ… FIX: Reduce customer's TotalDue if order had unpaid amount
            if (order.CustomerId.HasValue && order.AmountDue > 0)
            {
                await _customerService.ReduceCreditBalanceAsync(
                    order.CustomerId.Value,
                    order.AmountDue
                );
            }

            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
            return ApiResponse<bool>.Ok(true, "ØªÙ… Ø¥Ù„ØºØ§Ø¡ Ø§Ù„Ø·Ù„Ø¨");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return ApiResponse<bool>.Fail(ErrorCodes.SYSTEM_INTERNAL_ERROR,
                $"Ø­Ø¯Ø« Ø®Ø·Ø£ Ø£Ø«Ù†Ø§Ø¡ Ø¥Ù„ØºØ§Ø¡ Ø§Ù„Ø·Ù„Ø¨: {ex.Message}");
        }
    }

    /// <summary>
    /// Process a full or partial refund for a completed order.
    /// Creates a NEW Return Order with negative totals, restores stock, and creates audit trail.
    /// Original order status is updated to Refunded or PartiallyRefunded.
    /// </summary>
    public async Task<ApiResponse<OrderDto>> RefundAsync(int orderId, int userId, string? reason, List<RefundItemDto>? items = null)
    {
        var isPartialRefund = items != null && items.Count > 0;

        // For full refund, reason is required
        if (!isPartialRefund && string.IsNullOrWhiteSpace(reason))
            return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR, "Ø³Ø¨Ø¨ Ø§Ù„Ø§Ø³ØªØ±Ø¬Ø§Ø¹ Ù…Ø·Ù„ÙˆØ¨ Ù„Ù„Ø§Ø³ØªØ±Ø¬Ø§Ø¹ Ø§Ù„ÙƒØ§Ù…Ù„");

        // Use transaction for atomicity
        await using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            var originalOrder = await _unitOfWork.Orders.Query()
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId
                    && o.TenantId == _currentUser.TenantId
                    && o.BranchId == _currentUser.BranchId);

            if (originalOrder == null)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));

            if (originalOrder.OrderType == OrderType.Return)
                return ApiResponse<OrderDto>.Fail(
                    ErrorCodes.ORDER_INVALID_STATE_TRANSITION,
                    "Ù„Ø§ ÙŠÙ…ÙƒÙ† Ø§Ø³ØªØ±Ø¬Ø§Ø¹ Ø·Ù„Ø¨ Ù…Ø±ØªØ¬Ø¹");

            // Validate: Order must be Completed or PartiallyRefunded
            if (originalOrder.Status != OrderStatus.Completed && originalOrder.Status != OrderStatus.PartiallyRefunded)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_INVALID_STATE_TRANSITION,
                    "ÙŠÙ…ÙƒÙ† Ø§Ø³ØªØ±Ø¬Ø§Ø¹ Ø§Ù„Ø·Ù„Ø¨Ø§Øª Ø§Ù„Ù…ÙƒØªÙ…Ù„Ø© Ø£Ùˆ Ø§Ù„Ù…Ø³ØªØ±Ø¯Ø© Ø¬Ø²Ø¦ÙŠØ§Ù‹ ÙÙ‚Ø·");

            var remainingRefundableAmount = Math.Round(originalOrder.Total - originalOrder.RefundAmount, 2);
            if (remainingRefundableAmount <= 0)
                return ApiResponse<OrderDto>.Fail(
                    ErrorCodes.ORDER_INVALID_STATE_TRANSITION,
                    "Ù‡Ø°Ø§ Ø§Ù„Ø·Ù„Ø¨ Ù…Ø³ØªØ±Ø¬Ø¹ Ø¨Ø§Ù„ÙƒØ§Ù…Ù„ Ø¨Ø§Ù„ÙØ¹Ù„");

            // Get user for snapshot
            var refundUser = await _unitOfWork.Users.GetByIdAsync(userId);
            if (refundUser == null)
                return ApiResponse<OrderDto>.Fail(ErrorCodes.USER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.USER_NOT_FOUND));

            // Get current shift (optional for return orders)
            var currentShift = await _unitOfWork.Shifts.Query()
                .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId
                                       && s.BranchId == _currentUser.BranchId
                                       && s.UserId == userId
                                       && !s.IsClosed);

            // Build stock changes JSON for audit trail
            var stockChanges = new List<object>();
            decimal totalRefundAmount = 0;
            var refundReason = reason ?? "";

            // Create the Return Order
            var returnOrder = new Order
            {
                TenantId = originalOrder.TenantId,
                BranchId = originalOrder.BranchId,
                ShiftId = currentShift?.Id,
                OrderNumber = GenerateReturnOrderNumber(),
                UserId = userId,
                UserName = refundUser.Name,
                // Link to original customer
                CustomerId = originalOrder.CustomerId,
                CustomerName = originalOrder.CustomerName,
                CustomerPhone = originalOrder.CustomerPhone,
                // Return order type
                OrderType = OrderType.Return,
                OriginalOrderId = originalOrder.Id,
                Status = OrderStatus.Completed,
                // Branch Snapshot from original order
                BranchName = originalOrder.BranchName,
                BranchAddress = originalOrder.BranchAddress,
                BranchPhone = originalOrder.BranchPhone,
                CurrencyCode = originalOrder.CurrencyCode,
                TaxRate = originalOrder.TaxRate,
                // Notes linking to original order
                Notes = $"Ù…Ø±ØªØ¬Ø¹ Ù„Ù„Ø·Ù„Ø¨ #{originalOrder.OrderNumber}" +
                        (string.IsNullOrWhiteSpace(refundReason) ? "" : $" - السبب: {refundReason}"),
                CompletedAt = DateTime.UtcNow
            };

            if (isPartialRefund)
            {
                // PARTIAL REFUND - Only specified items
                foreach (var refundItem in items!)
                {
                    var orderItem = originalOrder.Items.FirstOrDefault(i => i.Id == refundItem.ItemId);
                    if (orderItem == null)
                        return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR,
                            $"Ø¹Ù†ØµØ± Ø§Ù„Ø·Ù„Ø¨ Ø±Ù‚Ù… {refundItem.ItemId} ØºÙŠØ± Ù…ÙˆØ¬ÙˆØ¯");

                    if (refundItem.Quantity <= 0)
                        return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR,
                            "ÙƒÙ…ÙŠØ© Ø§Ù„Ø§Ø³ØªØ±Ø¬Ø§Ø¹ ÙŠØ¬Ø¨ Ø£Ù† ØªÙƒÙˆÙ† Ø£ÙƒØ¨Ø± Ù…Ù† ØµÙØ±");

                    var availableForRefund = Math.Max(0, orderItem.Quantity - orderItem.RefundedQuantity);

                    if (availableForRefund <= 0)
                        return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR,
                            $"Ø§Ù„Ù…Ù†ØªØ¬ {orderItem.ProductName} Ù…Ø³ØªØ±Ø¬Ø¹ Ø¨Ø§Ù„ÙƒØ§Ù…Ù„ Ø¨Ø§Ù„ÙØ¹Ù„");

                    if (refundItem.Quantity > availableForRefund)
                        return ApiResponse<OrderDto>.Fail(ErrorCodes.VALIDATION_ERROR,
                            $"ÙƒÙ…ÙŠØ© Ø§Ù„Ø§Ø³ØªØ±Ø¬Ø§Ø¹ ({refundItem.Quantity}) Ø£ÙƒØ¨Ø± Ù…Ù† Ø§Ù„ÙƒÙ…ÙŠØ© Ø§Ù„Ù…ØªØ§Ø­Ø© ({availableForRefund}) Ù„Ù„Ù…Ù†ØªØ¬ {orderItem.ProductName}");

                    // Calculate refund amount with order-level discount, service, and delivery allocated proportionally.
                    var itemRefundAmount = CalculateRefundAmountForItem(originalOrder, orderItem, refundItem.Quantity);
                    totalRefundAmount += itemRefundAmount;

                    // Add NEGATIVE item to return order
                    var returnItem = new OrderItem
                    {
                        ProductId = orderItem.ProductId,
                        ProductName = orderItem.ProductName,
                        ProductNameEn = orderItem.ProductNameEn,
                        ProductSku = orderItem.ProductSku,
                        ProductBarcode = orderItem.ProductBarcode,
                        UnitPrice = -orderItem.UnitPrice, // Negative price
                        UnitCost = orderItem.UnitCost,
                        OriginalPrice = orderItem.OriginalPrice,
                        Quantity = refundItem.Quantity,
                        IsCustomItem = orderItem.IsCustomItem,
                        CustomName = orderItem.CustomName,
                        CustomUnitPrice = orderItem.CustomUnitPrice,
                        CustomTaxRate = orderItem.CustomTaxRate,
                        TaxRate = orderItem.TaxRate,
                        TaxInclusive = orderItem.TaxInclusive,
                        DiscountType = orderItem.DiscountType,
                        DiscountValue = orderItem.DiscountValue,
                        DiscountAmount = -Math.Round((orderItem.DiscountAmount / orderItem.Quantity) * refundItem.Quantity, 2),
                        TaxAmount = -Math.Round((orderItem.TaxAmount / orderItem.Quantity) * refundItem.Quantity, 2),
                        Subtotal = -Math.Round((orderItem.Subtotal / orderItem.Quantity) * refundItem.Quantity, 2),
                        Total = -itemRefundAmount,
                        Notes = refundItem.Reason ?? refundReason,
                        BatchId = orderItem.BatchId,
                        BatchNumber = orderItem.BatchNumber,
                        ExpiryDate = orderItem.ExpiryDate
                    };
                    returnOrder.Items.Add(returnItem);

                    // Restore stock ONLY if product tracks inventory (skip custom items)
                    if (orderItem.ProductId.HasValue && !orderItem.IsCustomItem)
                    {
                        var product = await _unitOfWork.Products.GetByIdAsync(orderItem.ProductId.Value);
                        if (product != null)
                        {
                            if (RequiresDirectStockTracking(product))
                            {
                                var currentStock = await _inventoryService.GetCurrentStockAsync(orderItem.ProductId.Value);
                                var newStock = await _inventoryService.IncrementStockAsync(orderItem.ProductId.Value, refundItem.Quantity, originalOrder.Id, orderItem.BatchId);

                                stockChanges.Add(new
                                {
                                    ProductId = orderItem.ProductId.Value,
                                    ProductName = orderItem.ProductName,
                                    Quantity = refundItem.Quantity,
                                    BalanceBefore = currentStock,
                                    BalanceAfter = newStock,
                                    Reason = refundItem.Reason ?? refundReason
                                });
                            }

                            // Restore recipe ingredients if applicable
                            var recipeResponse = await _recipeService.GetByProductIdAsync(orderItem.ProductId.Value, originalOrder.TenantId);
                            var recipe = recipeResponse.Data;
                            if (recipe != null && recipe.AutoDeductIngredients)
                            {
                                await _recipeService.DeductIngredientsAsync(
                                    recipe.Id,
                                    multiplier: -refundItem.Quantity,
                                    branchId: originalOrder.BranchId,
                                    tenantId: originalOrder.TenantId);
                            }
                        }
                    }

                    orderItem.RefundedQuantity += refundItem.Quantity;

                    // Build combined reason
                    if (!string.IsNullOrWhiteSpace(refundItem.Reason))
                    {
                        refundReason += (string.IsNullOrEmpty(refundReason) ? "" : " | ") +
                            $"{orderItem.ProductName}: {refundItem.Reason}";
                    }
                }

                totalRefundAmount = Math.Round(totalRefundAmount, 2);
                if (totalRefundAmount <= 0)
                    return ApiResponse<OrderDto>.Fail(
                        ErrorCodes.VALIDATION_ERROR,
                        "Ù„Ø§ ØªÙˆØ¬Ø¯ Ø¹Ù†Ø§ØµØ± ØµØ§Ù„Ø­Ø© Ù„Ù„Ø§Ø³ØªØ±Ø¬Ø§Ø¹");

                if (totalRefundAmount > remainingRefundableAmount)
                    return ApiResponse<OrderDto>.Fail(
                        ErrorCodes.VALIDATION_ERROR,
                        "Ù‚ÙŠÙ…Ø© Ø§Ù„Ø§Ø³ØªØ±Ø¬Ø§Ø¹ ØªØªØ¬Ø§ÙˆØ² Ø§Ù„Ù…ØªØ¨Ù‚ÙŠ Ù…Ù† Ø§Ù„Ø·Ù„Ø¨");

                // Update original order for partial refund
                originalOrder.RefundAmount = Math.Round(originalOrder.RefundAmount + totalRefundAmount, 2);
                if (originalOrder.RefundAmount > originalOrder.Total)
                    originalOrder.RefundAmount = originalOrder.Total;

                var isFullyRefundedAfterPartial = originalOrder.Items
                    .All(i => i.RefundedQuantity >= i.Quantity);

                originalOrder.Status = isFullyRefundedAfterPartial
                    ? OrderStatus.Refunded
                    : OrderStatus.PartiallyRefunded;
            }
            else
            {
                // FULL REFUND - All items
                foreach (var item in originalOrder.Items)
                {
                    if (item.Quantity <= 0)
                        continue;

                    var remainingQuantity = Math.Max(0, item.Quantity - item.RefundedQuantity);
                    if (remainingQuantity <= 0)
                        continue;

                    var quantityRatio = (decimal)remainingQuantity / item.Quantity;
                    var itemRefundAmount = CalculateRefundAmountForItem(originalOrder, item, remainingQuantity);
                    totalRefundAmount += itemRefundAmount;

                    // Add NEGATIVE item to return order
                    var returnItem = new OrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        ProductNameEn = item.ProductNameEn,
                        ProductSku = item.ProductSku,
                        ProductBarcode = item.ProductBarcode,
                        UnitPrice = -item.UnitPrice,
                        UnitCost = item.UnitCost,
                        OriginalPrice = item.OriginalPrice,
                        Quantity = remainingQuantity,
                        IsCustomItem = item.IsCustomItem,
                        CustomName = item.CustomName,
                        CustomUnitPrice = item.CustomUnitPrice,
                        CustomTaxRate = item.CustomTaxRate,
                        TaxRate = item.TaxRate,
                        TaxInclusive = item.TaxInclusive,
                        DiscountType = item.DiscountType,
                        DiscountValue = item.DiscountValue,
                        DiscountAmount = -Math.Round(item.DiscountAmount * quantityRatio, 2),
                        TaxAmount = -Math.Round(item.TaxAmount * quantityRatio, 2),
                        Subtotal = -Math.Round(item.Subtotal * quantityRatio, 2),
                        Total = -itemRefundAmount,
                        Notes = refundReason,
                        BatchId = item.BatchId,
                        BatchNumber = item.BatchNumber,
                        ExpiryDate = item.ExpiryDate
                    };
                    returnOrder.Items.Add(returnItem);

                    // Restore stock ONLY if product tracks inventory (skip custom items)
                    if (item.ProductId.HasValue && !item.IsCustomItem)
                    {
                        var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId.Value);
                        if (product != null)
                        {
                            if (RequiresDirectStockTracking(product))
                            {
                                var currentStock = await _inventoryService.GetCurrentStockAsync(item.ProductId.Value);
                                var newStock = await _inventoryService.IncrementStockAsync(item.ProductId.Value, remainingQuantity, originalOrder.Id, item.BatchId);

                                stockChanges.Add(new
                                {
                                    ProductId = item.ProductId.Value,
                                    ProductName = item.ProductName,
                                    Quantity = remainingQuantity,
                                    BalanceBefore = currentStock,
                                    BalanceAfter = newStock
                                });
                            }

                            // Restore recipe ingredients if applicable
                            var recipeResponse = await _recipeService.GetByProductIdAsync(item.ProductId.Value, originalOrder.TenantId);
                            var recipe = recipeResponse.Data;
                            if (recipe != null && recipe.AutoDeductIngredients)
                            {
                                await _recipeService.DeductIngredientsAsync(
                                    recipe.Id,
                                    multiplier: -remainingQuantity,
                                    branchId: originalOrder.BranchId,
                                    tenantId: originalOrder.TenantId);
                            }
                        }
                    }

                    item.RefundedQuantity += remainingQuantity;
                }

                totalRefundAmount = Math.Round(totalRefundAmount, 2);
                var roundingDelta = Math.Round(remainingRefundableAmount - totalRefundAmount, 2);
                if (roundingDelta != 0 && Math.Abs(roundingDelta) <= 0.05m && returnOrder.Items.Any())
                {
                    var lastReturnItem = returnOrder.Items.Last();
                    lastReturnItem.Total = Math.Round(lastReturnItem.Total - roundingDelta, 2);
                    totalRefundAmount = Math.Round(totalRefundAmount + roundingDelta, 2);
                }

                if (totalRefundAmount <= 0)
                    return ApiResponse<OrderDto>.Fail(
                        ErrorCodes.ORDER_INVALID_STATE_TRANSITION,
                        "Ù‡Ø°Ø§ Ø§Ù„Ø·Ù„Ø¨ Ù…Ø³ØªØ±Ø¬Ø¹ Ø¨Ø§Ù„ÙƒØ§Ù…Ù„ Ø¨Ø§Ù„ÙØ¹Ù„");

                if (totalRefundAmount > remainingRefundableAmount)
                    return ApiResponse<OrderDto>.Fail(
                        ErrorCodes.VALIDATION_ERROR,
                        "Ù‚ÙŠÙ…Ø© Ø§Ù„Ø§Ø³ØªØ±Ø¬Ø§Ø¹ ØªØªØ¬Ø§ÙˆØ² Ø§Ù„Ù…ØªØ¨Ù‚ÙŠ Ù…Ù† Ø§Ù„Ø·Ù„Ø¨");

                originalOrder.RefundAmount = Math.Round(originalOrder.RefundAmount + totalRefundAmount, 2);
                if (originalOrder.RefundAmount > originalOrder.Total)
                    originalOrder.RefundAmount = originalOrder.Total;
                originalOrder.Status = OrderStatus.Refunded;
            }

            // Set return order totals (all negative)
            returnOrder.Subtotal = returnOrder.Items.Sum(i => i.Subtotal);
            returnOrder.TaxAmount = returnOrder.Items.Sum(i => i.TaxAmount);
            returnOrder.DiscountAmount = returnOrder.Items.Sum(i => i.DiscountAmount);
            returnOrder.Total = returnOrder.Items.Sum(i => i.Total);
            var refundPaymentAllocations = await BuildRefundPaymentAllocationsAsync(originalOrder, totalRefundAmount, !isPartialRefund);
            var paidRefundAmount = Math.Round(refundPaymentAllocations.Sum(a => a.Amount), 2);
            returnOrder.AmountPaid = -paidRefundAmount;
            returnOrder.AmountDue = 0;
            returnOrder.ChangeAmount = 0;

            // Update original order metadata
            originalOrder.RefundedAt = DateTime.UtcNow;
            originalOrder.RefundReason = string.IsNullOrWhiteSpace(originalOrder.RefundReason)
                ? refundReason
                : originalOrder.RefundReason + " | " + refundReason;
            originalOrder.RefundedByUserId = userId;
            originalOrder.RefundedByUserName = refundUser.Name;

            // Save the return order
            await _unitOfWork.Orders.AddAsync(returnOrder);

            // Create or update RefundLog entry for audit trail (OrderId is unique)
            var stockChangesJson = System.Text.Json.JsonSerializer.Serialize(stockChanges);
            var existingRefundLog = await _unitOfWork.RefundLogs.Query()
                .FirstOrDefaultAsync(rl => rl.OrderId == originalOrder.Id);

            if (existingRefundLog == null)
            {
                var refundLog = new RefundLog
                {
                    TenantId = _currentUser.TenantId,
                    BranchId = _currentUser.BranchId,
                    OrderId = originalOrder.Id,
                    UserId = userId,
                    RefundAmount = originalOrder.RefundAmount,
                    Reason = refundReason,
                    StockChangesJson = stockChangesJson
                };
                await _unitOfWork.RefundLogs.AddAsync(refundLog);
            }
            else
            {
                existingRefundLog.UserId = userId;
                existingRefundLog.RefundAmount = originalOrder.RefundAmount;
                existingRefundLog.Reason = string.IsNullOrWhiteSpace(existingRefundLog.Reason)
                    ? refundReason
                    : string.IsNullOrWhiteSpace(refundReason)
                        ? existingRefundLog.Reason
                        : existingRefundLog.Reason + " | " + refundReason;

                existingRefundLog.StockChangesJson = MergeStockChangesJson(
                    existingRefundLog.StockChangesJson,
                    stockChangesJson);
            }

            // Deduct loyalty points for refunded amount
            if (originalOrder.CustomerId.HasValue)
            {
                int pointsToDeduct = (int)Math.Floor(totalRefundAmount);
                await _customerService.DeductRefundStatsAsync(
                    originalOrder.CustomerId.Value,
                    totalRefundAmount,
                    pointsToDeduct
                );

                // âœ… FIX: Reduce customer's TotalDue if order had unpaid amount
                var debtToReduce = Math.Min(
                    originalOrder.AmountDue,
                    Math.Max(0, Math.Round(totalRefundAmount - paidRefundAmount, 2)));

                if (debtToReduce > 0)
                {
                    await _customerService.ReduceCreditBalanceAsync(
                        originalOrder.CustomerId.Value,
                        debtToReduce
                    );

                    originalOrder.AmountDue = Math.Max(0, Math.Round(originalOrder.AmountDue - debtToReduce, 2));
                }
            }

            await _unitOfWork.SaveChangesAsync();

            // INTEGRATION: Record cash register transaction for cash refunds
            // Calculate cash refund amount from original order's cash payments
            foreach (var allocation in refundPaymentAllocations)
            {
                var refundPayment = new Payment
                {
                    TenantId = originalOrder.TenantId,
                    BranchId = originalOrder.BranchId,
                    OrderId = returnOrder.Id,
                    Method = allocation.Method,
                    Amount = -allocation.Amount,
                    Reference = allocation.Reference,
                    WalletId = allocation.WalletId
                };

                await _unitOfWork.Payments.AddAsync(refundPayment);
                returnOrder.Payments.Add(refundPayment);
            }

            var originalCashPayments = refundPaymentAllocations
                .Where(a => a.Method == PaymentMethod.Cash)
                .Sum(a => a.Amount);

            if (originalCashPayments > 0)
            {
                var cashRefundAmount = originalCashPayments;

                if (cashRefundAmount > 0)
                {
                    var cashBalanceResponse = await _cashRegisterService.GetCurrentBalanceAsync(originalOrder.BranchId);
                    if (!cashBalanceResponse.Success)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<OrderDto>.Fail(
                            ErrorCodes.SYSTEM_INTERNAL_ERROR,
                            "ÙØ´Ù„ Ø§Ù„ØªØ­Ù‚Ù‚ Ù…Ù† Ø±ØµÙŠØ¯ Ø§Ù„Ø®Ø²ÙŠÙ†Ø© Ù‚Ø¨Ù„ Ø§Ù„Ù…Ø±ØªØ¬Ø¹");
                    }

                    if (cashBalanceResponse.Data!.CurrentBalance < cashRefundAmount)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<OrderDto>.Fail(
                            ErrorCodes.CASH_REGISTER_INSUFFICIENT_BALANCE,
                            "Ø±ØµÙŠØ¯ Ø§Ù„Ø®Ø²ÙŠÙ†Ø© ØºÙŠØ± ÙƒØ§ÙÙ Ù„ØªÙ†ÙÙŠØ° Ø§Ù„Ù…Ø±ØªØ¬Ø¹ Ø§Ù„Ù†Ù‚Ø¯ÙŠ");
                    }

                    await _cashRegisterService.RecordTransactionAsync(
                        type: CashRegisterTransactionType.Refund,
                        amount: cashRefundAmount, // âœ… POSITIVE amount - type determines sign
                        description: $"Ù…Ø±ØªØ¬Ø¹ - Ø·Ù„Ø¨ #{originalOrder.OrderNumber}",
                        referenceType: "Order",
                        referenceId: returnOrder.Id,
                        shiftId: currentShift?.Id
                    );
                }
            }

            foreach (var walletAllocation in refundPaymentAllocations.Where(a => a.Method == PaymentMethod.Wallet && a.WalletId.HasValue))
            {
                var walletRefund = await _walletService.RecordOrderRefundAsync(
                    walletAllocation.WalletId!.Value,
                    walletAllocation.Amount,
                    returnOrder.Id,
                    originalOrder.OrderNumber,
                    walletAllocation.Reference,
                    userId,
                    refundUser.Name,
                    CancellationToken.None);
                if (!walletRefund.Success)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<OrderDto>.Fail(walletRefund.ErrorCode ?? ErrorCodes.WALLET_INSUFFICIENT_BALANCE, walletRefund.Message);
                }
            }

            originalOrder.Notes = string.IsNullOrWhiteSpace(originalOrder.Notes)
                ? $"تم إنشاء مرتجع: #{returnOrder.OrderNumber}"
                : originalOrder.Notes + $" | ?? ????? ?????: #{returnOrder.OrderNumber}";

            await _unitOfWork.SaveChangesAsync();

            await transaction.CommitAsync();

            // Update original order notes with return order reference
            if (originalOrder.Notes?.Contains(returnOrder.OrderNumber) != true)
            originalOrder.Notes = string.IsNullOrWhiteSpace(originalOrder.Notes)
                ? $"تم إنشاء مرتجع: #{returnOrder.OrderNumber}"
                : originalOrder.Notes + $" | ?? ????? ?????: #{returnOrder.OrderNumber}";
            await _unitOfWork.SaveChangesAsync();

            var message = isPartialRefund
                ? $"ØªÙ… Ø¥Ù†Ø´Ø§Ø¡ ÙØ§ØªÙˆØ±Ø© Ø§Ù„Ù…Ø±ØªØ¬Ø¹ Ø§Ù„Ø¬Ø²Ø¦ÙŠ #{returnOrder.OrderNumber}"
                : $"ØªÙ… Ø¥Ù†Ø´Ø§Ø¡ ÙØ§ØªÙˆØ±Ø© Ø§Ù„Ù…Ø±ØªØ¬Ø¹ #{returnOrder.OrderNumber}";

            // Return the NEW return order (not the original)
            return ApiResponse<OrderDto>.Ok(MapToDto(returnOrder), message);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return ApiResponse<OrderDto>.Fail(ErrorCodes.SYSTEM_INTERNAL_ERROR,
                $"Ø­Ø¯Ø« Ø®Ø·Ø£ Ø£Ø«Ù†Ø§Ø¡ Ø§Ø³ØªØ±Ø¬Ø§Ø¹ Ø§Ù„Ø·Ù„Ø¨: {ex.Message}");
        }
    }

    private static ApiResponse<bool> ValidateStateTransition(OrderStatus currentStatus, OrderStatus newStatus)
    {
        if (!ValidTransitions.TryGetValue(currentStatus, out var validNextStates))
            return ApiResponse<bool>.Fail(ErrorCodes.ORDER_INVALID_STATE_TRANSITION, ErrorMessages.Get(ErrorCodes.ORDER_INVALID_STATE_TRANSITION));

        if (!validNextStates.Contains(newStatus))
            return ApiResponse<bool>.Fail(ErrorCodes.ORDER_INVALID_STATE_TRANSITION, ErrorMessages.Get(ErrorCodes.ORDER_INVALID_STATE_TRANSITION));

        return ApiResponse<bool>.Ok(true);
    }

    private static string MergeStockChangesJson(string? existingJson, string newJson)
    {
        var mergedEntries = new List<System.Text.Json.JsonElement>();

        if (!string.IsNullOrWhiteSpace(existingJson))
        {
            try
            {
                using var existingDoc = System.Text.Json.JsonDocument.Parse(existingJson);
                if (existingDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in existingDoc.RootElement.EnumerateArray())
                        mergedEntries.Add(item.Clone());
                }
            }
            catch
            {
                // Keep new entries only if existing payload is malformed.
            }
        }

        if (!string.IsNullOrWhiteSpace(newJson))
        {
            using var newDoc = System.Text.Json.JsonDocument.Parse(newJson);
            if (newDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in newDoc.RootElement.EnumerateArray())
                    mergedEntries.Add(item.Clone());
            }
        }

        return System.Text.Json.JsonSerializer.Serialize(mergedEntries);
    }

    private static decimal CalculateRefundAmountForItem(Order order, OrderItem item, decimal quantity)
    {
        if (order.Total <= 0 || item.Quantity <= 0 || quantity <= 0)
            return 0;

        var itemTotals = order.Items
            .Where(i => i.Quantity > 0 && i.Total > 0)
            .Sum(i => i.Total);

        if (itemTotals <= 0 || item.Total <= 0)
            return 0;

        var itemShare = item.Total / itemTotals;
        var quantityShare = quantity / item.Quantity;
        return Math.Round(order.Total * itemShare * quantityShare, 2);
    }

    private async Task<List<RefundPaymentAllocation>> BuildRefundPaymentAllocationsAsync(
        Order originalOrder,
        decimal totalRefundAmount,
        bool isFullRefund)
    {
        var originalGroups = originalOrder.Payments
            .Where(p => p.Amount > 0)
            .GroupBy(p => (p.Method, p.WalletId))
            .Select(g => new
            {
                g.Key.Method,
                g.Key.WalletId,
                Amount = Math.Round(g.Sum(p => p.Amount), 2),
                Reference = g.Select(p => p.Reference).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))
            })
            .Where(g => g.Amount > 0)
            .ToList();

        if (originalGroups.Count == 0 || totalRefundAmount <= 0 || originalOrder.Total <= 0)
            return new List<RefundPaymentAllocation>();

        var previousReturnOrders = await _unitOfWork.Orders.Query()
            .AsNoTracking()
            .Include(o => o.Payments)
            .Where(o => o.TenantId == originalOrder.TenantId
                     && o.BranchId == originalOrder.BranchId
                     && o.OriginalOrderId == originalOrder.Id
                     && o.OrderType == OrderType.Return
                     && !o.IsDeleted)
            .ToListAsync();

        var alreadyRefunded = previousReturnOrders
            .SelectMany(o => o.Payments)
            .Where(p => p.Amount < 0)
            .GroupBy(p => (p.Method, p.WalletId))
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(p => Math.Abs(p.Amount)), 2));

        var groupsWithCapacity = originalGroups
            .Select(g =>
            {
                alreadyRefunded.TryGetValue((g.Method, g.WalletId), out var refunded);
                return new
                {
                    g.Method,
                    g.WalletId,
                    g.Amount,
                    g.Reference,
                    Capacity = Math.Max(0, Math.Round(g.Amount - refunded, 2))
                };
            })
            .Where(g => g.Capacity > 0)
            .ToList();

        if (groupsWithCapacity.Count == 0)
            return new List<RefundPaymentAllocation>();

        var originalPaidTotal = Math.Round(originalGroups.Sum(g => g.Amount), 2);
        var remainingCapacity = Math.Round(groupsWithCapacity.Sum(g => g.Capacity), 2);
        var targetPaidRefund = isFullRefund
            ? Math.Min(totalRefundAmount, remainingCapacity)
            : Math.Min(
                Math.Round(totalRefundAmount * (originalPaidTotal / originalOrder.Total), 2),
                remainingCapacity);

        var allocations = new List<RefundPaymentAllocation>();
        var remaining = targetPaidRefund;

        foreach (var group in groupsWithCapacity)
        {
            if (remaining <= 0)
                break;

            var proportionalAmount = Math.Round(targetPaidRefund * (group.Amount / originalPaidTotal), 2);
            var amount = Math.Min(group.Capacity, Math.Min(proportionalAmount, remaining));
            if (amount <= 0)
                continue;

            allocations.Add(new RefundPaymentAllocation(group.Method, group.WalletId, amount, group.Reference));
            remaining = Math.Round(remaining - amount, 2);
        }

        foreach (var group in groupsWithCapacity)
        {
            if (remaining <= 0)
                break;

            var alreadyAllocated = allocations
                .Where(a => a.Method == group.Method && a.WalletId == group.WalletId)
                .Sum(a => a.Amount);
            var extraCapacity = Math.Round(group.Capacity - alreadyAllocated, 2);
            var extra = Math.Min(extraCapacity, remaining);
            if (extra <= 0)
                continue;

            var existingIndex = allocations.FindIndex(a => a.Method == group.Method && a.WalletId == group.WalletId);
            if (existingIndex >= 0)
            {
                var existing = allocations[existingIndex];
                allocations[existingIndex] = existing with { Amount = Math.Round(existing.Amount + extra, 2) };
            }
            else
            {
                allocations.Add(new RefundPaymentAllocation(group.Method, group.WalletId, extra, group.Reference));
            }

            remaining = Math.Round(remaining - extra, 2);
        }

        return allocations.Where(a => a.Amount > 0).ToList();
    }

    private async Task<ApiResponse<bool>> ValidateRecipeIngredientStockAsync(
        IEnumerable<(int ProductId, decimal Quantity, string ProductName)> items,
        int tenantId,
        int branchId,
        bool allowNegativeStock)
    {
        if (allowNegativeStock)
            return ApiResponse<bool>.Ok(true);

        var productQuantities = items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => new
            {
                Quantity = g.Sum(i => i.Quantity),
                ProductName = g.Select(i => i.ProductName).FirstOrDefault() ?? ""
            });

        if (productQuantities.Count == 0)
            return ApiResponse<bool>.Ok(true);

        var productIds = productQuantities.Keys.ToList();
        var recipes = await _unitOfWork.Recipes.Query()
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .ThenInclude(i => i.RawMaterial)
            .Where(r => r.TenantId == tenantId
                     && r.IsActive
                     && r.AutoDeductIngredients
                     && productIds.Contains(r.ProductId))
            .ToListAsync();

        if (recipes.Count == 0)
            return ApiResponse<bool>.Ok(true);

        var requiredByRawMaterial = new Dictionary<int, (string Name, decimal Quantity)>();

        foreach (var recipe in recipes)
        {
            if (!productQuantities.TryGetValue(recipe.ProductId, out var soldProduct))
                continue;

            if (recipe.YieldQuantity <= 0)
                return ApiResponse<bool>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.RawMaterial == null)
                    return ApiResponse<bool>.Fail(ErrorCodes.PRODUCT_NOT_FOUND, ErrorMessages.Get(ErrorCodes.PRODUCT_NOT_FOUND));

                decimal quantityInProductUnit;
                try
                {
                    quantityInProductUnit = NormalizeToProductUnit(
                        ingredient.Quantity,
                        ingredient.Unit,
                        ingredient.RawMaterial.Unit);
                }
                catch
                {
                    return ApiResponse<bool>.Fail(ErrorCodes.VALIDATION_ERROR, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));
                }

                var requiredQty = Math.Round((quantityInProductUnit / recipe.YieldQuantity) * soldProduct.Quantity, 4);
                if (requiredQty <= 0)
                    continue;

                if (requiredByRawMaterial.TryGetValue(ingredient.RawMaterialProductId, out var current))
                {
                    requiredByRawMaterial[ingredient.RawMaterialProductId] = (
                        current.Name,
                        Math.Round(current.Quantity + requiredQty, 4));
                }
                else
                {
                    requiredByRawMaterial[ingredient.RawMaterialProductId] = (
                        ingredient.RawMaterial.Name,
                        requiredQty);
                }
            }
        }

        if (requiredByRawMaterial.Count == 0)
            return ApiResponse<bool>.Ok(true);

        var rawMaterialIds = requiredByRawMaterial.Keys.ToList();
        var inventoryByRawMaterial = await _unitOfWork.BranchInventories.Query()
            .AsNoTracking()
            .Where(bi => bi.TenantId == tenantId
                      && bi.BranchId == branchId
                      && rawMaterialIds.Contains(bi.ProductId))
            .ToDictionaryAsync(bi => bi.ProductId, bi => bi.Quantity);

        foreach (var required in requiredByRawMaterial)
        {
            inventoryByRawMaterial.TryGetValue(required.Key, out var available);
            if (available < required.Value.Quantity)
            {
                return ApiResponse<bool>.Fail(
                    ErrorCodes.INSUFFICIENT_STOCK,
                    $"ÙƒÙ…ÙŠØ© ØºÙŠØ± ÙƒØ§ÙÙŠØ© Ù…Ù† '{required.Value.Name}' ÙÙŠ Ø§Ù„Ù…Ø®Ø²ÙˆÙ†. Ø§Ù„Ù…ØªØ§Ø­: {available}, Ø§Ù„Ù…Ø·Ù„ÙˆØ¨: {required.Value.Quantity}");
            }
        }

        return ApiResponse<bool>.Ok(true);
    }

    private static decimal NormalizeToProductUnit(decimal quantity, UnitOfMeasure fromUnit, UnitOfMeasure productUnit)
    {
        if (fromUnit == productUnit)
            return quantity;

        if (IsWeight(fromUnit) && IsWeight(productUnit))
        {
            var grams = fromUnit == UnitOfMeasure.Kilogram ? quantity * 1000m : quantity;
            return productUnit == UnitOfMeasure.Kilogram ? grams / 1000m : grams;
        }

        if (IsVolume(fromUnit) && IsVolume(productUnit))
        {
            var milliliters = fromUnit == UnitOfMeasure.Liter ? quantity * 1000m : quantity;
            return productUnit == UnitOfMeasure.Liter ? milliliters / 1000m : milliliters;
        }

        throw new InvalidOperationException("Incompatible recipe ingredient unit.");
    }

    private static bool IsWeight(UnitOfMeasure unit)
        => unit is UnitOfMeasure.Kilogram or UnitOfMeasure.Gram;

    private static bool IsVolume(UnitOfMeasure unit)
        => unit is UnitOfMeasure.Liter or UnitOfMeasure.Milliliter;

    /// <summary>
    /// Calculate item totals including tax amount with proper rounding.
    /// Tax Exclusive (Additive): Price is NET (excluding tax), tax is added on top.
    ///
    /// Formulas:
    ///   NetTotal = UnitPrice * Quantity
    ///   TaxAmount = NetTotal * (TaxRate / 100)
    ///   Total = NetTotal + TaxAmount
    ///
    /// Example (100 EGP Net with 14% VAT):
    ///   NetTotal = 100 EGP
    ///   TaxAmount = 100 * 0.14 = 14 EGP
    ///   Total = 100 + 14 = 114 EGP
    /// </summary>
    private static void CalculateItemTotals(OrderItem item)
    {
        var grossSubtotal = Math.Round(item.UnitPrice * item.Quantity, 2);
        item.Subtotal = grossSubtotal;
        item.DiscountType = NormalizeDiscountType(item.DiscountType);

        // Apply discount if any
        if (item.DiscountType == "percentage" && item.DiscountValue.HasValue)
        {
            var percentageDiscount = Math.Clamp(item.DiscountValue.Value, 0m, 100m);
            item.DiscountAmount = Math.Round(grossSubtotal * (percentageDiscount / 100m), 2);
        }
        else if (item.DiscountType == "fixed" && item.DiscountValue.HasValue)
        {
            var fixedDiscount = Math.Clamp(item.DiscountValue.Value, 0m, grossSubtotal);
            item.DiscountAmount = Math.Round(fixedDiscount, 2);
        }
        else
            item.DiscountAmount = 0;

        // Net amount after discount
        var netAfterDiscount = Math.Round(grossSubtotal - item.DiscountAmount, 2);

        // Tax Exclusive (Additive): Calculate tax and add on top
        // TaxAmount = NetAmount * (TaxRate / 100)
        item.TaxAmount = Math.Round(netAfterDiscount * (item.TaxRate / 100m), 2);

        // Total = Net + Tax
        item.Total = Math.Round(netAfterDiscount + item.TaxAmount, 2);
    }

    private static void CalculateOrderTotals(Order order)
    {
        // Subtotal = Sum of gross item subtotals before discounts
        order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal), 2);
        order.DiscountType = NormalizeDiscountType(order.DiscountType);

        var itemDiscountsTotal = Math.Round(order.Items.Sum(i => i.DiscountAmount), 2);
        var netAfterItemDiscounts = Math.Round(order.Subtotal - itemDiscountsTotal, 2);

        // Apply order-level discount after item-level discounts
        if (order.DiscountType == "percentage" && order.DiscountValue.HasValue)
        {
            var percentageDiscount = Math.Clamp(order.DiscountValue.Value, 0m, 100m);
            order.DiscountAmount = Math.Round(netAfterItemDiscounts * (percentageDiscount / 100m), 2);
        }
        else if (order.DiscountType == "fixed" && order.DiscountValue.HasValue)
        {
            var fixedDiscount = Math.Clamp(order.DiscountValue.Value, 0m, netAfterItemDiscounts);
            order.DiscountAmount = Math.Round(fixedDiscount, 2);
        }
        else
            order.DiscountAmount = 0;

        if (order.DiscountAmount > netAfterItemDiscounts)
            order.DiscountAmount = netAfterItemDiscounts;

        var afterDiscount = netAfterItemDiscounts - order.DiscountAmount;

        // P0-4: Tax = SUM of per-item taxes (respects product-specific tax rates).
        // If there's an order-level discount, scale item taxes proportionally.
        if (order.DiscountAmount > 0 && netAfterItemDiscounts > 0)
        {
            // Each item's tax is reduced proportionally by the discount ratio.
            // Example: 10% order discount â†’ each item's taxable amount is 90% of its net after item discount.
            var discountRatio = order.DiscountAmount / netAfterItemDiscounts;
            order.TaxAmount = Math.Round(order.Items.Sum(item =>
            {
                var itemNetAfterItemDiscount = item.Subtotal - item.DiscountAmount;
                var itemAfterDiscount = itemNetAfterItemDiscount * (1m - discountRatio);
                return itemAfterDiscount * (item.TaxRate / 100m);
            }), 2);
        }
        else
        {
            // No order-level discount: tax = simple sum of item.TaxAmount
            order.TaxAmount = Math.Round(order.Items.Sum(i => i.TaxAmount), 2);
        }

        // Service charge on net amount after discount
        order.ServiceChargeAmount = Math.Round(afterDiscount * (order.ServiceChargePercent / 100m), 2);

        // Total = (Subtotal - Discount) + Tax + Service Charge + Delivery Fee
        order.Total = Math.Round(afterDiscount + order.TaxAmount + order.ServiceChargeAmount + order.DeliveryFee, 2);
        order.AmountDue = Math.Round(order.Total - order.AmountPaid, 2);
    }

    private static string? NormalizeDiscountType(string? discountType)
    {
        if (string.IsNullOrWhiteSpace(discountType))
            return null;

        var normalized = discountType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "percentage" => "percentage",
            "fixed" => "fixed",
            _ => normalized
        };
    }

    private static (bool Success, string? Message) ValidateDiscount(
        string? discountType,
        decimal? discountValue,
        decimal maxFixedAmount)
    {
        var normalizedType = NormalizeDiscountType(discountType);
        if (normalizedType == null && !discountValue.HasValue)
            return (true, null);

        if (normalizedType != "percentage" && normalizedType != "fixed")
            return (false, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        if (!discountValue.HasValue || discountValue.Value < 0)
            return (false, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        if (normalizedType == "percentage" && discountValue.Value > 100)
            return (false, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        if (normalizedType == "fixed" && discountValue.Value > Math.Round(Math.Max(0, maxFixedAmount), 2))
            return (false, ErrorMessages.Get(ErrorCodes.VALIDATION_ERROR));

        return (true, null);
    }

    private static bool RequestContainsAnyDiscount(CreateOrderRequest request)
    {
        if (RequestContainsDiscount(request.DiscountType, request.DiscountValue))
            return true;

        return request.Items.Any(item => RequestContainsDiscount(item.DiscountType, item.DiscountValue));
    }

    private static bool RequestContainsDiscount(string? discountType, decimal? discountValue)
        => !string.IsNullOrWhiteSpace(discountType) || discountValue.HasValue;

    /// <summary>
    /// Resolves the selling price with priority: 1) ProductBatch.SellingPrice, 2) BranchProductPrice.Price, 3) Product.Price
    /// </summary>
    private async Task<(decimal price, int? batchId, string? batchNumber, DateTime? expiryDate, decimal? batchCost, (string ErrorCode, string Message)? error)> ResolveBatchSaleSnapshotAsync(
        int productId, int branchId, int tenantId, decimal defaultPrice, int? preferredBatchId = null)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        
        if (product != null && product.IsBatchTracked)
        {
            var batchQuery = _unitOfWork.ProductBatches.Query()
                .Where(pb => pb.TenantId == tenantId
                    && pb.BranchId == branchId
                    && pb.ProductId == productId
                    && !pb.IsDeleted
                    && pb.Status == BatchStatus.Active
                    && pb.Quantity > 0);

            var oldestBatch = await batchQuery
                .OrderBy(pb => pb.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(pb => pb.PurchaseDate)
                .ThenBy(pb => pb.CreatedAt)
                .ThenBy(pb => pb.Id)
                .FirstOrDefaultAsync();

            if (oldestBatch == null)
            {
                var legacyBranchPrice = await _inventoryService.GetEffectivePriceAsync(productId, branchId);
                return (legacyBranchPrice > 0 ? legacyBranchPrice : defaultPrice, null, null, null, null, null);
            }

            if (oldestBatch == null)
                return (defaultPrice, null, null, null, null,
                    (ErrorCodes.INSUFFICIENT_STOCK, "Ù„Ø§ ØªÙˆØ¬Ø¯ Ø¯ÙØ¹Ø§Øª Ù…ØªØ§Ø­Ø© Ù„Ù‡Ø°Ø§ Ø§Ù„Ù…Ù†ØªØ¬ ÙÙŠ Ø§Ù„ÙØ±Ø¹ Ø§Ù„Ø­Ø§Ù„ÙŠ"));

            ProductBatch selectedBatch = oldestBatch;
            if (preferredBatchId.HasValue)
            {
                var requestedBatch = await batchQuery
                    .FirstOrDefaultAsync(pb => pb.Id == preferredBatchId.Value);

                if (requestedBatch == null)
                {
                    return (defaultPrice, null, null, null, null,
                        (ErrorCodes.VALIDATION_ERROR, "Ø§Ù„Ø¨Ø§ØªØ´ Ø§Ù„Ù…Ø­Ø¯Ø¯ ØºÙŠØ± Ù…ØªØ§Ø­ Ø£Ùˆ ØºÙŠØ± ØµØ§Ù„Ø­"));
                }

                if (requestedBatch.Id != oldestBatch.Id)
                {
                    var canChangeBatch = await _permissionService.HasPermissionAsync(_currentUser.UserId, Permission.PosChangeBatch);
                    if (!canChangeBatch)
                    {
                        return (defaultPrice, null, null, null, null,
                            (ErrorCodes.INSUFFICIENT_PRIVILEGES, ErrorMessages.Get(ErrorCodes.INSUFFICIENT_PRIVILEGES)));
                    }
                }

                selectedBatch = requestedBatch;
            }

            var price = selectedBatch.SellingPrice ?? defaultPrice;
            return (price, selectedBatch.Id, selectedBatch.BatchNumber, selectedBatch.ExpiryDate, selectedBatch.CostPrice, null);
        }

        var branchPrice = await _inventoryService.GetEffectivePriceAsync(productId, branchId);
        if (branchPrice > 0 && branchPrice != defaultPrice)
            return (branchPrice, null, null, null, null, null);

        return (defaultPrice, null, null, null, null, null);
    }

    private static decimal ResolveNetUnitPrice(decimal configuredPrice)
    {
        return configuredPrice;
    }

    private static string GenerateOrderNumber()
        => $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

    private static string GenerateReturnOrderNumber()
        => $"RET-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

    private static OrderDto MapToDto(Order order) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        Status = order.Status.ToString(),
        OrderType = order.OrderType.ToString(),
        TableId = order.TableId,
        TableNumberSnapshot = order.TableNumberSnapshot,
        OrderSource = order.OrderSource.ToString(),
        ExternalOrderNumber = order.ExternalOrderNumber,
        KitchenPrintCount = order.KitchenPrintCount,
        LastKitchenPrintedAt = order.LastKitchenPrintedAt,
        // Branch Snapshot
        BranchId = order.BranchId,
        BranchName = order.BranchName,
        BranchAddress = order.BranchAddress,
        BranchPhone = order.BranchPhone,
        // Currency
        CurrencyCode = order.CurrencyCode,
        // Totals
        Subtotal = order.Subtotal,
        DiscountType = order.DiscountType,
        DiscountValue = order.DiscountValue,
        DiscountAmount = order.DiscountAmount,
        DiscountCode = order.DiscountCode,
        TaxRate = order.TaxRate,
        TaxAmount = order.TaxAmount,
        ServiceChargePercent = order.ServiceChargePercent,
        ServiceChargeAmount = order.ServiceChargeAmount,
        Total = order.Total,
        AmountPaid = order.AmountPaid,
        AmountDue = order.AmountDue,
        ChangeAmount = order.ChangeAmount,
        // Customer
        CustomerName = order.CustomerName,
        CustomerPhone = order.CustomerPhone,
        CustomerId = order.CustomerId,
        Notes = order.Notes,
        // User
        UserId = order.UserId,
        UserName = order.UserName,
        // Shift
        ShiftId = order.ShiftId,
        // Timestamps
        CreatedAt = order.CreatedAt,
        CompletedAt = order.CompletedAt,
        CancelledAt = order.CancelledAt,
        CancellationReason = order.CancellationReason,
        // Refund Information
        RefundedAt = order.RefundedAt,
        RefundReason = order.RefundReason,
        RefundAmount = order.RefundAmount,
        RefundedByUserId = order.RefundedByUserId,
        RefundedByUserName = order.RefundedByUserName,
        OriginalOrderId = order.OriginalOrderId,
        // Delivery fields
        DeliveryPersonId = order.DeliveryPersonId,
        DeliveryPersonName = order.DeliveryPerson?.Name,
        DeliveryAddress = order.DeliveryAddress,
        DeliveryFee = order.DeliveryFee,
        DeliveryStatus = order.DeliveryStatus?.ToString(),
        DeliveryNotes = order.DeliveryNotes,
        AssignedAt = order.AssignedAt,
        DeliveredAt = order.DeliveredAt,
        Items = order.Items.Select(i => new OrderItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId, // Now nullable in both Entity and DTO
            // Custom Item Fields
            IsCustomItem = i.IsCustomItem,
            CustomName = i.CustomName,
            CustomUnitPrice = i.CustomUnitPrice,
            CustomTaxRate = i.CustomTaxRate,
            // Product Snapshot
            ProductName = i.ProductName,
            ProductNameEn = i.ProductNameEn,
            ProductSku = i.ProductSku,
            ProductBarcode = i.ProductBarcode,
            UnitPrice = i.UnitPrice,
            OriginalPrice = i.OriginalPrice,
            Quantity = i.Quantity,
            ParentOrderItemId = i.ParentOrderItemId,
            KitchenPrintedQuantity = i.KitchenPrintedQuantity,
            LastKitchenPrintedAt = i.LastKitchenPrintedAt,
            RefundedQuantity = i.RefundedQuantity,
            DiscountType = i.DiscountType,
            DiscountValue = i.DiscountValue,
            DiscountAmount = i.DiscountAmount,
            DiscountReason = i.DiscountReason,
            TaxRate = i.TaxRate,
            TaxAmount = i.TaxAmount,
            TaxInclusive = i.TaxInclusive,
            Subtotal = i.Subtotal,
            Total = i.Total,
            Notes = i.Notes,
            // Batch Info
            BatchId = i.BatchId,
            BatchNumber = i.BatchNumber,
            ExpiryDate = i.ExpiryDate
        }).ToList(),
        Payments = order.Payments?.Select(p => new PaymentDto
        {
            Id = p.Id,
            Method = p.Method.ToString(),
            Amount = p.Amount,
            Reference = p.Reference,
            CreatedAt = p.CreatedAt
        }).ToList() ?? new()
    };

    /// <summary>
    /// Mark a delivery order as delivered and complete it
    /// </summary>
    public async Task<ApiResponse<OrderDto>> MarkAsDeliveredAsync(int orderId)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.DeliveryPerson)
            .FirstOrDefaultAsync(o => o.Id == orderId
                && o.TenantId == _currentUser.TenantId
                && o.BranchId == _currentUser.BranchId);

        if (order == null)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));

        // Validate this is a delivery order
        if (order.OrderType != OrderType.Delivery)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_INVALID_STATE_TRANSITION, "Ù‡Ø°Ø§ Ø§Ù„Ø·Ù„Ø¨ Ù„ÙŠØ³ Ø·Ù„Ø¨ ØªÙˆØµÙŠÙ„");

        if (order.Status != OrderStatus.Completed)
            return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_INVALID_STATE_TRANSITION, ErrorMessages.Get(ErrorCodes.ORDER_INVALID_STATE_TRANSITION));

        order.Status = OrderStatus.Completed;
        order.DeliveryStatus = DeliveryStatus.Delivered;
        order.DeliveredAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(MapToDto(order), "ØªÙ… ØªØ³Ù„ÙŠÙ… Ø§Ù„Ø·Ù„Ø¨ Ø¨Ù†Ø¬Ø§Ø­");
    }
}
