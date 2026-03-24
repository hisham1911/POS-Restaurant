# 🥩 Al-Amana Butcher Shop - Data Summary

**Date:** February 9, 2026  
**Status:** ✅ Ready to Use

---

## 🚀 Quick Start

```powershell
.\reset-data.ps1
```

Type `RESET` when prompted, wait 30-60 seconds.

---

## 📊 Data Overview

### Business Info
- **Shop:** مجزر الأمانة (Al-Amana Butcher)
- **Branch:** Main Branch - Downtown Cairo
- **Type:** Butcher Shop / Meat Market

### Users (3)
- 1 Admin + 2 Cashiers

### Categories (3)
1. **Beef** 🥩 - 10 products
2. **Minced & Processed** 🍖 - 6 products
3. **Offal & By-products** 🫀 - 8 products

### Products (24 Total)

**Beef Products (10):**
- Qaraqish (25 EGP)
- Meat Cuts (380 EGP)
- Ribs (320 EGP)
- Premium Meat (400 EGP)
- Doush Meat (340 EGP)
- Mouza Meat (400 EGP)
- Head Meat (225 EGP)
- Red Meat Cubes (380 EGP)
- Steak (450 EGP)
- Beefsteak (420 EGP)

**Minced & Processed (6):**
- Kebab Halla (380 EGP)
- Red Kebab Halla (380 EGP)
- Burger (250 EGP)
- Special Sausage (300 EGP)
- Special Minced (300 EGP)
- Mazalika (270 EGP)

**Offal & By-products (8):**
- Trotters per Kg (180 EGP)
- Trotters (280 EGP)
- Fat Tarab (75 EGP)
- Sweetbreads (130 EGP)
- Spleen (150 EGP)
- Mumbar (260 EGP)
- Um El-Shalatit (140 EGP)
- Kidney Fat (75 EGP)

### Transactional Data
- **Shifts:** 15 (14 closed + 1 open today)
- **Orders:** ~100-120 completed orders
- **Purchase Invoices:** 5 from suppliers
- **Expenses:** 8 various expenses
- **Cash Transactions:** 6 deposits/withdrawals

---

## 🔐 Login Credentials

| Role | Email | Password |
|------|-------|----------|
| **Admin** | admin@kasserpro.com | Admin@123 |
| **Cashier** | mohamed@kasserpro.com | 123456 |
| **Cashier** | ali@kasserpro.com | 123456 |

---

## 💰 Financial Model

All calculations use **Tax Exclusive (Additive)** model:

```
NetTotal = UnitPrice × Quantity
TaxAmount = NetTotal × (14 / 100)
TotalAmount = NetTotal + TaxAmount
```

**Tax Rate:** 14% (Egypt VAT)

---

## ✅ Verification

After running the script:

### Backend
- [ ] Application starts without errors
- [ ] Console shows "✅ تم تحميل بيانات المجزر بنجاح!"
- [ ] Database file exists

### Frontend
- [ ] Login works
- [ ] POS shows 24 products
- [ ] Products grouped in 3 categories
- [ ] Can create new order
- [ ] Inventory shows quantities in kg
- [ ] Reports show historical data

---

## 📦 Inventory

- All products measured in kilograms (kg)
- Initial stock: 20-80 kg per product
- Low stock threshold: 5-15 kg
- Stock updates automatically with sales and purchases

---

## 📝 Notes

1. **Order Type:** Most orders are "Takeaway" (suitable for butcher shop)
2. **Quantities:** Small quantities (1-3 kg) per order for realism
3. **Prices:** Range from 25 to 450 EGP based on meat type
4. **Stock:** Updates automatically with each sale or purchase

---

**Last Updated:** February 9, 2026  
**Status:** Ready for Testing ✅  
**Shop:** Al-Amana Butcher 🥩

