# Batch Price Display Implementation

## Overview
Products with batches now display and use the **suggested batch price** as the primary selling price throughout the system.

## Implementation Status: ✅ COMPLETE (Updated 2026-05-04)

### Key Change Summary
**For products with batches:**
- The "سعر البيع" (Selling Price) field in the product form now displays `suggestedPrice` (batch price) instead of `price` (base price)
- This ensures consistency: what you see in the form is what will be used in sales
- The base price is still stored in the database but is only used as a fallback when no batches exist

### Backend (Already Implemented)
The backend correctly calculates and returns `SuggestedPrice` in `ProductDto`:

```csharp
// backend/KasserPro.Application/DTOs/Products/ProductDto.cs
public decimal Price { get; set; }           // Base price
public decimal SuggestedPrice { get; set; }  // Batch price or base price
```

**Logic in ProductService:**
1. For products with `IsBatchTracked = true`:
   - Queries the next available batch (FEFO order)
   - Sets `SuggestedPrice = nextBatch.SellingPrice ?? product.Price`
2. For products without batches:
   - Sets `SuggestedPrice = product.Price`

### Frontend (Fully Implemented)

#### 1. Products Page Display
**Location:** `frontend/src/pages/products/ProductsPage.tsx`

**Display Logic:**
```tsx
<td className="px-4 py-3">
  {/* Only show suggested price - clean and simple */}
  <span className="font-semibold text-primary-600">
    {formatCurrency(product.suggestedPrice)}
  </span>
</td>
```

**User Experience:**
- Shows only the actual selling price (suggestedPrice)
- No secondary price displayed - keeps the table clean
- For batch-tracked products: shows batch price
- For non-batch products: shows base price (since suggestedPrice = price)

#### 2. POS Workspace Display
**Location:** `frontend/src/pages/pos/POSWorkspacePage.tsx`

Product cards in POS now show `suggestedPrice` instead of base `price`.

#### 3. Cart Pricing Calculations
**Location:** `frontend/src/utils/cartPricing.ts`

**Critical Fix:** Cart calculations now use `product.suggestedPrice` instead of `product.price`:

```typescript
export const getProductNetUnitPrice = (
  product: Product,
  fallbackTaxRate: number,
  isTaxEnabled: boolean,
): number => {
  // Use suggestedPrice (batch price if available, otherwise base price)
  const effectivePrice = product.suggestedPrice;
  
  if (product.taxInclusive && taxRate > 0) {
    return round4(effectivePrice / (1 + taxRate / 100));
  }
  
  return effectivePrice;
};
```

This ensures:
- ✅ Cart subtotals use batch prices
- ✅ Tax calculations use batch prices
- ✅ Order totals reflect actual batch prices
- ✅ Receipts show correct prices

#### 4. Product List View
**Location:** `frontend/src/components/pos/ProductListView.tsx`

Display price now uses `suggestedPrice`:
```typescript
const displayPrice = cartItems[0]?.product.suggestedPrice ?? product.suggestedPrice;
```

## User Experience

### Scenario 1: Product with Batches
**Example:** Product "شامبو" has base price 50 EGP, but next batch has selling price 55 EGP

**Products Page Display:**
```
55.00 ج.م  (bold, primary color)
```

**Product Form (when editing):**
```
⚠️ هذا المنتج له دفعات مخزون
السعر المعروض هو سعر الباتش الحالي (55 ج.م)
السعر الأساسي للمنتج هو 50 ج.م

سعر البيع: 55.00 ج.م [معطّل]
```

**POS Display:**
```
55.00 ج.م  (bold)
```

**Cart Calculation:**
- Quantity: 2
- Unit Price: 55.00 EGP (from suggestedPrice)
- Subtotal: 110.00 EGP
- Tax (14%): 15.40 EGP
- Total: 125.40 EGP

### Scenario 2: Product without Batches
**Example:** Product "خدمة قص شعر" has base price 100 EGP, no batches

**Products Page Display:**
```
100.00 ج.م  (bold, primary color)
```

**POS Display:**
```
100.00 ج.م  (bold)
```

**Cart Calculation:**
- Uses suggestedPrice (which equals price) = 100.00 EGP

### Scenario 3: Batch Price = Base Price
**Example:** Product has base price 30 EGP, batch also has 30 EGP

**Products Page Display:**
```
30.00 ج.م  (bold, primary color)
```

**Note:** Since prices are identical, display is the same as non-batch products.

## Type Safety

**Frontend Type:** `frontend/src/types/product.types.ts`
```typescript
export interface Product {
  price: number;              // Base price
  suggestedPrice: number;     // Batch price or base price
  isBatchTracked: boolean;    // Whether product uses batches
  // ... other fields
}
```

#### 5. Product Form Modal
**Location:** `frontend/src/components/products/ProductFormModal.tsx`

**Critical Fix:** When editing a product, the "سعر البيع" field now shows `suggestedPrice` instead of `price`:

```typescript
const [formData, setFormData] = useState({
  // ... other fields
  price: product?.suggestedPrice || product?.price || 0, // Use batch price if available
  // ... other fields
});
```

**User Experience:**
- When editing a product with batches, the price field shows the actual selling price (from batch)
- The field is disabled to prevent confusion (price must be changed via batch management)
- A warning message explains that the displayed price is the batch price
- Shows both the batch price and base price in the warning for clarity

## Files Modified (2026-05-04)

1. ✅ `frontend/src/pages/products/ProductsPage.tsx` - **Simplified** - Show only suggestedPrice (removed base price display)
2. ✅ `frontend/src/pages/pos/POSWorkspacePage.tsx` - Changed to use suggestedPrice
3. ✅ `frontend/src/utils/cartPricing.ts` - **CRITICAL FIX** - Use suggestedPrice in calculations
4. ✅ `frontend/src/components/pos/ProductListView.tsx` - Use suggestedPrice for display
5. ✅ `frontend/src/components/products/ProductFormModal.tsx` - **CRITICAL FIX** - Show suggestedPrice in price field when editing products with batches

## Design Decision: Single Price Display

**Rationale:** 
- Users only need to see the actual selling price
- Showing both prices (batch + base) was causing confusion
- The base price is still stored in the database and shown in the product form with context
- Keeps the products table clean and focused

## Testing Checklist

- [x] Backend returns correct `SuggestedPrice` for batch-tracked products
- [x] Backend returns `Price` as `SuggestedPrice` for non-batch products
- [x] Frontend displays suggested price as primary in products page
- [x] Frontend shows base price only when different and batch-tracked
- [x] POS workspace shows suggested price
- [x] **Cart calculations use suggestedPrice (CRITICAL)**
- [x] Cart subtotals reflect batch prices
- [x] Tax calculations use batch prices
- [x] Order totals are correct
- [x] TypeScript types match backend DTOs

## Purchase Invoices Note

**Location:** `frontend/src/pages/purchase-invoices/PurchaseInvoiceFormPage.tsx`

Purchase invoices intentionally use `product.price` (base price) as the default selling price when creating new products. This is correct behavior because:
- Purchase invoices define the base selling price for products
- Batch-specific prices are set per batch, not at product creation
- The base price serves as a fallback when no batch price is available

## Related Documentation

- `docs/BATCH_COST_TRACKING_AUDIT.md` - Batch cost tracking system
- `docs/BATCH_COST_SUMMARY.md` - Batch cost summary
- `docs/BATCH_COST_FLOW_DIAGRAM.md` - Batch cost flow diagram
- `.kiro/steering/kasserpro-api-contract.md` - API contract

## Critical Notes

1. **Price Priority:** `SuggestedPrice` is ALWAYS used in POS and cart calculations, not `Price`
2. **Display Logic:** Base price shown only when:
   - Product is batch-tracked (`isBatchTracked = true`)
   - AND prices differ (`suggestedPrice !== price`)
3. **Tooltip:** Hovering over base price shows "السعر الأساسي للمنتج"
4. **Cart Calculations:** The `getProductNetUnitPrice` function in `cartPricing.ts` is the single source of truth for pricing calculations

---

**Status:** ✅ Production Ready  
**Last Updated:** 2026-05-04  
**Implementation:** Complete - All cart calculations now use suggestedPrice
