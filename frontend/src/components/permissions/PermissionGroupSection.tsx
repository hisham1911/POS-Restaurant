import type { PermissionGroupDto } from "@/types/permission.types";

interface Props {
  group: PermissionGroupDto;
  activePermissions: string[];
  defaultPermissions: string[];
  onToggle: (key: string) => void;
}

export const PermissionGroupSection = ({
  group,
  activePermissions,
  defaultPermissions,
  onToggle,
}: Props) => {
  const allSelected = group.permissions.every((p) =>
    activePermissions.includes(p.name)
  );
  const noneSelected = group.permissions.every(
    (p) => !activePermissions.includes(p.name)
  );

  const handleSelectAll = () => {
    group.permissions
      .filter((p) => !activePermissions.includes(p.name))
      .forEach((p) => onToggle(p.name));
  };

  const handleDeselectAll = () => {
    group.permissions
      .filter((p) => activePermissions.includes(p.name))
      .forEach((p) => onToggle(p.name));
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
              className="text-xs text-blue-600 hover:text-blue-700 font-medium"
            >
              تحديد الكل
            </button>
          )}
          {!noneSelected && (
            <button
              onClick={handleDeselectAll}
              className="text-xs text-red-600 hover:text-red-700 font-medium"
            >
              إلغاء الكل
            </button>
          )}
        </div>
      </div>

      {/* Permissions list */}
      <div className="divide-y divide-gray-100">
        {group.permissions.map((permission) => {
          const isActive = activePermissions.includes(permission.name);
          const isDefault = defaultPermissions.includes(permission.name);
          const isCustomized = isActive !== isDefault;

          return (
            <div
              key={permission.name}
              className="flex items-start gap-3 px-4 py-3 hover:bg-gray-50 transition-colors"
            >
              <input
                type="checkbox"
                id={`perm-${permission.name}`}
                checked={isActive}
                onChange={() => onToggle(permission.name)}
                className="mt-0.5 accent-blue-600 h-4 w-4 cursor-pointer"
              />
              <label
                htmlFor={`perm-${permission.name}`}
                className="flex-1 cursor-pointer"
              >
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium text-gray-800">
                    {permission.label}
                  </span>

                  {/* Sensitive permission warning */}
                  {permission.isSensitive && (
                    <span className="text-xs bg-red-50 text-red-600 px-1.5 py-0.5 rounded font-medium">
                      حساس
                    </span>
                  )}

                  {/* Customized vs default badge */}
                  {isCustomized && (
                    <span className="text-xs bg-amber-50 text-amber-600 px-1.5 py-0.5 rounded">
                      {isActive && !isDefault ? "مضاف" : "محذوف من الافتراضي"}
                    </span>
                  )}
                </div>

                {permission.description && (
                  <p className="text-xs text-gray-500 mt-0.5">
                    {permission.description}
                  </p>
                )}
              </label>
            </div>
          );
        })}
      </div>
    </div>
  );
};
