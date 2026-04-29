import { useState, useRef, useEffect, useMemo } from "react";
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
  AlertTriangle,
  Info,
  Filter,
  Layers,
  User,
  Calendar,
  BarChart3,
  ArrowDown,
  ArrowUp,
  Minus,
  ShieldAlert,
} from "lucide-react";
import {
  useGetStockTakingsQuery,
  useGetStockTakingByIdQuery,
  useCreateStockTakingMutation,
  useCancelStockTakingMutation,
  useCompleteStockTakingMutation,
  useUpsertStockTakingItemMutation,
  useRemoveStockTakingItemMutation,
} from "@/api/stockTakingApi";
import { useGetBranchInventoryQuery } from "@/api/inventoryApi";
import { useGetCategoriesQuery } from "@/api/categoriesApi";
import { useGetBatchesByProductQuery } from "@/api/productBatchApi";
import { useAppSelector } from "../../store/hooks";
import { selectCurrentBranch } from "../../store/slices/branchSlice";
import { Button, Card, Loading } from "@/components/common";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import { usePermission } from "@/hooks/usePermission";
import { formatDateTime } from "@/utils/formatters";
import { toast } from "sonner";
import type {
  StockTaking,
  StockTakingItem,
  StockTakingType,
  StockTakingStatus,
} from "@/types/stockTaking.types";
import type { ProductBatch } from "@/types/productBatch.types";
import clsx from "clsx";

export const StockTakingPage = () => {
  const { hasPermission } = usePermission();
  const canManage = hasPermission("InventoryManage");
  const currentBranch = useAppSelector(selectCurrentBranch);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [selectedStockTaking, setSelectedStockTaking] = useState<StockTaking | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [reviewStockTaking, setReviewStockTaking] = useState<StockTaking | null>(null);
  const [statusFilter, setStatusFilter] = useState<StockTakingStatus | "">("");

  const { data, isLoading } = useGetStockTakingsQuery({
    page,
    pageSize: 10,
    status: statusFilter || undefined,
  });

  // Auto-open from query param (e.g., /stock-taking?open=123)
  const [queryParamHandled, setQueryParamHandled] = useState(false);
  useEffect(() => {
    if (queryParamHandled) return;
    const params = new URLSearchParams(window.location.search);
    const openId = params.get("open");
    if (openId) {
      const id = parseInt(openId, 10);
      if (!isNaN(id)) {
        // Will open after list loads
        const st = data?.data?.items?.find((x) => x.id === id);
        if (st) {
          setSelectedStockTaking(st);
          setQueryParamHandled(true);
          window.history.replaceState({}, document.title, window.location.pathname);
        }
      }
    }
  }, [data?.data?.items, queryParamHandled]);

  const [createStockTaking, { isLoading: isCreating }] = useCreateStockTakingMutation();
  const [cancelStockTaking, { isLoading: isCancelling }] = useCancelStockTakingMutation();
  const [completeStockTaking, { isLoading: isCompleting }] = useCompleteStockTakingMutation();

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

  const typeLabel = (type: StockTakingType, categoryName?: string) => {
    if (type === "Full") return "جرد كامل";
    return categoryName ? `جرد جزئي — ${categoryName}` : "جرد جزئي";
  };

  const allItems = data?.data?.items ?? [];
  const filteredItems = allItems.filter((st) => {
    const matchesSearch =
      !search ||
      st.stockTakingNumber.toLowerCase().includes(search.toLowerCase()) ||
      (st.createdByUserName && st.createdByUserName.toLowerCase().includes(search.toLowerCase()));
    return matchesSearch;
  });
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

      {/* Filters */}
      <Card>
        <div className="flex flex-col sm:flex-row gap-3">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
            <Input
              placeholder="بحث برقم الجرد أو المنشئ..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-10"
            />
          </div>
          <div className="flex gap-2">
            <select
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value as StockTakingStatus | "");
                setPage(1);
              }}
              className="px-3 py-2 border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            >
              <option value="">كل الحالات</option>
              <option value="InProgress">قيد التنفيذ</option>
              <option value="Completed">مكتمل</option>
              <option value="Cancelled">ملغى</option>
            </select>
          </div>
        </div>
      </Card>

      {isLoading ? (
        <Loading />
      ) : filteredItems.length === 0 ? (
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
                <th className="px-4 py-3 text-right font-semibold text-gray-600">النوع</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">الحالة</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">المنشئ</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">التاريخ</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">عدد البنود</th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">الفرق الكلي</th>
                <th className="px-4 py-3 text-center font-semibold text-gray-600 w-32">إجراءات</th>
              </tr>
            </thead>
            <tbody>
              {filteredItems.map((st) => (
                <tr key={st.id} className="border-b hover:bg-gray-50">
                  <td className="px-4 py-3 font-mono font-medium">{st.stockTakingNumber}</td>
                  <td className="px-4 py-3 text-sm text-gray-600">
                    <div className="flex items-center gap-1">
                      <Layers className="w-3.5 h-3.5 text-gray-400" />
                      {typeLabel(st.type, st.categoryName)}
                    </div>
                  </td>
                  <td className="px-4 py-3">{statusBadge(st.status)}</td>
                  <td className="px-4 py-3 text-sm text-gray-600">
                    <div className="flex items-center gap-1">
                      <User className="w-3.5 h-3.5 text-gray-400" />
                      {st.createdByUserName || "—"}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-600">
                    <div className="flex items-center gap-1">
                      <Calendar className="w-3.5 h-3.5 text-gray-400" />
                      {formatDateTime(st.startedAt)}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-sm text-gray-600">{st.itemCount}</td>
                  <td className={clsx(
                    "px-4 py-3 font-semibold text-sm",
                    st.totalDifference > 0 ? "text-success-600" : st.totalDifference < 0 ? "text-danger-600" : "text-gray-600"
                  )}>
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
                        <button
                          onClick={() => handleCancel(st.id)}
                          disabled={isCancelling}
                          className="p-2 hover:bg-danger-50 rounded-lg transition-colors text-danger-600"
                          title="إلغاء"
                        >
                          <XCircle className="w-4 h-4" />
                        </button>
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
      {isCreateModalOpen && (
        <CreateStockTakingModal
          isOpen={isCreateModalOpen}
          onClose={() => setIsCreateModalOpen(false)}
          onCreate={async (type, categoryId, notes) => {
            try {
              const result = await createStockTaking({ type, categoryId, notes }).unwrap();
              if (result.success && result.data) {
                toast.success("تم إنشاء الجرد بنجاح");
                setSelectedStockTaking(result.data);
                setIsCreateModalOpen(false);
              } else {
                toast.error(result.message || "حدث خطأ");
              }
            } catch {
              toast.error("حدث خطأ أثناء إنشاء الجرد");
            }
          }}
          isCreating={isCreating}
        />
      )}

      {/* Detail/Edit Modal */}
      {selectedStockTaking && (
        <StockTakingDetailModal
          stockTaking={selectedStockTaking}
          onClose={() => setSelectedStockTaking(null)}
          onOpenReview={(st) => {
            setSelectedStockTaking(null);
            setReviewStockTaking(st);
          }}
          canManage={canManage}
        />
      )}

      {/* Review Modal */}
      {reviewStockTaking && (
        <StockTakingReviewModal
          stockTaking={reviewStockTaking}
          onClose={() => setReviewStockTaking(null)}
          onConfirm={async (id, apply) => {
            try {
              const result = await completeStockTaking({ id, body: { applyAdjustments: apply } }).unwrap();
              if (result.success) {
                toast.success(apply ? "تم إتمام الجرد وتطبيق الفروقات بنجاح" : "تم إتمام الجرد بدون تطبيق");
                setReviewStockTaking(null);
              } else {
                toast.error(result.message || "حدث خطأ");
              }
            } catch {
              toast.error("حدث خطأ أثناء إتمام الجرد");
            }
          }}
          isCompleting={isCompleting}
        />
      )}
    </div>
  );
};

const CreateStockTakingModal = ({
  isOpen,
  onClose,
  onCreate,
  isCreating,
}: {
  isOpen: boolean;
  onClose: () => void;
  onCreate: (type: StockTakingType, categoryId: number | undefined, notes: string) => Promise<void>;
  isCreating: boolean;
}) => {
  const [type, setType] = useState<StockTakingType>("Full");
  const [categoryId, setCategoryId] = useState<number | undefined>(undefined);
  const [notes, setNotes] = useState("");
  const { data: categoriesData } = useGetCategoriesQuery({ pageSize: 100 });

  const categories = categoriesData?.data ?? [];

  const handleSubmit = () => {
    onCreate(type, type === "Partial" ? categoryId : undefined, notes);
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="إنشاء جرد جديد">
      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">نوع الجرد</label>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => { setType("Full"); setCategoryId(undefined); }}
              className={clsx(
                "flex-1 px-4 py-3 rounded-lg border text-sm font-medium transition-colors text-center",
                type === "Full"
                  ? "bg-primary-50 border-primary-500 text-primary-700"
                  : "bg-white border-gray-200 text-gray-600 hover:bg-gray-50"
              )}
            >
              <Package className="w-4 h-4 mx-auto mb-1" />
              جرد كامل
            </button>
            <button
              type="button"
              onClick={() => setType("Partial")}
              className={clsx(
                "flex-1 px-4 py-3 rounded-lg border text-sm font-medium transition-colors text-center",
                type === "Partial"
                  ? "bg-primary-50 border-primary-500 text-primary-700"
                  : "bg-white border-gray-200 text-gray-600 hover:bg-gray-50"
              )}
            >
              <Layers className="w-4 h-4 mx-auto mb-1" />
              جرد جزئي
            </button>
          </div>
        </div>

        {type === "Partial" && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">الفئة</label>
            <select
              value={categoryId ?? ""}
              onChange={(e) => setCategoryId(e.target.value ? parseInt(e.target.value) : undefined)}
              className="w-full px-3 py-2 border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            >
              <option value="">اختر فئة...</option>
              {categories.map((c: any) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
            {!categoryId && (
              <p className="text-xs text-danger-600 mt-1">مطلوب اختيار فئة للجرد الجزئي</p>
            )}
          </div>
        )}

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">ملاحظات (اختياري)</label>
          <Input
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="مثلاً: جرد نهاية الشهر..."
          />
        </div>

        <div className="flex gap-2 justify-end pt-2">
          <Button variant="outline" onClick={onClose}>إلغاء</Button>
          <Button
            variant="primary"
            onClick={handleSubmit}
            isLoading={isCreating}
            leftIcon={<Plus className="w-4 h-4" />}
            disabled={type === "Partial" && !categoryId}
          >
            إنشاء
          </Button>
        </div>
      </div>
    </Modal>
  );
};

const StockTakingDetailModal = ({
  stockTaking,
  onClose,
  onOpenReview,
  canManage,
}: {
  stockTaking: StockTaking;
  onClose: () => void;
  onOpenReview: (st: StockTaking) => void;
  canManage: boolean;
}) => {
  const { data, refetch } = useGetStockTakingByIdQuery(stockTaking.id, { refetchOnMountOrArgChange: true });
  const [upsertItem, { isLoading: isUpserting }] = useUpsertStockTakingItemMutation();
  const [removeItem, { isLoading: isRemoving }] = useRemoveStockTakingItemMutation();
  const currentBranch = useAppSelector(selectCurrentBranch);
  const { data: inventoryData } = useGetBranchInventoryQuery(currentBranch?.id ?? 0, { skip: !currentBranch?.id });

  const [searchProduct, setSearchProduct] = useState("");
  const [selectedProductId, setSelectedProductId] = useState<number | null>(null);
  const [selectedProductName, setSelectedProductName] = useState("");
  const [selectedProductIsBatchTracked, setSelectedProductIsBatchTracked] = useState(false);
  const [actualQty, setActualQty] = useState("");
  const [itemReason, setItemReason] = useState("");
  const [selectedBatchId, setSelectedBatchId] = useState<number | undefined>(undefined);
  const [showBatches, setShowBatches] = useState(false);
  const searchInputRef = useRef<HTMLInputElement>(null);

  const st = data?.data ?? stockTaking;
  const items: StockTakingItem[] = st.items ?? [];

  const inventory = inventoryData ?? [];
  const filteredProducts = inventory.filter(
    (i: any) =>
      i.productName?.toLowerCase().includes(searchProduct.toLowerCase()) ||
      (i.productSku && i.productSku.toLowerCase().includes(searchProduct.toLowerCase()))
  );

  const productCount = items.length;
  const diffItems = items.filter((i) => i.difference !== 0);
  const positiveDiffCount = items.filter((i) => i.difference > 0).length;
  const negativeDiffCount = items.filter((i) => i.difference < 0).length;
  const zeroDiffCount = items.filter((i) => i.difference === 0).length;
  const totalPositive = items.filter((i) => i.difference > 0).reduce((sum, i) => sum + i.difference, 0);
  const totalNegative = items.filter((i) => i.difference < 0).reduce((sum, i) => sum + Math.abs(i.difference), 0);

  const handleAddItem = async () => {
    if (!selectedProductId || actualQty === "") {
      toast.error("اختر منتج وأدخل الكمية الفعلية");
      return;
    }
    try {
      const result = await upsertItem({
        stockTakingId: stockTaking.id,
        body: {
          productId: selectedProductId,
          actualQuantity: parseInt(actualQty) || 0,
          reason: itemReason || undefined,
          batchId: selectedBatchId,
        },
      }).unwrap();
      if (result.success) {
        toast.success("تم إضافة/تحديث البند");
        refetch();
        setSelectedProductId(null);
        setSelectedProductName("");
        setSelectedProductIsBatchTracked(false);
        setSelectedBatchId(undefined);
        setShowBatches(false);
        setActualQty("");
        setItemReason("");
        setSearchProduct("");
        setTimeout(() => searchInputRef.current?.focus(), 50);
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
      refetch();
    } catch {
      toast.error("حدث خطأ");
    }
  };

  const handleSelectProduct = (p: any) => {
    setSelectedProductId(p.productId);
    setSelectedProductName(p.productName);
    setSearchProduct(p.productName);
    setSelectedProductIsBatchTracked(p.isBatchTracked ?? false);
    setSelectedBatchId(undefined);
    setShowBatches(p.isBatchTracked ?? false);
    setActualQty(p.quantity?.toString() ?? "0");
  };

  const existingItem = items.find((i) => i.productId === selectedProductId && i.batchId === selectedBatchId);

  return (
    <Modal isOpen={true} onClose={onClose} title={`جرد ${stockTaking.stockTakingNumber}`} size="xl">
      <div className="space-y-4 max-h-[80vh] overflow-y-auto pr-1">
        {/* Header */}
        <div className="flex items-center gap-3 flex-wrap">
          <span
            className={clsx(
              "px-3 py-1 rounded-lg text-xs font-semibold border",
              st.status === "InProgress"
                ? "bg-amber-100 text-amber-800 border-amber-200"
                : st.status === "Completed"
                ? "bg-success-100 text-success-800 border-success-200"
                : "bg-danger-100 text-danger-800 border-danger-200"
            )}
          >
            {st.status === "InProgress" ? "قيد التنفيذ" : st.status === "Completed" ? "مكتمل" : "ملغى"}
          </span>
          <span className="text-sm text-gray-500">{formatDateTime(st.startedAt)}</span>
          {st.notes && <span className="text-sm text-gray-600">— {st.notes}</span>}
        </div>

        {/* Progress Indicators */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <div className="bg-gray-50 rounded-lg p-3 text-center">
            <div className="text-xs text-gray-500 mb-1">البنود المجرّدة</div>
            <div className="text-lg font-bold text-slate-900">{productCount}</div>
          </div>
          <div className="bg-success-50 rounded-lg p-3 text-center">
            <div className="text-xs text-success-700 mb-1">فرق موجب</div>
            <div className="text-lg font-bold text-success-700">+{positiveDiffCount}</div>
          </div>
          <div className="bg-danger-50 rounded-lg p-3 text-center">
            <div className="text-xs text-danger-700 mb-1">فرق سالب</div>
            <div className="text-lg font-bold text-danger-700">-{negativeDiffCount}</div>
          </div>
          <div className="bg-gray-50 rounded-lg p-3 text-center">
            <div className="text-xs text-gray-500 mb-1">متطابق</div>
            <div className="text-lg font-bold text-slate-900">{zeroDiffCount}</div>
          </div>
        </div>
        <div className="flex gap-3 text-sm">
          <span className="text-success-600 font-semibold">+{totalPositive} وحدة زيادة</span>
          <span className="text-gray-300">|</span>
          <span className="text-danger-600 font-semibold">-{totalNegative} وحدة نقص</span>
        </div>

        {/* Info tooltip */}
        <div className="flex items-start gap-2 bg-blue-50 border border-blue-200 rounded-lg p-3">
          <Info className="w-4 h-4 text-blue-600 mt-0.5 shrink-0" />
          <p className="text-xs text-blue-800">
            كمية النظام المعروضة بجانب كل منتج هي الكمية وقت إضافته للجرد — وليس وقت فتح الجلسة.
            إذا أُضيف بندان في أوقات مختلفة قد يعكسان حالات مخزون مختلفة.
          </p>
        </div>

        {/* Add Item Section */}
        {st.status === "InProgress" && canManage && (
          <Card className="space-y-3">
            <h3 className="font-bold text-slate-900 text-sm">إضافة/تعديل بند جرد</h3>
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
              <Input
                ref={searchInputRef}
                placeholder="بحث باسم المنتج أو SKU..."
                value={searchProduct}
                onChange={(e) => {
                  setSearchProduct(e.target.value);
                  if (selectedProductId) {
                    setSelectedProductId(null);
                    setSelectedProductName("");
                    setSelectedProductIsBatchTracked(false);
                    setSelectedBatchId(undefined);
                    setShowBatches(false);
                  }
                }}
                className="pl-10"
              />
            </div>
            {searchProduct && !selectedProductId && filteredProducts.length > 0 && (
              <div className="max-h-40 overflow-y-auto border rounded-lg divide-y">
                {filteredProducts.slice(0, 10).map((p: any) => (
                  <button
                    key={p.productId}
                    type="button"
                    onClick={() => handleSelectProduct(p)}
                    className="w-full text-right px-3 py-2 text-sm hover:bg-gray-50 transition-colors"
                  >
                    <div className="font-medium">{p.productName}</div>
                    <div className="text-xs text-gray-500">
                      الكمية في النظام: {p.quantity} {p.productSku ? `| SKU: ${p.productSku}` : ""}
                    </div>
                  </button>
                ))}
              </div>
            )}

            {/* Batch selection */}
            {showBatches && selectedProductId && (
              <BatchSelector
                productId={selectedProductId}
                branchId={currentBranch?.id ?? 0}
                selectedBatchId={selectedBatchId}
                onSelectBatch={(batch) => {
                  setSelectedBatchId(batch.id);
                  setActualQty(batch.quantity?.toString() ?? "0");
                }}
              />
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
            <p className="text-xs text-gray-400">
              الجرد حاليًا يدعم الكميات الصحيحة فقط. دعم الكميات الكسرية (مثل كيلو/متر) سيُضاف في مرحلة لاحقة.
            </p>

            {existingItem && (
              <p className="text-xs text-amber-600">
                هذا المنتج موجود مسبقاً — سيتم تحديث كميته الحالية ({existingItem.actualQuantity})
              </p>
            )}

            <Button
              variant="primary"
              onClick={handleAddItem}
              isLoading={isUpserting}
              leftIcon={<Save className="w-4 h-4" />}
              className="w-full"
            >
              حفظ البند والاستمرار
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
                {st.status === "InProgress" && canManage && (
                  <th className="px-3 py-2 text-center font-semibold text-gray-600 text-sm w-12"></th>
                )}
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan={st.status === "InProgress" && canManage ? 5 : 4} className="text-center py-8 text-gray-400 text-sm">
                    لا توجد بنود جرد. ابدأ بإضافة منتجات.
                  </td>
                </tr>
              ) : (
                items.map((item) => (
                  <tr key={item.id} className="border-b hover:bg-gray-50">
                    <td className="px-3 py-2 text-sm">
                      <div className="font-medium">{item.productName}</div>
                      {item.productSku && <div className="text-xs text-gray-500">{item.productSku}</div>}
                      {item.batchId && <div className="text-xs text-purple-600">باتش #{item.batchId}</div>}
                      {item.reason && <div className="text-xs text-amber-600 mt-0.5">{item.reason}</div>}
                    </td>
                    <td className="px-3 py-2 text-sm text-center text-gray-600">
                      <span title="كمية النظام لحظة إضافة هذا البند">
                        {item.systemQuantity}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-sm text-center font-semibold">{item.actualQuantity}</td>
                    <td className={clsx(
                      "px-3 py-2 text-sm text-center font-bold",
                      item.difference > 0 ? "text-success-600" : item.difference < 0 ? "text-danger-600" : "text-gray-600"
                    )}>
                      {item.difference > 0 ? "+" : ""}{item.difference}
                    </td>
                    {st.status === "InProgress" && canManage && (
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
        {st.status === "InProgress" && canManage && (
          <Button
            variant="primary"
            onClick={() => onOpenReview(st)}
            className="w-full"
            leftIcon={<CheckCircle className="w-4 h-4" />}
          >
            إتمام الجرد وعرض المراجعة
          </Button>
        )}
      </div>
    </Modal>
  );
};

const BatchSelector = ({
  productId,
  branchId,
  selectedBatchId,
  onSelectBatch,
}: {
  productId: number;
  branchId: number;
  selectedBatchId?: number;
  onSelectBatch: (batch: ProductBatch) => void;
}) => {
  const { data: batchData, isLoading } = useGetBatchesByProductQuery({ productId, branchId });
  const batches: ProductBatch[] = batchData?.data ?? [];

  if (isLoading) return <div className="text-xs text-gray-500 py-2"><Loader2 className="w-3 h-3 animate-spin inline" /> جاري تحميل الباتشات...</div>;
  if (batches.length === 0) return <div className="text-xs text-danger-600 py-2">لا توجد باتشات نشطة لهذا المنتج</div>;

  return (
    <div className="space-y-2">
      <label className="block text-sm font-medium text-gray-700">اختر الباتش</label>
      <div className="max-h-32 overflow-y-auto border rounded-lg divide-y">
        {batches.map((batch) => (
          <button
            key={batch.id}
            type="button"
            onClick={() => onSelectBatch(batch)}
            className={clsx(
              "w-full text-right px-3 py-2 text-sm hover:bg-gray-50 transition-colors flex justify-between items-center",
              selectedBatchId === batch.id && "bg-purple-50 text-purple-700"
            )}
          >
            <div>
              <span className="font-medium">{batch.batchNumber}</span>
              <span className="text-xs text-gray-500 mr-2">ينتهي: {batch.expiryDate}</span>
            </div>
            <span className="text-xs bg-gray-100 px-2 py-0.5 rounded">{batch.quantity}</span>
          </button>
        ))}
      </div>
    </div>
  );
};

const StockTakingReviewModal = ({
  stockTaking,
  onClose,
  onConfirm,
  isCompleting,
}: {
  stockTaking: StockTaking;
  onClose: () => void;
  onConfirm: (id: number, apply: boolean) => Promise<void>;
  isCompleting: boolean;
}) => {
  const { data } = useGetStockTakingByIdQuery(stockTaking.id, { refetchOnMountOrArgChange: true });
  const st = data?.data ?? stockTaking;
  const items: StockTakingItem[] = st.items ?? [];

  const totalItems = items.length;
  const positiveItems = items.filter((i) => i.difference > 0);
  const negativeItems = items.filter((i) => i.difference < 0);
  const zeroItems = items.filter((i) => i.difference === 0);
  const totalPositive = positiveItems.reduce((sum, i) => sum + i.difference, 0);
  const totalNegative = negativeItems.reduce((sum, i) => sum + Math.abs(i.difference), 0);

  // Threshold: difference > 20% or > 10 units
  const threshold = (item: StockTakingItem) => {
    if (item.systemQuantity === 0) return item.difference > 10;
    return Math.abs(item.difference) > 10 || Math.abs(item.difference / item.systemQuantity) > 0.2;
  };

  const largeDiffs = [...positiveItems, ...negativeItems].filter(threshold);

  return (
    <Modal isOpen={true} onClose={onClose} title="مراجعة الجرد قبل التطبيق" size="xl">
      <div className="space-y-4 max-h-[80vh] overflow-y-auto pr-1">
        {/* Summary cards */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <div className="bg-gray-50 rounded-lg p-3 text-center">
            <div className="text-xs text-gray-500 mb-1">إجمالي البنود</div>
            <div className="text-xl font-bold text-slate-900">{totalItems}</div>
          </div>
          <div className="bg-success-50 rounded-lg p-3 text-center">
            <div className="text-xs text-success-700 mb-1">بنود بفرق موجب</div>
            <div className="text-xl font-bold text-success-700">{positiveItems.length}</div>
            <div className="text-xs text-success-600">+{totalPositive} وحدة</div>
          </div>
          <div className="bg-danger-50 rounded-lg p-3 text-center">
            <div className="text-xs text-danger-700 mb-1">بنود بفرق سالب</div>
            <div className="text-xl font-bold text-danger-700">{negativeItems.length}</div>
            <div className="text-xs text-danger-600">-{totalNegative} وحدة</div>
          </div>
          <div className="bg-gray-50 rounded-lg p-3 text-center">
            <div className="text-xs text-gray-500 mb-1">بدون فرق</div>
            <div className="text-xl font-bold text-slate-900">{zeroItems.length}</div>
          </div>
        </div>

        {/* Large differences warning */}
        {largeDiffs.length > 0 && (
          <div className="bg-red-50 border border-red-200 rounded-lg p-3">
            <div className="flex items-center gap-2 mb-2">
              <ShieldAlert className="w-4 h-4 text-red-600" />
              <span className="text-sm font-bold text-red-800">فروقات كبيرة تحتاج مراجعة</span>
            </div>
            <ul className="space-y-1">
              {largeDiffs.map((item) => (
                <li key={item.id} className="text-xs text-red-700 flex justify-between items-center bg-white rounded px-2 py-1">
                  <span>{item.productName} {item.batchId ? `(باتش #${item.batchId})` : ""}</span>
                  <span className="font-bold">
                    {item.difference > 0 ? "+" : ""}{item.difference}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}

        {/* Items list */}
        <Card padding="none">
          <div className="px-4 py-2 border-b bg-gray-50 text-sm font-semibold text-gray-700">تفاصيل البنود</div>
          <div className="max-h-60 overflow-y-auto divide-y">
            {items.length === 0 ? (
              <div className="text-center py-6 text-gray-400 text-sm">لا توجد بنود</div>
            ) : (
              items.map((item) => {
                const isLarge = threshold(item);
                return (
                  <div key={item.id} className={clsx("px-4 py-2 text-sm flex justify-between items-center", isLarge && "bg-red-50")}>
                    <div>
                      <div className="font-medium">{item.productName}</div>
                      {item.batchId && <div className="text-xs text-purple-600">باتش #{item.batchId}</div>}
                      {item.reason && <div className="text-xs text-amber-600">{item.reason}</div>}
                    </div>
                    <div className="text-left">
                      <div className={clsx(
                        "font-bold",
                        item.difference > 0 ? "text-success-600" : item.difference < 0 ? "text-danger-600" : "text-gray-600"
                      )}>
                        {item.difference > 0 ? "+" : ""}{item.difference}
                      </div>
                      <div className="text-xs text-gray-500">
                        نظام: {item.systemQuantity} | فعلي: {item.actualQuantity}
                      </div>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </Card>

        {/* Actions */}
        <div className="flex flex-col gap-3 pt-2">
          <Button
            variant="primary"
            onClick={() => onConfirm(stockTaking.id, true)}
            isLoading={isCompleting}
            className="w-full"
            leftIcon={<CheckCircle className="w-4 h-4" />}
          >
            تأكيد التطبيق — تعديل المخزون وإغلاق الجلسة
          </Button>
          <Button
            variant="outline"
            onClick={() => onConfirm(stockTaking.id, false)}
            isLoading={isCompleting}
            className="w-full"
          >
            إغلاق الجلسة فقط بدون تطبيق
          </Button>
          <div className="flex items-start gap-2 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg p-2">
            <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
            <span>
              إذا اخترت "إغلاق بدون تطبيق" — لن تُعدّل كميات المخزون، وستظل الفروقات في سجل الجرد فقط.
            </span>
          </div>
          <Button variant="outline" onClick={onClose} className="w-full">
            العودة للتعديل
          </Button>
        </div>
      </div>
    </Modal>
  );
};
