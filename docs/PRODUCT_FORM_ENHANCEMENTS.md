# 🎨 تحسينات فورم إضافة المنتجات

## ✨ الميزات الجديدة

### 1. اختيار الأيقونات (Emoji Icons)
- 60+ أيقونة emoji للمنتجات
- تصنيفات: مشروبات، طعام، حلويات، فواكه، إلخ
- واجهة سهلة لاختيار الأيقونة
- معاينة مباشرة للأيقونة المختارة

### 2. إعدادات الضرائب
- **TaxRate**: معدل ضريبة مخصص للمنتج (اختياري)
  - إذا لم يُحدد، يستخدم معدل الفرع الافتراضي (14%)
- **TaxInclusive**: هل السعر شامل الضريبة؟
  - نعم (شامل) - السعر المعروض يشمل الضريبة
  - لا (غير شامل) - الضريبة تُضاف على السعر

### 3. حقل الوصف
- وصف تفصيلي للمنتج
- textarea متعدد الأسطر
- اختياري

### 4. نقطة إعادة الطلب (Reorder Point)
- تحديد متى يجب إعادة طلب المنتج
- مفيد لإدارة المخزون التلقائية
- اختياري

### 5. كميات مخصصة لكل فرع
- **ميزة جديدة**: تحديد كمية مبدئية مختلفة لكل فرع
- مفيد عند:
  - فروع بأحجام مختلفة
  - منتجات موسمية
  - توزيع غير متساوي للمخزون
- يمكن تفعيلها/تعطيلها بـ checkbox

## 📋 الحقول المتاحة الآن

### معلومات أساسية
- ✅ اسم المنتج (عربي) - مطلوب
- ✅ اسم المنتج (إنجليزي) - اختياري
- ✅ الوصف - اختياري جديد
- ✅ التصنيف - مطلوب
- ✅ الأيقونة - اختياري جديد

### التسعير والضرائب
- ✅ سعر البيع - مطلوب
- ✅ سعر التكلفة - اختياري
- ✅ معدل الضريبة - اختياري جديد
- ✅ السعر شامل الضريبة - جديد

### الأكواد
- ✅ SKU - اختياري
- ✅ الباركود - اختياري

### المخزون
- ✅ الكمية المتاحة - مطلوب
- ✅ حد التنبيه - مطلوب
- ✅ نقطة إعادة الطلب - اختياري جديد
- ✅ كميات مخصصة لكل فرع - اختياري جديد

## 🔧 التغييرات في الباك إند

### DTOs Updated

#### CreateProductRequest
```csharp
public class CreateProductRequest
{
    // ... existing fields
    
    // Tax settings
    public decimal? TaxRate { get; set; }
    public bool TaxInclusive { get; set; } = true;
    
    // Inventory fields
    public int? ReorderPoint { get; set; }
    
    // Branch-specific initial stock
    public Dictionary<int, int>? BranchStockQuantities { get; set; }
}
```

#### UpdateProductRequest
```csharp
public class UpdateProductRequest
{
    // ... existing fields
    
    // Tax settings
    public decimal? TaxRate { get; set; }
    public bool TaxInclusive { get; set; } = true;
    
    // Inventory fields
    public int? ReorderPoint { get; set; }
}
```

### ProductService.CreateAsync

الآن يدعم:
1. حفظ TaxRate و TaxInclusive
2. حفظ ReorderPoint
3. توزيع كميات مختلفة لكل فرع عبر BranchStockQuantities

```csharp
// Use branch-specific quantity if provided
var quantity = request.BranchStockQuantities?.ContainsKey(branch.Id) == true
    ? request.BranchStockQuantities[branch.Id]
    : request.StockQuantity;
```

## 🎨 UI/UX Improvements

### تنظيم أفضل
- الفورم مقسم لأقسام واضحة:
  - المعلومات الأساسية
  - الأيقونة
  - التسعير والضرائب
  - الأكواد
  - المخزون

### Icons
- 60 أيقونة emoji منظمة
- Grid layout سهل التصفح
- Hover effects
- Selected state واضح

### Branch-Specific Stock
- Checkbox لتفعيل الميزة
- Grid يعرض كل الفروع
- Input لكل فرع
- Scrollable إذا كان عدد الفروع كبير

## 📊 أمثلة الاستخدام

### مثال 1: منتج بضريبة مخصصة
```typescript
{
  name: "سجائر",
  price: 50,
  taxRate: 50, // ضريبة 50% للسجائر
  taxInclusive: true
}
```

### مثال 2: منتج بكميات مختلفة للفروع
```typescript
{
  name: "قهوة إسبريسو",
  stockQuantity: 100, // default
  branchStockQuantities: {
    1: 200, // الفرع الرئيسي
    2: 50,  // فرع صغير
    3: 100  // فرع متوسط
  }
}
```

### مثال 3: منتج بنقطة إعادة طلب
```typescript
{
  name: "حليب",
  stockQuantity: 50,
  lowStockThreshold: 10, // تنبيه عند 10
  reorderPoint: 20 // إعادة طلب عند 20
}
```

## 🧪 Testing Checklist

### Frontend
- [ ] اختيار أيقونة يعمل
- [ ] إلغاء اختيار أيقونة يعمل
- [ ] تفعيل/تعطيل كميات الفروع
- [ ] تحديث كميات الفروع
- [ ] حفظ معدل ضريبة مخصص
- [ ] تبديل TaxInclusive
- [ ] حفظ نقطة إعادة الطلب

### Backend
- [ ] CreateAsync يحفظ TaxRate
- [ ] CreateAsync يحفظ TaxInclusive
- [ ] CreateAsync يحفظ ReorderPoint
- [ ] CreateAsync يوزع الكميات حسب BranchStockQuantities
- [ ] UpdateAsync يحفظ الحقول الجديدة
- [ ] GetAllAsync يرجع الحقول الجديدة
- [ ] GetByIdAsync يرجع الحقول الجديدة

### Integration
- [ ] إنشاء منتج بأيقونة
- [ ] إنشاء منتج بضريبة مخصصة
- [ ] إنشاء منتج بكميات مختلفة للفروع
- [ ] التحقق من BranchInventory لكل فرع
- [ ] تعديل منتج موجود

## 🔗 Related Files

### Backend
- `src/KasserPro.Application/DTOs/Products/CreateProductRequest.cs`
- `src/KasserPro.Application/DTOs/Products/UpdateProductRequest.cs`
- `src/KasserPro.Application/DTOs/Products/ProductDto.cs`
- `src/KasserPro.Application/Services/Implementations/ProductService.cs`

### Frontend
- `client/src/components/products/ProductFormModal.tsx`
- `client/src/types/product.types.ts`

## 💡 Future Enhancements

1. **Upload صور حقيقية** بدلاً من emoji فقط
2. **Bulk import** للمنتجات من Excel/CSV
3. **Product variants** (مقاسات، ألوان، إلخ)
4. **Pricing tiers** (أسعار بالجملة)
5. **Expiry date tracking** للمنتجات القابلة للتلف
6. **Supplier management** مباشرة من فورم المنتج
