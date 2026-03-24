# دليل دمج تحسينات الورديات
## Shift Enhancements - Integration Guide

**التاريخ**: 9 فبراير 2026  
**الحالة**: ✅ جاهز للدمج

---

## 📋 نظرة عامة

تم إنشاء جميع المكونات الأساسية لتحسينات الورديات. هذا الدليل يشرح كيفية دمجها مع التطبيق الحالي.

---

## 🎯 المكونات المنشأة

### 1. Modals (5 مكونات)
- ✅ `ForceCloseShiftModal` - إغلاق بالقوة (Admin only)
- ✅ `HandoverShiftModal` - تسليم الوردية
- ✅ `InactivityAlertModal` - تنبيه عدم النشاط
- ✅ `ShiftRecoveryModal` - استعادة بعد التعطل
- ✅ `ActiveShiftsList` - قائمة الورديات المفتوحة

### 2. Hooks & Utils
- ✅ `useInactivityMonitor` - مراقبة عدم النشاط
- ✅ `shiftPersistence` - حفظ واستعادة الورديات

---

## 🔧 خطوات الدمج

### الخطوة 1: دمج Shift Recovery في App.tsx

```typescript
// client/src/App.tsx
import { useEffect, useState } from 'react';
import { ShiftRecoveryModal } from './components/shifts';
import { shiftPersistence } from './utils/shiftPersistence';
import { useGetCurrentShiftQuery } from './api/shiftsApi';

function App() {
  const [showRecovery, setShowRecovery] = useState(false);
  const [recoveredShift, setRecoveredShift] = useState(null);
  const { data: currentShift } = useGetCurrentShiftQuery();

  // Check for saved shift on app start
  useEffect(() => {
    const saved = shiftPersistence.load();
    if (saved && !currentShift?.data) {
      setRecoveredShift(saved.shift);
      setShowRecovery(true);
    }
  }, [currentShift]);

  // Start auto-save when shift is open
  useEffect(() => {
    if (currentShift?.data && !currentShift.data.isClosed) {
      shiftPersistence.startAutoSave(() => currentShift.data);
    } else {
      shiftPersistence.stopAutoSave();
    }

    return () => shiftPersistence.stopAutoSave();
  }, [currentShift]);

  const handleRestore = () => {
    // Restore shift logic here
    setShowRecovery(false);
  };

  const handleDiscard = () => {
    shiftPersistence.clear();
    setShowRecovery(false);
  };

  return (
    <>
      {/* Your existing app content */}
      
      {/* Shift Recovery Modal */}
      {recoveredShift && (
        <ShiftRecoveryModal
          shift={recoveredShift}
          savedAt={shiftPersistence.load()?.savedAt || ''}
          isOpen={showRecovery}
          onRestore={handleRestore}
          onDiscard={handleDiscard}
        />
      )}
    </>
  );
}
```

---

### الخطوة 2: دمج Inactivity Monitor في Shift Page

```typescript
// client/src/pages/shifts/ShiftManagement.tsx
import { useState } from 'react';
import { useInactivityMonitor } from '../../hooks/useInactivityMonitor';
import { 
  InactivityAlertModal, 
  HandoverShiftModal 
} from '../../components/shifts';
import { useGetCurrentShiftQuery, useCloseShiftMutation } from '../../api/shiftsApi';

export default function ShiftManagement() {
  const { data: currentShift } = useGetCurrentShiftQuery();
  const [closeShift] = useCloseShiftMutation();
  
  const [showInactivityAlert, setShowInactivityAlert] = useState(false);
  const [showHandover, setShowHandover] = useState(false);

  // Setup inactivity monitor
  const { recordActivity, snooze } = useInactivityMonitor({
    shift: currentShift?.data || null,
    enabled: true,
    onInactivityAlert: () => setShowInactivityAlert(true),
  });

  // Record activity on user actions
  const handleUserAction = () => {
    recordActivity();
  };

  const handleCloseShift = async () => {
    // Your close shift logic
    setShowInactivityAlert(false);
  };

  const handleContinue = () => {
    snooze(); // Snooze for 1 hour
    setShowInactivityAlert(false);
  };

  return (
    <div onClick={handleUserAction}>
      {/* Your existing shift management UI */}

      {/* Inactivity Alert */}
      {currentShift?.data && (
        <InactivityAlertModal
          shift={currentShift.data}
          isOpen={showInactivityAlert}
          onClose={() => setShowInactivityAlert(false)}
          onCloseShift={handleCloseShift}
          onHandover={() => {
            setShowInactivityAlert(false);
            setShowHandover(true);
          }}
          onContinue={handleContinue}
        />
      )}

      {/* Handover Modal */}
      {currentShift?.data && (
        <HandoverShiftModal
          shift={currentShift.data}
          isOpen={showHandover}
          onClose={() => setShowHandover(false)}
          onSuccess={() => {
            // Refresh shift data
          }}
          availableUsers={[
            // Fetch from API or pass as prop
          ]}
        />
      )}
    </div>
  );
}
```

---

### الخطوة 3: إضافة Active Shifts List للـ Admin

```typescript
// client/src/pages/admin/ShiftsOverview.tsx
import { useState } from 'react';
import { ActiveShiftsList, ForceCloseShiftModal } from '../../components/shifts';
import { useAuth } from '../../hooks/useAuth';
import { Shift } from '../../types/shift.types';

export default function ShiftsOverview() {
  const { user } = useAuth();
  const [selectedShift, setSelectedShift] = useState<Shift | null>(null);
  const [showForceClose, setShowForceClose] = useState(false);

  const handleForceClose = (shift: Shift) => {
    setSelectedShift(shift);
    setShowForceClose(true);
  };

  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold mb-6">إدارة الورديات</h1>

      {/* Active Shifts List */}
      <ActiveShiftsList
        onForceClose={handleForceClose}
        currentUserId={user?.id}
        isAdmin={user?.role === 'Admin'}
      />

      {/* Force Close Modal */}
      {selectedShift && (
        <ForceCloseShiftModal
          shift={selectedShift}
          isOpen={showForceClose}
          onClose={() => setShowForceClose(false)}
          onSuccess={() => {
            setShowForceClose(false);
            // Refresh shifts list
          }}
        />
      )}
    </div>
  );
}
```

---

### الخطوة 4: إضافة Handover Button في Shift Details

```typescript
// client/src/pages/shifts/ShiftDetails.tsx
import { useState } from 'react';
import { HandoverShiftModal } from '../../components/shifts';
import { useGetShiftQuery } from '../../api/shiftsApi';

export default function ShiftDetails({ shiftId }: { shiftId: number }) {
  const { data: shift } = useGetShiftQuery(shiftId);
  const [showHandover, setShowHandover] = useState(false);

  if (!shift?.data) return null;

  return (
    <div>
      {/* Existing shift details */}

      {/* Handover Button */}
      {!shift.data.isClosed && (
        <button
          onClick={() => setShowHandover(true)}
          className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
        >
          🔄 تسليم الوردية
        </button>
      )}

      {/* Handover Modal */}
      <HandoverShiftModal
        shift={shift.data}
        isOpen={showHandover}
        onClose={() => setShowHandover(false)}
        onSuccess={() => {
          // Refresh shift data
        }}
        availableUsers={[
          // Fetch from API
        ]}
      />
    </div>
  );
}
```

---

## 🔌 API Integration

### جلب المستخدمين المتاحين للتسليم

```typescript
// client/src/api/usersApi.ts
export const usersApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getAvailableUsersForHandover: builder.query<ApiResponse<User[]>, void>({
      query: () => '/users/available-for-handover',
    }),
  }),
});

export const { useGetAvailableUsersForHandoverQuery } = usersApi;
```

استخدام في Component:

```typescript
const { data: users } = useGetAvailableUsersForHandoverQuery();

<HandoverShiftModal
  shift={shift}
  availableUsers={users?.data || []}
  // ...
/>
```

---

## 🎨 UI Integration Examples

### 1. إضافة Badge للورديات المسلمة

```typescript
{shift.isHandedOver && (
  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800">
    🔄 تم التسليم من {shift.handedOverFromUserName}
  </span>
)}
```

### 2. إضافة Warning لعدم النشاط

```typescript
{shift.inactiveHours >= 12 && (
  <div className="bg-orange-50 border border-orange-200 rounded p-3">
    <p className="text-sm text-orange-800">
      ⏰ تحذير: لم يتم تسجيل نشاط منذ {shift.inactiveHours} ساعة
    </p>
  </div>
)}
```

### 3. إضافة Badge للإغلاق بالقوة

```typescript
{shift.isForceClosed && (
  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-red-100 text-red-800">
    ⚠️ تم الإغلاق بالقوة بواسطة {shift.forceClosedByUserName}
  </span>
)}
```

---

## 🧪 Testing Checklist

### Manual Testing

- [ ] فتح وردية جديدة
- [ ] التحقق من حفظ الوردية في LocalStorage
- [ ] إعادة تشغيل التطبيق والتحقق من ظهور Recovery Modal
- [ ] اختبار تسليم الوردية لمستخدم آخر
- [ ] اختبار إغلاق بالقوة (Admin)
- [ ] اختبار تنبيه عدم النشاط (بعد 12 ساعة)
- [ ] التحقق من ظهور الورديات المفتوحة في Active Shifts List
- [ ] اختبار Snooze في تنبيه عدم النشاط

### E2E Tests (TODO)

```typescript
// client/e2e/shift-enhancements.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Shift Enhancements', () => {
  test('should show recovery modal on app restart', async ({ page }) => {
    // Test implementation
  });

  test('should allow handover to another user', async ({ page }) => {
    // Test implementation
  });

  test('should allow admin to force close shift', async ({ page }) => {
    // Test implementation
  });

  test('should show inactivity alert after 12 hours', async ({ page }) => {
    // Test implementation
  });
});
```

---

## 📊 Performance Considerations

### LocalStorage Auto-Save
- يحفظ كل دقيقة (60 ثانية)
- حجم البيانات صغير (~2KB)
- لا يؤثر على الأداء

### Inactivity Check
- يفحص كل دقيقة (60 ثانية)
- عملية خفيفة (مقارنة timestamps)
- لا يؤثر على الأداء

### API Calls
- `updateActivity` - لا يحتاج invalidation
- `getActiveShifts` - يستخدم cache
- `forceClose` & `handover` - invalidate shifts cache

---

## 🔒 Security & Permissions

### Force Close
- ✅ Admin only (backend validation)
- ✅ Reason required
- ✅ Audit log

### Handover
- ✅ Any user can handover their own shift
- ✅ Cannot handover to same user
- ✅ Target user must not have open shift
- ✅ Audit log

### Active Shifts
- ✅ Cashier sees only their own shift
- ✅ Admin sees all shifts in branch
- ✅ Filtered by TenantId & BranchId

---

## 🎯 Next Steps

### 1. Integration (عاجل)
- [ ] دمج Recovery Modal في App.tsx
- [ ] دمج Inactivity Monitor في Shift pages
- [ ] إضافة Active Shifts List للـ Admin
- [ ] إضافة Handover button في Shift details

### 2. API Enhancement (اختياري)
- [ ] إنشاء endpoint لجلب المستخدمين المتاحين
- [ ] إضافة WebSocket notifications للتسليم
- [ ] إضافة email notifications للإغلاق بالقوة

### 3. Testing (مهم)
- [ ] E2E tests
- [ ] Manual testing
- [ ] Performance testing

### 4. Documentation (مهم)
- [ ] User guide with screenshots
- [ ] API documentation update
- [ ] Video tutorial (optional)

---

## 📝 Notes

### Known Limitations
- Inactivity check يعتمد على `lastActivityAt` من Backend
- Recovery modal يظهر فقط إذا كان LocalStorage يحتوي على بيانات
- Available users للتسليم يحتاج endpoint جديد (أو استخدام users list الحالي)

### Future Enhancements
- إضافة WebSocket للتحديثات الفورية
- إضافة notifications للمستخدمين
- إضافة تقرير مفصل للتسليمات
- إضافة dashboard للورديات المفتوحة

---

## 🎉 الخلاصة

**جميع المكونات جاهزة للدمج** ✅

المطلوب فقط:
1. دمج المكونات في الصفحات الحالية (2-3 ساعات)
2. اختبار يدوي (1 ساعة)
3. E2E tests (2 ساعات)

**الوقت المتوقع للإكمال**: 5-6 ساعات عمل

---

**تاريخ الإنشاء**: 9 فبراير 2026  
**الحالة**: ✅ جاهز للاستخدام  
**المطور**: Kiro AI Assistant
