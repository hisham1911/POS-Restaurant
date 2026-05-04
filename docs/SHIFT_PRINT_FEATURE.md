# ✅ إضافة ميزة طباعة تقرير الوردية

## 📋 الملخص

تم إضافة زر طباعة في نافذة تفاصيل الوردية يسمح بطباعة تقرير شامل ومبسط للوردية.

---

## 🎯 المشكلة

المستخدم طلب:
1. إضافة خيار لطباعة الوردية عند عرض بياناتها
2. تبسيط المعلومات المعروضة (كانت معقدة وغير واضحة)
3. طباعة ملخص المنتجات المباعة (مثلاً: 100 بيتزا، 7 فطير)

---

## ✅ الحل المُنفذ

### 1. إضافة زر الطباعة

**الموقع:** `frontend/src/components/shifts/ShiftDetailsDrawer.tsx`

```typescript
<button
  onClick={handlePrint}
  className="flex items-center gap-2 rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-primary-700"
>
  <Printer className="h-4 w-4" />
  طباعة التقرير
</button>
```

### 2. تبسيط عرض تفاصيل الوردية

**التغييرات:**
- ✅ إزالة حقل "حالة المراجعة" (مربك وغير مهم للمستخدم العادي)
- ✅ إخفاء "ديون مسددة" و"المصروفات" و"المرتجعات" إذا كانت = 0
- ✅ إخفاء قسم "ملاحظات إضافية" بالكامل إذا لم يكن هناك ملاحظات أو حالات خاصة
- ✅ تبسيط عرض عدد الطلبات في المعلومات الأساسية
- ✅ إزالة كارت "سداد الديون" المنفصل ودمجه في الملخص المالي

### 3. التقرير المطبوع

**المحتويات:**

#### 📋 معلومات الوردية
- الكاشير
- الحالة (مفتوحة/مغلقة)
- وقت الفتح
- وقت الإغلاق

#### 💰 الملخص المالي
- رصيد الافتتاح
- إجمالي المبيعات
- إجمالي المقبوض
- المبلغ الآجل
- ديون مسددة (إذا > 0)
- المصروفات (إذا > 0)
- المرتجعات (إذا > 0)
- الرصيد المتوقع
- الرصيد الفعلي
- الفرق (فائض/عجز)

#### 💳 التحصيل حسب وسيلة الدفع
- نقدي
- بطاقة
- فوري
- تحويل بنكي

#### 📊 إحصائيات الطلبات
- إجمالي الطلبات
- مكتملة
- ملغاة
- مسترجعة

#### 📝 ملاحظات (إذا وجدت)

---

## ⚠️ قيد التطوير: ملخص المنتجات

### المشكلة الحالية

لا يمكن حالياً عرض ملخص المنتجات المباعة (مثل: 100 بيتزا، 7 فطير) لأن:

1. **`ShiftOrder` لا يحتوي على `items`**
   - الـ `Shift` يحتوي على `orders: ShiftOrder[]`
   - لكن `ShiftOrder` يحتوي فقط على معلومات أساسية (رقم الطلب، الإجمالي، الحالة)
   - لا يحتوي على `items: OrderItem[]`

2. **الحلول الممكنة:**

#### الحل 1: تعديل الباك-اند (الأفضل)
```csharp
// في ShiftsController.cs - endpoint GetShiftById
// إضافة include للـ order items

var shift = await _context.Shifts
    .Include(s => s.Orders)
        .ThenInclude(o => o.Items)  // ✅ إضافة هذا السطر
    .FirstOrDefaultAsync(s => s.Id == id);
```

**المميزات:**
- ✅ حل نظيف ومباشر
- ✅ البيانات تأتي مع الوردية مباشرة
- ✅ لا حاجة لطلبات إضافية

**العيوب:**
- ⚠️ يزيد حجم الـ response
- ⚠️ يحتاج تعديل في الباك-اند

#### الحل 2: Endpoint منفصل للملخص
```csharp
// إضافة endpoint جديد
[HttpGet("{id}/products-summary")]
public async Task<IActionResult> GetShiftProductsSummary(int id)
{
    var summary = await _context.OrderItems
        .Where(oi => oi.Order.ShiftId == id 
                  && oi.Order.Status == OrderStatus.Completed)
        .GroupBy(oi => oi.ProductName)
        .Select(g => new {
            ProductName = g.Key,
            TotalQuantity = g.Sum(oi => oi.Quantity),
            TotalAmount = g.Sum(oi => oi.Total)
        })
        .OrderByDescending(x => x.TotalQuantity)
        .ToListAsync();
    
    return Ok(ApiResponse<object>.Success(summary));
}
```

**المميزات:**
- ✅ لا يؤثر على الـ endpoints الموجودة
- ✅ يُستدعى فقط عند الحاجة (عند الطباعة)
- ✅ البيانات مُجمّعة من الباك-اند (أسرع)

**العيوب:**
- ⚠️ طلب إضافي من الفرونت

#### الحل 3: جلب كل الطلبات من الفرونت
```typescript
// في handlePrint
const ordersWithItems = await Promise.all(
  shiftDetails.orders.map(order => 
    fetch(`/api/orders/${order.id}`).then(r => r.json())
  )
);

const aggregated = aggregateProducts(ordersWithItems);
```

**المميزات:**
- ✅ لا يحتاج تعديل في الباك-اند

**العيوب:**
- ❌ بطيء جداً (طلب لكل order)
- ❌ يستهلك bandwidth كبير
- ❌ غير عملي للورديات الكبيرة

---

## 🎯 التوصية

**الحل الموصى به: الحل 2 (Endpoint منفصل)**

### الخطوات المطلوبة:

#### 1. الباك-اند
```csharp
// في ShiftsController.cs
[HttpGet("{id}/products-summary")]
[HasPermission(Permission.OrdersView)]
public async Task<IActionResult> GetShiftProductsSummary(int id, CancellationToken ct)
{
    var shift = await _context.Shifts
        .FirstOrDefaultAsync(s => s.Id == id 
                              && s.TenantId == _currentUser.TenantId
                              && s.BranchId == _currentUser.BranchId, ct);
    
    if (shift is null)
        return NotFound(ApiResponse<object>.Fail(
            ErrorCodes.SHIFT_NOT_FOUND,
            ErrorMessages.Get(ErrorCodes.SHIFT_NOT_FOUND)));
    
    var summary = await _context.OrderItems
        .Where(oi => oi.Order.ShiftId == id 
                  && oi.Order.TenantId == _currentUser.TenantId
                  && oi.Order.BranchId == _currentUser.BranchId
                  && oi.Order.Status == OrderStatus.Completed)
        .GroupBy(oi => oi.ProductName)
        .Select(g => new {
            ProductName = g.Key,
            TotalQuantity = g.Sum(oi => oi.Quantity),
            TotalAmount = g.Sum(oi => oi.Total)
        })
        .OrderByDescending(x => x.TotalQuantity)
        .ToListAsync(ct);
    
    return Ok(ApiResponse<object>.Success(summary));
}
```

#### 2. الفرونت-اند

**Types:**
```typescript
// في shift.types.ts
export interface ShiftProductSummary {
  productName: string;
  totalQuantity: number;
  totalAmount: number;
}
```

**API:**
```typescript
// في shiftsApi.ts
getShiftProductsSummary: builder.query<ApiResponse<ShiftProductSummary[]>, number>({
  query: (id) => `/shifts/${id}/products-summary`,
}),
```

**Component:**
```typescript
// في ShiftDetailsDrawer.tsx
const { data: productsSummary } = useGetShiftProductsSummaryQuery(shiftId, {
  skip: !isOpen || !shiftId,
});

// في generatePrintContent
<div class="section">
  <div class="section-title">📦 ملخص المنتجات المباعة</div>
  <table>
    <thead>
      <tr>
        <th>المنتج</th>
        <th>الكمية</th>
        <th>الإجمالي</th>
      </tr>
    </thead>
    <tbody>
      ${productsSummary?.data?.map(p => `
        <tr>
          <td>${p.productName}</td>
          <td>${p.totalQuantity}</td>
          <td>${formatCurrency(p.totalAmount)}</td>
        </tr>
      `).join('')}
    </tbody>
  </table>
</div>
```

---

## 📝 ملاحظات

1. **الطباعة تعمل حالياً** بدون ملخص المنتجات
2. **ملخص المنتجات** يحتاج تطوير إضافي في الباك-اند
3. **التقرير المطبوع** يفتح في نافذة جديدة ويستخدم `window.print()`
4. **التصميم** responsive ومناسب للطباعة على A4

---

## 🔄 الخطوات التالية

إذا أراد المستخدم إضافة ملخص المنتجات:

1. ✅ تنفيذ الـ endpoint في الباك-اند
2. ✅ إضافة الـ API call في الفرونت
3. ✅ تحديث التقرير المطبوع ليشمل الجدول
4. ✅ اختبار مع وردية تحتوي على طلبات كثيرة

---

**تاريخ التوثيق:** 4 مايو 2026  
**الحالة:** ✅ الطباعة الأساسية مُنفذة | ⏳ ملخص المنتجات قيد الانتظار
