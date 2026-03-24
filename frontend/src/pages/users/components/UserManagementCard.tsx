import { useState } from "react";
import { Card, Loading } from "../../../components/common";
import { Plus, Edit, Trash2, Power, PowerOff } from "lucide-react";
import {
  useGetAllUsersQuery,
  useDeleteUserMutation,
  useToggleUserStatusMutation,
} from "../../../api/usersApi";
import { toast } from "react-hot-toast";
import UserFormModal from "./UserFormModal";
import type { UserDto } from "../../../types/user.types";

export default function UserManagementCard() {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserDto | null>(null);

  const { data: usersData, isLoading } = useGetAllUsersQuery();
  const [deleteUser] = useDeleteUserMutation();
  const [toggleStatus] = useToggleUserStatusMutation();

  const users = usersData?.data || [];

  const handleDelete = async (userId: number, userName: string) => {
    if (!confirm(`هل أنت متأكد من حذف المستخدم "${userName}"؟`)) return;

    try {
      await deleteUser(userId).unwrap();
      toast.success("تم حذف المستخدم بنجاح");
    } catch (error) {
      toast.error("فشل حذف المستخدم");
    }
  };

  const handleToggleStatus = async (userId: number, currentStatus: boolean) => {
    try {
      await toggleStatus({ id: userId, data: { isActive: !currentStatus } }).unwrap();
      toast.success(currentStatus ? "تم تعطيل المستخدم" : "تم تفعيل المستخدم");
    } catch (error) {
      toast.error("فشل تغيير حالة المستخدم");
    }
  };

  const handleEdit = (user: UserDto) => {
    setEditingUser(user);
    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    setIsModalOpen(false);
    setEditingUser(null);
  };

  if (isLoading) return <Loading />;

  return (
    <>
      <Card>
        <div className="flex justify-between items-center mb-6">
          <h2 className="text-2xl font-bold">قائمة المستخدمين</h2>
          <button
            onClick={() => setIsModalOpen(true)}
            className="flex items-center gap-2 bg-blue-500 text-white px-4 py-2 rounded-lg hover:bg-blue-600 transition"
          >
            <Plus className="w-5 h-5" />
            إضافة مستخدم جديد
          </button>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-gray-50 border-b">
              <tr>
                <th className="px-4 py-3 text-right text-sm font-semibold text-gray-700">
                  الاسم
                </th>
                <th className="px-4 py-3 text-right text-sm font-semibold text-gray-700">
                  البريد الإلكتروني
                </th>
                <th className="px-4 py-3 text-right text-sm font-semibold text-gray-700">
                  الهاتف
                </th>
                <th className="px-4 py-3 text-right text-sm font-semibold text-gray-700">
                  الدور
                </th>
                <th className="px-4 py-3 text-right text-sm font-semibold text-gray-700">
                  الفرع
                </th>
                <th className="px-4 py-3 text-right text-sm font-semibold text-gray-700">
                  الحالة
                </th>
                <th className="px-4 py-3 text-right text-sm font-semibold text-gray-700">
                  الإجراءات
                </th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {users.map((user) => (
                <tr key={user.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 text-sm">{user.name}</td>
                  <td className="px-4 py-3 text-sm text-gray-600">
                    {user.email}
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-600">
                    {user.phone || "-"}
                  </td>
                  <td className="px-4 py-3 text-sm">
                    <span
                      className={`px-2 py-1 rounded-full text-xs font-medium ${
                        user.role === "Admin"
                          ? "bg-purple-100 text-purple-700"
                          : user.role === "SystemOwner"
                            ? "bg-red-100 text-red-700"
                            : "bg-blue-100 text-blue-700"
                      }`}
                    >
                      {user.role === "Admin"
                        ? "مدير"
                        : user.role === "SystemOwner"
                          ? "مالك النظام"
                          : "كاشير"}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-600">
                    {user.role === "Admin" ? "كل الفروع" : user.branchName || "-"}
                  </td>
                  <td className="px-4 py-3 text-sm">
                    <span
                      className={`px-2 py-1 rounded-full text-xs font-medium ${
                        user.isActive
                          ? "bg-green-100 text-green-700"
                          : "bg-gray-100 text-gray-700"
                      }`}
                    >
                      {user.isActive ? "نشط" : "معطل"}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-sm">
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => handleEdit(user)}
                        className="p-2 text-blue-600 hover:bg-blue-50 rounded-lg transition"
                        title="تعديل"
                      >
                        <Edit className="w-4 h-4" />
                      </button>
                      <button
                        onClick={() => handleToggleStatus(user.id, user.isActive)}
                        className={`p-2 rounded-lg transition ${
                          user.isActive
                            ? "text-orange-600 hover:bg-orange-50"
                            : "text-green-600 hover:bg-green-50"
                        }`}
                        title={user.isActive ? "تعطيل" : "تفعيل"}
                      >
                        {user.isActive ? (
                          <PowerOff className="w-4 h-4" />
                        ) : (
                          <Power className="w-4 h-4" />
                        )}
                      </button>
                      <button
                        onClick={() => handleDelete(user.id, user.name)}
                        className="p-2 text-red-600 hover:bg-red-50 rounded-lg transition"
                        title="حذف"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {users.length === 0 && (
            <div className="text-center py-12 text-gray-500">
              لا يوجد مستخدمين حالياً
            </div>
          )}
        </div>
      </Card>

      {isModalOpen && (
        <UserFormModal
          user={editingUser}
          onClose={handleCloseModal}
        />
      )}
    </>
  );
}
