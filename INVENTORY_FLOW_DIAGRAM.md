# 📊 رسم توضيحي: كيف يعمل نظام المخزون في KasserPro

## 🏗️ البنية الأساسية

```
┌─────────────────────────────────────────────────────────────┐
│                    🏢 TENANT (المستأجر)                     │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │ 🏪 الفرع     │  │ 🏪 فرع       │  │ 🏪 فرع       │     │
│  │   الرئيسي    │  │   المعادي    │  │  المهندسين   │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│         │                 │                 │              │
│         ▼                 ▼                 ▼              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │ 📦 مخزون     │  │ 📦 مخزون     │  │ 📦 مخزون     │     │
│  │  الفرع       │  │  الفرع       │  │  الفرع       │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

## 📦 جداول الداتابيس

```
┌─────────────────────────────────────────────────────────────┐
│                      Products (المنتجات)                    │
├─────────────────────────────────────────────────────────────┤
│ Id, Name, Price, Type, TrackInventory                       │
│ StockQuantity = 0 (deprecated - لا يُستخدم)                │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ ProductId
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              BranchInventories (مخزون الفروع)              │
├─────────────────────────────────────────────────────────────┤
│ Id, TenantId, BranchId, ProductId                           │
│ Quantity ← الكمية الفعلية (المصدر الموثوق)                 │
│ ReorderLevel, LastUpdatedAt                                 │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ كل تغيير يُسجل في
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              StockMovements (حركات المخزون)                │
├─────────────────────────────────────────────────────────────┤
│ Type: Sale, Receiving, Transfer, Adjustment                │
│ Quantity, BalanceBefore, BalanceAfter                       │
│ ReferenceType, ReferenceId, Reason, UserId                  │
└─────────────────────────────────────────────────────────────┘
```


## 🔄 سيناريو 1: إضافة منتج جديد

### من صفحة المنتجات (الوضع الافتراضي)

```
👤 المستخدم في: الفرع الرئيسي
📝 يضيف منتج: قهوة تركي
📊 الكمية: 100

Frontend (ProductFormModal)
    │
    │ POST /api/products
    │ { name: "قهوة تركي", stockQuantity: 100, ... }
    │
    ▼
Backend (ProductService.CreateAsync)
    │
    ├─► 1. إنشاء Product
    │      StockQuantity = 0 (deprecated)
    │
    ├─► 2. إنشاء BranchInventory لكل فرع:
    │      
    │      ┌─────────────────────────────────┐
    │      │ الفرع الرئيسي: Quantity = 100  │ ✓ الفرع الحالي
    │      ├─────────────────────────────────┤
    │      │ فرع المعادي: Quantity = 0      │
    │      ├─────────────────────────────────┤
    │      │ فرع المهندسين: Quantity = 0    │
    │      └─────────────────────────────────┘
    │
    └─► 3. حفظ في الداتابيس
```

### من صفحة المنتجات (كميات مخصصة)

```
👤 المستخدم في: الفرع الرئيسي
📝 يضيف منتج: شاي أخضر
☑️ تحديد كمية مخصصة لكل فرع

Frontend (ProductFormModal)
    │
    │ POST /api/products
    │ { 
    │   name: "شاي أخضر",
    │   branchStockQuantities: {
    │     1: 50,  // الفرع الرئيسي
    │     2: 30,  // فرع المعادي
    │     3: 20   // فرع المهندسين
    │   }
    │ }
    │
    ▼
Backend (ProductService.CreateAsync)
    │
    ├─► 1. إنشاء Product
    │
    ├─► 2. إنشاء BranchInventory لكل فرع:
    │      
    │      ┌─────────────────────────────────┐
    │      │ الفرع الرئيسي: Quantity = 50   │ ✓
    │      ├─────────────────────────────────┤
    │      │ فرع المعادي: Quantity = 30     │ ✓
    │      ├─────────────────────────────────┤
    │      │ فرع المهندسين: Quantity = 20   │ ✓
    │      └─────────────────────────────────┘
    │
    └─► 3. حفظ في الداتابيس
```

## 🔄 سيناريو 2: فاتورة شراء

```
👤 المستخدم في: الفرع الرئيسي
📄 فاتورة شراء جديدة
📦 المنتج: قهوة تركي
📊 الكمية: 50

Frontend (PurchaseInvoiceFormPage)
    │
    │ 1. POST /api/purchase-invoices (Draft)
    │    { items: [{ productId: 1, quantity: 50, ... }] }
    │
    ▼
Backend (PurchaseInvoiceService.CreateAsync)
    │
    ├─► إنشاء PurchaseInvoice (Status = Draft)
    ├─► إنشاء PurchaseInvoiceItems
    └─► ❌ لا تحديث للمخزون (لسه Draft)

    │
    │ 2. POST /api/purchase-invoices/{id}/confirm
    │
    ▼
Backend (PurchaseInvoiceService.ConfirmAsync)
    │
    ├─► 1. تغيير Status → Confirmed
    │
    ├─► 2. لكل منتج في الفاتورة:
    │      
    │      ┌─────────────────────────────────────────┐
    │      │ البحث عن BranchInventory               │
    │      │ (ProductId + BranchId + TenantId)       │
    │      └─────────────────────────────────────────┘
    │               │
    │               ├─► إذا موجود:
    │               │   ├─ BalanceBefore = 100
    │               │   ├─ Quantity += 50
    │               │   └─ BalanceAfter = 150
    │               │
    │               └─► إذا غير موجود:
    │                   ├─ إنشاء BranchInventory جديد
    │                   ├─ BalanceBefore = 0
    │                   ├─ Quantity = 50
    │                   └─ BalanceAfter = 50
    │
    ├─► 3. إنشاء StockMovement:
    │      Type = Receiving
    │      Quantity = 50
    │      BalanceBefore = 100
    │      BalanceAfter = 150
    │      ReferenceType = "PurchaseInvoice"
    │      ReferenceId = InvoiceId
    │
    ├─► 4. تحديث Product metadata:
    │      LastPurchasePrice = 50.00
    │      LastPurchaseDate = Now
    │      AverageCost = (weighted average)
    │
    └─► 5. حفظ كل التغييرات

النتيجة:
┌─────────────────────────────────────────┐
│ BranchInventory (الفرع الرئيسي)        │
│ Quantity: 100 → 150 ✓                   │
└─────────────────────────────────────────┘
┌─────────────────────────────────────────┐
│ StockMovement                           │
│ Type: Receiving                         │
│ Quantity: +50                           │
│ Balance: 100 → 150                      │
└─────────────────────────────────────────┘
```

## 🔄 سيناريو 3: بيع من POS

```
👤 الكاشير في: فرع المعادي
🛒 إضافة منتج للسلة: قهوة تركي × 3
💰 إتمام الطلب

Frontend (POSPage)
    │
    │ POST /api/orders
    │ { items: [{ productId: 1, quantity: 3, ... }] }
    │
    ▼
Backend (OrderService.CreateAsync)
    │
    ├─► 1. إنشاء Order (Status = Draft)
    ├─► 2. إنشاء OrderItems
    └─► ❌ لا تحديث للمخزون (لسه Draft)

    │
    │ POST /api/orders/{id}/complete
    │
    ▼
Backend (OrderService.CompleteAsync)
    │
    ├─► 1. التحقق من المخزون:
    │      ┌─────────────────────────────────┐
    │      │ BranchInventory (فرع المعادي)  │
    │      │ ProductId = 1                   │
    │      │ Quantity = 30                   │
    │      │ المطلوب = 3                     │
    │      │ ✓ متاح (30 >= 3)               │
    │      └─────────────────────────────────┘
    │
    ├─► 2. تغيير Status → Completed
    │
    ├─► 3. خصم المخزون:
    │      InventoryService.BatchDecrementStockAsync
    │      
    │      ┌─────────────────────────────────┐
    │      │ BranchInventory (فرع المعادي)  │
    │      │ BalanceBefore = 30              │
    │      │ Quantity -= 3                   │
    │      │ BalanceAfter = 27               │
    │      └─────────────────────────────────┘
    │
    ├─► 4. إنشاء StockMovement:
    │      Type = Sale
    │      Quantity = -3
    │      BalanceBefore = 30
    │      BalanceAfter = 27
    │      ReferenceType = "Order"
    │      ReferenceId = OrderId
    │
    └─► 5. حفظ كل التغييرات

النتيجة:
┌─────────────────────────────────────────┐
│ BranchInventory (فرع المعادي)          │
│ Quantity: 30 → 27 ✓                     │
└─────────────────────────────────────────┘
┌─────────────────────────────────────────┐
│ StockMovement                           │
│ Type: Sale                              │
│ Quantity: -3                            │
│ Balance: 30 → 27                        │
└─────────────────────────────────────────┘
```

## 🔄 سيناريو 4: نقل بين الفروع

```
👤 Admin في: الفرع الرئيسي
📦 نقل مخزون
من: الفرع الرئيسي (150 وحدة)
إلى: فرع المعادي (27 وحدة)
الكمية: 50

Frontend (InventoryPage → Transfers)
    │
    │ 1. POST /api/inventory/transfer
    │    { fromBranchId: 1, toBranchId: 2, productId: 1, quantity: 50 }
    │
    ▼
Backend (InventoryService.CreateTransferAsync)
    │
    ├─► إنشاء InventoryTransfer (Status = Pending)
    └─► ❌ لا تحديث للمخزون (لسه Pending)

    │
    │ 2. POST /api/inventory/transfer/{id}/approve
    │
    ▼
Backend (InventoryService.ApproveTransferAsync)
    │
    ├─► 1. التحقق من المخزون في الفرع المصدر
    │
    ├─► 2. خصم من الفرع المصدر:
    │      ┌─────────────────────────────────────┐
    │      │ BranchInventory (الفرع الرئيسي)    │
    │      │ BalanceBefore = 150                 │
    │      │ Quantity -= 50                      │
    │      │ BalanceAfter = 100                  │
    │      └─────────────────────────────────────┘
    │
    ├─► 3. إنشاء StockMovement (المصدر):
    │      Type = Transfer
    │      Quantity = -50
    │      ReferenceType = "InventoryTransfer"
    │
    └─► 4. تغيير Status → Approved

    │
    │ 3. POST /api/inventory/transfer/{id}/receive
    │
    ▼
Backend (InventoryService.ReceiveTransferAsync)
    │
    ├─► 1. إضافة للفرع الوجهة:
    │      ┌─────────────────────────────────────┐
    │      │ BranchInventory (فرع المعادي)      │
    │      │ BalanceBefore = 27                  │
    │      │ Quantity += 50                      │
    │      │ BalanceAfter = 77                   │
    │      └─────────────────────────────────────┘
    │
    ├─► 2. إنشاء StockMovement (الوجهة):
    │      Type = Transfer
    │      Quantity = +50
    │      ReferenceType = "InventoryTransfer"
    │
    └─► 3. تغيير Status → Completed

النتيجة النهائية:
┌─────────────────────────────────────────┐
│ الفرع الرئيسي: 150 → 100 ✓             │
│ فرع المعادي: 27 → 77 ✓                 │
└─────────────────────────────────────────┘
```

## 📊 عرض الكميات في Frontend

```
Frontend يطلب المنتجات:
GET /api/products

Backend (ProductService.GetAllAsync)
    │
    ├─► 1. جلب كل المنتجات
    │
    ├─► 2. جلب BranchInventory للفرع الحالي:
    │      WHERE TenantId = CurrentUser.TenantId
    │        AND BranchId = CurrentUser.BranchId
    │
    ├─► 3. دمج البيانات:
    │      Product.StockQuantity = BranchInventory.Quantity
    │
    └─► 4. إرجاع النتيجة

مثال:
المستخدم في: فرع المعادي
المنتج: قهوة تركي

Response:
{
  id: 1,
  name: "قهوة تركي",
  stockQuantity: 77,  ← من BranchInventory (فرع المعادي)
  ...
}

┌─────────────────────────────────────────┐
│ الفرع الرئيسي يرى: 100                 │
│ فرع المعادي يرى: 77                    │
│ فرع المهندسين يرى: 0                   │
└─────────────────────────────────────────┘
```

## 🔍 Audit Trail (سجل التدقيق)

```
كل حركة مخزون مسجلة في StockMovements:

SELECT * FROM StockMovements 
WHERE ProductId = 1 
ORDER BY CreatedAt DESC

┌────────────────────────────────────────────────────────┐
│ Time       │ Type      │ Qty  │ Before │ After │ Ref  │
├────────────────────────────────────────────────────────┤
│ 10:30 AM   │ Transfer  │ +50  │ 27     │ 77    │ IT-1 │
│ 10:25 AM   │ Transfer  │ -50  │ 150    │ 100   │ IT-1 │
│ 09:15 AM   │ Sale      │ -3   │ 30     │ 27    │ O-42 │
│ 08:00 AM   │ Receiving │ +50  │ 100    │ 150   │ PI-5 │
└────────────────────────────────────────────────────────┘

✓ كل حركة موثقة
✓ يمكن تتبع أي تغيير
✓ معرفة من عمل التغيير (UserId)
✓ معرفة السبب (Reason)
```

## 🎯 الخلاصة

```
┌─────────────────────────────────────────────────────────┐
│                   المبادئ الأساسية                      │
├─────────────────────────────────────────────────────────┤
│ 1. BranchInventory هو المصدر الموثوق للكميات          │
│ 2. Product.StockQuantity = 0 (deprecated)               │
│ 3. كل فرع له مخزون منفصل                               │
│ 4. كل حركة مسجلة في StockMovements                     │
│ 5. المنتج الجديد يُضاف للفرع الحالي فقط               │
│ 6. الفروع الأخرى تبدأ بصفر                             │
│ 7. النقل بين الفروع عبر Transfers                      │
└─────────────────────────────────────────────────────────┘
```
