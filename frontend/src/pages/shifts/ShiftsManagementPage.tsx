import { useState } from "react";
import { Users, Clock, AlertCircle, Eye, CheckCircle2, XCircle } from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { usePermission } from "@/hooks/usePermission";
import { Card } from "@/components/common/Card";
import { Loading } from "@/components/common/Loading";
import {
  ActiveShiftsList,
  ForceCloseShiftModal,
  ShiftDetailsDrawer,
} from "@/components/shifts";
import { Shift } from "@/types/shift.types";
import { useGetShiftsQuery } from "@/api/shiftsApi";
import { formatCurrency, formatDateTime } from "@/utils/formatters";

export const ShiftsManagementPage = () => {
  const { user } = useAuth();
  const { hasPermission } = usePermission();
  const canManageShifts = hasPermission("ShiftsManage");
  const [selectedShift, setSelectedShift] = useState<Shift | null>(null);
  const [showForceCloseModal, setShowForceCloseModal] = useState(false);
  const [showDetailsDrawer, setShowDetailsDrawer] = useState(false);

  const {
    data: shiftsData,
    isLoading: shiftsLoading,
    error: shiftsError,
  } = useGetShiftsQuery();

  const handleForceClose = (shift: Shift) => {
    setSelectedShift(shift);
    setShowForceCloseModal(true);
  };

  const handleViewDetails = (shift: Shift) => {
    setSelectedShift(shift);
    setShowDetailsDrawer(true);
  };

  if (!canManageShifts) {
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

  const allShifts = shiftsData?.data ?? [];
  const closedShifts = allShifts.filter((s) => s.isClosed);

  return (
    <div className="h-full overflow-auto p-4 sm:p-6 space-y-4 sm:space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">إدارة الورديات</h1>
          <p className="text-gray-500 mt-1">
            متابعة ومراقبة جميع الورديات في الفرع
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
        isAdmin={canManageShifts}
      />

      {/* Historical Shifts Table */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
          <h2 className="text-lg font-bold text-gray-800">
            سجل الورديات المغلقة
          </h2>
          <span className="text-sm text-gray-500">
            {closedShifts.length} وردية
          </span>
        </div>

        {shiftsLoading ? (
          <div className="p-8">
            <Loading />
          </div>
        ) : shiftsError ? (
          <div className="p-6 text-center text-red-600">
            حدث خطأ أثناء تحميل الورديات
          </div>
        ) : closedShifts.length === 0 ? (
          <div className="p-8 text-center text-gray-500 text-sm">
            لا توجد ورديات مغلقة في السجل
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-gray-600">
                <tr>
                  <th className="px-4 py-3 text-right font-semibold">#</th>
                  <th className="px-4 py-3 text-right font-semibold">الكاشير</th>
                  <th className="px-4 py-3 text-right font-semibold">الفتح</th>
                  <th className="px-4 py-3 text-right font-semibold">الإغلاق</th>
                  <th className="px-4 py-3 text-right font-semibold">المبيعات</th>
                  <th className="px-4 py-3 text-right font-semibold">الفرق</th>
                  <th className="px-4 py-3 text-right font-semibold">المراجعة</th>
                  <th className="px-4 py-3 text-right font-semibold">إجراء</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {closedShifts.map((shift) => (
                  <tr
                    key={shift.id}
                    className="hover:bg-gray-50 transition-colors cursor-pointer"
                    onClick={() => handleViewDetails(shift)}
                  >
                    <td className="px-4 py-3 font-medium text-gray-900">
                      {shift.id}
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {shift.userName}
                    </td>
                    <td className="px-4 py-3 text-gray-500">
                      {formatDateTime(shift.openedAt)}
                    </td>
                    <td className="px-4 py-3 text-gray-500">
                      {shift.closedAt ? formatDateTime(shift.closedAt) : "—"}
                    </td>
                    <td className="px-4 py-3 font-medium text-gray-900">
                      {formatCurrency(shift.totalSales)}
                    </td>
                    <td className="px-4 py-3">
                      {shift.difference === 0 ? (
                        <span className="text-green-600 font-medium">٠</span>
                      ) : shift.difference > 0 ? (
                        <span className="text-green-600 font-medium">
                          +{formatCurrency(shift.difference)}
                        </span>
                      ) : (
                        <span className="text-red-600 font-medium">
                          {formatCurrency(shift.difference)}
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      {shift.isReconciled ? (
                        <span className="inline-flex items-center gap-1 rounded-full bg-green-50 px-2 py-0.5 text-xs font-medium text-green-700">
                          <CheckCircle2 className="h-3 w-3" />
                          تمت
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 rounded-full bg-gray-50 px-2 py-0.5 text-xs font-medium text-gray-600">
                          <XCircle className="h-3 w-3" />
                          لم تتم
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          handleViewDetails(shift);
                        }}
                        className="inline-flex items-center gap-1 rounded-lg bg-primary-50 px-2.5 py-1.5 text-xs font-medium text-primary-700 hover:bg-primary-100 transition-colors"
                      >
                        <Eye className="h-3.5 w-3.5" />
                        عرض
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

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

      {/* Details Drawer */}
      <ShiftDetailsDrawer
        shift={selectedShift}
        isOpen={showDetailsDrawer}
        onClose={() => {
          setShowDetailsDrawer(false);
          setSelectedShift(null);
        }}
      />
    </div>
  );
};

export default ShiftsManagementPage;

