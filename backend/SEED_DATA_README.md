# 🌱 Seed Data - Quick Reference

## 🚀 Quick Start

```bash
# Delete database and reseed
rm backend/KasserPro.API/kasserpro.db
cd backend/KasserPro.API
dotnet run
```

---

## 📚 Documentation Files

| File | Description |
|------|-------------|
| `COMPLETE_SEED_DATA_GUIDE.md` | 📖 دليل شامل لجميع البيانات |
| `SEED_DATA_IMPLEMENTATION_SUMMARY.md` | 📋 ملخص التنفيذ والإحصائيات |
| `MULTI_TENANT_DEMO_GUIDE.md` | 🎯 دليل العرض التوضيحي |
| `SYSTEM_OWNER_CREDENTIALS_API.md` | 🔐 API لعرض بيانات الدخول |

---

## 🏪 Tenants (4)

| # | Name | Slug | Admin Email | Products | Orders |
|---|------|------|-------------|----------|--------|
| 1 | مجزر الأمانة | al-amana-butcher | admin@kasserpro.com | 24 | ~100+ |
| 2 | محل أدوات منزلية | home-appliances | samy@homeappliances.com | 5 | ~70 |
| 3 | سوبر ماركت الخير | supermarket | karim@supermarket.com | 4 | ~180 |
| 4 | مطعم الأمير | restaurant | tarek@restaurant.com | 4 | ~135 |

---

## 👥 All Accounts (14 Users)

### System Owner
```
owner@kasserpro.com / Owner@123
```

### Tenant 1: مجزر الأمانة (3 users)
```
admin@kasserpro.com / Admin@123
mohamed@kasserpro.com / 123456
ali@kasserpro.com / 123456
```

### Tenant 2: محل أدوات منزلية (3 users)
```
samy@homeappliances.com / Admin@123
nour@homeappliances.com / 123456
hoda@homeappliances.com / 123456
```

### Tenant 3: سوبر ماركت (4 users)
```
karim@supermarket.com / Admin@123
fatma@supermarket.com / 123456
zainab@supermarket.com / 123456
mariam@supermarket.com / 123456
```

### Tenant 4: مطعم (3 users)
```
tarek@restaurant.com / Admin@123
omar@restaurant.com / 123456
youssef@restaurant.com / 123456
```

---

## 📊 Data Summary

```
Total Users: 14
Total Tenants: 4
Total Products: 37
Total Customers: 49
Total Orders: ~485
Total Expenses: 43
Total Shifts: 54
```

---

## 🔧 Seeder Files

| File | Purpose |
|------|---------|
| `ButcherDataSeeder.cs` | Tenant 1 - Complete data |
| `MultiTenantSeeder.cs` | Creates Tenants 2-4 (basic structure) |
| `HomeAppliancesSeeder.cs` | Tenant 2 - Complete data |
| `SupermarketSeeder.cs` | Tenant 3 - Complete data |
| `RestaurantSeeder.cs` | Tenant 4 - Complete data |

---

## 🎯 API Endpoints

### Get All Credentials (System Owner only)
```bash
GET /api/system/credentials
Authorization: Bearer {system_owner_token}
```

### Get All Tenants (System Owner only)
```bash
GET /api/system/tenants
Authorization: Bearer {system_owner_token}
```

---

## 💡 Tips

1. **View all accounts**: Use `GET /api/system/credentials`
2. **Switch accounts**: Copy credentials and login
3. **Test different scenarios**: Each tenant has different order types
4. **Check reports**: All tenants have realistic data for reports

---

## 🔍 Verification

```bash
# Count users
sqlite3 backend/KasserPro.API/kasserpro.db "SELECT COUNT(*) FROM Users;"

# Count orders per tenant
sqlite3 backend/KasserPro.API/kasserpro.db "SELECT TenantId, COUNT(*) FROM Orders GROUP BY TenantId;"

# Count customers per tenant
sqlite3 backend/KasserPro.API/kasserpro.db "SELECT TenantId, COUNT(*) FROM Customers GROUP BY TenantId;"
```

---

## 📝 Notes

- All calculations use **Tax Exclusive** (14% Egypt VAT)
- All data is **realistic** and **production-ready**
- All passwords are **BCrypt hashed**
- System Owner has `TenantId = null` and `BranchId = null`

---

## ✅ Ready to Demo!

**4 complete stores ready for presentation! 🎉**
