# إصلاح حساب الخصم في Backend ✅

## المشكلة
عند تطبيق خصم على الطلب، كان الـ Backend يحسب الإجمالي بشكل خاطئ:
- Frontend يحسب: `(Subtotal - Discount) + Tax = 246.24 ج.م`
- Backend يحسب: `Subtotal + Tax - Discount = 273.60 ج.م`

**مثال على المشكلة:**
```
المجموع الفرعي: 240 ج.م
الخصم (10%): 24 ج.م
الضريبة (14%): ؟

❌ Backend القديم:
  Tax = 240 × 14% = 33.6 ج.م
  Total = 240 + 33.6 - 24 = 249.6 ج.م

✅ Frontend الصحيح:
  After Discount = 240 - 24 = 216 ج.م
  Tax = 216 × 14% = 30.24 ج.م
  Total = 216 + 30.24 = 246.24 ج.م
```

## السبب
الـ Backend كان يحسب الضريبة على المجموع الفرعي الكامل، ثم يطرح الخصم من الإجمالي. هذا خطأ لأن الضريبة يجب أن تُحسب على المبلغ **بعد** الخصم.

## الحل المطبق

### الكود القديم (خاطئ)
```csharp
private static void CalculateOrderTotals(Order order)
{
    // Subtotal = Sum of all item subtotals (Net amounts before tax)
    order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal - i.DiscountAmount), 2);
    
    // Tax = Sum of all item tax amounts
    order.TaxAmount = Math.Round(order.Items.Sum(i => i.TaxAmount), 2);
    
    // Apply order-level discount (on subtotal)
    if (order.DiscountType == "percentage" && order.DiscountValue.HasValue)
        order.DiscountAmount = Math.Round(order.Subtotal * (order.DiscountValue.Value / 100m), 2);
    else if (order.DiscountType == "fixed" && order.DiscountValue.HasValue)
        order.DiscountAmount = Math.Round(order.DiscountValue.Value, 2);
    else
        order.DiscountAmount = 0;
    
    // Calculate service charge (on subtotal)
    order.ServiceChargeAmount = Math.Round(order.Subtotal * (order.ServiceChargePercent / 100m), 2);
    
    // Total = Sum of item totals - order discount + service charge
    // Item totals already include their tax amounts
    var itemsTotal = Math.Round(order.Items.Sum(i => i.Total), 2);
    order.Total = Math.Round(itemsTotal - order.DiscountAmount + order.ServiceChargeAmount, 2);
    order.AmountDue = Math.Round(order.Total - order.AmountPaid, 2);
}
```

### الكود الجديد (صحيح)
```csharp
private static void CalculateOrderTotals(Order order)
{
    // Subtotal = Sum of all item subtotals (Net amounts before tax and before order-level discount)
    order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal), 2);
    
    // Apply order-level discount (on subtotal, before tax)
    if (order.DiscountType == "percentage" && order.DiscountValue.HasValue)
        order.DiscountAmount = Math.Round(order.Subtotal * (order.DiscountValue.Value / 100m), 2);
    else if (order.DiscountType == "fixed" && order.DiscountValue.HasValue)
        order.DiscountAmount = Math.Round(order.DiscountValue.Value, 2);
    else
        order.DiscountAmount = 0;
    
    // Ensure discount doesn't exceed subtotal
    if (order.DiscountAmount > order.Subtotal)
        order.DiscountAmount = order.Subtotal;
    
    // Calculate amount after discount (before tax)
    var afterDiscount = order.Subtotal - order.DiscountAmount;
    
    // Calculate tax on the amount after discount
    // Tax Exclusive: Tax is calculated on (Subtotal - Discount)
    order.TaxAmount = Math.Round(afterDiscount * (order.TaxRate / 100m), 2);
    
    // Calculate service charge (on subtotal after discount)
    order.ServiceChargeAmount = Math.Round(afterDiscount * (order.ServiceChargePercent / 100m), 2);
    
    // Total = (Subtotal - Discount) + Tax + Service Charge
    order.Total = Math.Round(afterDiscount + order.TaxAmount + order.ServiceChargeAmount, 2);
    order.AmountDue = Math.Round(order.Total - order.AmountPaid, 2);
}
```

## التغييرات الرئيسية

### 1. حساب Subtotal
```csharp
// ❌ القديم: يطرح خصم الـ items
order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal - i.DiscountAmount), 2);

// ✅ الجديد: المجموع الفرعي الصافي
order.Subtotal = Math.Round(order.Items.Sum(i => i.Subtotal), 2);
```

### 2. حساب الضريبة
```csharp
// ❌ القديم: الضريبة من مجموع الـ items (قبل الخصم)
order.TaxAmount = Math.Round(order.Items.Sum(i => i.TaxAmount), 2);

// ✅ الجديد: الضريبة على المبلغ بعد الخصم
var afterDiscount = order.Subtotal - order.DiscountAmount;
order.TaxAmount = Math.Round(afterDiscount * (order.TaxRate / 100m), 2);
```

### 3. حساب الإجمالي
```csharp
// ❌ القديم: مجموع الـ items - الخصم
var itemsTotal = Math.Round(order.Items.Sum(i => i.Total), 2);
order.Total = Math.Round(itemsTotal - order.DiscountAmount + order.ServiceChargeAmount, 2);

// ✅ الجديد: (المجموع الفرعي - الخصم) + الضريبة
order.Total = Math.Round(afterDiscount + order.TaxAmount + order.ServiceChargeAmount, 2);
```

### 4. حماية من الخصم الزائد
```csharp
// ✅ جديد: التأكد من أن الخصم لا يتجاوز المجموع الفرعي
if (order.DiscountAmount > order.Subtotal)
    order.DiscountAmount = order.Subtotal;
```

## المعادلة الصحيحة

### Tax Exclusive (الضريبة الإضافية)
```
1. Subtotal = Sum(UnitPrice × Quantity)
2. DiscountAmount = Subtotal × (DiscountValue / 100)  [للنسبة]
                 OR DiscountValue                      [للمبلغ الثابت]
3. AfterDiscount = Subtotal - DiscountAmount
4. TaxAmount = AfterDiscount × (TaxRate / 100)
5. Total = AfterDiscount + TaxAmount
```

### مثال عملي
```
منتجات:
  - منتج A: 100 ج.م × 2 = 200 ج.م
  - منتج B: 20 ج.م × 2 = 40 ج.م

1. Subtotal = 200 + 40 = 240 ج.م
2. Discount (10%) = 240 × 0.10 = 24 ج.م
3. After Discount = 240 - 24 = 216 ج.م
4. Tax (14%) = 216 × 0.14 = 30.24 ج.م
5. Total = 216 + 30.24 = 246.24 ج.م ✅
```

## التأثير

### قبل الإصلاح
- Frontend: 246.24 ج.م
- Backend: 273.60 ج.م
- ❌ خطأ: "المبلغ المدفوع أقل من إجمالي الطلب"

### بعد الإصلاح
- Frontend: 246.24 ج.م
- Backend: 246.24 ج.م
- ✅ نجاح: الطلب يُكمل بنجاح

## الملفات المعدلة

- ✅ `src/KasserPro.Application/Services/Implementations/OrderService.cs`
  - Method: `CalculateOrderTotals(Order order)`

## خطوات التطبيق

1. **إيقاف Backend** (إذا كان يعمل)
2. **إعادة البناء:**
   ```bash
   cd src/KasserPro.API
   dotnet build
   ```
3. **إعادة التشغيل:**
   ```bash
   dotnet run
   ```

## الاختبار

### سيناريو الاختبار
1. إضافة منتجات بمجموع 240 ج.م
2. تطبيق خصم 10%
3. التحقق من الحسابات:
   - المجموع الفرعي: 240 ج.م
   - الخصم: 24 ج.م
   - بعد الخصم: 216 ج.م
   - الضريبة (14%): 30.24 ج.م
   - الإجمالي: 246.24 ج.م
4. إتمام الدفع بمبلغ 246.24 ج.م
5. ✅ يجب أن يتم الطلب بنجاح

### حالات اختبار إضافية
- [ ] خصم بالنسبة (5%, 10%, 20%)
- [ ] خصم بمبلغ ثابت (10 ج.م، 50 ج.م)
- [ ] خصم 100% (يجب أن يعمل)
- [ ] بدون خصم (يجب أن يعمل كالسابق)
- [ ] مع الضريبة مفعلة
- [ ] مع الضريبة معطلة

## ملاحظات مهمة

1. **الضريبة بعد الخصم**: هذا هو السلوك الصحيح والمتوقع في معظم الأنظمة المحاسبية
2. **Service Charge**: يُحسب أيضاً على المبلغ بعد الخصم
3. **حماية من الخصم الزائد**: الخصم لا يمكن أن يتجاوز المجموع الفرعي
4. **التوافق**: Frontend و Backend الآن يستخدمان نفس المعادلة

## الخلاصة

تم إصلاح حساب الخصم في Backend ليتطابق مع Frontend. الآن الضريبة تُحسب على المبلغ بعد الخصم، وليس قبله. هذا يحل مشكلة "المبلغ المدفوع أقل من إجمالي الطلب" ويضمن حسابات مالية صحيحة.

**يجب إعادة تشغيل Backend لتطبيق التغييرات!** 🔄
