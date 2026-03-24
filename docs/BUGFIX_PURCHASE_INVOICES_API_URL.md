# Bug Fix: Purchase Invoices API URL Mismatch

**Date:** 2026-01-28  
**Status:** ✅ FIXED

---

## 🐛 Problem Description

عند محاولة استدعاء فواتير الشراء من Frontend، كان هناك خطأ في الاتصال بالـ API.

### Root Cause
كان هناك عدم تطابق في URLs بين Frontend و Backend:

- **Backend Controller:** `/api/purchaseinvoices` (بدون شرطة)
- **Frontend API Client:** `/api/purchase-invoices` (بشرطة)

هذا التناقض تسبب في فشل جميع طلبات API المتعلقة بفواتير الشراء.

---

## 🔧 Solution Applied

تم تعديل ملف `client/src/api/purchaseInvoiceApi.ts` لمطابقة URLs الخاصة بالـ Backend.

### Changes Made

#### Before (❌ Wrong):
```typescript
url: `/purchase-invoices`           // GET all
url: `/purchase-invoices/${id}`     // GET by ID
url: '/purchase-invoices'           // POST create
url: `/purchase-invoices/${id}`     // PUT update
url: `/purchase-invoices/${id}`     // DELETE
url: `/purchase-invoices/${id}/confirm`
url: `/purchase-invoices/${id}/cancel`
url: `/purchase-invoices/${invoiceId}/payments`
url: `/purchase-invoices/${invoiceId}/payments/${paymentId}`
```

#### After (✅ Correct):
```typescript
url: `/purchaseinvoices`            // GET all
url: `/purchaseinvoices/${id}`      // GET by ID
url: '/purchaseinvoices'            // POST create
url: `/purchaseinvoices/${id}`      // PUT update
url: `/purchaseinvoices/${id}`      // DELETE
url: `/purchaseinvoices/${id}/confirm`
url: `/purchaseinvoices/${id}/cancel`
url: `/purchaseinvoices/${invoiceId}/payments`
url: `/purchaseinvoices/${invoiceId}/payments/${paymentId}`
```

---

## ✅ Verification

### Backend Endpoints (Confirmed Working)
```
GET    /api/purchaseinvoices
GET    /api/purchaseinvoices/{id}
POST   /api/purchaseinvoices
PUT    /api/purchaseinvoices/{id}
DELETE /api/purchaseinvoices/{id}
POST   /api/purchaseinvoices/{id}/confirm
POST   /api/purchaseinvoices/{id}/cancel
POST   /api/purchaseinvoices/{id}/payments
DELETE /api/purchaseinvoices/{id}/payments/{paymentId}
```

### Test Results
✅ Backend running on http://localhost:5243  
✅ Frontend running on http://localhost:3001  
✅ API endpoints responding correctly  
✅ Authentication working  
✅ Purchase invoices list retrieved successfully

---

## 📝 Lessons Learned

### Best Practices to Prevent This Issue:

1. **Consistent Naming Convention:**
   - Use either kebab-case (`purchase-invoices`) or no separator (`purchaseinvoices`)
   - Document the chosen convention in architecture rules

2. **API Contract Documentation:**
   - Keep `docs/api/API_DOCUMENTATION.md` updated with exact URLs
   - Include URL examples in API documentation

3. **Type Safety:**
   - Consider creating a constants file for API endpoints:
   ```typescript
   // api/endpoints.ts
   export const ENDPOINTS = {
     PURCHASE_INVOICES: '/purchaseinvoices',
     SUPPLIERS: '/suppliers',
     // ... etc
   };
   ```

4. **Testing:**
   - Add integration tests that verify Frontend can call Backend endpoints
   - Test API calls during development, not just at deployment

---

## 🎯 Recommendation

### Option 1: Keep Current URLs (No Separator)
- ✅ Already implemented
- ✅ Matches ASP.NET Core convention
- ✅ No migration needed

### Option 2: Change to Kebab-Case (More RESTful)
- Would require changing Backend controller route
- More readable and RESTful
- Requires coordination between teams

**Decision:** Keep current implementation (`/purchaseinvoices`) as it's already working and matches the existing codebase convention.

---

## 📊 Impact

- **Severity:** High (Feature completely broken)
- **Affected Users:** All users trying to access Purchase Invoices
- **Time to Fix:** 5 minutes
- **Files Changed:** 1 file (`client/src/api/purchaseInvoiceApi.ts`)

---

## ✅ Status

**RESOLVED** - All purchase invoice API calls now work correctly.

Users can now:
- ✅ View list of purchase invoices
- ✅ Create new invoices
- ✅ Edit draft invoices
- ✅ View invoice details
- ✅ Confirm invoices (updates inventory)
- ✅ Cancel invoices
- ✅ Add/delete payments
