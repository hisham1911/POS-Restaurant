# ✅ Checklist اختبار - الضريبة الاختيارية في فواتير الشراء

> **التاريخ:** 2 مايو 2026  
> **الهدف:** اختبار شامل للميزة الجديدة

---

## 🧪 Backend Testing

### Test 1: إنشاء فاتورة بضريبة (الافتراضي)

```bash
POST /api/purchaseinvoices
Content-Type: application/json
Authorization: Bearer {token}

{
  "supplierId": 1,
  "invoiceDate": "2026-05-02T00:00:00Z",
  "items": [
    {
      "productId": 1,
      "quantity": 10,
      "purchasePrice": 100,
      "sellingPrice": 120
    }
  ],
  "notes": "فاتورة اختبار بضريبة"
}
```

**Expected Response:**
```json
{
  "success": true,
  "data": {
    "id": 123,
    "invoiceNumber": "PI-2026-00123",
    "subtotal": 1000.00,
    "taxRate": 14,
    "isTaxEnabled": true,
    "taxAmount": 140.00,
    "total": 1140.00,
    "status": "Draft"
  }
}
```

**Verification:**
- [ ] `isTaxEnabled` = `true` (default)
- [ ] `taxAmount` = `140.00` (14% of 1000)
- [ ] `total` = `1140.00` (1000 + 140)

---

### Test 2: إنشاء فاتورة بدون ضريبة

```bash
POST /api/purchaseinvoices
Content-Type: application/json
Authorization: Bearer {token}

{
  "supplierId": 1,
  "invoiceDate": "2026-05-02T00:00:00Z",
  "items": [
    {
      "productId": 1,
      "quantity": 10,
      "purchasePrice": 100,
      "sellingPrice": 120
    }
  ],
  "isTaxEnabled": false,
  "notes": "فاتورة اختبار بدون ضريبة"
}
```

**Expected Response:**
```json
{
  "success": true,
  "data": {
    "id": 124,
    "invoiceNumber": "PI-2026-00124",
    "subtotal": 1000.00,
    "taxRate": 14,
    "isTaxEnabled": false,
    "taxAmount": 0.00,
    "total": 1000.00,
    "status": "Draft"
  }
}
```

**Verification:**
- [ ] `isTaxEnabled` = `false`
- [ ] `taxAmount` = `0.00`
- [ ] `total` = `1000.00` (subtotal only)

---

### Test 3: Preview API - بضريبة

```bash
POST /api/purchaseinvoices/prepare
Content-Type: application/json
Authorization: Bearer {token}

{
  "supplierId": 1,
  "invoiceDate": "2026-05-02T00:00:00Z",
  "items": [
    {
      "productId": 1,
      "quantity": 10,
      "purchasePrice": 100,
      "sellingPrice": 120
    }
  ],
  "isTaxEnabled": true
}
```

**Expected Response:**
```json
{
  "success": true,
  "data": {
    "subtotal": 1000.00,
    "taxRate": 14,
    "isTaxEnabled": true,
    "taxAmount": 140.00,
    "total": 1140.00
  }
}
```

**Verification:**
- [ ] `isTaxEnabled` = `true`
- [ ] `taxAmount` = `140.00`
- [ ] `total` = `1140.00`

---

### Test 4: Preview API - بدون ضريبة

```bash
POST /api/purchaseinvoices/prepare
Content-Type: application/json
Authorization: Bearer {token}

{
  "supplierId": 1,
  "invoiceDate": "2026-05-02T00:00:00Z",
  "items": [
    {
      "productId": 1,
      "quantity": 10,
      "purchasePrice": 100,
      "sellingPrice": 120
    }
  ],
  "isTaxEnabled": false
}
```

**Expected Response:**
```json
{
  "success": true,
  "data": {
    "subtotal": 1000.00,
    "taxRate": 14,
    "isTaxEnabled": false,
    "taxAmount": 0.00,
    "total": 1000.00
  }
}
```

**Verification:**
- [ ] `isTaxEnabled` = `false`
- [ ] `taxAmount` = `0.00`
- [ ] `total` = `1000.00`

---

### Test 5: تحديث فاتورة - تغيير حالة الضريبة

```bash
# Step 1: إنشاء فاتورة بضريبة
POST /api/purchaseinvoices
{
  "supplierId": 1,
  "items": [...],
  "isTaxEnabled": true
}
# Response: { "id": 125, "total": 1140.00 }

# Step 2: تحديث الفاتورة لإلغاء الضريبة
PUT /api/purchaseinvoices/125
{
  "supplierId": 1,
  "items": [...],
  "isTaxEnabled": false
}
```

**Expected Response:**
```json
{
  "success": true,
  "data": {
    "id": 125,
    "isTaxEnabled": false,
    "taxAmount": 0.00,
    "total": 1000.00
  }
}
```

**Verification:**
- [ ] `isTaxEnabled` تغيرت من `true` إلى `false`
- [ ] `taxAmount` تغيرت من `140.00` إلى `0.00`
- [ ] `total` تغيرت من `1140.00` إلى `1000.00`

---

### Test 6: Tenant.IsTaxEnabled = false

```bash
# Scenario: الشركة غير خاضعة للضريبة
# Tenant.IsTaxEnabled = false

POST /api/purchaseinvoices
{
  "supplierId": 1,
  "items": [...],
  "isTaxEnabled": true  # المستخدم يحاول تفعيل الضريبة
}
```

**Expected Response:**
```json
{
  "success": true,
  "data": {
    "id": 126,
    "isTaxEnabled": true,  # حُفظت كـ true في الفاتورة
    "taxAmount": 0.00,     # لكن الضريبة = 0 (الشركة تتحكم)
    "total": 1000.00
  }
}
```

**Verification:**
- [ ] `isTaxEnabled` = `true` (في الفاتورة)
- [ ] `taxAmount` = `0.00` (الشركة معطّلة)
- [ ] `total` = `1000.00` (بدون ضريبة)

---

### Test 7: Backward Compatibility - فواتير موجودة

```bash
# Step 1: تشغيل Migration
dotnet ef database update

# Step 2: التحقق من الفواتير الموجودة
GET /api/purchaseinvoices
```

**Expected:**
- [ ] كل الفواتير الموجودة: `isTaxEnabled` = `true`
- [ ] `taxAmount` لم يتغير
- [ ] `total` لم يتغير

---

## 🎨 Frontend Testing

### Test 8: صفحة إنشاء فاتورة جديدة - UI

**الخطوات:**
1. افتح `/purchase-invoices/new`
2. تحقق من وجود Checkbox "تطبيق الضريبة (14%)"
3. تحقق أن Checkbox مُفعّل افتراضيًا ✅

**Verification:**
- [ ] Checkbox موجود في قسم "بيانات الفاتورة"
- [ ] Checkbox مُفعّل افتراضيًا (`checked={true}`)
- [ ] النص: "تطبيق الضريبة (14%)"

---

### Test 9: إنشاء فاتورة بضريبة - Frontend

**الخطوات:**
1. افتح `/purchase-invoices/new`
2. اختر مورد
3. أضف منتج: quantity=10, purchasePrice=100
4. تأكد أن Checkbox "تطبيق الضريبة" مُفعّل ✅
5. انتظر تحديث Preview (500ms)
6. تحقق من الإجماليات
7. اضغط "حفظ"

**Expected:**
- [ ] Preview يُحدّث تلقائيًا
- [ ] Subtotal: 1,000.00 ج.م
- [ ] Tax: 140.00 ج.م
- [ ] Total: 1,140.00 ج.م
- [ ] الفاتورة تُحفظ بنجاح
- [ ] Redirect إلى صفحة تفاصيل الفاتورة

---

### Test 10: إنشاء فاتورة بدون ضريبة - Frontend

**الخطوات:**
1. افتح `/purchase-invoices/new`
2. اختر مورد
3. أضف منتج: quantity=10, purchasePrice=100
4. ألغِ تفعيل Checkbox "تطبيق الضريبة" ☐
5. انتظر تحديث Preview (500ms)
6. تحقق من الإجماليات
7. اضغط "حفظ"

**Expected:**
- [ ] Preview يُحدّث فورًا بعد إلغاء التفعيل
- [ ] Subtotal: 1,000.00 ج.م
- [ ] Tax: **معفاة** (مش 0.00 ج.م)
- [ ] Total: 1,000.00 ج.م
- [ ] الفاتورة تُحفظ بنجاح
- [ ] Redirect إلى صفحة تفاصيل الفاتورة

---

### Test 11: Real-time Preview Update

**الخطوات:**
1. افتح `/purchase-invoices/new`
2. اختر مورد وأضف منتج
3. انتظر 500ms → Preview يُحدّث
4. غيّر حالة Checkbox "تطبيق الضريبة"
5. انتظر 500ms → Preview يُحدّث مرة أخرى
6. كرر الخطوة 4-5 عدة مرات

**Expected:**
- [ ] كل تغيير في Checkbox → request جديد للباك-اند
- [ ] الرسالة تظهر: "جاري تحديث المعاينة من الباك إند..."
- [ ] Preview يعكس الحساب الصحيح من الباك-اند
- [ ] لا توجد أخطاء في Console

---

### Test 12: تعديل فاتورة - تحميل البيانات

**الخطوات:**
1. أنشئ فاتورة بضريبة (isTaxEnabled = true)
2. افتح الفاتورة للتعديل: `/purchase-invoices/{id}/edit`
3. تحقق من حالة Checkbox

**Expected:**
- [ ] Checkbox "تطبيق الضريبة" مُفعّل ✅
- [ ] Subtotal صحيح
- [ ] Tax صحيح
- [ ] Total صحيح

---

### Test 13: تعديل فاتورة - تغيير حالة الضريبة

**الخطوات:**
1. افتح فاتورة موجودة (isTaxEnabled = true)
2. ألغِ تفعيل Checkbox "تطبيق الضريبة" ☐
3. انتظر تحديث Preview
4. تحقق من الإجماليات
5. اضغط "تحديث"
6. أعد فتح الفاتورة

**Expected:**
- [ ] Preview يُحدّث فورًا
- [ ] Tax يتغير من "140.00 ج.م" إلى "معفاة"
- [ ] Total ينخفض من "1,140.00 ج.م" إلى "1,000.00 ج.م"
- [ ] الفاتورة تُحدّث بنجاح
- [ ] عند إعادة الفتح: Checkbox غير مُفعّل ☐

---

### Test 14: Fallback Calculation

**الخطوات:**
1. افتح `/purchase-invoices/new`
2. افصل الإنترنت (لمنع Preview من الباك-اند)
3. اختر مورد وأضف منتج
4. تحقق من الإجماليات

**Expected:**
- [ ] الحساب المحلي (fallback) يعمل
- [ ] Subtotal صحيح
- [ ] Tax صحيح (أو 0 لو Checkbox غير مُفعّل)
- [ ] Total صحيح

---

### Test 15: TypeScript Type Safety

**الخطوات:**
1. افتح `frontend/src/types/purchaseInvoice.types.ts`
2. تحقق من وجود `isTaxEnabled` في كل الـ interfaces

**Expected:**
- [ ] `PurchaseInvoice`: `isTaxEnabled: boolean`
- [ ] `PurchaseInvoicePreview`: `isTaxEnabled: boolean`
- [ ] `CreatePurchaseInvoiceRequest`: `isTaxEnabled?: boolean`
- [ ] `UpdatePurchaseInvoiceRequest`: `isTaxEnabled?: boolean`

---

## 🔍 Edge Cases Testing

### Test 16: فاتورة بدون منتجات

**الخطوات:**
1. افتح `/purchase-invoices/new`
2. اختر مورد
3. لا تضف أي منتجات
4. اضغط "حفظ"

**Expected:**
- [ ] رسالة خطأ: "يرجى إضافة منتج واحد على الأقل"
- [ ] الفاتورة لا تُحفظ

---

### Test 17: فاتورة بكمية سالبة

**الخطوات:**
1. افتح `/purchase-invoices/new`
2. اختر مورد
3. أضف منتج بكمية سالبة: quantity=-10
4. اضغط "إضافة"

**Expected:**
- [ ] HTML5 validation يمنع الإدخال (min="1")
- [ ] أو رسالة خطأ من الباك-اند

---

### Test 18: فاتورة بسعر سالب

**الخطوات:**
1. افتح `/purchase-invoices/new`
2. اختر مورد
3. أضف منتج بسعر سالب: purchasePrice=-100
4. اضغط "إضافة"

**Expected:**
- [ ] HTML5 validation يمنع الإدخال (min="0")
- [ ] أو رسالة خطأ من الباك-اند

---

### Test 19: Concurrent Updates

**الخطوات:**
1. افتح نفس الفاتورة في تابين مختلفين
2. في Tab 1: غيّر isTaxEnabled إلى false واحفظ
3. في Tab 2: غيّر isTaxEnabled إلى true واحفظ

**Expected:**
- [ ] آخر تحديث يكسب (Last Write Wins)
- [ ] أو رسالة خطأ Concurrency Conflict (إذا كان RowVersion مُفعّل)

---

### Test 20: Large Numbers

**الخطوات:**
1. افتح `/purchase-invoices/new`
2. أضف منتج: quantity=1000000, purchasePrice=999999
3. تحقق من الإجماليات

**Expected:**
- [ ] Subtotal: 999,999,000,000.00 ج.م
- [ ] Tax: 139,999,860,000.00 ج.م (14%)
- [ ] Total: 1,139,998,860,000.00 ج.م
- [ ] لا overflow errors
- [ ] الأرقام معروضة بشكل صحيح

---

## 📊 Integration Testing

### Test 21: إنشاء → تأكيد → تقرير

**الخطوات:**
1. أنشئ فاتورة بدون ضريبة (isTaxEnabled = false)
2. أكّد الفاتورة (Confirm)
3. افتح تقرير المشتريات

**Expected:**
- [ ] الفاتورة تظهر في التقرير
- [ ] Tax Amount = 0.00
- [ ] Total = Subtotal
- [ ] التقرير يحسب الإجماليات بشكل صحيح

---

### Test 22: إنشاء → دفع → تقرير مالي

**الخطوات:**
1. أنشئ فاتورة بضريبة (isTaxEnabled = true)
2. أكّد الفاتورة
3. أضف دفعة (Payment)
4. افتح التقرير المالي

**Expected:**
- [ ] الفاتورة تظهر في التقرير
- [ ] Tax Amount = 140.00
- [ ] Total = 1140.00
- [ ] Amount Paid صحيح
- [ ] Amount Due صحيح

---

### Test 23: Backup & Restore

**الخطوات:**
1. أنشئ فاتورة بدون ضريبة (isTaxEnabled = false)
2. اعمل Backup للـ database
3. احذف الفاتورة
4. اعمل Restore من الـ backup

**Expected:**
- [ ] الفاتورة تعود بنفس البيانات
- [ ] isTaxEnabled = false
- [ ] Tax Amount = 0.00
- [ ] Total = Subtotal

---

## 🚀 Performance Testing

### Test 24: Preview Performance

**الخطوات:**
1. افتح `/purchase-invoices/new`
2. أضف 100 منتج
3. غيّر حالة Checkbox "تطبيق الضريبة"
4. قس الوقت حتى تحديث Preview

**Expected:**
- [ ] Preview يُحدّث في أقل من 2 ثانية
- [ ] لا تجميد في الـ UI
- [ ] لا أخطاء في Console

---

### Test 25: Multiple Invoices

**الخطوات:**
1. أنشئ 100 فاتورة (50 بضريبة، 50 بدون)
2. افتح صفحة قائمة الفواتير
3. قس وقت التحميل

**Expected:**
- [ ] الصفحة تُحمّل في أقل من 3 ثوانٍ
- [ ] Pagination يعمل بشكل صحيح
- [ ] لا تباطؤ في الـ UI

---

## ✅ Final Checklist

### Backend
- [ ] Test 1: إنشاء فاتورة بضريبة (الافتراضي)
- [ ] Test 2: إنشاء فاتورة بدون ضريبة
- [ ] Test 3: Preview API - بضريبة
- [ ] Test 4: Preview API - بدون ضريبة
- [ ] Test 5: تحديث فاتورة - تغيير حالة الضريبة
- [ ] Test 6: Tenant.IsTaxEnabled = false
- [ ] Test 7: Backward Compatibility - فواتير موجودة

### Frontend
- [ ] Test 8: صفحة إنشاء فاتورة جديدة - UI
- [ ] Test 9: إنشاء فاتورة بضريبة - Frontend
- [ ] Test 10: إنشاء فاتورة بدون ضريبة - Frontend
- [ ] Test 11: Real-time Preview Update
- [ ] Test 12: تعديل فاتورة - تحميل البيانات
- [ ] Test 13: تعديل فاتورة - تغيير حالة الضريبة
- [ ] Test 14: Fallback Calculation
- [ ] Test 15: TypeScript Type Safety

### Edge Cases
- [ ] Test 16: فاتورة بدون منتجات
- [ ] Test 17: فاتورة بكمية سالبة
- [ ] Test 18: فاتورة بسعر سالب
- [ ] Test 19: Concurrent Updates
- [ ] Test 20: Large Numbers

### Integration
- [ ] Test 21: إنشاء → تأكيد → تقرير
- [ ] Test 22: إنشاء → دفع → تقرير مالي
- [ ] Test 23: Backup & Restore

### Performance
- [ ] Test 24: Preview Performance
- [ ] Test 25: Multiple Invoices

---

## 📝 Test Report Template

```
# Test Report - الضريبة الاختيارية في فواتير الشراء

التاريخ: [DATE]
المختبر: [NAME]

## Backend Tests
- Test 1: ✅ Pass / ❌ Fail - [Notes]
- Test 2: ✅ Pass / ❌ Fail - [Notes]
...

## Frontend Tests
- Test 8: ✅ Pass / ❌ Fail - [Notes]
- Test 9: ✅ Pass / ❌ Fail - [Notes]
...

## Edge Cases
- Test 16: ✅ Pass / ❌ Fail - [Notes]
...

## Integration Tests
- Test 21: ✅ Pass / ❌ Fail - [Notes]
...

## Performance Tests
- Test 24: ✅ Pass / ❌ Fail - [Notes]
...

## Issues Found
1. [Issue Description]
2. [Issue Description]

## Overall Status
✅ All tests passed
❌ X tests failed

## Recommendations
1. [Recommendation]
2. [Recommendation]
```

---

**الحالة:** ✅ Checklist كامل  
**آخر تحديث:** 2 مايو 2026  
**المطور:** Kiro AI Assistant
