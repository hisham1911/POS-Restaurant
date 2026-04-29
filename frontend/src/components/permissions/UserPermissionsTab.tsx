import { useState, useMemo } from "react";
import { toast } from "sonner";
import {
  useGetAvailablePermissionsQuery,
  useGetUserPermissionsDtoQuery,
  useUpdateUserPermissionsMutation,
} from "@/api/permissionsApi";
import { PermissionGroupSection } from "./PermissionGroupSection";
import { PermissionsSaveBar } from "./PermissionsSaveBar";
import type { ApiResponse } from "@/types/api.types";

interface Props {
  userId: number;
}

export const UserPermissionsTab = ({ userId }: Props) => {
  const { data: availableData, isLoading: loadingAvailable } =
    useGetAvailablePermissionsQuery();
  const { data: userPermsData, isLoading: loadingUser } =
    useGetUserPermissionsDtoQuery(userId);
  const [updatePermissions, { isLoading: saving }] =
    useUpdateUserPermissionsMutation();

  // Local draft state — what the admin has selected but not saved yet
  const [draft, setDraft] = useState<string[] | null>(null);

  const userPermissions = userPermsData?.data;

  // Transform PermissionInfo[] into PermissionGroupDto[]
  const availableGroups = useMemo(() => {
    const infos = availableData?.data ?? [];
    const groups: Record<string, { groupName: string; permissions: Array<{ id: number; name: string; label: string; description: string; isSensitive: boolean }> }> = {};
    for (const info of infos) {
      if (!groups[info.groupAr]) {
        groups[info.groupAr] = { groupName: info.groupAr, permissions: [] };
      }
      groups[info.groupAr].permissions.push({
        id: 0,
        name: info.key,
        label: info.descriptionAr || info.description,
        description: info.descriptionAr || info.description,
        isSensitive: info.isSensitive ?? false,
      });
    }
    return Object.values(groups);
  }, [availableData]);

  // Use draft if admin made changes, otherwise use saved state
  const activePermissions =
    draft ?? userPermissions?.permissions ?? [];

  const togglePermission = (key: string) => {
    const base = draft ?? userPermissions?.permissions ?? [];
    setDraft(
      base.includes(key) ? base.filter((p) => p !== key) : [...base, key]
    );
  };

  // Compute diff for the save bar summary
  const added = useMemo(() => {
    if (!draft || !userPermissions) return [];
    return draft.filter(
      (key) => !userPermissions.permissions.includes(key)
    );
  }, [draft, userPermissions]);

  const removed = useMemo(() => {
    if (!draft || !userPermissions) return [];
    return userPermissions.permissions.filter(
      (key) => !draft.includes(key)
    );
  }, [draft, userPermissions]);

  const hasPendingChanges =
    draft !== null && (added.length > 0 || removed.length > 0);

  const handleSave = async () => {
    if (!draft) return;
    try {
      await updatePermissions({ userId, permissions: draft }).unwrap();
      toast.success("تم حفظ الصلاحيات — سيطبق التغيير بعد إعادة تسجيل دخول المستخدم");
      setDraft(null);
    } catch (err) {
      const error = err as { data: ApiResponse<null> };
      switch (error.data?.errorCode) {
        case "CANNOT_MODIFY_ADMIN_PERMISSIONS":
          toast.error("لا يمكن تعديل صلاحيات مستخدم إداري");
          break;
        case "USER_NOT_FOUND":
          toast.error("المستخدم غير موجود");
          break;
        default:
          toast.error(error.data?.message ?? "حدث خطأ أثناء الحفظ");
      }
    }
  };

  const handleReset = () => setDraft(null);

  if (loadingAvailable || loadingUser) {
    return (
      <div className="p-6 text-center text-gray-500">جاري التحميل...</div>
    );
  }

  return (
    <div className="flex flex-col gap-6 pb-32">
      {/* Customization badge */}
      {userPermissions && (
        <div className="flex items-center gap-2">
          <span
            className={`text-xs px-2 py-1 rounded-full font-medium ${
              userPermissions.isCustomized
                ? "bg-amber-100 text-amber-700"
                : "bg-gray-100 text-gray-600"
            }`}
          >
            {userPermissions.isCustomized
              ? "صلاحيات مخصصة"
              : "صلاحيات افتراضية"}
          </span>
        </div>
      )}

      {/* Permission groups */}
      {availableGroups.map((group) => (
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
