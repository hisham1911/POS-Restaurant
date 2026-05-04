# ✅ جعل الضريبة اختيارية في فواتير الشراء

> **التاريخ:** 2 مايو 2026  
> **الهدف:** السماح بإنشاء فواتير شراء بدون ضريبة

---

## 📋 المشكلة

كانت فواتير الشراء تُحسب دائمًا بضريبة، لكن في الواقع:
- بعض الموردين لا يضيفون ضريبة
- بعض المنتجات معفاة من الضريبة
- بعض الشركات لا تخضع للضريبة

---

## ✅ الحل

إضافة حقل `IsTaxEnabled` لفواتير الشراء للتحكم في تطبيق الضريبة.

---

## 🔧 التغييرات المُنفذة

### 1. **إضافة حقل `IsTaxEnabled` في Entity**

```csharp
// في PurchaseInvoice.cs
public class PurchaseInvoice : BaseEntity
{
    public decimal TaxRate { get; set; }
    
    /// <summary>
    /// Whether tax is enabled for this invoice
    /// </summary>
    public bool IsTaxEnabled { get; set; } = true;  // ✅ افتراضيًا مفعّل
    
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
}
```

---

### 2. **تحديث DTOs**

```csharp
// CreatePurchaseInvoiceRequest
public class CreatePurchaseInvoiceRequest
{
    public int SupplierId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public List<CreatePurchaseInvoiceItemRequest> Items { get; set; } = new();
    public string? Notes { get; set; }
    public bool IsTaxEnabled { get; set; } = true;  // ✅ جديد
}

// PurchaseInvoiceDto
public class PurchaseInvoiceDto
{
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public bool IsTaxEnabled { get; set; }  // ✅ جديد
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
}

// PurchaseInvoicePreviewDto
public class PurchaseInvoicePreviewDto
{
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public bool IsTaxEnabled { get; set; }  // ✅ جديد
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
}
```

---

### 3. **تحديث حساب الضريبة في Service**

#### أ) في `PrepareAsync()` (Preview)

```csharp
// ✅ قبل التعديل
var taxAmount = Math.Round(subtotal * (taxRate / 100m), 2);

// ✅ بعد التعديل
var isTaxEnabled = request.IsTaxEnabled && (tenant?.IsTaxEnabled ?? true);
var taxAmount = isTaxEnabled ? Math.Round(subtotal * (taxRate / 100m), 2) : 0m;
```

#### ب) في `CreateAsync()` (إنشاء الفاتورة)

```csharp
// ✅ قبل التعديل
invoice.TaxAmount = subtotal * (taxRate / 100);

// ✅ بعد التعديل
invoice.IsTaxEnabled = request.IsTaxEnabled && (tenant?.IsTaxEnabled ?? true);
invoice.TaxAmount = invoice.IsTaxEnabled ? subtotal * (taxRate / 100) : 0m;
```

#### ج) في `UpdateAsync()` (تحديث الفاتورة)

```csharp
// ✅ قبل التعديل
invoice.TaxAmount = subtotal * (invoice.TaxRate / 100);

// ✅ بعد التعديل
invoice.TaxAmount = invoice.IsTaxEnabled ? subtotal * (invoice.TaxRate / 100) : 0m;
```

---

### 4. **Migration**

```csharp
// 20260502190000_AddIsTaxEnabledToPurchaseInvoice.cs
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<bool>(
        name: "IsTaxEnabled",
        table: "PurchaseInvoices",
        type: "INTEGER",
        nullable: false,
        defaultValue: true);  // ✅ الفواتير الموجودة تبقى بضريبة
}
```

---

## 🔄 كيف يعمل النظام

### السيناريو 1: فاتورة بضريبة (الافتراضي)

```json
{
  "supplierId": 1,
  "isTaxEnabled": true,  // ✅ افتراضيًا
  "items": [
    { "productId": 1, "quantity": 10, "purchasePrice": 100 }
  ]
}
```

**النتيجة:**
```
Subtotal:   1000 ج.م
Tax (14%):   140 ج.م
Total:      1140 ج.م
```

---

### السيناريو 2: فاتورة بدون ضريبة

```json
{
  "supplierId": 1,
  "isTaxEnabled": false,  // ✅ بدون ضريبة
  "items": [
    { "productId": 1, "quantity": 10, "purchasePrice": 100 }
  ]
}
```

**النتيجة:**
```
Subtotal:   1000 ج.م
Tax:           0 ج.م
Total:      1000 ج.م
```

---

## 📊 مقارنة قبل وبعد

### ❌ قبل التعديل

```
كل فاتورة شراء = ضريبة إلزامية
```

| Subtotal | Tax Rate | Tax Amount | Total |
|----------|----------|------------|-------|
| 1000 ج.م | 14% | 140 ج.م | 1140 ج.م |

**المشكلة:** لا يمكن إنشاء فاتورة بدون ضريبة

---

### ✅ بعد التعديل

```
كل فاتورة شراء = ضريبة اختيارية
```

#### مع الضريبة:
| Subtotal | IsTaxEnabled | Tax Rate | Tax Amount | Total |
|----------|--------------|----------|------------|-------|
| 1000 ج.م | ✅ true | 14% | 140 ج.م | 1140 ج.م |

#### بدون الضريبة:
| Subtotal | IsTaxEnabled | Tax Rate | Tax Amount | Total |
|----------|--------------|----------|------------|-------|
| 1000 ج.م | ❌ false | 14% | 0 ج.م | 1000 ج.م |

---

## 🎯 القواعد

### 1. **الأولوية في تفعيل الضريبة**

```csharp
var isTaxEnabled = request.IsTaxEnabled && (tenant?.IsTaxEnabled ?? true);
```

**الشرط:**
- ✅ الفاتورة: `IsTaxEnabled = true`
- ✅ الشركة: `Tenant.IsTaxEnabled = true`
- **النتيجة:** الضريبة مفعّلة

**إذا:**
- ❌ الفاتورة: `IsTaxEnabled = false`
- **النتيجة:** الضريبة معطّلة (حتى لو الشركة مفعّلة)

**أو:**
- ✅ الفاتورة: `IsTaxEnabled = true`
- ❌ الشركة: `Tenant.IsTaxEnabled = false`
- **النتيجة:** الضريبة معطّلة

---

### 2. **القيمة الافتراضية**

```csharp
public bool IsTaxEnabled { get; set; } = true;
```

- الفواتير الجديدة: **بضريبة افتراضيًا** ✅
- الفواتير الموجودة: **تبقى بضريبة** ✅ (من Migration)

---

### 3. **حساب الضريبة**

```csharp
// ✅ الطريقة الصحيحة
invoice.TaxAmount = invoice.IsTaxEnabled 
    ? subtotal * (invoice.TaxRate / 100) 
    : 0m;

// ❌ الطريقة القديمة (محذوفة)
invoice.TaxAmount = subtotal * (invoice.TaxRate / 100);
```

---

## 🔍 أمثلة عملية

### مثال 1: مورد معفى من الضريبة

```
المورد: شركة ABC (معفاة من الضريبة)
الفاتورة: 10 منتجات × 100 ج.م = 1000 ج.م

الإعدادات:
- IsTaxEnabled = false

النتيجة:
- Subtotal: 1000 ج.م
- Tax: 0 ج.م
- Total: 1000 ج.م ✅
```

---

### مثال 2: مورد خاضع للضريبة

```
المورد: شركة XYZ (خاضعة للضريبة)
الفاتورة: 10 منتجات × 100 ج.م = 1000 ج.م

الإعدادات:
- IsTaxEnabled = true
- TaxRate = 14%

النتيجة:
- Subtotal: 1000 ج.م
- Tax: 140 ج.م
- Total: 1140 ج.م ✅
```

---

### مثال 3: شركة غير خاضعة للضريبة

```
الشركة: Tenant.IsTaxEnabled = false
الفاتورة: IsTaxEnabled = true (محاولة تفعيل)

النتيجة:
- isTaxEnabled = true && false = false
- Tax: 0 ج.م ✅ (الشركة تتحكم)
```

---

## 📝 ملاحظات مهمة

### 1. **Backward Compatibility**

```sql
-- Migration تضيف defaultValue: true
ALTER TABLE PurchaseInvoices 
ADD COLUMN IsTaxEnabled INTEGER NOT NULL DEFAULT 1;
```

**النتيجة:**
- ✅ الفواتير الموجودة تبقى بضريبة
- ✅ لا تأثير على البيانات الحالية

---

### 2. **التقارير المالية**

التقارير **لا تتأثر** لأنها تستخدم `TaxAmount` المحسوب:

```csharp
// في التقارير
var totalTax = invoices.Sum(i => i.TaxAmount);  // ✅ صحيح
```

- لو `IsTaxEnabled = true` → `TaxAmount = 140`
- لو `IsTaxEnabled = false` → `TaxAmount = 0`

---

### 3. **الفرونت-اند**

يجب تحديث الفرونت-اند لإضافة checkbox:

```tsx
<Checkbox
  label="تطبيق الضريبة"
  checked={isTaxEnabled}
  onChange={(e) => setIsTaxEnabled(e.target.checked)}
/>
```

---

## 🧪 اختبار الميزة

### Test Case 1: فاتورة بضريبة

```bash
POST /api/purchase-invoices
{
  "supplierId": 1,
  "isTaxEnabled": true,
  "items": [
    { "productId": 1, "quantity": 10, "purchasePrice": 100 }
  ]
}

# Expected:
# Subtotal: 1000
# TaxAmount: 140
# Total: 1140
```

---

### Test Case 2: فاتورة بدون ضريبة

```bash
POST /api/purchase-invoices
{
  "supplierId": 1,
  "isTaxEnabled": false,
  "items": [
    { "productId": 1, "quantity": 10, "purchasePrice": 100 }
  ]
}

# Expected:
# Subtotal: 1000
# TaxAmount: 0
# Total: 1000
```

---

### Test Case 3: شركة بدون ضريبة

```bash
# Tenant.IsTaxEnabled = false

POST /api/purchase-invoices
{
  "supplierId": 1,
  "isTaxEnabled": true,  # محاولة تفعيل
  "items": [
    { "productId": 1, "quantity": 10, "purchasePrice": 100 }
  ]
}

# Expected:
# Subtotal: 1000
# TaxAmount: 0  # الشركة تتحكم
# Total: 1000
```

---

## 🚀 الفوائد

1. ✅ **مرونة أكبر** - يمكن إنشاء فواتير بدون ضريبة
2. ✅ **دقة محاسبية** - تطابق الواقع (بعض الموردين بدون ضريبة)
3. ✅ **Backward Compatible** - الفواتير الموجودة لا تتأثر
4. ✅ **تحكم على مستوى الشركة** - `Tenant.IsTaxEnabled` له الأولوية

---

## 📚 الملفات المُعدلة

```
backend/KasserPro.Domain/Entities/PurchaseInvoice.cs
backend/KasserPro.Application/DTOs/PurchaseInvoices/CreatePurchaseInvoiceRequest.cs
backend/KasserPro.Application/DTOs/PurchaseInvoices/PurchaseInvoiceDto.cs
backend/KasserPro.Application/Services/Implementations/PurchaseInvoiceService.cs
backend/KasserPro.Infrastructure/Migrations/20260502190000_AddIsTaxEnabledToPurchaseInvoice.cs
```

---

**الحالة:** ✅ تم التنفيذ بنجاح  
**آخر تحديث:** 2 مايو 2026
