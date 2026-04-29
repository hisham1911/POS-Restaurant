# Permissions Feature — Implementation Plan
> **Status:** Ready for execution  
> **Stack:** React 18 · TypeScript · RTK Query · TailwindCSS · ASP.NET Core  
> **Decisions locked — do not revisit without explicit approval**

---

## ✅ Locked Decisions

| # | Decision | Value |
|---|----------|-------|
| 1 | Who can manage permissions? | **Admin only** — no delegation for now |
| 2 | Who can be customized? | **Operational users only** (Cashiers and non-admin roles) — never Admin or SystemOwner |
| 3 | Where does this feature live? | **Inside User Management only** — `/users/{id}/permissions` |
| 4 | Permission Templates? | **Phase 2** — not now, but design must support it |

---

## 🚫 Invariants — Never Break These

```
1. An operational user CANNOT edit their own permissions
2. Admin and SystemOwner permissions CANNOT be modified from any UI
3. Frontend guards (usePermission) are for UX only — backend enforces all real security
4. Every permission change MUST be audited (who, on whom, what changed, when, which tenant)
5. Frontend MUST NOT show UI that the backend cannot support
6. All endpoints MUST respect Tenant Isolation — no cross-tenant access
7. Permission changes take effect after the target user re-authenticates (SecurityStamp invalidation)
```

---

## Phase 1 — Fix the Foundation (Do This First)

### Task 1.1 — Remove Duplicate Entry Point

**Problem:** Permissions UI exists in two places (`/users` tab + `/settings/permissions`). This confuses admins.

**Action:**
- Delete or repurpose `PermissionsPage` at `/settings/permissions`
- Remove its route from `App.tsx`
- Remove its link from the sidebar/navigation
- The only path to manage permissions is now: `Users → Select User → Permissions Tab`

```
Files to touch:
- src/App.tsx                         → remove /settings/permissions route
- src/components/layout/Sidebar.tsx   → remove permissions nav link
- src/pages/settings/PermissionsPage.tsx → delete or repurpose
```

---

### Task 1.2 — Unify Access Model (Frontend + Backend)

**Problem:** Frontend opens the permissions page based on `SettingsManage` permission, but the backend `PermissionsController` requires `[Authorize(Roles = "Admin")]`. These are inconsistent.

**Action — Backend:**
```csharp
// PermissionsController.cs
// REMOVE: [Authorize(Roles = "Admin")]  ← too broad, inconsistent
// ADD:
[Authorize(Roles = "Admin,SystemOwner")]
// This makes the backend rule explicit and consistent
```

**Action — Frontend:**
```typescript
// Replace SettingsManage check with explicit Admin role check
// src/pages/users/UserManagementPage.tsx

const { user } = useAppSelector(selectCurrentUser);

// Show permissions tab only for Admins
const canManagePermissions = user?.role === 'Admin' || user?.role === 'SystemOwner';
```

> ⚠️ Do NOT use `usePermission('SettingsManage')` as the guard for this feature going forward.

---

### Task 1.3 — Lock Down Who Can Be Customized

**Problem:** The UI says "users" but the backend only works on Cashiers. This mismatch causes confusion and silent failures.

**Action — Backend:**
```csharp
// PermissionService.cs
// Make this rule explicit and return a clear error if violated

public async Task UpdateUserPermissionsAsync(string userId, List<Permission> permissions)
{
    var user = await _userManager.FindByIdAsync(userId);
    
    if (user == null)
        return Result.Fail("USER_NOT_FOUND");

    // ENFORCE: Only operational users can be customized
    var protectedRoles = new[] { "Admin", "SystemOwner" };
    var userRoles = await _userManager.GetRolesAsync(user);
    
    if (userRoles.Any(r => protectedRoles.Contains(r)))
        return Result.Fail("CANNOT_MODIFY_ADMIN_PERMISSIONS");

    // Continue with update...
}
```

**Action — Frontend:**
```typescript
// src/api/permissionsApi.ts
// Add error handling for this new error code

case 'CANNOT_MODIFY_ADMIN_PERMISSIONS':
  toast.error('لا يمكن تعديل صلاحيات مستخدم إداري');
  break;
```

```typescript
// In the user list UI — filter to only show operational users
// src/components/permissions/UserPermissionsList.tsx

const operationalUsers = users.filter(
  user => !['Admin', 'SystemOwner'].includes(user.role)
);
```

---

## Phase 2 — Rebuild the Permissions UI (Inside User Management)

### Task 2.1 — Add Permissions Tab to User Detail Page

The permissions UI lives as a tab inside the user detail view.

**Component structure:**
```
src/pages/users/
├── UserManagementPage.tsx       ← existing, add route for user detail
└── UserDetailPage.tsx           ← new: wraps tabs

src/components/permissions/
├── UserPermissionsTab.tsx       ← main tab component
├── PermissionGroupSection.tsx   ← renders one group of permissions
├── PermissionCheckbox.tsx       ← single permission row
├── PermissionsSaveBar.tsx       ← bottom bar: diff summary + save button
└── PermissionsBadge.tsx         ← shows "افتراضي" vs "مخصص"
```

**Route:**
```typescript
// src/App.tsx
<Route path="/users/:userId/permissions" element={
  <ProtectedRoute>
    <AdminOnlyRoute>       {/* new guard — Admin role only */}
      <UserDetailPage tab="permissions" />
    </AdminOnlyRoute>
  </ProtectedRoute>
} />
```

---

### Task 2.2 — AdminOnlyRoute Guard

```typescript
// src/components/auth/AdminOnlyRoute.tsx
import { Navigate } from 'react-router-dom';
import { useAppSelector } from '@/store/hooks';
import { selectCurrentUser } from '@/store/slices/authSlice';

interface Props { children: React.ReactNode }

export const AdminOnlyRoute = ({ children }: Props) => {
  const user = useAppSelector(selectCurrentUser);
  const isAdmin = user?.role === 'Admin' || user?.role === 'SystemOwner';

  if (!isAdmin) return <Navigate to="/unauthorized" replace />;
  return <>{children}</>;
};
```

---

### Task 2.3 — Permissions API Slice

```typescript
// src/api/permissionsApi.ts
import { baseApi } from './baseApi';
import type { ApiResponse } from '@/types/api.types';
import type { PermissionDto, UpdatePermissionsDto } from '@/types/permission.types';

export const permissionsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({

    // Get all available permissions (grouped)
    getAvailablePermissions: builder.query<ApiResponse<PermissionGroupDto[]>, void>({
      query: () => '/permissions/available',
      providesTags: ['Permissions'],
    }),

    // Get permissions for a specific user
    getUserPermissions: builder.query<ApiResponse<UserPermissionsDto>, string>({
      query: (userId) => `/permissions/user/${userId}`,
      providesTags: (_result, _error, userId) => [{ type: 'UserPermissions', id: userId }],
    }),

    // Update permissions for a specific user
    updateUserPermissions: builder.mutation<ApiResponse<void>, UpdatePermissionsDto>({
      query: ({ userId, permissions }) => ({
        url: `/permissions/user/${userId}`,
        method: 'PUT',
        body: { permissions },
      }),
      invalidatesTags: (_result, _error, { userId }) => [
        { type: 'UserPermissions', id: userId },
      ],
    }),

  }),
});

export const {
  useGetAvailablePermissionsQuery,
  useGetUserPermissionsQuery,
  useUpdateUserPermissionsMutation,
} = permissionsApi;
```

**Add to `baseApi.tagTypes`:**
```typescript
tagTypes: [
  // ...existing tags
  'Permissions',
  'UserPermissions',
]
```

---

### Task 2.4 — TypeScript Types

```typescript
// src/types/permission.types.ts

export interface PermissionDto {
  id: number;
  name: string;          // e.g. "ProductsView"
  label: string;         // Arabic label: "عرض المنتجات"
  description: string;   // Arabic description
  isSensitive: boolean;  // true = show warning badge
}

export interface PermissionGroupDto {
  groupName: string;     // e.g. "المبيعات", "المخزون"
  permissions: PermissionDto[];
}

export interface UserPermissionsDto {
  userId: string;
  userName: string;
  role: string;
  isCustomized: boolean;           // true = has custom permissions, false = defaults
  currentPermissions: number[];    // list of active permission IDs
  defaultPermissions: number[];    // what this role gets by default
}

export interface UpdatePermissionsDto {
  userId: string;
  permissions: number[];
}
```

---

### Task 2.5 — UserPermissionsTab Component

```typescript
// src/components/permissions/UserPermissionsTab.tsx
import { useState, useMemo } from 'react';
import { toast } from 'sonner';
import {
  useGetAvailablePermissionsQuery,
  useGetUserPermissionsQuery,
  useUpdateUserPermissionsMutation,
} from '@/api/permissionsApi';
import { PermissionGroupSection } from './PermissionGroupSection';
import { PermissionsSaveBar } from './PermissionsSaveBar';
import type { ApiResponse } from '@/types/api.types';

interface Props {
  userId: string;
}

export const UserPermissionsTab = ({ userId }: Props) => {
  const { data: availableData, isLoading: loadingAvailable } = useGetAvailablePermissionsQuery();
  const { data: userPermsData, isLoading: loadingUser } = useGetUserPermissionsQuery(userId);
  const [updatePermissions, { isLoading: saving }] = useUpdateUserPermissionsMutation();

  // Local draft state — what the admin has selected but not saved yet
  const [draft, setDraft] = useState<number[] | null>(null);

  const userPermissions = userPermsData?.data;
  const availableGroups = availableData?.data ?? [];

  // Use draft if admin made changes, otherwise use saved state
  const activePermissions = draft ?? userPermissions?.currentPermissions ?? [];

  const togglePermission = (id: number) => {
    const base = draft ?? userPermissions?.currentPermissions ?? [];
    setDraft(
      base.includes(id) ? base.filter(p => p !== id) : [...base, id]
    );
  };

  // Compute diff for the save bar summary
  const added = useMemo(() => {
    if (!draft || !userPermissions) return [];
    return draft.filter(id => !userPermissions.currentPermissions.includes(id));
  }, [draft, userPermissions]);

  const removed = useMemo(() => {
    if (!draft || !userPermissions) return [];
    return userPermissions.currentPermissions.filter(id => !draft.includes(id));
  }, [draft, userPermissions]);

  const hasPendingChanges = draft !== null && (added.length > 0 || removed.length > 0);

  const handleSave = async () => {
    if (!draft) return;
    try {
      await updatePermissions({ userId, permissions: draft }).unwrap();
      toast.success('تم حفظ الصلاحيات — سيطبق التغيير بعد إعادة تسجيل دخول المستخدم');
      setDraft(null);
    } catch (err) {
      const error = err as { data: ApiResponse<null> };
      switch (error.data?.errorCode) {
        case 'CANNOT_MODIFY_ADMIN_PERMISSIONS':
          toast.error('لا يمكن تعديل صلاحيات مستخدم إداري');
          break;
        case 'USER_NOT_FOUND':
          toast.error('المستخدم غير موجود');
          break;
        default:
          toast.error(error.data?.message ?? 'حدث خطأ أثناء الحفظ');
      }
    }
  };

  const handleReset = () => setDraft(null);

  if (loadingAvailable || loadingUser) {
    return <div className="p-6 text-center text-gray-500">جاري التحميل...</div>;
  }

  return (
    <div className="flex flex-col gap-6 pb-32">
      {/* Customization badge */}
      {userPermissions && (
        <div className="flex items-center gap-2">
          <span className={`text-xs px-2 py-1 rounded-full font-medium ${
            userPermissions.isCustomized
              ? 'bg-warning-100 text-warning-700'
              : 'bg-gray-100 text-gray-600'
          }`}>
            {userPermissions.isCustomized ? 'صلاحيات مخصصة' : 'صلاحيات افتراضية'}
          </span>
        </div>
      )}

      {/* Permission groups */}
      {availableGroups.map(group => (
        <PermissionGroupSection
          key={group.groupName}
          group={group}
          activePermissions={activePermissions}
          defaultPermissions={userPermissions?.defaultPermissions ?? []}
          onToggle={togglePermission}
        />
      ))}

      {/* Save bar — shown only when there are pending changes */}
      {hasPendingChanges && (
        <PermissionsSaveBar
          added={added}
          removed={removed}
          onSave={handleSave}
          onReset={handleReset}
          saving={saving}
          availableGroups={availableGroups}
        />
      )}
    </div>
  );
};
```

---

### Task 2.6 — PermissionGroupSection Component

```typescript
// src/components/permissions/PermissionGroupSection.tsx
import type { PermissionGroupDto } from '@/types/permission.types';

interface Props {
  group: PermissionGroupDto;
  activePermissions: number[];
  defaultPermissions: number[];
  onToggle: (id: number) => void;
}

export const PermissionGroupSection = ({
  group,
  activePermissions,
  defaultPermissions,
  onToggle,
}: Props) => {
  const allSelected = group.permissions.every(p => activePermissions.includes(p.id));
  const noneSelected = group.permissions.every(p => !activePermissions.includes(p.id));

  const handleSelectAll = () => {
    group.permissions
      .filter(p => !activePermissions.includes(p.id))
      .forEach(p => onToggle(p.id));
  };

  const handleDeselectAll = () => {
    group.permissions
      .filter(p => activePermissions.includes(p.id))
      .forEach(p => onToggle(p.id));
  };

  return (
    <div className="border border-gray-200 rounded-xl overflow-hidden">
      {/* Group header */}
      <div className="bg-gray-50 px-4 py-3 flex items-center justify-between">
        <h3 className="font-semibold text-gray-800">{group.groupName}</h3>
        <div className="flex gap-2">
          {!allSelected && (
            <button
              onClick={handleSelectAll}
              className="text-xs text-primary-600 hover:text-primary-700 font-medium"
            >
              تحديد الكل
            </button>
          )}
          {!noneSelected && (
            <button
              onClick={handleDeselectAll}
              className="text-xs text-danger-600 hover:text-danger-700 font-medium"
            >
              إلغاء الكل
            </button>
          )}
        </div>
      </div>

      {/* Permissions list */}
      <div className="divide-y divide-gray-100">
        {group.permissions.map(permission => {
          const isActive = activePermissions.includes(permission.id);
          const isDefault = defaultPermissions.includes(permission.id);
          const isCustomized = isActive !== isDefault;

          return (
            <div
              key={permission.id}
              className="flex items-start gap-3 px-4 py-3 hover:bg-gray-50 transition-colors"
            >
              <input
                type="checkbox"
                id={`perm-${permission.id}`}
                checked={isActive}
                onChange={() => onToggle(permission.id)}
                className="mt-0.5 accent-primary-600 h-4 w-4 cursor-pointer"
              />
              <label
                htmlFor={`perm-${permission.id}`}
                className="flex-1 cursor-pointer"
              >
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium text-gray-800">
                    {permission.label}
                  </span>

                  {/* Sensitive permission warning */}
                  {permission.isSensitive && (
                    <span className="text-xs bg-danger-50 text-danger-600 px-1.5 py-0.5 rounded font-medium">
                      حساس
                    </span>
                  )}

                  {/* Customized vs default badge */}
                  {isCustomized && (
                    <span className="text-xs bg-warning-50 text-warning-600 px-1.5 py-0.5 rounded">
                      {isActive && !isDefault ? 'مضاف' : 'محذوف من الافتراضي'}
                    </span>
                  )}
                </div>

                {permission.description && (
                  <p className="text-xs text-gray-500 mt-0.5">{permission.description}</p>
                )}
              </label>
            </div>
          );
        })}
      </div>
    </div>
  );
};
```

---

### Task 2.7 — PermissionsSaveBar Component

```typescript
// src/components/permissions/PermissionsSaveBar.tsx
import type { PermissionGroupDto } from '@/types/permission.types';

interface Props {
  added: number[];
  removed: number[];
  onSave: () => void;
  onReset: () => void;
  saving: boolean;
  availableGroups: PermissionGroupDto[];
}

// Helper to get permission label by ID
const getLabel = (id: number, groups: PermissionGroupDto[]) => {
  for (const group of groups) {
    const found = group.permissions.find(p => p.id === id);
    if (found) return found.label;
  }
  return String(id);
};

export const PermissionsSaveBar = ({
  added,
  removed,
  onSave,
  onReset,
  saving,
  availableGroups,
}: Props) => (
  <div className="fixed bottom-0 start-0 end-0 bg-white border-t border-gray-200 shadow-lg px-6 py-4 z-50">
    <div className="max-w-4xl mx-auto flex items-start justify-between gap-4">

      {/* Change summary */}
      <div className="flex-1 text-sm">
        {added.length > 0 && (
          <p className="text-success-700 mb-1">
            <span className="font-semibold">تمت الإضافة: </span>
            {added.map(id => getLabel(id, availableGroups)).join('، ')}
          </p>
        )}
        {removed.length > 0 && (
          <p className="text-danger-700 mb-1">
            <span className="font-semibold">تمت الإزالة: </span>
            {removed.map(id => getLabel(id, availableGroups)).join('، ')}
          </p>
        )}
        <p className="text-gray-500 text-xs mt-1">
          ⚠️ سيطبق التغيير بعد إعادة تسجيل دخول المستخدم
        </p>
      </div>

      {/* Actions */}
      <div className="flex gap-3 shrink-0">
        <button
          onClick={onReset}
          disabled={saving}
          className="px-4 py-2 text-sm text-gray-600 bg-gray-100 hover:bg-gray-200 rounded-lg transition-colors disabled:opacity-50"
        >
          تراجع
        </button>
        <button
          onClick={onSave}
          disabled={saving}
          className="px-5 py-2 text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 rounded-lg transition-colors disabled:opacity-60"
        >
          {saving ? 'جاري الحفظ...' : 'حفظ الصلاحيات'}
        </button>
      </div>
    </div>
  </div>
);
```

---

## Phase 3 — Backend Hardening

### Task 3.1 — Audit Log on Every Permission Change

```csharp
// PermissionService.cs — add audit after every successful update

public async Task<Result> UpdateUserPermissionsAsync(
    string targetUserId,
    List<Permission> newPermissions,
    string changedByUserId,  // ← add this param
    string tenantId)
{
    // ... validation and update logic ...

    // After successful update — write audit
    await _auditService.LogAsync(new PermissionAuditEntry
    {
        ChangedByUserId  = changedByUserId,
        TargetUserId     = targetUserId,
        TenantId         = tenantId,
        AddedPermissions = newPermissions.Except(previousPermissions).ToList(),
        RemovedPermissions = previousPermissions.Except(newPermissions).ToList(),
        ChangedAt        = DateTime.UtcNow,
    });
}
```

---

### Task 3.2 — Clear Error Codes from Backend

All permission-related errors must return a specific `errorCode` so the frontend can handle them properly.

| Situation | errorCode |
|-----------|-----------|
| Trying to modify Admin/SystemOwner | `CANNOT_MODIFY_ADMIN_PERMISSIONS` |
| User not found | `USER_NOT_FOUND` |
| Caller is not Admin | `PERMISSION_DENIED` |
| Cross-tenant access attempt | `TENANT_ISOLATION_VIOLATION` |

---

### Task 3.3 — Tenant Isolation Check

```csharp
// PermissionsController.cs — verify target user belongs to same tenant

[HttpPut("user/{userId}")]
[Authorize(Roles = "Admin,SystemOwner")]
public async Task<IActionResult> UpdatePermissions(string userId, UpdatePermissionsRequest request)
{
    var callerTenantId = User.GetTenantId();
    var targetUser = await _userManager.FindByIdAsync(userId);

    // CRITICAL: Tenant isolation
    if (targetUser?.TenantId != callerTenantId)
        return Forbid(); // never reveal cross-tenant user existence

    // ... proceed
}
```

---

## Execution Order

```
Step 1 → Task 1.1  Remove duplicate entry point
Step 2 → Task 1.2  Unify access model (frontend + backend)
Step 3 → Task 1.3  Lock down who can be customized
Step 4 → Task 2.1  Add permissions tab to user detail page
Step 5 → Task 2.2  AdminOnlyRoute guard
Step 6 → Task 2.3  Permissions API slice (add tags to baseApi)
Step 7 → Task 2.4  TypeScript types
Step 8 → Task 2.5  UserPermissionsTab
Step 9 → Task 2.6  PermissionGroupSection
Step 10 → Task 2.7  PermissionsSaveBar
Step 11 → Task 3.1  Audit log
Step 12 → Task 3.2  Error codes
Step 13 → Task 3.3  Tenant isolation check
```

---

## Pre-Commit Checklist (for each task)

```bash
cd frontend && npx tsc --noEmit   # 0 errors required
```

- [ ] No `any` types used
- [ ] All API calls go through RTK Query (no raw fetch)
- [ ] Errors handled via `errorCode` not `message`
- [ ] New API tags added to `baseApi.tagTypes`
- [ ] Route guarded with `<AdminOnlyRoute>`
- [ ] RTL-aware Tailwind classes used (`ms-*`, `text-start`, `pe-*`)
- [ ] Toasts use `sonner` not `react-hot-toast`
- [ ] Tenant isolation verified in backend
- [ ] Audit log fires on every permission change

---

## Out of Scope (Phase 2 — Do Not Build Now)

- Permission Templates (كاشير أساسي / متقدم / مسؤول مخزن)
- Copy permissions from one user to another
- Audit log UI / reports screen
- Delegated permission management (non-admin managing permissions)
