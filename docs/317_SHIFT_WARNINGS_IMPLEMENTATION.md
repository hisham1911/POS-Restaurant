# ✅ Shift Warnings System - Implementation Summary

## 📋 Overview

تم تنفيذ نظام إنذار الورديات بدلاً من الإغلاق التلقائي. النظام يراقب الورديات المفتوحة ويرسل تحذيرات للمستخدمين والأدمن بدون إغلاق تلقائي.

---

## 🎯 Features Implemented

### 1. Backend

#### ✅ Error Codes (ErrorCodes.cs)
- `SHIFT_WARNING_12_HOURS` - تحذير بعد 12 ساعة
- `SHIFT_CRITICAL_24_HOURS` - تحذير شديد بعد 24 ساعة

#### ✅ Background Service (ShiftWarningBackgroundService.cs)
- يعمل كل 30 دقيقة
- يفحص كل الورديات المفتوحة
- بعد 12 ساعة: تحذير عادي + تسجيل في Audit Logs
- بعد 24 ساعة: تحذير شديد + إشعار للأدمن + تسجيل في Audit Logs
- لا يغلق الورديات تلقائياً

#### ✅ API Endpoint
**GET /api/shifts/warnings**
```typescript
interface ShiftWarningDto {
  level: 'None' | 'Warning' | 'Critical';
  message: string;
  hoursOpen: number;
  shouldWarn: boolean;
  isCritical: boolean;
  shiftId?: number;
}
```

#### ✅ Service Method (ShiftService.cs)
- `GetShiftWarningsAsync(userId)` - يجلب التحذيرات للوردية الحالية

#### ✅ Configuration (appsettings.json)
```json
{
  "ShiftWarnings": {
    "Enabled": true,
    "WarningHours": 12,
    "CriticalHours": 24
  }
}
```

---

### 2. Frontend

#### ✅ Types (shift.types.ts)
```typescript
interface ShiftWarning {
  level: 'None' | 'Warning' | 'Critical';
  message: string;
  hoursOpen: number;
  shouldWarn: boolean;
  isCritical: boolean;
  shiftId?: number;
}
```

#### ✅ API Integration (shiftsApi.ts)
- `useGetShiftWarningsQuery()` - RTK Query hook
- Polling every 5 minutes في ShiftPage
- Polling every 10 minutes في POSPage

#### ✅ UI Component (ShiftWarningBanner.tsx)
- عرض التحذيرات بشكل واضح
- ألوان مختلفة للتحذير العادي والشديد
- Animated (pulse effect)
- قابل للإغلاق
- يعرض الوقت المفتوح بالساعات والدقائق

#### ✅ Integration
- **ShiftPage**: عرض التحذير في أعلى الصفحة
- **POSPage**: عرض التحذير في أعلى صفحة نقطة البيع

---

## 🔄 How It Works

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                  Shift Warning System                        │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. User opens shift                                         │
│     └─> Shift.OpenedAt = DateTime.UtcNow                    │
│                                                              │
│  2. Background Service (every 30 min)                        │
│     ├─> Check all open shifts                               │
│     ├─> Calculate hoursOpen                                 │
│     └─> Log warnings to AuditLogs                           │
│                                                              │
│  3. Frontend (polling)                                       │
│     ├─> ShiftPage: every 5 minutes                          │
│     ├─> POSPage: every 10 minutes                           │
│     └─> GET /api/shifts/warnings                            │
│                                                              │
│  4. Warning Levels                                           │
│     ├─> < 12h: No warning                                   │
│     ├─> ≥ 12h: ⚠️ Warning banner                            │
│     └─> ≥ 24h: 🚨 Critical banner + Admin notification      │
│                                                              │
│  5. User Action Required                                     │
│     └─> Manual shift close (no auto-close)                  │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 📝 Configuration Options

### Backend (appsettings.json)

```json
{
  "ShiftWarnings": {
    "Enabled": true,           // Enable/disable warnings
    "WarningHours": 12,        // Hours before warning
    "CriticalHours": 24        // Hours before critical warning
  }
}
```

### Frontend Polling Intervals

- **ShiftPage**: 5 minutes (300,000 ms)
- **POSPage**: 10 minutes (600,000 ms)

---

## 🎨 UI Examples

### Warning Banner (12+ hours)
```
┌────────────────────────────────────────────────────────┐
│ ⚠️ تحذير                    🕐 12 ساعة و 30 دقيقة     │
│                                                         │
│ ⚠️ تحذير: الوردية مفتوحة منذ أكثر من 12 ساعة.        │
│ يُنصح بإغلاقها وفتح وردية جديدة                       │
│                                                         │
│                                                    [X]  │
└────────────────────────────────────────────────────────┘
```

### Critical Banner (24+ hours)
```
┌────────────────────────────────────────────────────────┐
│ 🚨 تحذير شديد               🕐 25 ساعة و 15 دقيقة     │
│                                                         │
│ 🚨 تحذير شديد: الوردية مفتوحة منذ أكثر من 24 ساعة!   │
│ يجب إغلاقها فوراً                                      │
│                                                         │
│ ┌─────────────────────────────────────────────────┐   │
│ │ ⚡ يُرجى إغلاق الوردية الحالية وفتح وردية      │   │
│ │ جديدة في أقرب وقت ممكن للحفاظ على دقة         │   │
│ │ السجلات المالية.                                │   │
│ └─────────────────────────────────────────────────┘   │
│                                                    [X]  │
└────────────────────────────────────────────────────────┘
```

---

## 📊 Audit Logs

### Warning Log Entry
```json
{
  "action": "ShiftWarning",
  "entityType": "Shift",
  "entityId": "123",
  "details": "⚠️ الوردية مفتوحة منذ 12.5 ساعة. يُنصح بإغلاقها وفتح وردية جديدة.",
  "userId": 5,
  "userName": "أحمد محمد",
  "ipAddress": "System",
  "userAgent": "ShiftWarningBackgroundService"
}
```

### Critical Warning Log Entry
```json
{
  "action": "ShiftCriticalWarning",
  "entityType": "Shift",
  "entityId": "123",
  "details": "🚨 تحذير شديد: الوردية مفتوحة منذ 25.3 ساعة! يجب إغلاقها فوراً.",
  "userId": 5,
  "userName": "أحمد محمد",
  "ipAddress": "System",
  "userAgent": "ShiftWarningBackgroundService"
}
```

### Admin Notification
```json
{
  "action": "AdminNotification",
  "entityType": "Shift",
  "entityId": "123",
  "details": "🚨 إشعار للمدير: الوردية #123 للمستخدم أحمد محمد في فرع الفرع الرئيسي مفتوحة منذ 25.3 ساعة",
  "userId": 1,
  "userName": "Admin",
  "ipAddress": "System",
  "userAgent": "ShiftWarningBackgroundService"
}
```

---

## ✅ Checklist

### Backend
- [x] Error codes added
- [x] Background service created
- [x] Service method implemented
- [x] Controller endpoint added
- [x] Configuration added
- [x] Service registered in Program.cs

### Frontend
- [x] Types defined
- [x] API endpoint added
- [x] Warning banner component created
- [x] Integrated in ShiftPage
- [x] Integrated in POSPage
- [x] Polling configured

### Documentation
- [x] API documented in API_DOCUMENTATION.md
- [x] Error codes documented
- [x] Configuration documented

---

## 🚀 Testing

### Manual Testing Steps

1. **Open a shift**
   ```bash
   POST /api/shifts/open
   { "openingBalance": 1000 }
   ```

2. **Wait or modify database**
   ```sql
   -- For testing, modify OpenedAt to 13 hours ago
   UPDATE Shifts 
   SET OpenedAt = datetime('now', '-13 hours')
   WHERE IsClosed = 0;
   ```

3. **Check warnings**
   ```bash
   GET /api/shifts/warnings
   ```

4. **Verify UI**
   - Go to /shift page
   - Should see ⚠️ warning banner
   - Go to /pos page
   - Should see warning banner

5. **Test critical warning**
   ```sql
   -- Modify to 25 hours ago
   UPDATE Shifts 
   SET OpenedAt = datetime('now', '-25 hours')
   WHERE IsClosed = 0;
   ```

6. **Verify critical UI**
   - Should see 🚨 critical banner
   - Check audit logs for admin notifications

---

## 📈 Future Enhancements

- [ ] Push notifications للموبايل
- [ ] Email notifications للأدمن
- [ ] SMS notifications
- [ ] Dashboard widget للورديات المفتوحة طويلاً
- [ ] تقرير شهري بالورديات التي تجاوزت 12 ساعة
- [ ] إمكانية تخصيص الأوقات لكل فرع

---

## 🔗 Related Files

### Backend
- `backend/KasserPro.Application/Common/ErrorCodes.cs`
- `backend/KasserPro.Infrastructure/Services/ShiftWarningBackgroundService.cs`
- `backend/KasserPro.Application/Services/Implementations/ShiftService.cs`
- `backend/KasserPro.Application/Services/Interfaces/IShiftService.cs`
- `backend/KasserPro.Application/DTOs/Shifts/ShiftWarningDto.cs`
- `backend/KasserPro.API/Controllers/ShiftsController.cs`
- `backend/KasserPro.API/Program.cs`
- `backend/KasserPro.API/appsettings.json`

### Frontend
- `frontend/src/types/shift.types.ts`
- `frontend/src/api/shiftsApi.ts`
- `frontend/src/components/shifts/ShiftWarningBanner.tsx`
- `frontend/src/components/shifts/index.ts`
- `frontend/src/pages/shifts/ShiftPage.tsx`
- `frontend/src/pages/pos/POSPage.tsx`

### Documentation
- `project-resources/docs/api/API_DOCUMENTATION.md`

---

**Implementation Date:** February 21, 2026  
**Status:** ✅ Complete  
**Version:** 1.0
