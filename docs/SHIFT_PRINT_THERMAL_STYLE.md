# 🖨️ تحديث: استخدام أسلوب Thermal Printer للطباعة

## المشكلة
- الطباعة الحالية معقدة وشكلها "زبالة" حسب المستخدم
- الإجماليات لا تطبع بشكل صحيح

## الحل
استخدام نفس أسلوب طباعة التقرير اليومي (thermal printer style):
- عرض 80mm (302px)
- تصميم بسيط ونظيف
- خط صغير ومقروء
- مناسب للطابعات الحرارية

## التغييرات المطلوبة

### في `ShiftDetailsDrawer.tsx`:

1. **تغيير handlePrint:**
```typescript
const handlePrint = () => {
  const html = generateShiftReceiptHtml(shiftDetails, productsSummary);
  const printWindow = window.open("", "_blank", "width=350,height=700");
  if (printWindow) {
    printWindow.document.write(html);
    printWindow.document.close();
  }
};
```

2. **استخدام نفس CSS من DailyReportPage:**
- max-width: 302px
- font-size: 11px
- thermal printer layout
- @page { margin: 2mm; size: 80mm auto; }

3. **تنسيق المنتجات:**
```html
<div class="product-row">
  <span class="product-name">${p.productName}</span>
  <span class="product-qty">×${p.totalQuantity}</span>
  <span class="product-total">${fmt(p.totalAmount)}</span>
</div>
```

4. **إضافة صف الإجمالي:**
```html
<div class="row total">
  <span>الإجمالي</span>
  <span>×${totalQty}</span>
  <span class="value">${fmt(totalAmount)} ج.م</span>
</div>
```

## الملفات المطلوب تعديلها
- `frontend/src/components/shifts/ShiftDetailsDrawer.tsx`

## الخطوات
1. نسخ CSS من `DailyReportPage.tsx`
2. تطبيق نفس الأسلوب على `generateShiftReceiptHtml`
3. إضافة جدول المنتجات بنفس تنسيق `topProducts`
4. إضافة صف الإجمالي في النهاية

**الحالة:** جاهز للتنفيذ
