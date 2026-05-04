# Task 5 Summary: Display Suggested Price from Batch

**Date:** May 3, 2026  
**Status:** ✅ COMPLETED  
**User Request:** "جميل بس حدث سعر المنتج اللي معروض في المنتجات والمعروض في نقطة البيع خليه يظهر سعر الباتش المقترح"

---

## Overview

Updated the product display logic to show the **suggested price from the next available batch** instead of the base product price in:
1. Product list page (`ProductsPage.tsx`)
2. POS product cards (`ProductCard.tsx`)

This ensures that users see the actual selling price that will be used when creating orders (FEFO batch selection).

---

## Changes Made

### Backend

#### 1. ProductDto Enhancement
**File:** `backend/KasserPro.Application/DTOs/Products/ProductDto.cs`

Added new property:
```csharp
public decimal SuggestedPrice { get; set; }
```

#### 2. ProductService - GetAllAsync Method
**File:** `backend/KasserPro.Application/Services/Implementations/ProductService.cs`

- Calculate `SuggestedPrice` **after** fetching products (not in LINQ projection)
- For batch-tracked products: Query next active batch using FEFO order
- Use batch's `SellingPrice` if available, otherwise fall back to `Product.Price`
- Handle nullable `ProductBatch.SellingPrice` with null-coalescing operator

```csharp
// Calculate SuggestedPrice for batch-tracked products
var batchTrackedProductIds = pagedItems
    .Where(p => p.IsBatchTracked)
    .Select(p => p.Id)
    .ToList();

if (batchTrackedProductIds.Any())
{
    var nextBatchPrices = await _unitOfWork.ProductBatches.Query()
        .Where(pb => pb.TenantId == tenantId 
                  && pb.BranchId == branchId 
                  && batchTrackedProductIds.Contains(pb.ProductId)
                  && pb.Status == BatchStatus.Active
                  && pb.Quantity > 0)
        .GroupBy(pb => pb.ProductId)
        .Select(g => new
        {
            ProductId = g.Key,
            SellingPrice = (decimal?)g.OrderBy(pb => pb.ExpiryDate ?? DateTime.MaxValue)
                            .ThenBy(pb => pb.Id)
                            .Select(pb => pb.SellingPrice)
                            .FirstOrDefault()
        })
        .ToListAsync();

    foreach (var product in pagedItems.Where(p => p.IsBatchTracked))
    {
        var batchPrice = nextBatchPrices.FirstOrDefault(bp => bp.ProductId == product.Id);
        product.SuggestedPrice = batchPrice?.SellingPrice ?? product.Price;
    }
}
```

#### 3. ProductService - GetByIdAsync Method

Similar logic for single product retrieval:
```csharp
decimal suggestedPrice = product.Price;
if (product.IsBatchTracked)
{
    var nextBatch = await _unitOfWork.ProductBatches.Query()
        .Where(pb => pb.TenantId == tenantId 
                  && pb.BranchId == branchId 
                  && pb.ProductId == product.Id
                  && pb.Status == BatchStatus.Active
                  && pb.Quantity > 0)
        .OrderBy(pb => pb.ExpiryDate ?? DateTime.MaxValue)
        .ThenBy(pb => pb.Id)
        .FirstOrDefaultAsync();
    
    if (nextBatch != null)
    {
        suggestedPrice = nextBatch.SellingPrice ?? product.Price;
    }
}
```

### Frontend

#### 1. Product Type Definition
**File:** `frontend/src/types/product.types.ts`

Added:
```typescript
export interface Product {
  // ... existing properties
  suggestedPrice: number;  // NEW: Price from next batch or base price
}
```

#### 2. Products Page
**File:** `frontend/src/pages/products/ProductsPage.tsx`

Updated price display:
```tsx
<div className="text-sm text-gray-900 font-medium">
  {product.suggestedPrice.toFixed(2)} ج.م
</div>
{product.isBatchTracked && product.suggestedPrice !== product.price && (
  <div className="text-xs text-gray-500 line-through">
    السعر الأساسي: {product.price.toFixed(2)} ج.م
  </div>
)}
```

#### 3. POS Product Card
**File:** `frontend/src/components/pos/ProductCard.tsx`

Changed from `product.price` to `product.suggestedPrice`:
```tsx
<p className="text-lg font-bold text-gray-900">
  {product.suggestedPrice.toFixed(2)} ج.م
</p>
```

---

## Build Error Resolution

### Issue
Build failed with:
```
error CS0266: Cannot implicitly convert type 'decimal?' to 'decimal'
warning CS8629: Nullable value type may be null
```

### Root Cause
`ProductBatch.SellingPrice` is defined as `decimal?` (nullable) in the entity.

### Solution
Added null-coalescing operators in two places:

1. **GetByIdAsync (Line 183):**
   ```csharp
   suggestedPrice = nextBatch.SellingPrice ?? product.Price;
   ```

2. **GetAllAsync GroupBy Query (Line 126):**
   ```csharp
   SellingPrice = (decimal?)g.OrderBy(...).Select(pb => pb.SellingPrice).FirstOrDefault()
   ```

---

## Testing Checklist

### Backend
- [x] Build succeeds (0 errors, 0 warnings)
- [ ] Products without batches return `SuggestedPrice = Price`
- [ ] Products with batches return `SuggestedPrice = BatchSellingPrice`
- [ ] Products with batches but null `SellingPrice` fall back to `Price`

### Frontend
- [ ] Product list shows suggested price
- [ ] Product list shows base price (strikethrough) when different
- [ ] POS cards show suggested price
- [ ] Prices match what will be used in orders

---

## User Experience

### Before
- Product list and POS showed base `Product.Price`
- Actual order used batch price (FEFO)
- **Mismatch** between displayed price and order price

### After
- Product list and POS show `SuggestedPrice` (from next batch)
- Order uses same batch price (FEFO)
- **Consistent** pricing across UI and orders

---

## Related Documents

1. `docs/PRODUCT_SUGGESTED_PRICE.md` - Original feature specification
2. `docs/PRODUCT_SUGGESTED_PRICE_FIX.md` - Fix for "ليس رقما" issue (moved calculation out of LINQ)
3. `docs/PRODUCT_SUGGESTED_PRICE_BUILD_FIX.md` - Fix for nullable decimal build error
4. `docs/PRODUCT_PRICE_BATCH_WARNING.md` - Warning banner in product edit modal

---

## Next Steps

1. **Runtime Testing**: Verify prices display correctly in UI
2. **Order Testing**: Confirm order creation uses same price as displayed
3. **Edge Cases**: Test products with no batches, expired batches, null selling prices

---

**Status:** ✅ Implementation complete. Build successful. Ready for testing.
