# 🚀 Phase 2 Development Roadmap

> **الإصدار:** 2.0.0 (مخطط)  
> **تاريخ البدء المتوقع:** يناير 2026  
> **المتطلبات:** إكمال Phase 1 ✅

---

## 📋 Overview

هذا الملف يحدد خارطة الطريق للمرحلة الثانية من تطوير KasserPro بناءً على تحليل Phase 1.

---

## 🎯 Phase 2 Goals

1. **تحسين تجربة المستخدم** - إضافة ميزات مطلوبة للاستخدام اليومي
2. **زيادة الموثوقية** - Offline mode, error recovery
3. **توسيع القدرات** - Dashboard, Analytics, Printing
4. **إدارة أفضل** - User management, Customer database

---

## 📦 Feature Breakdown

### Sprint 1: Pre-Production Fixes (1 Week)

| Task                              | Priority    | Effort  |
| --------------------------------- | ----------- | ------- |
| Remove DebugController.cs         | 🔴 Critical | 5 min   |
| Add Rate Limiting middleware      | 🔴 Critical | 2 hours |
| Environment variables for secrets | 🔴 Critical | 1 hour  |
| Production CORS configuration     | 🔴 Critical | 30 min  |
| Error boundary component          | 🟡 High     | 2 hours |

**Deliverable:** Production-ready security configuration

---

### Sprint 2: Receipt Printing (1-2 Weeks)

#### Backend Tasks

```
□ Create ReceiptTemplate entity
□ Add GET /api/receipts/templates endpoint
□ Add GET /api/orders/{id}/receipt endpoint (formatted data)
```

#### Frontend Tasks

```
□ Create PrintableReceipt component
□ Create ReceiptPreview modal
□ Add print button to OrderDetailsModal
□ Add print button to PaymentModal (after success)
□ Thermal printer CSS styles (80mm width)
```

#### Files to Create

```
client/src/components/print/
├── PrintableReceipt.tsx
├── ReceiptPreview.tsx
└── printStyles.css

src/KasserPro.Application/DTOs/ReceiptDto.cs
src/KasserPro.API/Controllers/ReceiptsController.cs
```

**Deliverable:** Working receipt printing with thermal printer support

---

### Sprint 3: User Management UI (1 Week)

#### Backend Tasks

```
□ Add GET /api/users endpoint (Admin only)
□ Add PUT /api/users/{id} endpoint
□ Add DELETE /api/users/{id} endpoint
□ Add PUT /api/users/{id}/password endpoint
```

#### Frontend Tasks

```
□ Create UsersPage
□ Create UserFormModal
□ Create ChangePasswordModal
□ Add route /users (Admin only)
□ Add navigation link
```

#### Files to Create

```
client/src/pages/users/
├── UsersPage.tsx
├── UserFormModal.tsx
└── ChangePasswordModal.tsx

client/src/api/usersApi.ts
client/src/types/user.types.ts (extend existing)
```

**Deliverable:** Full user CRUD with password management

---

### Sprint 4: Dashboard & Analytics (2 Weeks)

#### Backend Tasks

```
□ Add GET /api/dashboard/summary endpoint
□ Add GET /api/dashboard/sales-chart endpoint
□ Add GET /api/dashboard/top-products endpoint
□ Add GET /api/dashboard/hourly-sales endpoint
```

#### Frontend Tasks

```
□ Install chart library (recharts or chart.js)
□ Create DashboardPage
□ Create SalesChart component
□ Create TopProductsCard component
□ Create HourlySalesChart component
□ Create SummaryCards component
```

#### Files to Create

```
client/src/pages/dashboard/
├── DashboardPage.tsx
└── components/
    ├── SalesChart.tsx
    ├── TopProductsCard.tsx
    ├── HourlySalesChart.tsx
    └── SummaryCards.tsx

client/src/api/dashboardApi.ts
src/KasserPro.API/Controllers/DashboardController.cs
src/KasserPro.Application/Services/DashboardService.cs
```

**Deliverable:** Visual dashboard with sales analytics

---

### Sprint 5: Offline Mode / PWA (2 Weeks)

#### Tasks

```
□ Add service worker (vite-plugin-pwa)
□ Configure workbox for API caching
□ Add IndexedDB for offline orders
□ Create useOffline hook
□ Add offline indicator in UI
□ Queue orders for sync when online
□ Add sync status component
```

#### Files to Create

```
client/src/
├── sw.ts                    # Service worker
├── hooks/useOffline.ts      # Offline detection
├── hooks/useOrderQueue.ts   # Offline order queue
├── utils/db.ts              # IndexedDB wrapper
└── components/common/
    ├── OfflineIndicator.tsx
    └── SyncStatus.tsx

vite.config.ts               # Add PWA plugin
```

**Deliverable:** App works offline, syncs when connection restored

---

### Sprint 6: Order Refunds (1 Week)

#### Backend Tasks

```
□ Add POST /api/orders/{id}/refund endpoint
□ Add refund reason to Order entity
□ Update shift totals on refund
□ Add audit log for refunds
```

#### Frontend Tasks

```
□ Add Refund button to OrderDetailsModal
□ Create RefundModal with reason input
□ Update order status display for refunded
□ Add refunded orders filter
```

#### Files to Create

```
client/src/components/orders/
└── RefundModal.tsx

src/KasserPro.Application/DTOs/RefundOrderRequest.cs
```

**Deliverable:** Complete refund flow with audit trail

---

### Sprint 7: Customer Management (2 Weeks)

#### Backend Tasks

```
□ Create Customer entity
□ Create CustomerRepository
□ Create CustomerService
□ Add CRUD endpoints for customers
□ Link customers to orders (optional)
□ Add customer search endpoint
```

#### Frontend Tasks

```
□ Create CustomersPage
□ Create CustomerFormModal
□ Create CustomerSearchModal (for POS)
□ Add customer selection to PaymentModal
□ Display customer info on receipts
```

#### Files to Create

```
src/KasserPro.Domain/Entities/Customer.cs
src/KasserPro.Application/Services/CustomerService.cs
src/KasserPro.API/Controllers/CustomersController.cs

client/src/pages/customers/
├── CustomersPage.tsx
├── CustomerFormModal.tsx
└── CustomerSearchModal.tsx

client/src/api/customersApi.ts
client/src/types/customer.types.ts
```

**Deliverable:** Customer database with order linking

---

## 📅 Timeline Overview

```
Week 1:     Sprint 1 - Pre-Production Fixes
Week 2-3:   Sprint 2 - Receipt Printing
Week 4:     Sprint 3 - User Management
Week 5-6:   Sprint 4 - Dashboard
Week 7-8:   Sprint 5 - Offline Mode
Week 9:     Sprint 6 - Refunds
Week 10-11: Sprint 7 - Customers
Week 12:    Testing & Polish
```

**Total Estimated Time:** 12 Weeks (3 Months)

---

## 🔧 Technical Debt to Address

| Item                         | Sprint  | Notes                        |
| ---------------------------- | ------- | ---------------------------- |
| Replace `as any` casts       | 1       | Create typed error interface |
| Add unit tests for cartSlice | 1-2     | Tax calculation tests        |
| Add component tests          | Ongoing | Jest + React Testing Library |
| Improve loading states       | Ongoing | Skeleton loaders             |
| Add form validation          | Ongoing | Zod schemas for all forms    |

---

## 📦 Dependencies to Add

### Frontend

```json
{
  "recharts": "^2.x", // Charts
  "vite-plugin-pwa": "^0.x", // PWA support
  "idb": "^7.x", // IndexedDB wrapper
  "react-to-print": "^2.x" // Print support
}
```

### Backend

```xml
<PackageReference Include="AspNetCoreRateLimit" Version="5.x" />
```

---

## ✅ Definition of Done (Phase 2)

- [ ] All Sprint features implemented
- [ ] Receipt printing working with thermal printers
- [ ] Dashboard showing real-time analytics
- [ ] App works offline with sync
- [ ] User management complete
- [ ] Customer database functional
- [ ] All existing E2E tests passing
- [ ] New E2E tests for Phase 2 features
- [ ] Documentation updated
- [ ] Performance audit passed

---

## 📝 Notes

- كل Sprint يمكن أن يكون مستقل ويُطلق بشكل منفصل
- الأولوية للـ Receipt Printing لأنه الأكثر طلباً
- الـ Offline Mode مهم للمناطق ذات الاتصال الضعيف
- Dashboard يعطي قيمة عالية للمديرين

---

_Last Updated: January 2026_
