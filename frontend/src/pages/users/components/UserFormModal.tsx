import { useState, useEffect } from "react";
import { X, ChevronDown } from "lucide-react";
import {
  useCreateUserMutation,
  useUpdateUserMutation,
} from "../../../api/usersApi";
import { useGetBranchesQuery } from "../../../api/branchesApi";
import { toast } from "react-hot-toast";
import type { UserDto } from "../../../types/user.types";
import { Portal } from "../../../components/common/Portal";
import { handleApiError } from "../../../utils/errorHandler";

interface UserFormModalProps {
  user: UserDto | null;
  onClose: () => void;
}

export default function UserFormModal({ user, onClose }: UserFormModalProps) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [phone, setPhone] = useState("");
  const [role, setRole] = useState("Cashier");
  const [branchId, setBranchId] = useState<number | undefined>();

  const { data: branchesData } = useGetBranchesQuery();
  const branches = branchesData?.data || [];

  const [createUser, { isLoading: creating }] = useCreateUserMutation();
  const [updateUser, { isLoading: updating }] = useUpdateUserMutation();

  const isEditing = !!user;
  const isLoading = creating || updating;
  const shouldShowBranchField = role === "Cashier";

  useEffect(() => {
    if (user) {
      setName(user.name);
      setEmail(user.email);
      setPhone(user.phone || "");
      setRole(user.role);
      setBranchId(user.branchId);
    }
  }, [user]);

  useEffect(() => {
    // Branch selection is only relevant for cashiers in the form UX.
    if (role !== "Cashier") {
      setBranchId(undefined);
    }
  }, [role]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!name.trim() || !email.trim()) {
      toast.error("الاسم والبريد الإلكتروني مطلوبان");
      return;
    }

    if (!isEditing && !password.trim()) {
      toast.error("كلمة المرور مطلوبة");
      return;
    }

    try {
      if (isEditing) {
        await updateUser({
          id: user.id,
          data: {
            name: name.trim(),
            email: email.trim(),
            phone: phone.trim() || undefined,
            role,
            branchId,
          },
        }).unwrap();
        toast.success("تم تحديث المستخدم بنجاح");
      } else {
        await createUser({
          name: name.trim(),
          email: email.trim(),
          password: password.trim(),
          phone: phone.trim() || undefined,
          role,
          branchId,
        }).unwrap();
        toast.success("تم إنشاء المستخدم بنجاح");
      }
      onClose();
    } catch (error: unknown) {
      toast.error(handleApiError(error));
    }
  };

  return (
    <Portal>
      <div
        className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/50"
        onClick={onClose}
      >
        <div
          className="bg-white rounded-2xl shadow-2xl w-full max-w-md max-h-[90vh] flex flex-col overflow-hidden"
          onClick={(e) => e.stopPropagation()}
          dir="rtl"
        >
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b border-gray-200 flex-shrink-0">
            <h2 className="text-xl font-bold text-gray-800">
              {isEditing ? "تعديل مستخدم" : "إضافة مستخدم جديد"}
            </h2>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            >
              <X className="w-5 h-5 text-gray-500" />
            </button>
          </div>

          <form
            onSubmit={handleSubmit}
            className="p-6 space-y-4 overflow-y-auto flex-1"
          >
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                الاسم *
              </label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                placeholder="أدخل اسم المستخدم"
                required
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                البريد الإلكتروني *
              </label>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                placeholder="example@domain.com"
                required
              />
            </div>

            {!isEditing && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  كلمة المرور *
                </label>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                  placeholder="أدخل كلمة المرور"
                  required
                />
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                الهاتف
              </label>
              <input
                type="tel"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                placeholder="01xxxxxxxxx"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                الدور *
              </label>
              <div className="relative">
                <select
                  value={role}
                  onChange={(e) => setRole(e.target.value)}
                  className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
                  required
                >
                  <option value="Cashier">كاشير</option>
                  <option value="Admin">مدير</option>
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>

            {shouldShowBranchField && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  الفرع
                </label>
                <div className="relative">
                  <select
                    value={branchId || ""}
                    onChange={(e) =>
                      setBranchId(
                        e.target.value ? Number(e.target.value) : undefined,
                      )
                    }
                    className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
                  >
                    <option value="">اختر الفرع</option>
                    {branches.map((branch) => (
                      <option key={branch.id} value={branch.id}>
                        {branch.name}
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
                </div>
              </div>
            )}
          </form>

          <div className="flex gap-3 p-6 border-t border-gray-200 flex-shrink-0">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 px-4 py-2.5 border border-gray-300 text-gray-700 rounded-xl hover:bg-gray-50 transition-colors font-medium"
            >
              إلغاء
            </button>
            <button
              type="submit"
              onClick={handleSubmit}
              disabled={isLoading}
              className="flex-1 px-4 py-2.5 bg-primary-600 text-white rounded-xl hover:bg-primary-700 transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isLoading ? "جاري الحفظ..." : isEditing ? "تحديث" : "إضافة"}
            </button>
          </div>
        </div>
      </div>
    </Portal>
  );
}
