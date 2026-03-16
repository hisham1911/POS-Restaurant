import { useState } from 'react';
import { Card, Loading, Button, Input, Modal } from '../../components/common';
import {
  Users,
  Edit,
  Lock,
  CheckCircle,
  XCircle,
  Building2,
  Search,
} from 'lucide-react';
import {
  useGetAllSystemUsersQuery,
  useUpdateSystemUserMutation,
  useToggleSystemUserStatusMutation,
  useResetSystemUserPasswordMutation,
  SystemUser,
} from '../../api/systemUsersApi';
import { toast } from 'sonner';

export default function SystemUsersPage() {
  const { data: users, isLoading, error } = useGetAllSystemUsersQuery();
  const [updateUser] = useUpdateSystemUserMutation();
  const [toggleStatus] = useToggleSystemUserStatusMutation();
  const [resetPassword] = useResetSystemUserPasswordMutation();

  const [searchTerm, setSearchTerm] = useState('');
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [passwordDialogOpen, setPasswordDialogOpen] = useState(false);
  const [selectedUser, setSelectedUser] = useState<SystemUser | null>(null);
  const [editForm, setEditForm] = useState({ name: '', email: '', phone: '' });
  const [newPassword, setNewPassword] = useState('');

  const handleEditClick = (user: SystemUser) => {
    setSelectedUser(user);
    setEditForm({
      name: user.name,
      email: user.email,
      phone: user.phone || '',
    });
    setEditDialogOpen(true);
  };

  const handleEditSubmit = async () => {
    if (!selectedUser) return;

    try {
      await updateUser({
        userId: selectedUser.id,
        data: editForm,
      }).unwrap();
      toast.success('تم تحديث بيانات المستخدم بنجاح');
      setEditDialogOpen(false);
    } catch (error) {
      toast.error('فشل تحديث بيانات المستخدم');
    }
  };

  const handleToggleStatus = async (userId: number) => {
    try {
      await toggleStatus(userId).unwrap();
      toast.success('تم تغيير حالة المستخدم بنجاح');
    } catch (error) {
      toast.error('فشل تغيير حالة المستخدم');
    }
  };

  const handlePasswordClick = (user: SystemUser) => {
    setSelectedUser(user);
    setNewPassword('');
    setPasswordDialogOpen(true);
  };

  const handlePasswordSubmit = async () => {
    if (!selectedUser || !newPassword) return;

    try {
      await resetPassword({
        userId: selectedUser.id,
        data: { newPassword },
      }).unwrap();
      toast.success('تم إعادة تعيين كلمة المرور بنجاح');
      setPasswordDialogOpen(false);
      setNewPassword('');
    } catch (error) {
      toast.error('فشل إعادة تعيين كلمة المرور');
    }
  };

  const getRoleBadgeColor = (role: string) => {
    switch (role) {
      case 'SystemOwner':
        return 'bg-red-100 text-red-800';
      case 'Admin':
        return 'bg-blue-100 text-blue-800';
      case 'Cashier':
        return 'bg-green-100 text-green-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  const getRoleLabel = (role: string) => {
    switch (role) {
      case 'SystemOwner':
        return 'مالك النظام';
      case 'Admin':
        return 'مدير';
      case 'Cashier':
        return 'كاشير';
      default:
        return role;
    }
  };

  if (isLoading) {
    return <Loading />;
  }

  // Filter users by search term
  const filteredUsers = users?.filter(
    (user) =>
      user.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      user.email.toLowerCase().includes(searchTerm.toLowerCase()) ||
      user.tenantName.toLowerCase().includes(searchTerm.toLowerCase())
  );

  // Group users by tenant
  const groupedUsers = filteredUsers?.reduce((acc, user) => {
    const tenantName = user.tenantName || 'System';
    if (!acc[tenantName]) {
      acc[tenantName] = [];
    }
    acc[tenantName].push(user);
    return acc;
  }, {} as Record<string, SystemUser[]>);

  return (
    <div className="container mx-auto p-6" dir="rtl">
      <div className="mb-6">
        <div className="flex items-center gap-3 mb-2">
          <Users className="w-8 h-8 text-blue-600" />
          <h1 className="text-3xl font-bold">إدارة المستخدمين</h1>
        </div>
        <p className="text-gray-600">إدارة جميع مستخدمي النظام عبر جميع المحلات</p>
      </div>

      {/* Statistics */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <Card className="p-6">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-gray-600 text-sm mb-1">إجمالي المستخدمين</p>
              <p className="text-3xl font-bold text-blue-600">{users?.length || 0}</p>
            </div>
            <Users className="w-12 h-12 text-blue-200" />
          </div>
        </Card>

        <Card className="p-6">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-gray-600 text-sm mb-1">مستخدم نشط</p>
              <p className="text-3xl font-bold text-green-600">
                {users?.filter((u) => u.isActive).length || 0}
              </p>
            </div>
            <CheckCircle className="w-12 h-12 text-green-200" />
          </div>
        </Card>

        <Card className="p-6">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-gray-600 text-sm mb-1">مستخدم غير نشط</p>
              <p className="text-3xl font-bold text-red-600">
                {users?.filter((u) => !u.isActive).length || 0}
              </p>
            </div>
            <XCircle className="w-12 h-12 text-red-200" />
          </div>
        </Card>
      </div>

      {/* Search */}
      <div className="mb-6">
        <div className="relative">
          <Search className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-5 h-5" />
          <Input
            type="text"
            placeholder="بحث بالاسم أو البريد أو المحل..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="pr-10"
          />
        </div>
      </div>

      {/* Users by Tenant */}
      {groupedUsers &&
        Object.entries(groupedUsers).map(([tenantName, tenantUsers]) => (
          <Card key={tenantName} className="mb-6">
            <div className="p-6">
              <div className="flex items-center gap-2 mb-4">
                <Building2 className="w-6 h-6 text-blue-600" />
                <h2 className="text-xl font-bold">{tenantName}</h2>
                <span className="text-sm text-gray-500">({tenantUsers.length} مستخدم)</span>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead className="bg-gray-50">
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
                      <th className="px-4 py-3 text-center text-sm font-semibold text-gray-700">
                        الإجراءات
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-200">
                    {tenantUsers.map((user) => (
                      <tr key={user.id} className="hover:bg-gray-50">
                        <td className="px-4 py-3 text-sm">{user.name}</td>
                        <td className="px-4 py-3 text-sm text-gray-600">{user.email}</td>
                        <td className="px-4 py-3 text-sm text-gray-600">
                          {user.phone || '-'}
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getRoleBadgeColor(
                              user.role
                            )}`}
                          >
                            {getRoleLabel(user.role)}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-sm text-gray-600">
                          {user.branchName || '-'}
                        </td>
                        <td className="px-4 py-3">
                          {user.isActive ? (
                            <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">
                              <CheckCircle className="w-3 h-3" />
                              نشط
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                              <XCircle className="w-3 h-3" />
                              غير نشط
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex items-center justify-center gap-2">
                            <button
                              onClick={() => handleEditClick(user)}
                              className="p-2 text-blue-600 hover:bg-blue-50 rounded-lg transition-colors"
                              title="تعديل"
                            >
                              <Edit className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => handlePasswordClick(user)}
                              className="p-2 text-orange-600 hover:bg-orange-50 rounded-lg transition-colors"
                              title="إعادة تعيين كلمة المرور"
                            >
                              <Lock className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => handleToggleStatus(user.id)}
                              disabled={user.role === 'SystemOwner'}
                              className={`p-2 rounded-lg transition-colors ${
                                user.role === 'SystemOwner'
                                  ? 'text-gray-400 cursor-not-allowed'
                                  : user.isActive
                                  ? 'text-red-600 hover:bg-red-50'
                                  : 'text-green-600 hover:bg-green-50'
                              }`}
                              title={user.isActive ? 'تعطيل' : 'تفعيل'}
                            >
                              {user.isActive ? (
                                <XCircle className="w-4 h-4" />
                              ) : (
                                <CheckCircle className="w-4 h-4" />
                              )}
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </Card>
        ))}

      {/* Edit Dialog */}
      <Modal
        isOpen={editDialogOpen}
        onClose={() => setEditDialogOpen(false)}
        title="تعديل بيانات المستخدم"
      >
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">الاسم</label>
            <Input
              value={editForm.name}
              onChange={(e) => setEditForm({ ...editForm, name: e.target.value })}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              البريد الإلكتروني
            </label>
            <Input
              value={editForm.email}
              onChange={(e) => setEditForm({ ...editForm, email: e.target.value })}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">الهاتف</label>
            <Input
              value={editForm.phone}
              onChange={(e) => setEditForm({ ...editForm, phone: e.target.value })}
            />
          </div>
          <div className="flex gap-2 justify-end pt-4">
            <Button variant="secondary" onClick={() => setEditDialogOpen(false)}>
              إلغاء
            </Button>
            <Button onClick={handleEditSubmit}>حفظ</Button>
          </div>
        </div>
      </Modal>

      {/* Password Reset Dialog */}
      <Modal
        isOpen={passwordDialogOpen}
        onClose={() => setPasswordDialogOpen(false)}
        title="إعادة تعيين كلمة المرور"
      >
        <div className="space-y-4">
          <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
            <p className="text-sm text-yellow-800">
              سيتم إعادة تعيين كلمة المرور للمستخدم: <strong>{selectedUser?.name}</strong>
            </p>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              كلمة المرور الجديدة
            </label>
            <Input
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              placeholder="أدخل كلمة المرور الجديدة"
            />
          </div>
          <div className="flex gap-2 justify-end pt-4">
            <Button variant="secondary" onClick={() => setPasswordDialogOpen(false)}>
              إلغاء
            </Button>
            <Button onClick={handlePasswordSubmit} disabled={!newPassword}>
              إعادة تعيين
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
