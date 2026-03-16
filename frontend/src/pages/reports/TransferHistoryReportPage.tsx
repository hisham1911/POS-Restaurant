import { useState } from "react";
import {
  ArrowRightLeft,
  TrendingUp,
  Calendar,
  Loader2,
  AlertCircle,
  Package,
  Building2,
  CheckCircle,
  Clock,
  XCircle,
  ChevronDown,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDateTimeFull } from "@/utils/formatters";
import { useGetTransferHistoryReportQuery } from "@/api/inventoryReportsApi";
import { useGetBranchesQuery } from "@/api/branchesApi";

export const TransferHistoryReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(new Date().getDate() - 30))
      .toISOString()
      .split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);
  const [selectedBranchId, setSelectedBranchId] = useState<
    number | undefined
  >();

  const { data: branchesData } = useGetBranchesQuery();
  const branches = branchesData?.data || [];

  const { data, isLoading, isError, error } = useGetTransferHistoryReportQuery({
    fromDate,
    toDate,
    branchId: selectedBranchId,
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

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "Completed":
        return (
          <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-success-100 text-success-800">
            <CheckCircle className="w-3 h-3 ml-1" />
            مكتمل
          </span>
        );
      case "Pending":
      case "Approved":
        return (
          <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-warning-100 text-warning-800">
            <Clock className="w-3 h-3 ml-1" />
            قيد الانتظار
          </span>
        );
      case "Cancelled":
        return (
          <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-red-100 text-red-800">
            <XCircle className="w-3 h-3 ml-1" />
            ملغي
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
            {status}
          </span>
        );
    }
  };

  return (
    <div className="h-full overflow-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">
            تقرير تاريخ التحويلات
          </h1>
          <p className="text-gray-500 mt-1">سجل تحويلات المخزون بين الفروع</p>
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

      {/* Filters */}
      <Card>
        <div className="flex items-center gap-4">
          <span className="text-sm font-medium text-gray-700">الفرع:</span>
          <div className="relative">
            <select
              value={selectedBranchId || ""}
              onChange={(e) =>
                setSelectedBranchId(
                  e.target.value ? Number(e.target.value) : undefined,
                )
              }
              className="appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm min-w-[180px]"
            >
              <option value="">جميع الفروع</option>
              {branches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </select>
            <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
          </div>
        </div>
      </Card>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card className="bg-gradient-to-br from-primary-50 to-primary-100 border-primary-200">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-primary-500 rounded-xl flex items-center justify-center">
              <ArrowRightLeft className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-primary-700 font-medium">
                إجمالي التحويلات
              </p>
              <p className="text-2xl font-bold text-primary-600">
                {report?.totalTransfers || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-success-100 rounded-xl flex items-center justify-center">
              <CheckCircle className="w-6 h-6 text-success-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">مكتملة</p>
              <p className="text-2xl font-bold text-success-600">
                {report?.completedTransfers || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-warning-100 rounded-xl flex items-center justify-center">
              <Clock className="w-6 h-6 text-warning-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">قيد الانتظار</p>
              <p className="text-2xl font-bold text-warning-600">
                {report?.pendingTransfers || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <Package className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">الكمية المحولة</p>
              <p className="text-2xl font-bold text-blue-600">
                {report?.totalQuantityTransferred || 0}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Branch Statistics */}
      {report?.branchStats && report.branchStats.length > 0 && (
        <Card>
          <h3 className="text-lg font-bold text-gray-800 mb-4">
            إحصائيات الفروع
          </h3>
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b">
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    الفرع
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    تحويلات مرسلة
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    تحويلات مستلمة
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    كمية مرسلة
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    كمية مستلمة
                  </th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600">
                    صافي التغيير
                  </th>
                </tr>
              </thead>
              <tbody>
                {report.branchStats.map((stat) => (
                  <tr key={stat.branchId} className="border-b hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <Building2 className="w-4 h-4 text-gray-400" />
                        <span className="font-medium text-gray-800">
                          {stat.branchName}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {stat.transfersSent}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {stat.transfersReceived}
                    </td>
                    <td className="px-4 py-3 text-red-600">
                      {stat.quantitySent}
                    </td>
                    <td className="px-4 py-3 text-green-600">
                      {stat.quantityReceived}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={`font-semibold ${
                          stat.netChange >= 0
                            ? "text-green-600"
                            : "text-red-600"
                        }`}
                      >
                        {stat.netChange >= 0 ? "+" : ""}
                        {stat.netChange}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {/* Transfers Table */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">سجل التحويلات</h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  رقم التحويل
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  التاريخ
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  من فرع
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  إلى فرع
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المنتج
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الكمية
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الحالة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  السبب
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.transfers && report.transfers.length > 0 ? (
                report.transfers.map((transfer) => (
                  <tr key={transfer.id} className="border-b hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <span className="font-medium text-primary-600">
                        {transfer.transferNumber}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {formatDateTimeFull(transfer.createdAt)}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <Building2 className="w-4 h-4 text-red-400" />
                        <span className="text-gray-700">
                          {transfer.fromBranchName}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <Building2 className="w-4 h-4 text-green-400" />
                        <span className="text-gray-700">
                          {transfer.toBranchName}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <Package className="w-4 h-4 text-gray-400" />
                        <span className="text-gray-700">
                          {transfer.productName}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <span className="font-semibold text-gray-800">
                        {transfer.quantity}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      {getStatusBadge(transfer.status)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {transfer.reason || "-"}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={8}
                    className="px-4 py-8 text-center text-gray-400"
                  >
                    لا توجد تحويلات في هذه الفترة
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
                • <strong>التحويل:</strong> نقل مخزون منتج من فرع لآخر
              </li>
              <li>
                • <strong>الحالات:</strong> معلق (بانتظار الاستلام) | مكتمل (تم
                الاستلام) | مرفوض
              </li>
              <li>
                • <strong>المخزون:</strong> يُخصم فوراً من المرسل ويُضاف عند
                القبول
              </li>
              <li>
                • <strong>التصفية:</strong> يمكنك البحث حسب الفرع المرسل أو
                المستقبل
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default TransferHistoryReportPage;
