import { useState } from "react";
import {
  useGetTransfersQuery,
  useApproveTransferMutation,
  useReceiveTransferMutation,
  useCancelTransferMutation,
} from "../../api/inventoryApi";
import { useAppSelector } from "../../store/hooks";
import { selectIsAdmin } from "../../store/slices/authSlice";
import { selectBranches } from "../../store/slices/branchSlice";
import {
  ArrowRight,
  Check,
  X,
  Clock,
  Package,
  Filter,
  AlertTriangle,
  ChevronDown,
} from "lucide-react";
import { toast } from "sonner";
import type { TransferStatus } from "../../types/inventory.types";
import { formatDateTimeFull } from "../../utils/formatters";

export default function InventoryTransferList() {
  const isAdmin = useAppSelector(selectIsAdmin);
  const branches = useAppSelector(selectBranches);

  const [filters, setFilters] = useState({
    fromBranchId: undefined as number | undefined,
    toBranchId: undefined as number | undefined,
    status: undefined as TransferStatus | undefined,
  });

  const [pageNumber, setPageNumber] = useState(1);
  const [cancelReason, setCancelReason] = useState("");
  const [cancellingId, setCancellingId] = useState<number | null>(null);

  const { data: transfersData, isLoading } = useGetTransfersQuery({
    ...filters,
    pageNumber,
    pageSize: 20,
  });

  const [approveTransfer] = useApproveTransferMutation();
  const [receiveTransfer] = useReceiveTransferMutation();
  const [cancelTransfer] = useCancelTransferMutation();

  const handleApprove = async (id: number) => {
    if (!confirm("هل تريد الموافقة على طلب النقل؟")) return;

    try {
      await approveTransfer(id).unwrap();
      toast.success("تمت الموافقة على طلب النقل");
    } catch (error: any) {
      toast.error(error?.data?.message || "حدث خطأ في الموافقة");
    }
  };

  const handleReceive = async (id: number) => {
    if (!confirm("هل تم استلام المخزون؟ سيتم تحديث المخزون تلقائياً")) return;

    try {
      await receiveTransfer(id).unwrap();
      toast.success("تم استلام المخزون بنجاح");
    } catch (error: any) {
      toast.error(error?.data?.message || "حدث خطأ في الاستلام");
    }
  };

  const handleCancel = async (id: number) => {
    if (!cancelReason.trim()) {
      toast.error("الرجاء إدخال سبب الإلغاء");
      return;
    }

    try {
      await cancelTransfer({ id, request: { reason: cancelReason } }).unwrap();
      toast.success("تم إلغاء طلب النقل");
      setCancellingId(null);
      setCancelReason("");
    } catch (error: any) {
      toast.error(error?.data?.message || "حدث خطأ في الإلغاء");
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig = {
      Pending: {
        bg: "bg-yellow-100",
        text: "text-yellow-800",
        icon: Clock,
        label: "قيد الانتظار",
      },
      Approved: {
        bg: "bg-blue-100",
        text: "text-blue-800",
        icon: Check,
        label: "تمت الموافقة",
      },
      Received: {
        bg: "bg-green-100",
        text: "text-green-800",
        icon: Package,
        label: "تم الاستلام",
      },
      Cancelled: {
        bg: "bg-red-100",
        text: "text-red-800",
        icon: X,
        label: "ملغي",
      },
    };

    const config = statusConfig[status as keyof typeof statusConfig];
    if (!config) return null;

    const Icon = config.icon;

    return (
      <span
        className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium ${config.bg} ${config.text}`}
      >
        <Icon className="w-3 h-3" />
        {config.label}
      </span>
    );
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">طلبات نقل المخزون</h2>
          <p className="text-sm text-gray-600 mt-1">
            إدارة نقل المخزون بين الفروع
          </p>
        </div>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-lg border border-gray-200 p-4">
        <div className="flex items-center gap-2 mb-3">
          <Filter className="w-5 h-5 text-gray-400" />
          <h3 className="font-semibold text-gray-900">تصفية</h3>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              من فرع
            </label>
            <div className="relative">
              <select
                value={filters.fromBranchId || ""}
                onChange={(e) =>
                  setFilters({
                    ...filters,
                    fromBranchId: e.target.value ? Number(e.target.value) : undefined,
                  })
                }
                className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
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

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              إلى فرع
            </label>
            <div className="relative">
              <select
                value={filters.toBranchId || ""}
                onChange={(e) =>
                  setFilters({
                    ...filters,
                    toBranchId: e.target.value ? Number(e.target.value) : undefined,
                  })
                }
                className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
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

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              الحالة
            </label>
            <div className="relative">
              <select
                value={filters.status || ""}
                onChange={(e) =>
                  setFilters({
                    ...filters,
                    status: e.target.value ? (e.target.value as TransferStatus) : undefined,
                  })
                }
                className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
              >
                <option value="">جميع الحالات</option>
                <option value="Pending">قيد الانتظار</option>
                <option value="Approved">تمت الموافقة</option>
                <option value="Received">تم الاستلام</option>
                <option value="Cancelled">ملغي</option>
              </select>
              <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
            </div>
          </div>
        </div>
      </div>

      {/* Transfers List */}
      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        {transfersData && transfersData.items.length > 0 ? (
          <div className="divide-y divide-gray-200">
            {transfersData.items.map((transfer) => (
              <div key={transfer.id} className="p-6 hover:bg-gray-50">
                <div className="flex items-start justify-between mb-4">
                  <div className="flex-1">
                    <div className="flex items-center gap-3 mb-2">
                      <h3 className="text-lg font-semibold text-gray-900">
                        {transfer.transferNumber}
                      </h3>
                      {getStatusBadge(transfer.status)}
                    </div>

                    {/* Transfer Direction */}
                    <div className="flex items-center gap-3 mb-3">
                      <div className="flex items-center gap-2 text-sm">
                        <Package className="w-4 h-4 text-gray-400" />
                        <span className="font-medium text-gray-900">
                          {transfer.fromBranchName}
                        </span>
                      </div>
                      <ArrowRight className="w-5 h-5 text-gray-400" />
                      <div className="flex items-center gap-2 text-sm">
                        <Package className="w-4 h-4 text-gray-400" />
                        <span className="font-medium text-gray-900">
                          {transfer.toBranchName}
                        </span>
                      </div>
                    </div>

                    {/* Product Info */}
                    <div className="bg-gray-50 rounded-lg p-3 mb-3">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="font-medium text-gray-900">
                            {transfer.productName}
                          </p>
                          {transfer.productSku && (
                            <p className="text-xs text-gray-500">
                              كود: {transfer.productSku}
                            </p>
                          )}
                        </div>
                        <div className="text-left">
                          <p className="text-sm text-gray-600">الكمية</p>
                          <p className="text-lg font-bold text-blue-600">
                            {transfer.quantity}
                          </p>
                        </div>
                      </div>
                    </div>

                    {/* Reason */}
                    <div className="mb-3">
                      <p className="text-sm text-gray-600">
                        <span className="font-medium">السبب:</span> {transfer.reason}
                      </p>
                      {transfer.notes && (
                        <p className="text-sm text-gray-600 mt-1">
                          <span className="font-medium">ملاحظات:</span> {transfer.notes}
                        </p>
                      )}
                    </div>

                    {/* Timeline */}
                    <div className="text-xs text-gray-500 space-y-1">
                      <p>
                        أنشئ بواسطة {transfer.createdByUserName} في{" "}
                        {formatDateTimeFull(transfer.createdAt)}
                      </p>
                      {transfer.approvedByUserName && (
                        <p>
                          وافق عليه {transfer.approvedByUserName} في{" "}
                          {formatDateTimeFull(transfer.approvedAt!)}
                        </p>
                      )}
                      {transfer.receivedByUserName && (
                        <p>
                          استلمه {transfer.receivedByUserName} في{" "}
                          {formatDateTimeFull(transfer.receivedAt!)}
                        </p>
                      )}
                      {transfer.cancelledByUserName && (
                        <p className="text-red-600">
                          ألغاه {transfer.cancelledByUserName} في{" "}
                          {formatDateTimeFull(transfer.cancelledAt!)}
                          {transfer.cancellationReason && ` - ${transfer.cancellationReason}`}
                        </p>
                      )}
                    </div>
                  </div>

                  {/* Actions */}
                  {isAdmin && (
                    <div className="flex flex-col gap-2 mr-4">
                      {transfer.status === "Pending" && (
                        <>
                          <button
                            onClick={() => handleApprove(transfer.id)}
                            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 text-sm"
                          >
                            <Check className="w-4 h-4" />
                            موافقة
                          </button>
                          <button
                            onClick={() => setCancellingId(transfer.id)}
                            className="flex items-center gap-2 px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 text-sm"
                          >
                            <X className="w-4 h-4" />
                            إلغاء
                          </button>
                        </>
                      )}

                      {transfer.status === "Approved" && (
                        <>
                          <button
                            onClick={() => handleReceive(transfer.id)}
                            className="flex items-center gap-2 px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 text-sm"
                          >
                            <Package className="w-4 h-4" />
                            استلام
                          </button>
                          <button
                            onClick={() => setCancellingId(transfer.id)}
                            className="flex items-center gap-2 px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 text-sm"
                          >
                            <X className="w-4 h-4" />
                            إلغاء
                          </button>
                        </>
                      )}
                    </div>
                  )}
                </div>

                {/* Cancel Form */}
                {cancellingId === transfer.id && (
                  <div className="mt-4 p-4 bg-red-50 border border-red-200 rounded-lg">
                    <label className="block text-sm font-medium text-gray-700 mb-2">
                      سبب الإلغاء
                    </label>
                    <input
                      type="text"
                      value={cancelReason}
                      onChange={(e) => setCancelReason(e.target.value)}
                      placeholder="أدخل سبب الإلغاء..."
                      className="w-full px-4 py-2 border border-gray-300 rounded-lg mb-3"
                    />
                    <div className="flex gap-2">
                      <button
                        onClick={() => handleCancel(transfer.id)}
                        className="px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700"
                      >
                        تأكيد الإلغاء
                      </button>
                      <button
                        onClick={() => {
                          setCancellingId(null);
                          setCancelReason("");
                        }}
                        className="px-4 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300"
                      >
                        إلغاء
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        ) : (
          <div className="p-12 text-center">
            <Package className="w-16 h-16 text-gray-400 mx-auto mb-4" />
            <h3 className="text-lg font-semibold text-gray-900 mb-2">
              لا توجد طلبات نقل
            </h3>
            <p className="text-gray-600">لم يتم إنشاء أي طلبات نقل بعد</p>
          </div>
        )}

        {/* Pagination */}
        {transfersData && transfersData.totalPages > 1 && (
          <div className="px-6 py-4 bg-gray-50 border-t border-gray-200">
            <div className="flex items-center justify-between">
              <p className="text-sm text-gray-600">
                صفحة {transfersData.pageNumber} من {transfersData.totalPages}
              </p>
              <div className="flex gap-2">
                <button
                  onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                  disabled={pageNumber === 1}
                  className="px-4 py-2 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  السابق
                </button>
                <button
                  onClick={() =>
                    setPageNumber((p) => Math.min(transfersData.totalPages, p + 1))
                  }
                  disabled={pageNumber === transfersData.totalPages}
                  className="px-4 py-2 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  التالي
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
