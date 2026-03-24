# 🏪 KasserPro Sellable V1 - Implementation Plan

> **الهدف:** تحويل النظام من "Restaurant MVP" إلى "Generic Retail System"  
> **التاريخ:** يناير 2026  
> **المؤلف:** Solution Architect  
> **الحالة:** 📋 Strategy Document

---

## 📋 Executive Summary

هذه الخطة تغطي ثلاث ميزات أساسية مطلوبة لجعل النظام قابلاً للبيع للصيدليات ومتاجر الملابس وتجار التجزئة:

| Feature                 | Business Value            | Complexity |
| ----------------------- | ------------------------- | ---------- |
| **Stock Control**       | إدارة المخزون تلقائياً    | Medium     |
| **Customer Management** | بناء قاعدة بيانات العملاء | Low        |
| **Refunds**             | استرجاع المنتجات والأموال | High       |

### Critical Business Rules (يجب الالتزام بها)

1. ✅ **Inventory Model:** Simple SKU (المقاسات = منتجات منفصلة)
2. ✅ **Stock Behavior:** Auto-decrement on Complete + **ALLOW negative stock**
3. ✅ **Customers:** Auto-create by Phone if not exists
4. ✅ **Refunds:** Restore Stock + Update Status + Store Reason

---

## 🗄️ 1. Database Schema Changes

### 1.1 Product Entity Modifications

```
Current Product Fields:
✅ TrackInventory (bool) - Already exists
✅ StockQuantity (int?) - Already exists
❌ LowStockThreshold - NEEDS TO BE ADDED
❌ ReorderPoint - NEEDS TO BE ADDED (optional)
```

**New Fields to Add:**

| Field               | Type        | Default | Description                  |
| ------------------- | ----------- | ------- | ---------------------------- |
| `LowStockThreshold` | `int?`      | `null`  | Alert when stock falls below |
| `ReorderPoint`      | `int?`      | `null`  | Suggested reorder level      |
| `LastStockUpdate`   | `DateTime?` | `null`  | Track inventory changes      |

**Rationale:**

- `LowStockThreshold` enables "Low Stock Alerts" dashboard
- `ReorderPoint` useful for future purchase orders feature
- `LastStockUpdate` helps with audit trail

### 1.2 New Customer Entity

```csharp
// NEW: src/KasserPro.Domain/Entities/Customer.cs
public class Customer : BaseEntity
{
    public int TenantId { get; set; }

    // Primary Identifier (UNIQUE per Tenant)
    public string Phone { get; set; } = string.Empty;

    // Profile
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    // Stats (denormalized for performance)
    public int TotalOrders { get; set; } = 0;
    public decimal TotalSpent { get; set; } = 0;
    public DateTime? LastOrderAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
```

**Design Decisions:**

- Phone is the primary lookup key (Egyptian market standard)
- `TotalOrders` and `TotalSpent` are denormalized to avoid COUNT queries
- Customer is **optional** on orders (for walk-in customers)

### 1.3 Refund Tracking in Order Entity

**Option Analysis:**

| Approach                                   | Pros                          | Cons              |
| ------------------------------------------ | ----------------------------- | ----------------- |
| **A: Add fields to Order**                 | Simple, no new tables         | Mixes concerns    |
| **B: New Refund Entity**                   | Clean separation, audit trail | More complexity   |
| **C: Hybrid (Order fields + RefundItems)** | Best of both                  | Medium complexity |

**Recommendation: Option C (Hybrid)**

**New Fields on Order:**

| Field                | Type        | Description                                        |
| -------------------- | ----------- | -------------------------------------------------- |
| `RefundedAt`         | `DateTime?` | When refund was processed                          |
| `RefundReason`       | `string?`   | Why the refund was issued                          |
| `RefundedByUserId`   | `int?`      | Who processed the refund                           |
| `RefundedByUserName` | `string?`   | User name snapshot                                 |
| `RefundAmount`       | `decimal`   | Total refunded (may differ from Total for partial) |

**New RefundLog Entity (for audit):**

```csharp
// NEW: src/KasserPro.Domain/Entities/RefundLog.cs
public class RefundLog : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public int OrderId { get; set; }
    public int UserId { get; set; }

    public decimal RefundAmount { get; set; }
    public string Reason { get; set; } = string.Empty;

    // Stock Changes (JSON for audit)
    public string? StockChangesJson { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public User User { get; set; } = null!;
}
```

**Rationale:**

- `RefundLog` provides complete audit trail
- `StockChangesJson` stores which products had stock restored
- Enables future partial refunds extension

### 1.4 Stock Movement Entity (Audit Trail)

```csharp
// NEW: src/KasserPro.Domain/Entities/StockMovement.cs
public class StockMovement : BaseEntity
{
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public int ProductId { get; set; }

    public StockMovementType Type { get; set; }
    public int Quantity { get; set; } // Positive = in, Negative = out
    public int? ReferenceId { get; set; } // OrderId, RefundId, etc.
    public string? ReferenceType { get; set; } // "Order", "Refund", "Adjustment"

    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }

    public string? Notes { get; set; }
    public int UserId { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public User User { get; set; } = null!;
}

// NEW: src/KasserPro.Domain/Enums/StockMovementType.cs
public enum StockMovementType
{
    Sale = 1,           // Order completed (decrease)
    Refund = 2,         // Order refunded (increase)
    Adjustment = 3,     // Manual adjustment
    Receiving = 4,      // Stock received from supplier
    Damage = 5,         // Damaged goods
    Transfer = 6        // Branch transfer
}
```

**Rationale:**

- Complete audit trail for every stock change
- Enables stock history reports
- Required for accounting/inventory reconciliation

### 1.5 Database Indexes

```sql
-- Customer lookups by phone (most common query)
CREATE UNIQUE INDEX IX_Customers_TenantId_Phone
ON Customers(TenantId, Phone) WHERE IsDeleted = 0;

-- Product barcode lookup (POS scanning)
CREATE INDEX IX_Products_TenantId_Barcode
ON Products(TenantId, Barcode) WHERE Barcode IS NOT NULL AND IsDeleted = 0;

-- Product SKU lookup
CREATE INDEX IX_Products_TenantId_Sku
ON Products(TenantId, Sku) WHERE Sku IS NOT NULL AND IsDeleted = 0;

-- Low stock query
CREATE INDEX IX_Products_LowStock
ON Products(TenantId, StockQuantity, LowStockThreshold)
WHERE TrackInventory = 1 AND IsDeleted = 0;

-- Stock movements by product
CREATE INDEX IX_StockMovements_ProductId_CreatedAt
ON StockMovements(ProductId, CreatedAt DESC);

-- Refund logs by order
CREATE INDEX IX_RefundLogs_OrderId ON RefundLogs(OrderId);
```

### 1.6 Migration Summary

| Entity        | Action               | Priority |
| ------------- | -------------------- | -------- |
| Product       | ALTER (add 3 fields) | P0       |
| Customer      | CREATE               | P0       |
| Order         | ALTER (add 5 fields) | P1       |
| RefundLog     | CREATE               | P1       |
| StockMovement | CREATE               | P1       |

---

## 🔧 2. Backend Architecture

### 2.1 Service Layer Changes

#### New Services Required

```
Services/
├── Implementations/
│   ├── CustomerService.cs      # NEW
│   ├── InventoryService.cs     # NEW
│   ├── RefundService.cs        # NEW (or extend OrderService)
│   └── OrderService.cs         # MODIFY
└── Interfaces/
    ├── ICustomerService.cs     # NEW
    ├── IInventoryService.cs    # NEW
    └── IRefundService.cs       # NEW
```

### 2.2 InventoryService Design

```csharp
public interface IInventoryService
{
    // Stock Operations
    Task<ApiResponse<bool>> DecrementStockAsync(int productId, int quantity,
        int? orderId = null, string? notes = null);

    Task<ApiResponse<bool>> IncrementStockAsync(int productId, int quantity,
        int? orderId = null, string? notes = null);

    Task<ApiResponse<bool>> AdjustStockAsync(int productId, int newQuantity,
        string reason);

    // Queries
    Task<ApiResponse<List<ProductDto>>> GetLowStockProductsAsync();
    Task<ApiResponse<List<StockMovementDto>>> GetStockHistoryAsync(int productId);
}
```

**Critical Logic: Stock Decrement on Order Complete**

```
Flow:
1. OrderService.CompleteAsync() called
2. Start Transaction
3. Add Payments
4. Update Order Status
5. FOR EACH OrderItem:
   - IF Product.TrackInventory = true
   - Call InventoryService.DecrementStockAsync()
   - Record StockMovement
   - NOTE: Do NOT block if stock goes negative
6. Commit Transaction
```

**Why Allow Negative Stock?**

- Egyptian retail reality: Sales should NEVER be blocked
- Physical stock count may differ from system
- Better to sell and reconcile later
- Admin can see negative stock in reports

### 2.3 CustomerService Design

```csharp
public interface ICustomerService
{
    // CRUD
    Task<ApiResponse<CustomerDto>> CreateAsync(CreateCustomerRequest request);
    Task<ApiResponse<CustomerDto>> GetByIdAsync(int id);
    Task<ApiResponse<CustomerDto>> GetByPhoneAsync(string phone);
    Task<ApiResponse<List<CustomerDto>>> SearchAsync(string query);
    Task<ApiResponse<CustomerDto>> UpdateAsync(int id, UpdateCustomerRequest request);

    // Auto-create (for Order flow)
    Task<ApiResponse<CustomerDto>> GetOrCreateByPhoneAsync(string phone, string? name = null);

    // Stats
    Task UpdateCustomerStatsAsync(int customerId, decimal orderTotal);
}
```

**Auto-Create Flow in OrderService:**

```
When: OrderService.CompleteAsync() with CustomerPhone
Flow:
1. Check if Customer exists by Phone
2. If NOT exists → Create new Customer with Phone
3. Link Order.CustomerId to Customer
4. After order complete → Update Customer stats
```

### 2.4 RefundService Design

```csharp
public interface IRefundService
{
    Task<ApiResponse<OrderDto>> ProcessFullRefundAsync(int orderId, RefundRequest request);
    // Future: Task<ApiResponse<OrderDto>> ProcessPartialRefundAsync(...)
}

public class RefundRequest
{
    public string Reason { get; set; } = string.Empty;
    public bool RestoreStock { get; set; } = true;
}
```

**Refund Transaction Flow:**

```
ProcessFullRefundAsync(orderId, request):
1. BEGIN TRANSACTION
2. Validate order can be refunded (Status = Completed)
3. FOR EACH OrderItem:
   - IF RestoreStock AND Product.TrackInventory
   - Call InventoryService.IncrementStockAsync()
   - Record StockMovement (Type = Refund)
4. Update Order:
   - Status = Refunded
   - RefundedAt = now
   - RefundReason = request.Reason
   - RefundedByUserId = currentUser
5. Create RefundLog entry
6. Update Shift totals (if applicable)
7. COMMIT TRANSACTION
```

**Critical: Money Return**

- For V1, refund is "recorded" but money return is manual (cash back to customer)
- Future: Integration with payment gateways for Card refunds

### 2.5 OrderService Modifications

```csharp
// MODIFY: CompleteAsync() to include stock decrement
public async Task<ApiResponse<OrderDto>> CompleteAsync(int orderId, CompleteOrderRequest request)
{
    await using var transaction = await _unitOfWork.BeginTransactionAsync();
    try
    {
        // ... existing validation ...

        // NEW: Decrement stock for each item
        foreach (var item in order.Items)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
            if (product?.TrackInventory == true)
            {
                await _inventoryService.DecrementStockAsync(
                    product.Id,
                    item.Quantity,
                    orderId: order.Id,
                    notes: $"Order {order.OrderNumber}"
                );
            }
        }

        // NEW: Handle Customer auto-create
        if (!string.IsNullOrEmpty(request.CustomerPhone))
        {
            var customer = await _customerService.GetOrCreateByPhoneAsync(
                request.CustomerPhone,
                request.CustomerName
            );
            if (customer.Success)
            {
                order.CustomerId = customer.Data!.Id;
                order.CustomerName = customer.Data.Name;
                order.CustomerPhone = customer.Data.Phone;
            }
        }

        // ... existing completion logic ...

        // NEW: Update customer stats
        if (order.CustomerId.HasValue)
        {
            await _customerService.UpdateCustomerStatsAsync(order.CustomerId.Value, order.Total);
        }

        await transaction.CommitAsync();
        return ApiResponse<OrderDto>.Ok(MapToDto(order));
    }
    catch { ... }
}
```

### 2.6 New API Endpoints

#### Inventory Endpoints

| Method | Endpoint                           | Description              |
| ------ | ---------------------------------- | ------------------------ |
| GET    | `/api/products/low-stock`          | Products below threshold |
| GET    | `/api/products/{id}/stock-history` | Stock movement history   |
| POST   | `/api/products/{id}/stock-adjust`  | Manual stock adjustment  |

#### Customer Endpoints

| Method | Endpoint                          | Description                |
| ------ | --------------------------------- | -------------------------- |
| GET    | `/api/customers`                  | List customers (paginated) |
| GET    | `/api/customers/{id}`             | Get customer by ID         |
| GET    | `/api/customers/search?q={query}` | Search by phone/name       |
| POST   | `/api/customers`                  | Create customer            |
| PUT    | `/api/customers/{id}`             | Update customer            |

#### Refund Endpoints

| Method | Endpoint                  | Description              |
| ------ | ------------------------- | ------------------------ |
| POST   | `/api/orders/{id}/refund` | Process full refund      |
| GET    | `/api/refunds`            | List refunds (paginated) |
| GET    | `/api/reports/refunds`    | Refunds report           |

### 2.7 Validation Rules

**Refund Validation:**

```
- Order.Status MUST be Completed
- Order.RefundedAt MUST be null (prevent double refund)
- User MUST have permission (Admin or original cashier?)
- Order age limit? (e.g., within 30 days) - configurable
```

**Stock Adjustment Validation:**

```
- Only Admin can adjust stock
- Reason is REQUIRED
- Creates AuditLog entry
```

---

## 🎨 3. Frontend UX Changes

### 3.1 Barcode Scanning in POS

**Current State:** Products are clicked from grid

**New Behavior:**

```
POS Screen Changes:
1. Add "Search/Scan" input field at top of ProductGrid
2. Input accepts:
   - Barcode (exact match)
   - SKU (exact match)
   - Product name (fuzzy search)

3. Behavior:
   - On barcode scan (fast input + Enter)
   - Call: GET /api/products/lookup?barcode={value}
   - If found → Add to cart immediately
   - If not found → Show "Product not found" toast

4. Focus Management:
   - Auto-focus search input on page load
   - After adding product → return focus to search
   - Escape key → clear input
```

**Component Structure:**

```
client/src/components/pos/
├── BarcodeInput.tsx       # NEW: Search/scan input
├── ProductGrid.tsx        # MODIFY: Filter by search
└── ProductSearchResults.tsx # NEW: Dropdown results
```

### 3.2 Low Stock Alerts

**Display Locations:**

| Location      | Display Type     | When                 |
| ------------- | ---------------- | -------------------- |
| Dashboard     | Alert Card       | Always visible       |
| Products Page | Badge on row     | If low stock         |
| POS Grid      | Subtle indicator | If low stock         |
| Product Modal | Warning message  | If stock < threshold |

**Dashboard Alert Card:**

```
┌─────────────────────────────────────────┐
│ ⚠️ Low Stock Alert                       │
│ 5 products are below minimum stock      │
│                                         │
│ • Product A (Stock: 2, Min: 5)          │
│ • Product B (Stock: 0, Min: 3)          │
│ [View All Low Stock Products]           │
└─────────────────────────────────────────┘
```

**POS Grid Indicator:**

```tsx
// ProductCard.tsx
{
  product.trackInventory && product.stockQuantity !== null && (
    <div
      className={clsx(
        "absolute top-2 right-2 text-xs px-2 py-1 rounded",
        product.stockQuantity <= 0
          ? "bg-red-100 text-red-600"
          : product.stockQuantity <= (product.lowStockThreshold ?? 5)
          ? "bg-yellow-100 text-yellow-600"
          : "bg-green-100 text-green-600"
      )}
    >
      {product.stockQuantity <= 0 ? "نفد" : `${product.stockQuantity} متبقي`}
    </div>
  );
}
```

### 3.3 Refund Flow UI

**Entry Point:** Order Details Modal → "استرجاع" Button

**Flow:**

```
Step 1: View Completed Order
┌─────────────────────────────────────────┐
│ Order #ORD-20260108-ABC123              │
│ Status: ✅ Completed                     │
│                                         │
│ Items:                                  │
│ • Product A x2     200.00 EGP           │
│ • Product B x1     150.00 EGP           │
│                                         │
│ Total: 350.00 EGP                       │
│                                         │
│ [🔄 استرجاع]  [🖨️ طباعة]  [❌ إغلاق]    │
└─────────────────────────────────────────┘

Step 2: Refund Confirmation Modal
┌─────────────────────────────────────────┐
│ ⚠️ تأكيد الاسترجاع                       │
│                                         │
│ سيتم استرجاع المبلغ التالي:              │
│ 350.00 EGP                              │
│                                         │
│ سبب الاسترجاع: *                         │
│ ┌─────────────────────────────────────┐ │
│ │ منتج تالف                           │ │
│ └─────────────────────────────────────┘ │
│                                         │
│ ☑️ إعادة المنتجات للمخزون               │
│                                         │
│ [إلغاء]              [تأكيد الاسترجاع]  │
└─────────────────────────────────────────┘

Step 3: Success
┌─────────────────────────────────────────┐
│ ✅ تم الاسترجاع بنجاح                    │
│                                         │
│ Order #ORD-20260108-ABC123              │
│ Status: 🔄 Refunded                     │
│                                         │
│ المبلغ المسترد: 350.00 EGP              │
│ المخزون: تم التحديث ✓                   │
│                                         │
│ [طباعة إيصال الاسترجاع]  [إغلاق]        │
└─────────────────────────────────────────┘
```

**Refund Button Visibility:**

```typescript
// Only show for:
// 1. Order status = Completed
// 2. Order NOT already refunded
// 3. User is Admin OR order is from same day
const canRefund =
  order.status === "Completed" &&
  !order.refundedAt &&
  (isAdmin || isToday(order.completedAt));
```

### 3.4 Customer Selection in Cart

**Cart Section Update:**

```
┌─────────────────────────────────────────┐
│ 🛒 السلة                                │
├─────────────────────────────────────────┤
│ 👤 العميل (اختياري)                     │
│ ┌─────────────────────────────────────┐ │
│ │ 🔍 ابحث برقم الهاتف...               │ │
│ └─────────────────────────────────────┘ │
│                                         │
│ Product A x2                200.00      │
│ Product B x1                150.00      │
│                                         │
│ الإجمالي:                   350.00      │
│                                         │
│ [💳 ادفع]                               │
└─────────────────────────────────────────┘

Customer Search Dropdown:
┌─────────────────────────────────────────┐
│ 🔍 01012345678                          │
├─────────────────────────────────────────┤
│ 📱 01012345678 - أحمد محمد              │
│    آخر زيارة: منذ 5 أيام                │
├─────────────────────────────────────────┤
│ ➕ إنشاء عميل جديد: 01012345678         │
└─────────────────────────────────────────┘
```

**Behavior:**

1. User types phone number
2. Debounced search (300ms)
3. Show matching customers OR "Create new"
4. On select → Store in order
5. On complete → Customer stats updated

**Component Structure:**

```
client/src/components/pos/
├── CustomerSearch.tsx       # NEW: Search/select component
└── CustomerQuickCreate.tsx  # NEW: Quick create modal
```

---

## 📅 4. Execution Roadmap

### Phase Overview

```
Phase A: Database & Foundation (Week 1)
Phase B: Inventory System (Week 2)
Phase C: Customer Management (Week 3)
Phase D: Refund System (Week 4)
Phase E: Frontend Integration (Week 5-6)
Phase F: Testing & Polish (Week 7)
```

### Detailed Steps

#### Phase A: Database & Foundation (Week 1)

```
Step A1: Schema Migration (Day 1-2)
├── Create Customer entity
├── Create StockMovement entity
├── Create RefundLog entity
├── Add fields to Product (LowStockThreshold, etc.)
├── Add fields to Order (Refund fields)
├── Create migration file
└── Test migration on dev DB

Step A2: Repository Layer (Day 3)
├── Add ICustomerRepository
├── Add IStockMovementRepository
├── Add IRefundLogRepository
├── Implement in Infrastructure layer
└── Register in DI container

Step A3: Index Creation (Day 4)
├── Add indexes via migration
├── Test query performance
└── Document index strategy
```

**Dependencies:** None

#### Phase B: Inventory System (Week 2)

```
Step B1: InventoryService (Day 1-2)
├── Create IInventoryService interface
├── Implement InventoryService
├── Decrement/Increment logic
├── StockMovement recording
└── Unit tests

Step B2: Integrate with OrderService (Day 3-4)
├── Modify CompleteAsync() to call InventoryService
├── Add stock decrement in transaction
├── Test stock updates on order complete
└── Verify negative stock is allowed

Step B3: Inventory Endpoints (Day 5)
├── GET /products/low-stock
├── GET /products/{id}/stock-history
├── POST /products/{id}/stock-adjust
└── Integration tests
```

**Dependencies:** Phase A complete

#### Phase C: Customer Management (Week 3)

```
Step C1: CustomerService (Day 1-2)
├── Create ICustomerService interface
├── Implement CRUD operations
├── Implement GetOrCreateByPhoneAsync
├── Implement stats update
└── Unit tests

Step C2: Customer Endpoints (Day 3)
├── Create CustomersController
├── GET /customers (search)
├── POST /customers
├── PUT /customers/{id}
└── Integration tests

Step C3: Integrate with Orders (Day 4-5)
├── Modify OrderService.CompleteAsync()
├── Auto-create customer by phone
├── Update customer stats after order
└── E2E tests
```

**Dependencies:** Phase A complete

#### Phase D: Refund System (Week 4)

```
Step D1: RefundService (Day 1-2)
├── Create IRefundService interface
├── Implement ProcessFullRefundAsync
├── Transaction handling (stock + status)
├── RefundLog creation
└── Unit tests

Step D2: Order State Machine Update (Day 3)
├── Add Completed → Refunded transition
├── Update ValidTransitions dictionary
├── Add validation rules
└── Unit tests

Step D3: Refund Endpoint (Day 4)
├── POST /orders/{id}/refund
├── GET /refunds (list)
├── GET /reports/refunds
└── Integration tests

Step D4: Shift Integration (Day 5)
├── Update shift totals on refund
├── Refund affects cash drawer calculation
└── Report adjustments
```

**Dependencies:** Phase B complete (for stock restore)

#### Phase E: Frontend Integration (Week 5-6)

```
Step E1: RTK Query Endpoints (Day 1)
├── customersApi.ts (new)
├── Update productsApi.ts (stock endpoints)
├── Update ordersApi.ts (refund endpoint)
└── Types for new entities

Step E2: Barcode Input (Day 2-3)
├── BarcodeInput component
├── Product lookup by barcode
├── Keyboard focus management
├── Integration with ProductGrid

Step E3: Customer Search (Day 4-5)
├── CustomerSearch component
├── CustomerQuickCreate modal
├── Cart integration
└── Payment flow update

Step E4: Refund UI (Day 6-7)
├── RefundModal component
├── OrderDetails refund button
├── Refund confirmation flow
├── Success/receipt display

Step E5: Low Stock Alerts (Day 8)
├── Dashboard alert card
├── Products page badge
├── POS grid indicator

Step E6: Stock Management UI (Day 9-10)
├── Stock adjustment modal
├── Stock history view
├── Low stock products page
```

**Dependencies:** Backend phases B, C, D complete

#### Phase F: Testing & Polish (Week 7)

```
Step F1: E2E Tests (Day 1-3)
├── Full order → refund cycle
├── Stock tracking accuracy
├── Customer auto-create flow
├── Negative stock scenarios

Step F2: Performance Testing (Day 4)
├── Barcode lookup speed
├── Customer search speed
├── Stock reports performance

Step F3: Documentation (Day 5)
├── Update API documentation
├── Update user guide
├── Admin guide for inventory
```

---

## 🔗 Dependency Graph

```
                    ┌─────────────┐
                    │   Phase A   │
                    │  Database   │
                    └──────┬──────┘
                           │
           ┌───────────────┼───────────────┐
           │               │               │
           ▼               ▼               ▼
    ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
    │   Phase B   │ │   Phase C   │ │    (wait)   │
    │  Inventory  │ │  Customers  │ │             │
    └──────┬──────┘ └──────┬──────┘ └─────────────┘
           │               │
           └───────┬───────┘
                   │
                   ▼
            ┌─────────────┐
            │   Phase D   │
            │   Refunds   │
            └──────┬──────┘
                   │
                   ▼
            ┌─────────────┐
            │   Phase E   │
            │  Frontend   │
            └──────┬──────┘
                   │
                   ▼
            ┌─────────────┐
            │   Phase F   │
            │   Testing   │
            └─────────────┘
```

---

## 📊 Risk Assessment

| Risk                     | Probability | Impact | Mitigation                    |
| ------------------------ | ----------- | ------ | ----------------------------- |
| Negative stock confusion | Medium      | Low    | Clear UI indicators + reports |
| Refund fraud             | Low         | High   | Admin-only + audit logs       |
| Customer data privacy    | Medium      | High   | GDPR-lite: delete option      |
| Performance on barcode   | Low         | Medium | Index + caching               |

---

## ✅ Success Criteria

| Feature              | Acceptance Criteria                              |
| -------------------- | ------------------------------------------------ |
| Stock Control        | Auto-decrement on order, visible in product list |
| Negative Stock       | Orders complete even with 0 stock                |
| Low Stock Alert      | Dashboard shows products below threshold         |
| Customer Auto-Create | New customer created on first order by phone     |
| Customer Lookup      | Search by phone in < 200ms                       |
| Full Refund          | Stock restored + status updated + audit logged   |
| Refund Report        | Daily/weekly refund totals visible               |

---

## 📝 Next Steps

1. **Approve this plan** - Review with stakeholders
2. **Create migration files** - Start Phase A
3. **Set up feature branch** - `feature/sellable-v1`
4. **Daily standups** - Track progress against roadmap

---

_Document Version: 1.0_  
_Last Updated: January 2026_
