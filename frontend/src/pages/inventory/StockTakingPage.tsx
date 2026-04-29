import { useState, useCallback } from "react";
import {
  ClipboardList,
  Plus,
  Search,
  Trash2,
  CheckCircle,
  XCircle,
  Package,
  Save,
  Loader2,
  ArrowRight,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import { useGetStockTakingsQuery, useGetStockTakingByIdQuery, useCreateStockTakingMutation, useCancelStockTakingMutation, useCompleteStockTakingMutation, useUpsertStockTakingItemMutation, useRemoveStockTakingItemMutation } from "@/api/stockTakingApi";
import { useGetBranchInventoryQuery } from "@/api/inventoryApi";
import { useAppSelector } from "../../store/hooks";
import { selectCurrentBranch } from "../../store/slices/branchSlice";
import { Button, Card, Loading } from "@/components/common";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import { usePermission } from "@/hooks/usePermission";
import { formatDateTime } from "@/utils/formatters";
import { toast } from "sonner";
import type { StockTaking, StockTakingItem } from "@/types/stockTaking.types";
import clsx from "clsx";

export const StockTakingPage = () => {
  const { hasPermission } = usePermission();
  const canManage = hasPermission("InventoryManage");
  const currentBranch = useAppSelector(selectCurrentBranch);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [selectedStockTaking, setSelectedStockTaking] = useState<StockTaking | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [notes, setNotes] = useState("");

  const { data, isLoading } = useGetStockTakingsQuery({ page, pageSize: 10 });
  const [createStockTaking, { isLoading: isCreating }] = useCreateStockTakingMutation();
  const [cancelStockTaking, { isLoading: isCancelling }] = useCancelStockTakingMutation();
  const [completeStockTaking, { isLoading: isCompleting }] = useCompleteStockTakingMutation();

  const handleCreate = async () => {
    try {
      const result = await createStockTaking({ notes }).unwrap();
      if (result.success && result.data) {
        toast.success("تم إنشاء الجرد بنجاح");
        setSelectedStockTaking(result.data);
        setIsCreateModalOpen(false);
        setNotes("");
      } else {
        toast.error(result.message || "حدث خطأ");
      }
    } catch {
      toast.error("حدث خطأ أثناء إنشاء الجرد");
    }
  };

  const handleCancel = async (id: number) => {
    if (!confirm("هل أنت متأكد من إلغاء الجرد؟")) return;
    try {
      const result = await cancelStockTaking(id).unwrap();
      if (result.success) {
        toast.success("تم إلغاء الجرد بنجاح");
      } else {
        toast.error(result.message || "حدث خطأ");
      }
    } catch {
      toast.error("حدث خطأ أثناء إلغاء الجرد");
    }
  };

  const statusBadge = (status: string) => {
    const map: Record<string, string> = {
      InProgress: "bg-amber-100 text-amber-800 border-amber-200",
      Completed: "bg-success-100 text-success-800 border-success-200",
      Cancelled: "bg-danger-100 text-danger-800 border-danger-200",
    };
    const label: Record<string, string> = {
      InProgress: "قيد التنفيذ",
      Completed: "مكتمل",
      Cancelled: "ملغى",
    };
    return (
      <span className={clsx("px-2 py-1 rounded-lg text-xs font-semibold border", map[status] || "bg-gray-100 text-gray-800")}>
        {label[status] || status}
      </span>
    );
  };

  const items = data?.data?.items ?? [];
  const paged = data?.data;

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-black text-slate-900">الجرد (Stock Taking)</h1>
          <p className="mt-1 text-sm text-slate-500">إنشاء وإدارة جلسات الجرد الفعلي للمخزون</p>
        </div>
        {canManage && (
          <Button
            variant="primary"
            leftIcon={<Plus className="w-4 h-4" />}
            onClick={() => setIsCreateModalOpen(true)}
          >
            جرد جديد
          </Button>
        )}
      </div>

      <Card>
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
          <Input
            placeholder="بحث برقم الجرد..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-10"
          />
        </div>
      </Card>

      {isLoading ? (
        <Loading />
      ) : items.length === 0 ? (
        <Card className="text-center py-12">
          <ClipboardList className="w-12 h-12 mx-auto mb-4 text-gray-300" />
          <p className="text-gray-500 font-semibold">لا توجد جلسات جرد</p>
          <p className="text-gray-400 text-sm mt-1">ابدأ بإنشاء جرد جديد</p>
        </Card>
      ) : (
        <Card padding="none">
          <table className="w-full min-w-[640px]">
            <thead>
              <tr className="bg-gray-50 border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">رقم الجرد</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">الحالة</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">التاريخ</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">عدد البنود</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">الفرق الكلي</th>
                <th className="px-4 py-3 text-center font-semibold text-gray-600 w-32">إجراءات</th>
              </tr>
            </thead>
            <tbody>
              {items.map((st) => (
                <tr key={st.id} className="border-b hover:bg-gray-50">
                  <td className="px-4 py-3 font-mono font-medium">{st.stockTakingNumber}</td>
                  <td className="px-4 py-3">{statusBadge(st.status)}</td>
                  <td className="px-4 py-3 text-sm text-gray-600">{formatDateTime(st.startedAt)}</td>
                  <td className="px-4 py-3 text-sm text-gray-600">{st.itemCount}</td>
                  <td className={clsx("px-4 py-3 font-semibold text-sm", st.totalDifference > 0 ? "text-success-600" : st.totalDifference < 0 ? "text-danger-600" : "text-gray-600")}>
                    {st.totalDifference > 0 ? "+" : ""}{st.totalDifference}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-center gap-1">
                      <button
                        onClick={() => setSelectedStockTaking(st)}
                        className="p-2 hover:bg-primary-50 rounded-lg transition-colors text-primary-600"
                        title="عرض التفاصيل"
                      >
                        <ArrowRight className="w-4 h-4" />
                      </button>
                      {st.status === "InProgress" && canManage && (
                        <>
                          <button
                            onClick={() => handleCancel(st.id)}
                            disabled={isCancelling}
                            className="p-2 hover:bg-danger-50 rounded-lg transition-colors text-danger-600"
                            title="إلغاء"
                          >
                            <XCircle className="w-4 h-4" />
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {paged && paged.totalPages > 1 && (
            <div className="flex items-center justify-between px-4 py-3 border-t">
              <span className="text-sm text-gray-600">
                صفحة {paged.page} من {paged.totalPages} ({paged.totalCount} جرد)
              </span>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={!paged.hasPreviousPage}
                  rightIcon={<ChevronRight className="w-4 h-4" />}
                >
                  السابق
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage((p) => p + 1)}
                  disabled={!paged.hasNextPage}
                  leftIcon={<ChevronLeft className="w-4 h-4" />}
                >
                  التالي
                </Button>
              </div>
            </div>
          )}
        </Card>
      )}

      {/* Create Modal */}
      <Modal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        title="إنشاء جرد جديد"
      >
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">ملاحظات (اختياري)</label>
            <Input
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="مثلاً: جرد نهاية الشهر..."
            />
          </div>
          <div className="flex gap-2 justify-end">
            <Button variant="outline" onClick={() => setIsCreateModalOpen(false)}>إلغاء</Button>
            <Button
              variant="primary"
              onClick={handleCreate}
              isLoading={isCreating}
              leftIcon={<Plus className="w-4 h-4" />}
            >
              إنشاء
            </Button>
          </div>
        </div>
      </Modal>

      {/* Detail/Edit Modal */}
      {selectedStockTaking && (
        <StockTakingDetailModal
          stockTaking={selectedStockTaking}
          onClose={() => setSelectedStockTaking(null)}
          onComplete={async (id, apply) => {
            try {
              const result = await completeStockTaking({ id, body: { applyAdjustments: apply } }).unwrap();
              if (result.success) {
                toast.success("تم إتمام الجرد بنجاح");
                setSelectedStockTaking(null);
              } else {
                toast.error(result.message || "حدث خطأ");
              }
            } catch {
              toast.error("حدث خطأ أثناء إتمام الجرد");
            }
          }}
          isCompleting={isCompleting}
          canManage={canManage}
        />
      )}
    </div>
  );
};

const StockTakingDetailModal = ({
  stockTaking,
  onClose,
  onComplete,
  isCompleting,
  canManage,
}: {
  stockTaking: StockTaking;
  onClose: () => void;
  onComplete: (id: number, apply: boolean) => Promise<void>;
  isCompleting: boolean;
  canManage: boolean;
}) => {
  const { data } = useGetStockTakingByIdQuery(stockTaking.id, { skip: false });
  const [upsertItem, { isLoading: isUpserting }] = useUpsertStockTakingItemMutation();
  const [removeItem, { isLoading: isRemoving }] = useRemoveStockTakingItemMutation();
  const currentBranch = useAppSelector(selectCurrentBranch);
  const { data: inventoryData } = useGetBranchInventoryQuery(currentBranch?.id ?? 0, { skip: !currentBranch?.id });
  const [searchProduct, setSearchProduct] = useState("");
  const [selectedProductId, setSelectedProductId] = useState<number | null>(null);
  const [actualQty, setActualQty] = useState("");
  const [itemReason, setItemReason] = useState("");

  const inventory = inventoryData ?? [];
  const filteredProducts = inventory.filter(
    (i: any) =>
      i.productName.toLowerCase().includes(searchProduct.toLowerCase()) ||
      (i.productSku && i.productSku.toLowerCase().includes(searchProduct.toLowerCase()))
  );

  const handleAddItem = async () => {
    if (!selectedProductId || actualQty === "") {
      toast.error("اختر منتج وأدخل الكمية الفعلية");
      return;
    }
    try {
      const result = await upsertItem({
        stockTakingId: stockTaking.id,
        body: { productId: selectedProductId, actualQuantity: parseInt(actualQty) || 0, reason: itemReason || undefined },
      }).unwrap();
      if (result.success) {
        toast.success("تم إضافة/تحديث البند");
        setSelectedProductId(null);
        setActualQty("");
        setItemReason("");
        setSearchProduct("");
      } else {
        toast.error(result.message || "حدث خطأ");
      }
    } catch {
      toast.error("حدث خطأ");
    }
  };

  const handleRemoveItem = async (itemId: number) => {
    if (!confirm("هل أنت متأكد؟")) return;
    try {
      await removeItem({ stockTakingId: stockTaking.id, itemId }).unwrap();
      toast.success("تم حذف البند");
    } catch {
      toast.error("حدث خطأ");
    }
  };

  const items: StockTakingItem[] = data?.data?.items ?? [];

  return (
    <Modal isOpen={true} onClose={onClose} title={`جرد ${stockTaking.stockTakingNumber}`} size="xl">
      <div className="space-y-4">
        <div className="flex items-center gap-3">
          <span
            className={clsx(
              "px-3 py-1 rounded-lg text-xs font-semibold border",
              stockTaking.status === "InProgress"
                ? "bg-amber-100 text-amber-800 border-amber-200"
                : stockTaking.status === "Completed"
                ? "bg-success-100 text-success-800 border-success-200"
                : "bg-danger-100 text-danger-800 border-danger-200"
            )}
          >
            {stockTaking.status === "InProgress" ? "قيد التنفيذ" : stockTaking.status === "Completed" ? "مكتمل" : "ملغى"}
          </span>
          <span className="text-sm text-gray-500">{formatDateTime(stockTaking.startedAt)}</span>
          {stockTaking.notes && <span className="text-sm text-gray-600">— {stockTaking.notes}</span>}
        </div>

        {/* Add Item Section - only for InProgress */}
        {stockTaking.status === "InProgress" && canManage && (
          <Card className="space-y-3">
            <h3 className="font-bold text-slate-900 text-sm">إضافة بند جرد</h3>
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
              <Input
                placeholder="بحث باسم المنتج أو SKU..."
                value={searchProduct}
                onChange={(e) => setSearchProduct(e.target.value)}
                className="pl-10"
              />
            </div>
            {searchProduct && filteredProducts.length > 0 && (
              <div className="max-h-40 overflow-y-auto border rounded-lg divide-y">
                {filteredProducts.slice(0, 10).map((p: any) => (
                  <button
                    key={p.productId}
                    type="button"
                    onClick={() => {
                      setSelectedProductId(p.productId);
                      setSearchProduct(p.productName);
                      setActualQty(p.quantity.toString());
                    }}
                    className={clsx(
                      "w-full text-right px-3 py-2 text-sm hover:bg-gray-50 transition-colors",
                      selectedProductId === p.productId && "bg-primary-50 text-primary-700"
                    )}
                  >
                    <div className="font-medium">{p.productName}</div>
                    <div className="text-xs text-gray-500">
                      الكمية في النظام: {p.quantity} {p.productSku ? `| SKU: ${p.productSku}` : ""}
                    </div>
                  </button>
                ))}
              </div>
            )}
            <div className="grid grid-cols-2 gap-2">
              <Input
                type="number"
                label="الكمية الفعلية"
                value={actualQty}
                onChange={(e) => setActualQty(e.target.value)}
                placeholder="0"
              />
              <Input
                label="السبب (اختياري)"
                value={itemReason}
                onChange={(e) => setItemReason(e.target.value)}
                placeholder="مثلاً: تلف..."
              />
            </div>
            <Button
              variant="primary"
              onClick={handleAddItem}
              isLoading={isUpserting}
              leftIcon={<Save className="w-4 h-4" />}
              className="w-full"
            >
              حفظ البند
            </Button>
          </Card>
        )}

        {/* Items Table */}
        <Card padding="none">
          <table className="w-full min-w-[400px]">
            <thead>
              <tr className="bg-gray-50 border-b">
                <th className="px-3 py-2 text-right font-semibold text-gray-600 text-sm">المنتج</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-600 text-sm">النظام</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-600 text-sm">الفعلي</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-600 text-sm">الفرق</th>
                {stockTaking.status === "InProgress" && canManage && (
                  <th className="px-3 py-2 text-center font-semibold text-gray-600 text-sm w-12"></th>
                )}
              </tr>
            </thead>
            <tbody>
              {/* If API returns items directly, show them. Otherwise fallback to empty */}
              {items.length === 0 ? (
                <tr>
                  <td colSpan={stockTaking.status === "InProgress" && canManage ? 5 : 4} className="text-center py-8 text-gray-400 text-sm">
                    لا توجد بنود جرد
                  </td>
                </tr>
              ) : (
                items.map((item) => (
                  <tr key={item.id} className="border-b hover:bg-gray-50">
                    <td className="px-3 py-2 text-sm">
                      <div className="font-medium">{item.productName}</div>
                      {item.productSku && <div className="text-xs text-gray-500">{item.productSku}</div>}
                      {item.reason && <div className="text-xs text-amber-600 mt-0.5">{item.reason}</div>}
                    </td>
                    <td className="px-3 py-2 text-sm text-center text-gray-600">{item.systemQuantity}</td>
                    <td className="px-3 py-2 text-sm text-center font-semibold">{item.actualQuantity}</td>
                    <td className={clsx(
                      "px-3 py-2 text-sm text-center font-bold",
                      item.difference > 0 ? "text-success-600" : item.difference < 0 ? "text-danger-600" : "text-gray-600"
                    )}>
                      {item.difference > 0 ? "+" : ""}{item.difference}
                    </td>
                    {stockTaking.status === "InProgress" && canManage && (
                      <td className="px-3 py-2 text-center">
                        <button
                          onClick={() => handleRemoveItem(item.id)}
                          disabled={isRemoving}
                          className="p-1.5 hover:bg-danger-50 rounded-lg transition-colors text-danger-500"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </td>
                    )}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </Card>

        {/* Complete Action */}
        {stockTaking.status === "InProgress" && canManage && (
          <div className="flex gap-2">
            <Button
              variant="primary"
              onClick={() => onComplete(stockTaking.id, true)}
              isLoading={isCompleting}
              leftIcon={<CheckCircle className="w-4 h-4" />}
              className="flex-1"
            >
              إتمام الجرد وتطبيق الفروقات
            </Button>
            <Button
              variant="outline"
              onClick={() => onComplete(stockTaking.id, false)}
              isLoading={isCompleting}
              leftIcon={<CheckCircle className="w-4 h-4" />}
            >
              إتمام بدون تطبيق
            </Button>
          </div>
        )}
      </div>
    </Modal>
  );
};
