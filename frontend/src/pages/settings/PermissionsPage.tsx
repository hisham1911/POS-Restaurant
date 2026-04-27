import { useState } from "react";
import { Card, Loading } from "../../components/common";
import {
  useGetAllCashierPermissionsQuery,
  useGetAvailablePermissionsQuery,
  useUpdateUserPermissionsMutation,
} from "../../api/permissionsApi";
import { PermissionInfo } from "../../types/permission.types";
import { toast } from "react-hot-toast";

export default function PermissionsPage() {
  const [selectedUserId, setSelectedUserId] = useState<number | null>(null);
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([]);

  const { data: cashiers, isLoading: loadingCashiers } =
    useGetAllCashierPermissionsQuery();
  const { data: availablePermissions, isLoading: loadingPermissions } =
    useGetAvailablePermissionsQuery();
  const [updatePermissions, { isLoading: updating }] =
    useUpdateUserPermissionsMutation();

  const selectedCashier = cashiers?.data?.find((c) => c.userId === selectedUserId);

  const handleSelectCashier = (userId: number) => {
    setSelectedUserId(userId);
    const cashier = cashiers?.data?.find((c) => c.userId === userId);
    setSelectedPermissions(cashier?.permissions || []);
  };

  const togglePermission = (permissionKey: string) => {
    setSelectedPermissions((prev) =>
      prev.includes(permissionKey)
        ? prev.filter((p) => p !== permissionKey)
        : [...prev, permissionKey]
    );
  };

  const handleSave = async () => {
    if (!selectedUserId) return;

    try {
      await updatePermissions({
        userId: selectedUserId,
        data: { permissions: selectedPermissions },
      }).unwrap();
      toast.success("تم تحديث الصلاحيات بنجاح");
    } catch {
      // baseApi.ts already shows error toast
    }
  };

  if (loadingCashiers || loadingPermissions) return <Loading />;

  // Group permissions by group
  const groupedPermissions = availablePermissions?.data?.reduce((acc, perm) => {
    if (!acc[perm.groupAr]) acc[perm.groupAr] = [];
    acc[perm.groupAr].push(perm);
    return acc;
  }, {} as Record<string, PermissionInfo[]>);

  return (
    <div className="container mx-auto p-4 sm:p-6" dir="rtl">
      <h1 className="text-3xl font-bold mb-6">إدارة صلاحيات الكاشيرين</h1>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Cashier List */}
        <Card className="lg:col-span-1">
          <h2 className="text-xl font-semibold mb-4">اختر كاشير</h2>
          <div className="space-y-2">
            {cashiers?.data?.map((cashier) => (
              <button
                key={cashier.userId}
                onClick={() => handleSelectCashier(cashier.userId)}
                className={`w-full text-right p-3 rounded-lg transition ${
                  selectedUserId === cashier.userId
                    ? "bg-blue-500 text-white"
                    : "bg-gray-100 hover:bg-gray-200"
                }`}
              >
                <div className="font-semibold">{cashier.userName}</div>
                <div className="text-sm opacity-75">{cashier.email}</div>
              </button>
            ))}
          </div>
        </Card>

        {/* Permissions Editor */}
        <Card className="lg:col-span-2">
          {selectedCashier ? (
            <>
              <h2 className="text-xl font-semibold mb-4">
                صلاحيات: {selectedCashier.userName}
              </h2>

              <div className="space-y-6">
                {Object.entries(groupedPermissions || {}).map(
                  ([group, perms]) => (
                    <div key={group}>
                      <h3 className="font-semibold text-lg mb-3 text-gray-700">
                        {group}
                      </h3>
                      <div className="space-y-2 bg-gray-50 p-4 rounded-lg">
                        {perms.map((perm) => (
                          <label
                            key={perm.key}
                            className="flex items-center gap-3 cursor-pointer hover:bg-gray-100 p-2 rounded"
                          >
                            <input
                              type="checkbox"
                              checked={selectedPermissions.includes(perm.key)}
                              onChange={() => togglePermission(perm.key)}
                              className="w-5 h-5"
                            />
                            <div>
                              <div className="font-medium">
                                {perm.descriptionAr}
                              </div>
                              <div className="text-sm text-gray-500">
                                {perm.description}
                              </div>
                            </div>
                          </label>
                        ))}
                      </div>
                    </div>
                  )
                )}
              </div>

              <button
                onClick={handleSave}
                disabled={updating}
                className="mt-6 w-full bg-blue-500 text-white py-3 rounded-lg hover:bg-blue-600 disabled:opacity-50"
              >
                {updating ? "جاري الحفظ..." : "💾 حفظ الصلاحيات"}
              </button>
            </>
          ) : (
            <div className="text-center text-gray-500 py-12">
              اختر كاشيراً من القائمة لتعديل صلاحياته
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}

