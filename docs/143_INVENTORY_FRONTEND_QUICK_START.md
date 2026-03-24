# 🚀 Inventory Frontend - Quick Start Guide

## ⚡ Quick Integration (5 Minutes)

### Step 1: Add Route
```typescript
// In your App.tsx or router configuration
import InventoryPage from "./pages/inventory/InventoryPage";

<Route path="/inventory" element={<InventoryPage />} />
```

### Step 2: Add Navigation Link
```typescript
// In your sidebar/navigation component
import { Package } from "lucide-react";

<NavLink to="/inventory">
  <Package className="w-5 h-5" />
  <span>المخزون</span>
</NavLink>
```

### Step 3: Done! ✅
Navigate to `/inventory` and start using the system.

---

## 📦 What You Get

### 4 Main Features (All in One Page)

1. **Branch Inventory** - View all products in current branch
2. **Low Stock Alerts** - Monitor products below reorder level  
3. **Inventory Transfers** - Move stock between branches (Admin)
4. **Branch Pricing** - Set branch-specific prices (Admin)

---

## 🎯 Component Usage

### Use Individual Components

```typescript
import {
  BranchInventoryList,
  LowStockAlerts,
  InventoryTransferForm,
  InventoryTransferList,
  BranchPricingEditor,
} from "../components/inventory";

// Use anywhere in your app
<BranchInventoryList />
<LowStockAlerts />
<InventoryTransferForm onSuccess={() => {}} onCancel={() => {}} />
```

### Use Complete Page

```typescript
import InventoryPage from "../pages/inventory/InventoryPage";

// Full-featured page with tabs
<InventoryPage />
```

---

## 🔑 Key Features

### ✅ Branch Context Aware
All components automatically use the current branch from Redux state:
```typescript
const currentBranch = useAppSelector(selectCurrentBranch);
```

### ✅ Admin Access Control
Admin-only features automatically check permissions:
```typescript
const isAdmin = useAppSelector(selectIsAdmin);
```

### ✅ Real-time Updates
RTK Query automatically refetches data when:
- Inventory changes
- Transfers approved/received
- Prices updated

### ✅ Responsive Design
Works perfectly on mobile, tablet, and desktop.

---

## 📊 API Endpoints Used

```typescript
// Automatically available via RTK Query
useGetBranchInventoryQuery(branchId)
useGetLowStockItemsQuery(branchId?)
useCreateTransferMutation()
useGetTransfersQuery(params)
useApproveTransferMutation()
useReceiveTransferMutation()
useSetBranchPriceMutation()
```

---

## 🎨 Customization

### Change Colors
Edit Tailwind classes in components:
```typescript
// Primary button
className="bg-blue-600 hover:bg-blue-700"

// Success button
className="bg-green-600 hover:bg-green-700"

// Danger button
className="bg-red-600 hover:bg-red-700"
```

### Change Icons
Replace lucide-react icons:
```typescript
import { Package, AlertTriangle, ArrowRight } from "lucide-react";
```

### Add Custom Fields
Extend TypeScript types in `inventory.types.ts`:
```typescript
export interface BranchInventory {
  // ... existing fields
  customField?: string; // Add your field
}
```

---

## 🔍 Common Use Cases

### 1. Check Low Stock
```
Navigate to: /inventory
Click tab: "تنبيهات المخزون"
Filter by branch if needed
```

### 2. Transfer Inventory
```
Navigate to: /inventory
Click tab: "نقل المخزون"
Click: "طلب نقل جديد"
Fill form and submit
```

### 3. Set Branch Price
```
Navigate to: /inventory
Click tab: "أسعار الفروع"
Select branch
Click: "إضافة سعر مخصص"
Enter price and save
```

---

## 🐛 Troubleshooting

### Issue: "No branch selected"
**Solution:** Ensure branch selector is working and user has selected a branch.

### Issue: "Access denied" for admin features
**Solution:** Check user role in Redux state (`selectIsAdmin`).

### Issue: Data not loading
**Solution:** Check API connection and backend is running on port 5243.

### Issue: Cache not updating
**Solution:** RTK Query auto-invalidates. If stuck, refresh page.

---

## 📱 Mobile Support

All components are mobile-friendly:
- Tables convert to cards on small screens
- Forms stack vertically
- Touch-friendly buttons
- Responsive navigation

---

## 🎯 Performance Tips

1. **Pagination** - Transfer list loads 20 items at a time
2. **Conditional Queries** - Data only loads when branch selected
3. **Smart Caching** - RTK Query caches responses
4. **Lazy Loading** - Components load on demand

---

## 📚 Full Documentation

For detailed documentation, see:
- `FRONTEND_INVENTORY_IMPLEMENTATION.md` - Complete technical docs
- `BRANCH_INVENTORY_BACKEND_COMPLETE.md` - Backend API reference

---

## ✅ Quick Checklist

Before going live:
- [ ] Route added to router
- [ ] Navigation link added
- [ ] Backend API running (port 5243)
- [ ] Branch selector working
- [ ] User authentication working
- [ ] Admin permissions configured
- [ ] Test on mobile device
- [ ] Test all 4 features

---

## 🚀 You're Ready!

The inventory system is fully functional and ready to use. Navigate to `/inventory` and explore all features.

**Need help?** Check the full documentation or review the component source code.
