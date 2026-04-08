import { useState } from "react";
import { Users, Clock, AlertCircle } from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { Card } from "@/components/common/Card";
import { Loading } from "@/components/common/Loading";
import {
  ActiveShiftsList,
  ForceCloseShiftModal,
} from "@/components/shifts";
import { Shift } from "@/types/shift.types";

export const ShiftsManagementPage = () => {
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";
  const [selectedShift, setSelectedShift] = useState<Shift | null>(null);
  const [showForceCloseModal, setShowForceCloseModal] = useState(false);

  const handleForceClose = (shift: Shift) => {
    setSelectedShift(shift);
    setShowForceCloseModal(true);
  };

  if (!isAdmin) {
    return (
      <div className="h-full flex items-center justify-center">
        <Card className="max-w-md text-center p-8">
          <AlertCircle className="w-16 h-16 text-red-500 mx-auto mb-4" />
          <h2 className="text-xl font-bold text-gray-800 mb-2">
            غير مصرح لك
          </h2>
          <p className="text-gray-600">
            هذه الصفحة متاحة للمديرين فقط
          </p>
        </Card>
      </div>
    );
  }

  return (
    <div className="h-full overflow-auto p-4 sm:p-6 space-y-4 sm:space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">إدارة الورديات</h1>
          <p className="text-gray-500 mt-1">
            متابعة ومراقبة جميع الورديات المفتوحة في الفرع
          </p>
        </div>
        <div className="flex items-center gap-2 px-4 py-2 bg-blue-50 rounded-lg">
          <Users className="w-5 h-5 text-blue-600" />
          <span className="text-sm font-medium text-blue-800">
            عرض المدير
          </span>
        </div>
      </div>

      {/* Info Card */}
      <Card className="bg-blue-50 border-blue-200">
        <div className="flex items-start gap-3">
          <Clock className="w-5 h-5 text-blue-600 mt-0.5" />
          <div className="flex-1">
            <h3 className="font-medium text-blue-900">معلومات مهمة</h3>
            <ul className="text-sm text-blue-700 mt-2 space-y-1 list-disc list-inside">
              <li>يمكنك رؤية جميع الورديات المفتوحة في الفرع الحالي</li>
              <li>يمكنك إغلاق أي وردية بالقوة في حالات الطوارئ</li>
              <li>سيتم تسجيل جميع عمليات الإغلاق بالقوة في سجل التدقيق</li>
              <li>الورديات التي لم يتم تسجيل نشاط عليها لأكثر من 12 ساعة ستظهر بتحذير</li>
            </ul>
          </div>
        </div>
      </Card>

      {/* Active Shifts List */}
      <ActiveShiftsList
        onForceClose={handleForceClose}
        currentUserId={user?.id}
        isAdmin={isAdmin}
      />

      {/* Force Close Modal */}
      {selectedShift && (
        <ForceCloseShiftModal
          shift={selectedShift}
          isOpen={showForceCloseModal}
          onClose={() => {
            setShowForceCloseModal(false);
            setSelectedShift(null);
          }}
          onSuccess={() => {
            setShowForceCloseModal(false);
            setSelectedShift(null);
          }}
        />
      )}
    </div>
  );
};

export default ShiftsManagementPage;

