# 🚀 KasserPro Seed Data - Quick Reference

## 🔐 Login Credentials

### System Owner (مالك النظام)
```
📧 Email: owner@kasserpro.com
🔑 Password: Owner@123
👤 Role: SystemOwner
```
**Can:** Create/manage all tenants, full system access

---

### Admin (مدير المجزر)
```
📧 Email: admin@kasserpro.com
🔑 Password: Admin@123
👤 Role: Admin
🏢 Tenant: مجزر الأمانة
```
**Can:** Full tenant management, reports, backups

---

### Cashiers (الكاشيرات)
```
📧 Email: mohamed@kasserpro.com
🔑 Password: 123456
👤 Name: محمد الكاشير

📧 Email: ali@kasserpro.com
🔑 Password: 123456
👤 Name: علي الكاشير
```
**Can:** Create orders, manage shifts, accept payments

---

## 📊 Data Summary

| Item | Count | Notes |
|------|-------|-------|
| 👑 System Owner | 1 | Full system access |
| 🏢 Tenants | 1 | مجزر الأمانة |
| 🏪 Branches | 1 | الفرع الرئيسي |
| 👥 Users | 3 | 1 Admin + 2 Cashiers |
| 📦 Products | 24 | Butcher products |
| 👨‍💼 Customers | 6 | With purchase history |
| 🚚 Suppliers | 3 | Active suppliers |
| 🕐 Shifts | 15 | 14 closed + 1 open |
| 🛒 Orders | ~100-150 | Last 14 days |
| 💰 Expenses | 8 | Last 30 days |

---

## 🎯 Quick Test Scenarios

### 1. Login as System Owner
```
1. Go to login page
2. Email: owner@kasserpro.com
3. Password: Owner@123
4. Access: System management panel
```

### 2. Login as Admin
```
1. Go to login page
2. Email: admin@kasserpro.com
3. Password: Admin@123
4. Access: Full tenant dashboard
```

### 3. Login as Cashier
```
1. Go to login page
2. Email: mohamed@kasserpro.com
3. Password: 123456
4. Access: POS and orders
```

---

## 🔄 Reset Data

```bash
# Delete database
rm backend/KasserPro.API/kasserpro.db

# Restart application
dotnet run --project backend/KasserPro.API
```

---

## 📝 Architecture Rules

### Tax Calculation (Tax Exclusive)
```
NetTotal = UnitPrice × Quantity
TaxAmount = NetTotal × (14% / 100)
TotalAmount = NetTotal + TaxAmount
```

### Multi-Tenancy
- Every entity has `TenantId` + `BranchId`
- System Owner: `TenantId = null`, `BranchId = null`
- Use `ICurrentUserService` for tenant/branch context

---

## 📚 Full Documentation

See: `backend/SEED_DATA_DOCUMENTATION.md`
