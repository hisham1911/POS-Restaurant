# Receipt Visual Example 📄

## Professional Receipt Layout

This document shows the visual structure of the new professional receipt format.

---

## PDF Printer Receipt (80mm width)

```
┌─────────────────────────────────┐
│                                 │
│        KasserPro Store          │ ← Centered, Bold, 12pt
│         Main Branch             │
│                                 │
├─────────────────────────────────┤
│                                 │
│ Receipt #:         TEST-001     │ ← Right-aligned
│ Date:     31/01/2026 21:47      │ ← Right-aligned
│                                 │
├─────────────────────────────────┤
│                                 │
│ Item                    Total   │ ← Bold headers
├─────────────────────────────────┤
│                                 │
│ Espresso Coffee                 │ ← Item name
│   2 x 25.00 EGP    50.00 EGP   │ ← Qty × Price = Total
│                                 │
│ Cappuccino                      │
│   1 x 35.00 EGP    35.00 EGP   │
│                                 │
│ Croissant                       │
│   3 x 15.00 EGP    45.00 EGP   │
│                                 │
│ Orange Juice                    │
│   2 x 20.00 EGP    40.00 EGP   │
│                                 │
├─────────────────────────────────┤
│                                 │
│ Subtotal:          170.00 EGP   │ ← Right-aligned
│ Tax (14%):          23.80 EGP   │ ← Right-aligned
│                                 │
├═════════════════════════════════┤
│                                 │
│ TOTAL:             193.80 EGP   │ ← Bold, 11pt, Right-aligned
│                                 │
├═════════════════════════════════┤
│                                 │
│ Payment:                Cash    │ ← Right-aligned, Bold
│ Cashier:          Ahmed Ali     │ ← Right-aligned
│                                 │
├─────────────────────────────────┤
│                                 │
│          *TEST-001*             │ ← Centered barcode
│                                 │
│         Thank You!              │ ← Centered, Bold
│          شكراً لك               │ ← Centered, Bold, Arabic
│                                 │
└─────────────────────────────────┘
```

---

## Thermal Printer Receipt (32 characters)

```
================================
      KasserPro Store
       Main Branch
   (Double Size, Bold)
================================
Receipt #: TEST-001
Date: 31/01/2026 21:47

================================
Item                      Total
--------------------------------
Espresso Coffee
  2 x 25.00          50.00

Cappuccino
  1 x 35.00          35.00

Croissant
  3 x 15.00          45.00

Orange Juice
  2 x 20.00          40.00

--------------------------------
Subtotal:           170.00 EGP
Tax (14%):           23.80 EGP

================================
TOTAL:              193.80 EGP
   (Bold, Double Height)
================================
Payment:                  Cash
Cashier:           Ahmed Ali

--------------------------------
    |||||||||||||||||||||||
    |||||||||||||||||||||||
    |||||||||||||||||||||||
       (CODE128 Barcode)

      Thank You!
       شكراً لك

================================
```

---

## Layout Breakdown

### Header Section
```
┌─────────────────────────────────┐
│        KasserPro Store          │ ← Branch Name
│         Main Branch             │ ← Centered, Bold
└─────────────────────────────────┘
```
- **Font:** Arial 12pt Bold (PDF) / Double Size Bold (Thermal)
- **Alignment:** Center
- **Purpose:** Brand identity

### Receipt Information
```
┌─────────────────────────────────┐
│ Receipt #:         TEST-001     │ ← Receipt Number
│ Date:     31/01/2026 21:47      │ ← Date & Time
└─────────────────────────────────┘
```
- **Font:** Arial 9pt (PDF) / Normal (Thermal)
- **Alignment:** Right for values, Left for labels
- **Purpose:** Transaction identification

### Items Section
```
┌─────────────────────────────────┐
│ Item                    Total   │ ← Column Headers
├─────────────────────────────────┤
│ Espresso Coffee                 │ ← Item Name
│   2 x 25.00 EGP    50.00 EGP   │ ← Details
└─────────────────────────────────┘
```
- **Font:** Arial 9pt (PDF) / Normal (Thermal)
- **Alignment:** Left for items, Right for totals
- **Color:** Gray for quantity/price (PDF only)
- **Purpose:** Itemized list

### Totals Section
```
┌─────────────────────────────────┐
│ Subtotal:          170.00 EGP   │ ← Subtotal
│ Tax (14%):          23.80 EGP   │ ← Tax
├═════════════════════════════════┤
│ TOTAL:             193.80 EGP   │ ← Final Total
└─────────────────────────────────┘
```
- **Font:** Arial 9pt regular, 11pt bold for total (PDF)
- **Alignment:** Right
- **Style:** Bold for TOTAL
- **Purpose:** Financial summary

### Footer Section
```
┌─────────────────────────────────┐
│ Payment:                Cash    │ ← Payment Method
│ Cashier:          Ahmed Ali     │ ← Cashier Name
├─────────────────────────────────┤
│          *TEST-001*             │ ← Barcode
│         Thank You!              │ ← Thank You
│          شكراً لك               │ ← Arabic
└─────────────────────────────────┘
```
- **Font:** Arial 9pt (PDF) / Normal (Thermal)
- **Alignment:** Center for barcode and thank you
- **Purpose:** Transaction details and courtesy

---

## Color Scheme (PDF Only)

### Primary Text
- **Color:** Black (#000000)
- **Usage:** Headers, items, totals
- **Font Weight:** Regular or Bold

### Secondary Text
- **Color:** Gray (#808080)
- **Usage:** Quantity and price details
- **Font Weight:** Regular

### Separator Lines
- **Color:** Black (#000000) for main separators
- **Color:** Gray (#808080) for item separators
- **Style:** Solid lines

---

## Spacing & Margins

### PDF Printer
- **Left Margin:** 20px
- **Right Margin:** 20px (295px from left)
- **Center Point:** 157.5px
- **Line Height:** Font height + 3-8px
- **Section Spacing:** 5-10px

### Thermal Printer
- **Width:** 32 characters
- **Line Spacing:** 1 line between items
- **Section Spacing:** 1-2 blank lines
- **Separator Lines:** 32 characters (= or -)

---

## Font Sizes

### PDF Printer
| Element | Size | Weight |
|---------|------|--------|
| Header | 12pt | Bold |
| Total | 11pt | Bold |
| Bold Text | 10pt | Bold |
| Regular Text | 9pt | Regular |

### Thermal Printer
| Element | Style |
|---------|-------|
| Header | Double Width + Double Height + Bold |
| Total | Bold + Double Height |
| Bold Text | Bold |
| Regular Text | Normal |

---

## Alignment Guide

### Left-Aligned
- Item names
- Labels (Receipt #:, Date:, Payment:, Cashier:)

### Right-Aligned
- All amounts (item totals, subtotal, tax, total)
- Values (receipt number, date, payment method, cashier name)

### Center-Aligned
- Branch name
- Barcode
- Thank you message

---

## Example with Long Item Names

### PDF (Wraps to next line)
```
┌─────────────────────────────────┐
│ Extra Large Caramel Macchiato   │
│ with Whipped Cream and Extra    │
│ Shot                            │
│   1 x 55.00 EGP    55.00 EGP   │
└─────────────────────────────────┘
```

### Thermal (Truncates with ...)
```
Extra Large Caramel Macch...
  1 x 55.00          55.00
```

---

## Example with Arabic Product Names

```
┌─────────────────────────────────┐
│ قهوة عربية                      │ ← Arabic item name
│   2 x 30.00 EGP    60.00 EGP   │
│                                 │
│ شاي بالنعناع                    │ ← Arabic item name
│   1 x 15.00 EGP    15.00 EGP   │
└─────────────────────────────────┘
```

---

## Barcode Examples

### Thermal Printer (CODE128)
```
    |||||||||||||||||||||||
    |||||||||||||||||||||||
    |||||||||||||||||||||||
         TEST-001
```

### PDF Printer (Text representation)
```
        *TEST-001*
```

---

## Complete Example Receipt

### Scenario
- **Store:** KasserPro Main Branch
- **Cashier:** Ahmed Ali
- **Date:** January 31, 2026, 21:47
- **Items:** 4 products, 8 total items
- **Payment:** Cash
- **Tax Rate:** 14%

### Receipt Output
```
================================
      KasserPro Store
       Main Branch
================================
Receipt #: TEST-20260131214725
Date: 31/01/2026 21:47

================================
Item                      Total
--------------------------------
Espresso Coffee
  2 x 25.00          50.00

Cappuccino
  1 x 35.00          35.00

Croissant
  3 x 15.00          45.00

Orange Juice
  2 x 20.00          40.00

--------------------------------
Subtotal:           170.00 EGP
Tax (14%):           23.80 EGP

================================
TOTAL:              193.80 EGP
================================
Payment:                  Cash
Cashier:           Ahmed Ali

--------------------------------
    [CODE128 BARCODE]
      Thank You!
       شكراً لك
```

---

## Visual Comparison: Before vs After

### Before (Old Format)
```
Branch Name
Receipt: TEST-001
Date: 31/01/2026
Espresso Coffee - 50.00
Cappuccino - 35.00
Croissant - 45.00
Orange Juice - 40.00
Total: 170.00
```
❌ No alignment  
❌ No tax breakdown  
❌ No visual separation  
❌ Hard to read  

### After (New Format)
```
================================
      KasserPro Store
================================
Receipt #:         TEST-001
Date:     31/01/2026 21:47
================================
Item                      Total
--------------------------------
Espresso Coffee
  2 x 25.00          50.00
...
--------------------------------
Subtotal:           170.00 EGP
Tax (14%):           23.80 EGP
================================
TOTAL:              193.80 EGP
================================
```
✅ Professional alignment  
✅ Clear tax breakdown  
✅ Visual sections  
✅ Easy to read  

---

## Printing Tips

### For Best Results

#### PDF Printing
1. Use "Microsoft Print to PDF" or similar
2. Choose "Actual Size" (not "Fit to Page")
3. Paper size: 80mm width
4. Orientation: Portrait

#### Thermal Printing
1. Use 80mm thermal printer (XP-80C, XP-90, etc.)
2. Ensure paper is loaded correctly
3. Check printer is online
4. Test print before production use

---

**Document:** Receipt Visual Example  
**Version:** 1.0  
**Date:** January 31, 2026  
**Project:** KasserPro Desktop Bridge App  

---

## 📄 This is what your receipts look like now!

Professional, clean, and easy to read. 🎉
