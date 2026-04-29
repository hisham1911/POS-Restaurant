import type { PermissionGroupDto } from "@/types/permission.types";

interface Props {
  added: string[];
  removed: string[];
  onSave: () => void;
  onReset: () => void;
  saving: boolean;
  availableGroups: PermissionGroupDto[];
}

const getLabel = (key: string, groups: PermissionGroupDto[]) => {
  for (const group of groups) {
    const found = group.permissions.find((p) => p.name === key);
    if (found) return found.label;
  }
  return key;
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
      <div className="flex-1 text-sm">
        {added.length > 0 && (
          <p className="text-green-700 mb-1">
            <span className="font-semibold">تمت الإضافة: </span>
            {added.map((id) => getLabel(id, availableGroups)).join("، ")}
          </p>
        )}
        {removed.length > 0 && (
          <p className="text-red-700 mb-1">
            <span className="font-semibold">تمت الإزالة: </span>
            {removed.map((id) => getLabel(id, availableGroups)).join("، ")}
          </p>
        )}
        <p className="text-gray-500 text-xs mt-1">
          ⚠️ سيطبق التغيير بعد إعادة تسجيل دخول المستخدم
        </p>
      </div>

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
          className="px-5 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors disabled:opacity-60"
        >
          {saving ? "جاري الحفظ..." : "حفظ الصلاحيات"}
        </button>
      </div>
    </div>
  </div>
);
