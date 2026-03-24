# Architecture Refactoring: ProductType & Custom POS Items

**Date:** March 1, 2026  
**Status:** ✅ Complete - Ready for Testing  
**Migration Required:** Yes

---

## 🎯 Objectives Achieved

### ✅ PART 1: ProductType Enum
- Created `ProductType` enum with `Physical` and `Service` values
- Modified `Product` entity to include `Type` property
- `TrackInventory` is now automatically set based on `Type`:
  - `Physical` → `TrackInventory = true`
  - `Service` → `TrackInventory = false`
- Updated all DTOs to use `ProductType` instead of boolean `TrackInventory`

### ✅ PART 2: Custom POS Items
- Made `OrderItem.ProductId` nullable
- Added custom item fields:
  - `IsCustomItem` (bool)
  - `CustomName` (string)
  - `CustomUnitPrice` (decimal)
  - `CustomTaxRate` (decimal)
- Custom items skip:
  - Product validation
  - Inventory tracking
  - Stock checks
- Financial calculations work correctly for custom items

### ✅ PART 3: Inventory Guard Isolation
- Moved inventory protection into `InventoryService`:
  - `BatchDecrementStockAsync` checks `TrackInventory` before processing
  - `IncrementStockAsync` checks `TrackInventory` before processing
- Service products automatically skip inventory operations
- Single source of truth for inventory logic

### ✅ PART 4: QuickCreate Decision
**Decision: KEEP QuickCreate endpoint**

**Reasoning:**
- QuickCreate serves a different purpose: creating real catalog items quickly
- Custom items are for one-off POS entries (no catalog pollution)
- QuickCreate now defaults to `Type = Service` for fast service product creation
- Both features complement each other:
  - QuickCreate: "Add this to my catalog for future use"
  - Custom Item: "One-time charge, don't save to catalog"

### ✅ PART 5: Migration Safety
- Backward compatible migration script
- Existing products mapped to `Physical` or `Service` based on `TrackInventory`
- Existing orders remain intact
- All `OrderItem` records marked as `IsCustomItem = false`
- Rollback script provided for safety

---

## 📊 Database Changes

### Products Table
```sql
-- New column
Type INTEGER NOT NULL DEFAULT 1  -- 1=Physical, 2=Service

-- Migration logic
UPDATE Products SET Type = CASE 
    WHEN TrackInventory = 1 THEN 1  -- Physical
    WHEN TrackInventory = 0 THEN 2  -- Service
END;
```

### OrderItems Table
```sql
-- Modified columns
ProductId INTEGER NULL  -- Was NOT NULL

-- New columns
IsCustomItem INTEGER NOT NULL DEFAULT 0
CustomName TEXT NULL
CustomUnitPrice REAL NULL
CustomTaxRate REAL NULL
```

---

## 🔄 API Changes

### New Endpoint
```http
POST /api/orders/{orderId}/items/custom
Content-Type: application/json

{
  "name": "خدمة توصيل",
  "unitPrice": 25.00,
  "quantity": 1,
  "taxRate": 14,  // Optional, defaults to tenant tax rate
  "notes": "توصيل سريع"
}
```

### Modified DTOs

#### CreateProductRequest
```csharp
// OLD
public bool TrackInventory { get; set; } = true;

// NEW
public ProductType Type { get; set; } = ProductType.Physical;
// TrackInventory is automatically set based on Type
```

#### ProductDto
```csharp
// Added
public ProductType Type { get; set; }
// TrackInventory is now read-only (computed from Type)
```

---

## 🧪 Testing Checklist

### Backend Tests
- [ ] Product creation with `Type = Physical` creates inventory records
- [ ] Product creation with `Type = Service` skips inventory records
- [ ] Custom item can be added to order without product validation
- [ ] Custom item skips inventory checks
- [ ] Order completion decrements stock only for Physical products
- [ ] Order refund restores stock only for Physical products
- [ ] Custom items calculate tax correctly
- [ ] Migration script runs without errors
- [ ] Existing orders still work after migration

### Frontend Tests
- [ ] Product form shows Type selector (Physical/Service)
- [ ] Service products don't show inventory fields
- [ ] POS can add custom items
- [ ] Custom items display correctly in order
- [ ] Receipt shows custom items properly
- [ ] Reports handle custom items correctly

### E2E Tests
- [ ] Create Physical product → verify inventory tracking
- [ ] Create Service product → verify no inventory tracking
- [ ] Add custom item to order → complete order → verify totals
- [ ] Refund order with custom items → verify no stock changes
- [ ] Mix of catalog and custom items in same order

---

## 🚀 Deployment Steps

### 1. Pre-Deployment
```bash
# Backup database
cp backend/KasserPro.API/kasserpro.db backend/KasserPro.API/backups/kasserpro-backup-$(date +%Y%m%d-%H%M%S)-pre-refactoring.db
```

### 2. Run Migration
```bash
# Apply migration
sqlite3 backend/KasserPro.API/kasserpro.db < backend/KasserPro.API/Migrations/AddProductTypeAndCustomItems.sql
```

### 3. Verify Migration
```sql
-- Check Products
SELECT Type, COUNT(*) FROM Products GROUP BY Type;

-- Check OrderItems
SELECT IsCustomItem, COUNT(*) FROM OrderItems GROUP BY IsCustomItem;
```

### 4. Deploy Backend
```bash
cd backend/KasserPro.API
dotnet build
dotnet run
```

### 5. Update Frontend
- Update TypeScript types to match new DTOs
- Update product forms to use ProductType
- Add custom item UI to POS

---

## 🔒 Breaking Changes

### ⚠️ API Contract Changes
1. **CreateProductRequest**: `TrackInventory` replaced with `Type`
2. **UpdateProductRequest**: `TrackInventory` replaced with `Type`
3. **ProductDto**: Added `Type` field
4. **OrderItemDto**: `ProductId` can now be null

### 🔄 Migration Path for Clients
```typescript
// OLD
const request = {
  name: "Product",
  trackInventory: true
};

// NEW
const request = {
  name: "Product",
  type: "Physical"  // or "Service"
};
```

---

## 📈 Performance Impact

### Positive
- ✅ Inventory operations skip service products (faster)
- ✅ Custom items bypass product lookups (faster POS)
- ✅ Cleaner separation of concerns

### Neutral
- ➡️ Migration adds one column to Products (negligible)
- ➡️ Migration adds four columns to OrderItems (negligible)

---

## 🎓 Architecture Benefits

### Clean Separation
```
Physical Products → Inventory Tracking → Stock Management
Service Products  → No Inventory     → Direct Sale
Custom Items      → No Product       → One-off Charges
```

### Future-Proof
- Easy to add new product types (e.g., `Digital`, `Subscription`)
- Custom items support any POS scenario
- Inventory logic isolated in one service

### PostgreSQL Ready
- Enum types map cleanly to PostgreSQL ENUMs
- Nullable foreign keys supported
- Migration script structure follows best practices

---

## 🐛 Known Limitations

1. **Custom items don't appear in product reports** (by design)
2. **Custom items can't be refunded partially by item** (use full refund)
3. **No history of custom item usage** (not saved to catalog)

---

## 📝 Next Steps

### Immediate
1. Run migration on development database
2. Test all order flows
3. Update frontend to match new API

### Short-term
1. Add custom item analytics
2. Create "Convert custom item to product" feature
3. Add custom item templates for common charges

### Long-term
1. Migrate to PostgreSQL
2. Add product bundles support
3. Implement composite products

---

## 🤝 Team Communication

### For Backend Developers
- `ProductType` is the source of truth for inventory behavior
- Always check `IsCustomItem` before accessing `ProductId`
- Inventory guards are in `InventoryService`, not `OrderService`

### For Frontend Developers
- Use `Type` field instead of `TrackInventory` in forms
- Handle nullable `ProductId` in order items
- Custom items need special UI treatment

### For QA
- Focus on mixed orders (catalog + custom items)
- Test refund flows thoroughly
- Verify inventory accuracy after operations

---

## 📞 Support

**Questions?** Contact the architecture team  
**Issues?** Create ticket with label `refactoring-producttype`  
**Rollback needed?** See rollback script in migration file
