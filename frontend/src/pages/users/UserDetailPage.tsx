import { useParams, useNavigate } from "react-router-dom";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentUser } from "@/store/slices/authSlice";
import { ArrowRight, Shield } from "lucide-react";
import { UserPermissionsTab } from "@/components/permissions/UserPermissionsTab";

export default function UserDetailPage() {
  const { userId } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  const currentUser = useAppSelector(selectCurrentUser);
  const numericUserId = userId ? parseInt(userId, 10) : 0;

  if (!numericUserId) {
    return (
      <div className="container mx-auto p-6">
        <p className="text-red-600">معرف المستخدم غير صالح</p>
      </div>
    );
  }

  const canManagePermissions =
    currentUser?.role === "Admin" || currentUser?.role === "SystemOwner";

  return (
    <div className="container mx-auto p-4 sm:p-6" dir="rtl">
      {/* Header with back button */}
      <div className="mb-6">
        <button
          onClick={() => navigate("/users")}
          className="flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900 mb-3 transition-colors"
        >
          <ArrowRight className="w-4 h-4" />
          العودة لإدارة المستخدمين
        </button>
        <h1 className="text-2xl font-bold">تفاصيل المستخدم</h1>
        <p className="text-gray-600 text-sm mt-1">تعديل صلاحيات المستخدم</p>
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-200 mb-6">
        <nav className="-mb-px flex gap-6">
          <div className="flex items-center gap-2 border-b-2 border-primary-600 pb-3 px-1 text-primary-700 font-medium">
            <Shield className="w-4 h-4" />
            الصلاحيات
          </div>
        </nav>
      </div>

      {/* Content */}
      {canManagePermissions ? (
        <UserPermissionsTab userId={numericUserId} />
      ) : (
        <div className="bg-red-50 border border-red-200 rounded-xl p-6 text-center">
          <p className="text-red-700">
            ليس لديك صلاحية لإدارة صلاحيات المستخدمين
          </p>
        </div>
      )}
    </div>
  );
}
