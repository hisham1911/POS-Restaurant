# Phase 1 – Frontend Readiness (MVP)

> **المرجع الرسمي للـ API:** [API_DOCUMENTATION.md](../api/API_DOCUMENTATION.md)  
> **نظام التنسيقات:** [DESIGN_SYSTEM.md](../design/DESIGN_SYSTEM.md)  
> **التقنيات:** React 18 · TypeScript · Vite · TailwindCSS · Redux Toolkit + RTK Query  
> **حالة المشروع:** ✅ مكتمل وتفاعلي

---

## الحالة الحالية

| الميزة                     | الحالة |
| -------------------------- | ------ |
| Auth + Protected Routes    | ✅     |
| POS (منتجات، تصنيفات، سلة) | ✅     |
| دفع (نقدي/بطاقة/فوري)      | ✅     |
| ورديات + تقرير يومي        | ✅     |
| ضريبة 14% · ar-EG · EGP    | ✅     |
| اختيار Tenant/Branch       | ✅     |
| شاشة Audit Log             | ✅     |
| التقارير اليومية           | ✅     |
| توقيت القاهرة              | ✅     |

---

## هيكل المشروع

```
client/src/
├── api/          # RTK Query endpoints
│   ├── authApi.ts
│   ├── baseApi.ts
│   ├── branchesApi.ts
│   ├── auditApi.ts
│   ├── categoriesApi.ts
│   ├── ordersApi.ts
│   ├── productsApi.ts
│   ├── shiftsApi.ts
│   └── reportsApi.ts       # NEW
├── components/
│   ├── common/
│   ├── layout/
│   │   ├── MainLayout.tsx
│   │   └── BranchSelector.tsx
│   ├── pos/
│   ├── products/
│   └── orders/
├── hooks/
├── pages/
│   ├── auth/
│   ├── audit/
│   │   └── AuditLogPage.tsx  # UPDATED
│   ├── pos/
│   ├── orders/
│   ├── shifts/
│   └── reports/
│       └── DailyReportPage.tsx  # UPDATED
├── store/
│   ├── slices/
│   │   ├── authSlice.ts
│   │   ├── branchSlice.ts
│   │   ├── cartSlice.ts
│   │   └── uiSlice.ts
│   ├── hooks.ts
│   └── index.ts
├── types/
│   ├── api.types.ts
│   ├── audit.types.ts
│   ├── branch.types.ts
│   ├── tenant.types.ts
│   ├── report.types.ts     # NEW
│   └── ...
└── utils/
    └── formatters.ts       # UPDATED (Cairo timezone)
```

---

## الثوابت الرئيسية

```typescript
// utils/constants.ts
export const TAX_RATE = 14;
export const CURRENCY_SYMBOL = "ج.م";
export const PAYMENT_METHODS = ["Cash", "Card", "Fawry"];

// utils/formatters.ts
const TIMEZONE = "Africa/Cairo";

export const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("ar-EG", { style: "currency", currency: "EGP" }).format(amount);

export const formatDateTime = (date: string | Date) =>
  new Intl.DateTimeFormat("ar-EG", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: TIMEZONE,
  }).format(parseApiDate(date));
```

---

## Redux Slices

| Slice         | المحتوى                                               |
| ------------- | ----------------------------------------------------- |
| `authSlice`   | token, user, isAuthenticated, login/logout            |
| `cartSlice`   | items, addItem, removeItem, updateQuantity, clearCart |
| `uiSlice`     | modals, loading states                                |
| `branchSlice` | currentBranch, branches, setBranch                    |

---

## RTK Query APIs

| API             | الاستخدام                                   |
| --------------- | ------------------------------------------- |
| `authApi`       | login, getMe                                |
| `productsApi`   | getProducts, createProduct, updateProduct   |
| `categoriesApi` | getCategories                               |
| `ordersApi`     | getOrders, createOrder, completeOrder       |
| `shiftsApi`     | getCurrentShift, openShift, closeShift      |
| `branchesApi`   | getBranches, getCurrentTenant, updateTenant |
| `auditApi`      | getAuditLogs (with date filters)            |
| `reportsApi`    | getDailyReport, getSalesReport              |

---

## ✅ Checklist للإنهاء

- [x] إنشاء `tenant.types.ts`
- [x] إنشاء `branch.types.ts`
- [x] إنشاء `audit.types.ts`
- [x] إنشاء `report.types.ts`
- [x] إنشاء `branchesApi.ts`
- [x] إنشاء `auditApi.ts`
- [x] إنشاء `reportsApi.ts`
- [x] إنشاء `branchSlice.ts`
- [x] تحديث `baseApi.ts` (إضافة X-Branch-Id header + Tags)
- [x] تحديث `store/index.ts` (إضافة branchSlice مع persist)
- [x] إنشاء `BranchSelector.tsx`
- [x] تحديث `MainLayout.tsx` (إضافة BranchSelector + رابط Audit)
- [x] إنشاء `AuditLogPage.tsx` (مع وصف عربي للعمليات)
- [x] إنشاء `DailyReportPage.tsx` (مع API integration)
- [x] تحديث Router (App.tsx)
- [x] إصلاح التوقيت (Cairo timezone)
- [x] اختبار البناء

---

## 🏆 ملخص التحسينات

### 1. واجهة سجل التدقيق (Audit Log UI)

```typescript
// تحويل الأكواد البرمجية إلى جمل بشرية واضحة
const getActionDescription = (log: AuditLog): string => {
  if (entityType === "Order") {
    if (action === "Create") return "إنشاء طلب جديد";
    if (newStatus === "Completed") return "تم إتمام الدفع وإغلاق الطلب";
    if (newStatus === "Cancelled") return "إلغاء الطلب";
  }
  if (entityType === "Shift") {
    if (action === "Create") return "فتح وردية";
    if (isClosed) return "إغلاق الوردية";
  }
  if (entityType === "Payment") {
    if (method === "Cash") return "تسجيل دفعة نقدية";
    if (method === "Card") return "تسجيل دفعة بالبطاقة";
  }
  // ...
};
```

**الفوائد:**
- عرض "تم إتمام الدفع وإغلاق الطلب" بدلاً من "Update Order"
- عرض "فتح وردية" بدلاً من "Create Shift"
- إزالة عمود IP غير المفيد
- إضافة Badges ملونة للحالات (مكتمل، ملغي، مسودة)

### 2. إصلاح التوقيت (Cairo Timezone)

```typescript
// utils/formatters.ts
const parseApiDate = (date: string | Date): Date => {
  if (date instanceof Date) return date;
  // Backend يخزن UTC بدون 'Z' suffix
  // نضيف 'Z' لتفسيره كـ UTC ثم نعرضه بتوقيت القاهرة
  if (!date.endsWith('Z') && !date.includes('+')) {
    return new Date(date + 'Z');
  }
  return new Date(date);
};

export const formatDateTime = (date: string | Date) =>
  new Intl.DateTimeFormat("ar-EG", {
    timeZone: "Africa/Cairo",
    // ...
  }).format(parseApiDate(date));
```

**الفوائد:**
- عرض الوقت الصحيح (توقيت القاهرة UTC+2)
- إصلاح فارق الساعتين الذي كان يظهر
- تطبيق موحد على كل الصفحات

### 3. التقارير اليومية (Daily Reports)

```typescript
// reportsApi.ts
getDailyReport: builder.query<ApiResponse<DailyReport>, string | undefined>({
  query: (date) => ({
    url: "/reports/daily",
    params: date ? { date } : undefined,
  }),
});

// DailyReportPage.tsx
const { data } = useGetDailyReportQuery(selectedDate);
// عرض: GrossSales, NetSales, TotalOrders, TotalCash, TotalCard, TopProducts
```

**الفوائد:**
- تقرير يومي متكامل مع API
- عرض المبيعات حسب طريقة الدفع
- عرض أعلى المنتجات مبيعاً
- عرض المبيعات بالساعة

### 4. تكامل الورديات (Shift Integration)

```typescript
// ShiftPage.tsx
const { data: shift } = useGetCurrentShiftQuery();
// عرض: Orders list, TotalCash, TotalCard, TotalOrders
// حساب ديناميكي للإجماليات من الطلبات المكتملة
```

**الفوائد:**
- عرض الطلبات داخل الوردية
- حساب الإجماليات تلقائياً
- دعم Eager Loading للـ Orders و Payments

---

## 🔗 ربط Frontend ↔ Backend

| Frontend                         | Backend Endpoint           | الوصف              |
| -------------------------------- | -------------------------- | ------------------ |
| `branchesApi.getBranches()`      | `GET /api/branches`        | جلب قائمة الفروع   |
| `branchesApi.getCurrentTenant()` | `GET /api/tenants/current` | بيانات الشركة      |
| `auditApi.getAuditLogs(filters)` | `GET /api/audit-logs`      | سجل التدقيق        |
| `reportsApi.getDailyReport()`    | `GET /api/reports/daily`   | التقرير اليومي     |
| `shiftsApi.getCurrentShift()`    | `GET /api/shifts/current`  | الوردية + الطلبات  |
| Header `X-Branch-Id`             | كل الطلبات                 | تحديد الفرع الحالي |

---

## أوامر مهمة

```powershell
# تشغيل المشروع
cd client
npm run dev

# بناء للإنتاج
npm run build

# فحص الأخطاء
npm run lint
```

---

## ملاحظات تنفيذية

- اتبع نظام التنسيقات في [DESIGN_SYSTEM.md](../design/DESIGN_SYSTEM.md) للألوان والمكونات.
- استخدم `useAppSelector` و `useAppDispatch` للتعامل مع Redux.
- حافظ على Type Safety في كل المكونات.
- أنواع الدفع: `'Cash' | 'Card' | 'Fawry'`.
- الضريبة: `14%` · العملة: `EGP` · الإقليم: `ar-EG`.
- الفرع الحالي يُحفظ في localStorage ويُرسل مع كل طلب API.
- التوقيت: يُعرض بتوقيت القاهرة (Africa/Cairo).

---

## 🔧 سجل الإصلاحات (Hotfixes)

### المشاكل التي وُجدت وتم إصلاحها

| المشكلة                                            | الملف                 | الإصلاح                                        |
| -------------------------------------------------- | --------------------- | ---------------------------------------------- |
| Endpoint خاطئ `/shifts` بدلاً من `/shifts/history` | `shiftsApi.ts`        | تصحيح الـ endpoint                             |
| عدم تطابق `CompleteOrderRequest`                   | `order.types.ts`      | استخدام `Payments[]`                           |
| نقص حقول في Types                                  | `order.types.ts`      | إضافة `taxRate`, `completedAt`, `payments`     |
| التوقيت خاطئ (فارق ساعتين)                         | `formatters.ts`       | إضافة `parseApiDate()` + Cairo timezone        |
| Audit Log غير مفهوم                                | `AuditLogPage.tsx`    | إضافة `getActionDescription()` بالعربي        |
| Date Filter لا يعمل                                | `auditApi.ts`         | إصلاح params building                          |
| Reports Page لا تعمل                               | `DailyReportPage.tsx` | إنشاء `reportsApi.ts` + integration            |
| Shift Orders لا تظهر                               | `ShiftPage.tsx`       | إضافة `ShiftOrderDto` + mapping                |

---

## 🎯 الدروس المستفادة (Lessons Learned)

### 1. **توحيد التوقيت**
```
❌ المشكلة: Backend يخزن UTC، Frontend يعرض Local
✅ الحل: parseApiDate() يضيف 'Z' + formatDateTime() يستخدم Cairo timezone
```

### 2. **واجهة مستخدم واضحة**
```
❌ المشكلة: عرض "Update Order" للمستخدم العادي
✅ الحل: تحويل الأكواد إلى جمل عربية واضحة
```

### 3. **Type Safety**
```
❌ المشكلة: Types لا تتطابق مع Backend DTOs
✅ الحل: مراجعة وتحديث Types مع كل تغيير في API
```

### 4. **API Integration**
```
❌ المشكلة: صفحات بدون API calls
✅ الحل: إنشاء RTK Query endpoints لكل feature
```

---

## 📋 TODO للـ Phase 2

- [ ] توليد TypeScript Types من Swagger/OpenAPI
- [ ] إضافة E2E Tests (Cypress/Playwright)
- [ ] إضافة Error Boundaries للتعامل مع API errors
- [ ] تحسين Loading States و Skeleton screens
- [ ] إضافة Offline Support (PWA)
- [ ] Dark Mode
- [ ] Print Receipts
- [ ] Export Reports (PDF)

---

## 🔗 العلاقة بين Frontend و Backend

### دورة حياة الطلب (Order Lifecycle)

```
┌─────────────────────────────────────────────────────────────────┐
│                         Frontend Flow                            │
├─────────────────────────────────────────────────────────────────┤
│  1. User opens shift (POST /api/shifts/open)                    │
│  2. User adds items to cart (cartSlice)                         │
│  3. User clicks "Checkout" → PaymentModal opens                 │
│  4. User selects payment method & enters amount                 │
│  5. Frontend calls createOrder() → POST /api/orders             │
│  6. Frontend calls completeOrder() → POST /api/orders/{id}/complete │
│  7. Cart is cleared, success toast shown                        │
│  8. Shift totals updated automatically                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                         Backend Flow                             │
├─────────────────────────────────────────────────────────────────┤
│  1. POST /api/shifts/open                                       │
│     → ShiftService.OpenAsync()                                  │
│     → Validate user has no open shift                           │
│     → Create Shift with TenantId, BranchId, UserId              │
│                                                                 │
│  2. POST /api/orders                                            │
│     → OrderService.CreateAsync()                                │
│     → Validate open shift exists                                │
│     → Set ShiftId, TenantId, BranchId                          │
│     → Create Snapshots (Product prices, Branch info)            │
│     → Calculate totals (Subtotal, Tax 14%, Total)              │
│     → Return OrderDto with Status = "Draft"                    │
│                                                                 │
│  3. POST /api/orders/{id}/complete                             │
│     → OrderService.CompleteAsync()                             │
│     → Create Payment(s) with TenantId, BranchId                │
│     → Set Status = "Completed", AmountPaid, ChangeAmount       │
│     → AuditLog created automatically                           │
│     → Return updated OrderDto with Payments                    │
└─────────────────────────────────────────────────────────────────┘
```
