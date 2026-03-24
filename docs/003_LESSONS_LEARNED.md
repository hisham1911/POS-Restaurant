# 📚 الدروس المستفادة - Phase 1 & MVP

## 🔧 مشاكل تم حلها

### 1. مشكلة Port الاتصال
**المشكلة:** الفرونت إند كان يحاول الاتصال بـ `localhost:5000` بينما الباك إند يعمل على `localhost:5243`

**الحل:**
- تحديث `client/.env` → `VITE_API_URL=http://localhost:5243/api`
- تحديث `client/vite.config.ts` → proxy target إلى `localhost:5243`

**الدرس:** دائماً تحقق من إعدادات الـ ports في كلا الجانبين قبل البدء.

---

### 2. تحذيرات EF Core Query Filters
**المشكلة:** تحذيرات عن Global Query Filters غير متطابقة بين الكيانات المرتبطة

**الحل:** إضافة Query Filters لجميع الكيانات المرتبطة:
```csharp
modelBuilder.Entity<OrderItem>().HasQueryFilter(e => !e.IsDeleted);
modelBuilder.Entity<Payment>().HasQueryFilter(e => !e.IsDeleted);
modelBuilder.Entity<Shift>().HasQueryFilter(e => !e.IsDeleted);
modelBuilder.Entity<AuditLog>().HasQueryFilter(e => !e.IsDeleted);
```

**الدرس:** عند استخدام Global Query Filters، تأكد من تطبيقها على جميع الكيانات المرتبطة.

---

### 3. عدم استخدام TenantId في الـ Queries
**المشكلة:** بعض الـ Services لم تكن تفلتر البيانات حسب TenantId

**الحل:** تحديث جميع الـ queries لتشمل:
```csharp
.Where(p => p.TenantId == _currentTenantId)
```

**الدرس:** في نظام Multi-Tenant، كل query يجب أن يفلتر حسب TenantId.

---

### 4. نسبة الضريبة
**المشكلة:** كانت نسبة الضريبة 15% بدلاً من 14% (VAT مصر)

**الحل:** تحديث `TaxRate` في Order entity إلى 14%

**الدرس:** تأكد من الثوابت المالية حسب البلد المستهدف.

---

## 🚀 ميزات MVP المُنفذة

### 1. Idempotency Middleware
**الغرض:** منع تكرار العمليات الحرجة (إنشاء طلب، إكمال طلب، إلخ)

**التنفيذ:**
- `IdempotencyMiddleware.cs` - يتحقق من header `Idempotency-Key`
- يخزن الاستجابات في MemoryCache لمدة 24 ساعة
- يُرجع الاستجابة المخزنة مع header `X-Idempotency-Replayed: true`

**الاستخدام في Frontend:**
```typescript
headers: {
  "Idempotency-Key": `order-${Date.now()}-${Math.random().toString(36).substring(7)}`,
}
```

---

### 2. Price/Tax Snapshots
**الغرض:** حفظ الأسعار والضرائب وقت إنشاء الطلب (لا تتأثر بتغييرات لاحقة)

**الحقول المضافة للـ Order:**
- `BranchName`, `BranchAddress`, `BranchPhone` - snapshot الفرع
- `UserName` - snapshot المستخدم
- `CurrencyCode`, `ServiceChargePercent`, `ServiceChargeAmount`

**الحقول المضافة للـ OrderItem:**
- `ProductNameEn`, `ProductSku`, `ProductBarcode` - snapshot المنتج
- `OriginalPrice`, `DiscountType`, `DiscountValue`, `DiscountReason`
- `TaxRate`, `TaxInclusive`, `Subtotal`

---

### 3. Order State Machine
**الغرض:** منع التحولات غير الصالحة بين حالات الطلب

**التحولات المسموحة:**
```
Draft → Pending, Completed, Cancelled
Pending → Completed, Cancelled
Completed → (لا شيء)
Cancelled → (لا شيء)
```

**التنفيذ:**
```csharp
private static readonly Dictionary<OrderStatus, OrderStatus[]> ValidTransitions = new()
{
    { OrderStatus.Draft, new[] { OrderStatus.Pending, OrderStatus.Completed, OrderStatus.Cancelled } },
    { OrderStatus.Pending, new[] { OrderStatus.Completed, OrderStatus.Cancelled } },
    { OrderStatus.Completed, Array.Empty<OrderStatus>() },
    { OrderStatus.Cancelled, Array.Empty<OrderStatus>() }
};
```

---

### 4. Auto Audit Logging
**الغرض:** تسجيل تلقائي لجميع التغييرات على الكيانات المهمة

**التنفيذ:**
- `AuditSaveChangesInterceptor.cs` - EF Core Interceptor
- يسجل Create, Update, Delete للكيانات: Order, Product, Category, User, Branch, Shift, Payment
- يحفظ القيم القديمة والجديدة بصيغة JSON

---

### 5. Error Codes System
**الغرض:** توحيد رسائل الخطأ وتسهيل التعامل معها في Frontend

**التنفيذ:**
- `ErrorCodes.cs` - ثوابت لأكواد الأخطاء
- `ErrorMessages.cs` - رسائل عربية لكل كود
- `ApiResponse.ErrorCode` - حقل جديد في الاستجابة

**مثال:**
```csharp
return ApiResponse<OrderDto>.Fail(ErrorCodes.ORDER_NOT_FOUND, ErrorMessages.Get(ErrorCodes.ORDER_NOT_FOUND));
```

---

## ✅ أفضل الممارسات المتبعة

### Backend
1. **Clean Architecture** - فصل واضح بين الطبقات
2. **Repository Pattern** - عبر IUnitOfWork
3. **DTOs** - لا نُرجع Entities مباشرة
4. **Soft Delete** - باستخدام IsDeleted flag
5. **Global Query Filters** - لإخفاء المحذوفات تلقائياً

### Frontend
1. **RTK Query** - للـ API calls مع caching
2. **Redux Persist** - لحفظ الـ auth و branch في localStorage
3. **Type Safety** - TypeScript في كل مكان
4. **Component-based** - مكونات قابلة لإعادة الاستخدام

---

## 📋 Checklist للمشاريع المستقبلية

### قبل البدء
- [ ] تحديد الـ ports للـ Backend و Frontend
- [ ] إعداد ملفات `.env` بشكل صحيح
- [ ] التأكد من إعدادات CORS

### Multi-Tenant
- [ ] إضافة TenantId لجميع الكيانات
- [ ] إضافة Query Filters متسقة
- [ ] التحقق من TenantId في كل Service method

### API Integration
- [ ] مطابقة الـ DTOs بين Frontend و Backend
- [ ] التحقق من أسماء الـ properties (camelCase vs PascalCase)
- [ ] اختبار جميع الـ endpoints

---

## 🔗 الملفات المهمة

| الملف | الغرض |
|-------|-------|
| `client/.env` | إعدادات الفرونت إند |
| `src/KasserPro.API/appsettings.json` | إعدادات الباك إند |
| `client/vite.config.ts` | إعدادات Vite و proxy |
| `src/KasserPro.Infrastructure/Data/AppDbContext.cs` | Query Filters |
