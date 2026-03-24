# Purchase Invoice Pricing Strategy

**Date:** 2026-01-28  
**Feature:** Purchase Invoices - Pricing Logic

---

## 📊 Current Behavior (As Implemented)

### When Confirming a Purchase Invoice:

#### ✅ What Gets Updated:
1. **Inventory Quantity** (`StockQuantity`)
   - Increases by purchased quantity
   - Example: Had 10, bought 5 → Now 15

2. **Cost Tracking** (`Cost` field)
   - Updates to the latest purchase price
   - Example: Old cost 100, new purchase 120 → Cost = 120

3. **Average Cost** (`AverageCost`)
   - Uses **Weighted Average** formula:
   ```
   New Average = (Old Stock × Old Cost + New Stock × New Cost) / Total Stock
   ```
   - Example:
     - Had: 10 units @ 100 EGP = 1,000 EGP
     - Bought: 5 units @ 120 EGP = 600 EGP
     - New Average = (1,000 + 600) / 15 = 106.67 EGP

4. **Last Purchase Info**
   - `LastPurchasePrice`: Latest purchase price
   - `LastPurchaseDate`: Date of purchase

#### ❌ What Does NOT Get Updated:
- **Sale Price** (`Price` field) - **STAYS THE SAME**

---

## 🤔 The Question: What Should Happen to Sale Price?

### Option 1: Keep Sale Price Manual (Current Implementation) ✅ RECOMMENDED
**Pros:**
- Business has full control over pricing strategy
- Can maintain profit margins
- Can run promotions without affecting cost
- Prevents accidental price changes

**Cons:**
- Requires manual price updates
- Risk of selling below cost if not monitored

**Use Case:**
- You buy at 100 EGP, sell at 150 EGP (50% margin)
- Next purchase at 120 EGP
- You can choose to:
  - Keep selling at 150 EGP (lower margin but competitive)
  - Increase to 180 EGP (maintain 50% margin)
  - Set any price based on market conditions

### Option 2: Auto-Update Based on Cost + Margin
**Pros:**
- Automatic price adjustments
- Maintains consistent profit margin
- Less manual work

**Cons:**
- Loses pricing flexibility
- May price out of market
- Customers see frequent price changes

**Implementation:**
```csharp
// If product has a profit margin setting (e.g., 50%)
product.Price = product.Cost * (1 + product.ProfitMarginPercentage / 100);
```

### Option 3: Auto-Update Based on Average Cost + Margin
**Pros:**
- More stable pricing
- Reflects true inventory cost
- Better for FIFO/LIFO scenarios

**Cons:**
- Still automatic (less control)
- Complex to explain to users

**Implementation:**
```csharp
product.Price = product.AverageCost * (1 + product.ProfitMarginPercentage / 100);
```

### Option 4: Notify User of Cost Changes (Hybrid Approach)
**Pros:**
- User stays informed
- Maintains manual control
- Suggests price updates

**Cons:**
- Requires UI notifications
- More complex implementation

**Implementation:**
- Show alert: "تنبيه: تكلفة المنتج تغيرت من 100 إلى 120 جنيه. هل تريد تحديث سعر البيع؟"
- User can accept or ignore

---

## 💡 Recommended Strategy

### For Your Business (KasserPro):

**Use Option 1 (Manual) with Dashboard Alerts**

#### Why?
1. **Flexibility:** Different products have different strategies
   - Electronics: Low margin, high volume
   - Luxury items: High margin, low volume
   - Seasonal items: Variable pricing

2. **Market Competition:** You need to match competitor prices, not just cost + margin

3. **Promotions:** Can run sales without changing cost structure

#### Implementation:
1. ✅ Keep current behavior (manual price control)
2. ✅ Add "Cost vs Price" report showing:
   - Products where Price < Cost (selling at loss)
   - Products where margin is below target
   - Products where cost changed significantly
3. ✅ Add quick action: "Update prices based on new costs"

---

## 🔄 Scenario: Old Stock at Lower Cost

### Question: "لو السعر اتغير وعندي مخزون بسعر قديم اقل المفروض يحصل ايه؟"

### Current Implementation (Weighted Average) ✅ CORRECT

**Example:**
```
Initial State:
- Stock: 10 units @ 100 EGP each
- Average Cost: 100 EGP
- Sale Price: 150 EGP

Purchase Invoice:
- Buy: 5 units @ 120 EGP each

After Confirmation:
- Stock: 15 units
- Average Cost: (10×100 + 5×120) / 15 = 106.67 EGP
- Sale Price: 150 EGP (unchanged)

Financial Impact:
- Old margin: 150 - 100 = 50 EGP (50%)
- New margin: 150 - 106.67 = 43.33 EGP (40.5%)
```

### Why Weighted Average is Correct:

1. **Fair Cost Allocation:**
   - You have mixed inventory (old + new)
   - Average cost represents true inventory value

2. **Accounting Standard:**
   - Matches FIFO/LIFO principles
   - Accepted by tax authorities

3. **Profit Calculation:**
   - When you sell 1 unit at 150 EGP
   - Cost of Goods Sold (COGS) = 106.67 EGP
   - Profit = 43.33 EGP

4. **Inventory Valuation:**
   - Total inventory value = 15 × 106.67 = 1,600 EGP
   - Matches actual money spent (1,000 + 600 = 1,600)

---

## 🆕 Feature Request: Add New Product During Invoice Creation

### Current Limitation:
- Cannot create new products while creating purchase invoice
- Must go to Products page first

### Proposed Solution:

#### Option A: Quick Add Product Modal
```typescript
// Add button next to product dropdown
<Button onClick={() => setShowQuickAddProduct(true)}>
  + منتج جديد
</Button>

// Modal with minimal fields:
- Name (required)
- SKU (optional)
- Barcode (optional)
- Category (required)
- Initial Sale Price (required)
```

#### Option B: Inline Product Creation
```typescript
// If product not found in dropdown, show:
"المنتج غير موجود. هل تريد إضافته؟"
// Then show inline form
```

### Recommendation: **Option A (Modal)**
- Cleaner UI
- Validates all required fields
- Can set initial sale price
- Doesn't clutter invoice form

---

## 📋 Action Items

### Immediate Fixes:
1. ✅ Fix scroll in invoices table (DONE)
2. ⏳ Add "Quick Add Product" button in invoice form
3. ⏳ Document pricing strategy for users

### Future Enhancements:
1. Add "Cost vs Price Analysis" report
2. Add bulk price update tool
3. Add price change history
4. Add margin alerts

---

## 🎯 Summary

### Current Behavior is CORRECT for:
- ✅ Inventory tracking (weighted average)
- ✅ Cost calculation
- ✅ Financial reporting

### Needs Improvement:
- ❌ Cannot add new products during invoice creation
- ⚠️ No alerts when cost changes significantly
- ⚠️ No easy way to update prices based on new costs

### Recommended Next Steps:
1. Keep manual price control (current behavior)
2. Add quick product creation in invoice form
3. Add cost/price monitoring dashboard
4. Document pricing strategy in user manual
