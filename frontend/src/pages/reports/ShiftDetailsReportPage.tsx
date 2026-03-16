import { useState } from "react";
import {
  Clock,
  Calendar,
  Loader2,
  AlertCircle,
  DollarSign,
  ShoppingBag,
  AlertTriangle,
  CheckCircle,
  XCircle,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDateTime } from "@/utils/formatters";
import { useGetDetailedShiftsReportQuery } from "@/api/employeeReportsApi";

export const ShiftDetailsReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(1)).toISOString().split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);

  const { data, isLoading, isError, error } = useGetDetailedShiftsReportQuery({
    fromDate,
    toDate,
  });
  const report = data?.data;

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-primary-500" />
        <span className="mr-2 text-gray-600">جاري تحميل التقرير...</span>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="h-full flex items-center justify-center">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 text-red-500 mx-auto mb-4" />
          <p className="text-red-600">فشل في تحميل التقرير</p>
          <p className="text-gray-500 text-sm mt-2">
            {(error as any)?.data?.message || "حدث خطأ غير متوقع"}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="h-full overflow-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">
            تقرير تفاصيل الورديات
          </h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "تفاصيل جميع الورديات"}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <Calendar className="w-5 h-5 text-gray-400" />
            <input
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
              className="px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
            />
            <span className="text-gray-500">إلى</span>
            <input
              type="date"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
              className="px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
            />
          </div>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
        <Card className="bg-gradient-to-br from-primary-50 to-primary-100 border-primary-200">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-primary-500 rounded-xl flex items-center justify-center">
              <Clock className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-primary-700 font-medium">
                إجمالي الورديات
              </p>
              <p className="text-2xl font-bold text-primary-600">
                {report?.totalShifts || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-green-100 rounded-xl flex items-center justify-center">
              <CheckCircle className="w-6 h-6 text-green-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">مكتملة</p>
              <p className="text-2xl font-bold text-green-600">
                {report?.completedShifts || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center">
              <XCircle className="w-6 h-6 text-red-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">إغلاق إجباري</p>
              <p className="text-2xl font-bold text-red-600">
                {report?.forceClosedShifts || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-success-100 rounded-xl flex items-center justify-center">
              <DollarSign className="w-6 h-6 text-success-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">إجمالي المبيعات</p>
              <p className="text-2xl font-bold text-success-600">
                {formatCurrency(report?.totalRevenue || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <ShoppingBag className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">متوسط مبيعات الوردية</p>
              <p className="text-2xl font-bold text-blue-600">
                {formatCurrency(report?.averageShiftRevenue || 0)}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Table */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          تفاصيل الورديات
        </h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الكاشير
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  بداية الوردية
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  نهاية الوردية
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المبيعات
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  النقدية
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الفرق النقدي
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الحالة
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.shifts && report.shifts.length > 0 ? (
                report.shifts.map((shift) => (
                  <tr key={shift.shiftId} className="border-b hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-primary-100 rounded-full flex items-center justify-center">
                          <Clock className="w-5 h-5 text-primary-600" />
                        </div>
                        <span className="font-medium text-gray-800">
                          {shift.userName}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {formatDateTime(shift.openedAt)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {shift.closedAt
                        ? formatDateTime(shift.closedAt)
                        : "مفتوحة"}
                    </td>
                    <td className="px-4 py-3 font-semibold text-success-600">
                      {formatCurrency(shift.totalSales)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {formatCurrency(shift.closingBalance)}
                    </td>
                    <td className="px-4 py-3">
                      {shift.variance !== 0 ? (
                        <div className="flex items-center gap-1">
                          <AlertTriangle className="w-4 h-4 text-warning-500" />
                          <span
                            className={`font-medium ${shift.variance > 0 ? "text-green-600" : "text-red-600"}`}
                          >
                            {formatCurrency(shift.variance)}
                          </span>
                        </div>
                      ) : (
                        <span className="text-gray-400">0</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      {shift.isForceClosed ? (
                        <span className="px-2 py-1 rounded-full text-xs font-medium text-red-600 bg-red-100">
                          إغلاق إجباري
                        </span>
                      ) : shift.closedAt ? (
                        <span className="px-2 py-1 rounded-full text-xs font-medium text-green-600 bg-green-100">
                          مكتمل
                        </span>
                      ) : (
                        <span className="px-2 py-1 rounded-full text-xs font-medium text-blue-600 bg-blue-100">
                          مفتوح
                        </span>
                      )}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={7}
                    className="px-4 py-8 text-center text-gray-400"
                  >
                    لا توجد ورديات في هذه الفترة
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>

      {/* Info Card */}
      <Card className="bg-blue-50 border-blue-200">
        <div className="flex items-start gap-3">
          <Info className="w-5 h-5 text-blue-600 mt-0.5 flex-shrink-0" />
          <div className="flex-1">
            <h3 className="text-sm font-semibold text-blue-900 mb-2">
              معلومات التقرير
            </h3>
            <ul className="text-sm text-blue-800 space-y-1">
              <li>
                • <strong>الوردية:</strong> فترة عمل واحدة من الفتح إلى الإغلاق
              </li>
              <li>
                • <strong>الحالة:</strong> مكتملة (مغلقة) | مفتوحة (جارية) |
                ملغاة
              </li>
              <li>
                • <strong>المبيعات:</strong> إجمالي العمليات المكتملة في الوردية
              </li>
              <li>
                • <strong>الفرق:</strong> الفرق بين الخزينة المتوقعة والفعلية
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default ShiftDetailsReportPage;
