import { useState, useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import {
  Search,
  AlertTriangle,
  Package,
  Trash2,
  Filter,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import {
  useGetProductBatchesQuery,
  useDeleteProductBatchMutation,
} from "@/api/productBatchApi";
import { useGetBranchesQuery } from "@/api/branchesApi";
import { Button, ConfirmDialog } from "@/components/common";
import { Input } from "@/components/common/Input";
import { Card } from "@/components/common/Card";
import { Loading } from "@/components/common/Loading";
import { usePermission } from "@/hooks/usePermission";
import { useAppSelector } from "@/store/hooks";
import { formatCurrency } from "@/utils/formatters";
import { toast } from "sonner";
import type { ProductBatch, BatchStatus } from "@/types/productBatch.types";

const statusOptions: { value: BatchStatus | ""; label: string; color: string }[] = [
  { value: "", label: "الكل", color: "bg-gray-100 text-gray-700" },
  { value: "Active", label: "نشط", color: "bg-emerald-50 text-emerald-700" },
  { value: "Expired", label: "منتهي", color: "bg-red-50 text-red-700" },
  { value: "Depleted", label: "نفد", color: "bg-slate-100 text-slate-600" },
];

export const ProductBatchesPage = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const urlStatus = searchParams.get("status");
  const urlProductId = searchParams.get("productId");
  const isNearExpiry = urlStatus === "NearExpiry";

  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<BatchStatus | "">(
    isNearExpiry ? "" : (urlStatus as BatchStatus) || ""
  );
  const [branchFilter, setBranchFilter] = useState<number | "">("");
  const [showNearExpiryOnly, setShowNearExpiryOnly] = useState(isNearExpiry);
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [deletingId, setDeletingId] = useState<number | null>(null);

  const { hasPermission } = usePermission();
  const canManage = hasPermission("InventoryManage");

  const { data: branchesData } = useGetBranchesQuery();
  const branches = branchesData?.data || [];

  const filters = useMemo(() => {
    const f: Record<string, unknown> = {
      page,
      pageSize,
    };
    if (branchFilter) f.branchId = branchFilter;
    if (statusFilter) f.status = statusFilter;
    if (urlProductId) f.productId = Number(urlProductId);
    return f;
  }, [page, pageSize, branchFilter, statusFilter, urlProductId]);

  const { data: batchesData, isLoading } = useGetProductBatchesQuery(filters);
  const [deleteBatch] = useDeleteProductBatchMutation();

  const allItems = batchesData?.data?.items || [];
  const totalCount = batchesData?.data?.totalCount || 0;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  const filteredItems = useMemo(() => {
    let items = allItems;
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      items = items.filter(
        (b) =>
          b.batchNumber.toLowerCase().includes(q) ||
          b.productName.toLowerCase().includes(q)
      );
    }
    if (showNearExpiryOnly) {
      items = items.filter((b) => b.daysUntilExpiry >= -1 && b.daysUntilExpiry <= 30);
    }
    return items;
  }, [allItems, searchQuery, showNearExpiryOnly]);

  const handleDelete = async (id: number) => {
    try {
      await deleteBatch(id).unwrap();
      toast.success("تم حذف الباتش بنجاح");
      setDeletingId(null);
    } catch (err) {
      const error = err as { data?: { message?: string } };
      toast.error(error.data?.message || "فشل حذف الباتش");
    }
  };

  const statusBadge = (status: BatchStatus) => {
    const opt = statusOptions.find((s) => s.value === status) || statusOptions[0];
    return (
      <span className={`inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs font-medium ${opt.color} border-current`}>
        {status === "Expired" && <AlertTriangle className="w-3 h-3" />}
        {opt.label}
      </span>
    );
  };

  const daysLabel = (batch: ProductBatch) => {
    if (!batch.expiryDate) return <span className="text-gray-400">—</span>;
    const days = batch.daysUntilExpiry;
    if (days < 0) return <span className="text-red-600 font-medium">منتهي ({days})</span>;
    if (days <= 7) return <span className="text-amber-600 font-medium">{days} يوم</span>;
    if (days <= 30) return <span className="text-amber-500">{days} يوم</span>;
    return <span className="text-gray-600">{days} يوم</span>;
  };

  return (
    <div className="space-y-4 p-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
          <Package className="w-6 h-6 text-primary-600" />
          دفعات المخزون
        </h1>
      </div>

      {/* Filters */}
      <Card className="p-4">
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex-1 min-w-[200px]">
            <label className="block text-xs font-medium text-gray-500 mb-1">
              بحث
            </label>
            <div className="relative">
              <Search className="absolute start-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
              <Input
                value={searchQuery}
                onChange={(e) => {
                  setSearchQuery(e.target.value);
                  setPage(1);
                }}
                placeholder="اسم المنتج أو رقم الباتش..."
                className="ps-9"
              />
            </div>
          </div>

          <div className="w-40">
            <label className="block text-xs font-medium text-gray-500 mb-1">
              الحالة
            </label>
            <select
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value as BatchStatus | "");
                setPage(1);
              }}
              className="w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            >
              {statusOptions.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
          </div>

          <div className="w-48">
            <label className="block text-xs font-medium text-gray-500 mb-1">
              الفرع
            </label>
            <select
              value={branchFilter}
              onChange={(e) => {
                setBranchFilter(e.target.value ? Number(e.target.value) : "");
                setPage(1);
              }}
              className="w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            >
              <option value="">كل الفروع</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </div>

          <label className="inline-flex items-center gap-2 text-sm text-gray-700 select-none cursor-pointer">
            <input
              type="checkbox"
              className="rounded border-gray-300 text-primary-600 focus:ring-primary-500"
              checked={showNearExpiryOnly}
              onChange={(e) => {
                setShowNearExpiryOnly(e.target.checked);
                setPage(1);
              }}
            />
            قريب الانتهاء فقط
          </label>
        </div>
      </Card>

      {/* Table */}
      <Card className="overflow-hidden">
        {isLoading ? (
          <div className="p-8 flex justify-center">
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
                  <th className="px-4 py-3 text-start font-medium">رقم الباتش</th>
                  <th className="px-4 py-3 text-start font-medium">المنتج</th>
                  <th className="px-4 py-3 text-start font-medium">الكمية</th>
                  <th className="px-4 py-3 text-start font-medium">التكلفة</th>
                  <th className="px-4 py-3 text-start font-medium">تاريخ الصلاحية</th>
                  <th className="px-4 py-3 text-start font-medium">الأيام المتبقية</th>
                  <th className="px-4 py-3 text-start font-medium">الحالة</th>
                  <th className="px-4 py-3 text-start font-medium">الفرع</th>
                  {canManage && <th className="px-4 py-3 text-center font-medium">إجراء</th>}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {filteredItems.map((batch) => (
                  <tr key={batch.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-3 font-medium text-gray-900">
                      {batch.batchNumber}
                    </td>
                    <td className="px-4 py-3 text-gray-700">{batch.productName}</td>
                    <td className="px-4 py-3 text-gray-700">
                      {batch.quantity} / {batch.initialQuantity}
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {batch.costPrice ? formatCurrency(batch.costPrice) : "—"}
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {batch.expiryDate
                        ? new Date(batch.expiryDate).toLocaleDateString("ar-EG")
                        : "غير محدد"}
                    </td>
                    <td className="px-4 py-3">{daysLabel(batch)}</td>
                    <td className="px-4 py-3">{statusBadge(batch.status)}</td>
                    <td className="px-4 py-3 text-gray-500 text-xs">
                      {batch.branchName || "—"}
                    </td>
                    {canManage && (
                      <td className="px-4 py-3 text-center">
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-red-600 hover:text-red-700 hover:bg-red-50"
                          onClick={() => setDeletingId(batch.id)}
                        >
                          <Trash2 className="w-4 h-4" />
                        </Button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100">
            <span className="text-xs text-gray-500">
              {totalCount} نتيجة
            </span>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                <ChevronRight className="w-4 h-4" />
              </Button>
              <span className="text-sm text-gray-600">
                صفحة {page} من {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              >
                <ChevronLeft className="w-4 h-4" />
              </Button>
            </div>
          </div>
        )}
      </Card>

      {/* Delete Confirmation */}
      <ConfirmDialog
        open={deletingId !== null}
        onOpenChange={(open) => {
          if (!open) setDeletingId(null);
        }}
        title="حذف الباتش"
        description="هل أنت متأكد من حذف هذا الباتش؟ لا يمكن التراجع عن هذا الإجراء."
        confirmText="حذف"
        cancelText="إلغاء"
        variant="danger"
        onConfirm={() => deletingId && handleDelete(deletingId)}
      />
    </div>
  );
};

export default ProductBatchesPage;
