import { useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  ChevronLeft,
  ChevronRight,
  Package,
  PauseCircle,
  Pencil,
  PlayCircle,
  Plus,
  Search,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";
import {
  useDeleteProductBatchMutation,
  useGetProductBatchesQuery,
  useHoldBatchMutation,
  useReleaseBatchMutation,
} from "@/api/productBatchApi";
import { useGetBranchesQuery } from "@/api/branchesApi";
import { Button, ConfirmDialog } from "@/components/common";
import { Card } from "@/components/common/Card";
import { Input } from "@/components/common/Input";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { ProductBatchFormModal } from "@/components/inventory/ProductBatchFormModal";
import { usePermission } from "@/hooks/usePermission";
import { formatCurrency } from "@/utils/formatters";
import type { ApiResponse } from "@/types/api.types";
import type { BatchStatus, ProductBatch } from "@/types/productBatch.types";

const statusOptions: { value: BatchStatus | ""; label: string; color: string }[] =
  [
    { value: "", label: "الكل", color: "bg-gray-100 text-gray-700" },
    { value: "Active", label: "نشط", color: "bg-emerald-50 text-emerald-700" },
    { value: "OnHold", label: "معلق", color: "bg-amber-50 text-amber-700" },
    { value: "Expired", label: "منتهي", color: "bg-red-50 text-red-700" },
    { value: "Depleted", label: "نفد", color: "bg-slate-100 text-slate-600" },
  ];

type BatchActionType = "hold" | "release";

interface BatchActionState {
  id: number;
  type: BatchActionType;
  batchNumber: string;
}

export const ProductBatchesPage = () => {
  const [searchParams] = useSearchParams();
  const urlStatus = searchParams.get("status");
  const urlProductId = searchParams.get("productId");
  const isNearExpiry = urlStatus === "NearExpiry";

  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<BatchStatus | "">(
    isNearExpiry ? "" : (urlStatus as BatchStatus) || "",
  );
  const [branchFilter, setBranchFilter] = useState<number | "">("");
  const [showNearExpiryOnly, setShowNearExpiryOnly] = useState(isNearExpiry);
  const [page, setPage] = useState(1);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [editingBatch, setEditingBatch] = useState<ProductBatch | null>(null);
  const [actionState, setActionState] = useState<BatchActionState | null>(null);
  const [actionReason, setActionReason] = useState("");

  const pageSize = 20;
  const { hasPermission } = usePermission();
  const canManage = hasPermission("InventoryManage");

  const { data: branchesData } = useGetBranchesQuery();
  const branches = branchesData?.data || [];

  const filters = useMemo(() => {
    const nextFilters: Record<string, unknown> = {
      page,
      pageSize,
    };

    if (branchFilter) nextFilters.branchId = branchFilter;
    if (statusFilter) nextFilters.status = statusFilter;
    if (urlProductId) nextFilters.productId = Number(urlProductId);

    return nextFilters;
  }, [branchFilter, page, pageSize, statusFilter, urlProductId]);

  const { data: batchesData, isLoading } = useGetProductBatchesQuery(filters);
  const [deleteBatch] = useDeleteProductBatchMutation();
  const [holdBatch, { isLoading: isHolding }] = useHoldBatchMutation();
  const [releaseBatch, { isLoading: isReleasing }] = useReleaseBatchMutation();

  const allItems = batchesData?.data?.items || [];
  const totalCount = batchesData?.data?.totalCount || 0;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  const filteredItems = useMemo(() => {
    let items = allItems;

    if (searchQuery.trim()) {
      const query = searchQuery.trim().toLowerCase();
      items = items.filter(
        (batch) =>
          batch.batchNumber.toLowerCase().includes(query) ||
          batch.productName.toLowerCase().includes(query),
      );
    }

    if (showNearExpiryOnly) {
      items = items.filter(
        (batch) => batch.daysUntilExpiry >= -1 && batch.daysUntilExpiry <= 30,
      );
    }

    return items;
  }, [allItems, searchQuery, showNearExpiryOnly]);

  const handleDelete = async (id: number) => {
    try {
      await deleteBatch(id).unwrap();
      toast.success("تم حذف الدفعة بنجاح");
      setDeletingId(null);
    } catch (error) {
      const apiError = error as { data?: ApiResponse<unknown> };
      toast.error(apiError.data?.message || "فشل حذف الدفعة");
    }
  };

  const handleBatchAction = async () => {
    if (!actionState) return;
    if (!actionReason.trim()) {
      toast.error("الرجاء إدخال سبب الإجراء");
      return;
    }

    try {
      if (actionState.type === "hold") {
        await holdBatch({
          id: actionState.id,
          data: { reason: actionReason.trim() },
        }).unwrap();
        toast.success("تم تعليق الدفعة بنجاح");
      } else {
        await releaseBatch({
          id: actionState.id,
          data: { reason: actionReason.trim() },
        }).unwrap();
        toast.success("تم تفعيل الدفعة بنجاح");
      }

      setActionState(null);
      setActionReason("");
    } catch (error) {
      const apiError = error as { data?: ApiResponse<unknown> };
      toast.error(
        apiError.data?.message ||
          (actionState.type === "hold"
            ? "تعذر تعليق الدفعة"
            : "تعذر تفعيل الدفعة"),
      );
    }
  };

  const statusBadge = (status: BatchStatus) => {
    const option =
      statusOptions.find((statusOption) => statusOption.value === status) ||
      statusOptions[0];

    return (
      <span
        className={`inline-flex items-center gap-1 rounded-full border border-current px-2.5 py-0.5 text-xs font-medium ${option.color}`}
      >
        {status === "Expired" && <AlertTriangle className="h-3 w-3" />}
        {option.label}
      </span>
    );
  };

  const daysLabel = (batch: ProductBatch) => {
    if (!batch.expiryDate) return <span className="text-gray-400">-</span>;

    const days = batch.daysUntilExpiry;
    if (days < 0) {
      return <span className="font-medium text-red-600">منتهي ({days})</span>;
    }
    if (days <= 7) {
      return <span className="font-medium text-amber-600">{days} يوم</span>;
    }
    if (days <= 30) {
      return <span className="text-amber-500">{days} يوم</span>;
    }

    return <span className="text-gray-600">{days} يوم</span>;
  };

  return (
    <div className="space-y-4 p-4">
      <div className="flex items-center justify-between">
        <h1 className="flex items-center gap-2 text-2xl font-bold text-gray-900">
          <Package className="h-6 w-6 text-primary-600" />
          دفعات المخزون
        </h1>

        {canManage && (
          <Button
            variant="primary"
            onClick={() => setIsCreateModalOpen(true)}
            leftIcon={<Plus className="h-4 w-4" />}
          >
            إضافة دفعة
          </Button>
        )}
      </div>

      <Card className="p-4">
        <div className="flex flex-wrap items-end gap-3">
          <div className="min-w-[200px] flex-1">
            <label className="mb-1 block text-xs font-medium text-gray-500">
              بحث
            </label>
            <div className="relative">
              <Search className="absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
              <Input
                value={searchQuery}
                onChange={(event) => {
                  setSearchQuery(event.target.value);
                  setPage(1);
                }}
                placeholder="اسم المنتج أو رقم الدفعة..."
                className="ps-9"
              />
            </div>
          </div>

          <div className="w-40">
            <label className="mb-1 block text-xs font-medium text-gray-500">
              الحالة
            </label>
            <select
              value={statusFilter}
              onChange={(event) => {
                setStatusFilter(event.target.value as BatchStatus | "");
                setPage(1);
              }}
              className="w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            >
              {statusOptions.map((statusOption) => (
                <option key={statusOption.value} value={statusOption.value}>
                  {statusOption.label}
                </option>
              ))}
            </select>
          </div>

          <div className="w-48">
            <label className="mb-1 block text-xs font-medium text-gray-500">
              الفرع
            </label>
            <select
              value={branchFilter}
              onChange={(event) => {
                setBranchFilter(event.target.value ? Number(event.target.value) : "");
                setPage(1);
              }}
              className="w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            >
              <option value="">كل الفروع</option>
              {branches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </select>
          </div>

          <label className="inline-flex cursor-pointer select-none items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              className="rounded border-gray-300 text-primary-600 focus:ring-primary-500"
              checked={showNearExpiryOnly}
              onChange={(event) => {
                setShowNearExpiryOnly(event.target.checked);
                setPage(1);
              }}
            />
            قريب الانتهاء فقط
          </label>
        </div>
      </Card>

      <Card className="overflow-hidden">
        {isLoading ? (
          <div className="flex justify-center p-8">
            <Loading />
          </div>
        ) : filteredItems.length === 0 ? (
          <div className="p-8 text-center text-gray-500">
            لا توجد دفعات مطابقة للفلاتر
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-gray-600">
                <tr>
                  <th className="px-4 py-3 text-start font-medium">
                    رقم الدفعة
                  </th>
                  <th className="px-4 py-3 text-start font-medium">المنتج</th>
                  <th className="px-4 py-3 text-start font-medium">الكمية</th>
                  <th className="px-4 py-3 text-start font-medium">التكلفة</th>
                  <th className="px-4 py-3 text-start font-medium">سعر البيع</th>
                  <th className="px-4 py-3 text-start font-medium">
                    تاريخ الصلاحية
                  </th>
                  <th className="px-4 py-3 text-start font-medium">
                    الأيام المتبقية
                  </th>
                  <th className="px-4 py-3 text-start font-medium">الحالة</th>
                  <th className="px-4 py-3 text-start font-medium">الفرع</th>
                  {canManage && (
                    <th className="px-4 py-3 text-center font-medium">
                      الإجراءات
                    </th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {filteredItems.map((batch) => (
                  <tr key={batch.id} className="transition-colors hover:bg-gray-50">
                    <td className="px-4 py-3 font-medium text-gray-900">
                      {batch.batchNumber}
                    </td>
                    <td className="px-4 py-3 text-gray-700">{batch.productName}</td>
                    <td className="px-4 py-3 text-gray-700">
                      {batch.quantity} / {batch.initialQuantity}
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {batch.costPrice ? formatCurrency(batch.costPrice) : "-"}
                    </td>
                    <td className="px-4 py-3 font-medium text-emerald-700">
                      {batch.sellingPrice ? formatCurrency(batch.sellingPrice) : "-"}
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {batch.expiryDate
                        ? new Date(batch.expiryDate).toLocaleDateString("ar-EG")
                        : "غير محدد"}
                    </td>
                    <td className="px-4 py-3">{daysLabel(batch)}</td>
                    <td className="px-4 py-3">{statusBadge(batch.status)}</td>
                    <td className="px-4 py-3 text-xs text-gray-500">
                      {batch.branchName || "-"}
                    </td>
                    {canManage && (
                      <td className="px-4 py-3">
                        <div className="flex items-center justify-center gap-2">
                          <Button
                            variant="ghost"
                            size="sm"
                            className="text-primary-700 hover:bg-primary-50 hover:text-primary-800"
                            onClick={() => setEditingBatch(batch)}
                          >
                            <Pencil className="h-4 w-4" />
                            تعديل
                          </Button>

                          {batch.status === "Active" && (
                            <Button
                              variant="ghost"
                              size="sm"
                              className="text-amber-700 hover:bg-amber-50 hover:text-amber-800"
                              onClick={() =>
                                setActionState({
                                  id: batch.id,
                                  type: "hold",
                                  batchNumber: batch.batchNumber,
                                })
                              }
                            >
                              <PauseCircle className="h-4 w-4" />
                              تعليق
                            </Button>
                          )}

                          {batch.status === "OnHold" && (
                            <Button
                              variant="ghost"
                              size="sm"
                              className="text-emerald-700 hover:bg-emerald-50 hover:text-emerald-800"
                              onClick={() =>
                                setActionState({
                                  id: batch.id,
                                  type: "release",
                                  batchNumber: batch.batchNumber,
                                })
                              }
                            >
                              <PlayCircle className="h-4 w-4" />
                              تفعيل
                            </Button>
                          )}

                          <Button
                            variant="ghost"
                            size="sm"
                            className="text-red-600 hover:bg-red-50 hover:text-red-700"
                            onClick={() => setDeletingId(batch.id)}
                          >
                            <Trash2 className="h-4 w-4" />
                            حذف
                          </Button>
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {totalPages > 1 && (
          <div className="flex items-center justify-between border-t border-gray-100 px-4 py-3">
            <span className="text-xs text-gray-500">{totalCount} نتيجة</span>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage((prev) => Math.max(1, prev - 1))}
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
              <span className="text-sm text-gray-600">
                صفحة {page} من {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= totalPages}
                onClick={() => setPage((prev) => Math.min(totalPages, prev + 1))}
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}
      </Card>

      <ProductBatchFormModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        onSuccess={() => setIsCreateModalOpen(false)}
        productId={urlProductId ? Number(urlProductId) : undefined}
      />

      <ProductBatchFormModal
        isOpen={editingBatch !== null}
        onClose={() => setEditingBatch(null)}
        onSuccess={() => setEditingBatch(null)}
        batch={editingBatch}
      />

      <Modal
        isOpen={actionState !== null}
        onClose={() => {
          setActionState(null);
          setActionReason("");
        }}
        title={actionState?.type === "hold" ? "تعليق الدفعة" : "تفعيل الدفعة"}
        size="md"
      >
        <div className="space-y-4">
          <p className="text-sm text-gray-600">
            {actionState?.type === "hold"
              ? `أدخل سبب تعليق الدفعة ${actionState?.batchNumber}.`
              : `أدخل سبب إعادة تفعيل الدفعة ${actionState?.batchNumber}.`}
          </p>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700">
              السبب
            </label>
            <textarea
              value={actionReason}
              onChange={(event) => setActionReason(event.target.value)}
              rows={3}
              className="w-full rounded-lg border border-gray-300 px-4 py-2.5 focus:border-transparent focus:ring-2 focus:ring-primary-500"
              placeholder="اكتب سبب الإجراء"
            />
          </div>

          <div className="flex gap-3 pt-2">
            <Button
              type="button"
              variant="secondary"
              className="flex-1"
              onClick={() => {
                setActionState(null);
                setActionReason("");
              }}
            >
              إلغاء
            </Button>
            <Button
              type="button"
              variant="primary"
              className="flex-1"
              isLoading={isHolding || isReleasing}
              onClick={handleBatchAction}
            >
              {actionState?.type === "hold" ? "تعليق" : "تفعيل"}
            </Button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        open={deletingId !== null}
        onOpenChange={(open) => {
          if (!open) setDeletingId(null);
        }}
        title="حذف الدفعة"
        description="هل أنت متأكد من حذف هذه الدفعة؟ لا يمكن التراجع عن هذا الإجراء."
        confirmText="حذف"
        cancelText="إلغاء"
        variant="danger"
        onConfirm={() => deletingId && handleDelete(deletingId)}
      />
    </div>
  );
};

export default ProductBatchesPage;
