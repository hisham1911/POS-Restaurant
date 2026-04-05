import { useState, useEffect, useMemo } from "react";
import {
  X,
  Image as ImageIcon,
  Package,
  Wrench,
  ChevronDown,
} from "lucide-react";
import {
  Product,
  CreateProductRequest,
  UpdateProductRequest,
  ProductType,
} from "@/types/product.types";
import { useProducts, useCategories } from "@/hooks/useProducts";
import { useGetBranchesQuery } from "@/api/branchesApi";
import { useGetBranchInventoryQuery } from "@/api/inventoryApi";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentBranch } from "@/store/slices/branchSlice";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import { numberToDisplay, displayToNumber } from "@/hooks/useNumberInput";
import clsx from "clsx";

const LEGACY_CURRENT_BRANCH_FIELD = ["current", "Branch", "Stock"].join("");

interface ProductFormModalProps {
  product: Product | null;
  onClose: () => void;
}

// Emoji icons for products
const PRODUCT_ICONS = [
  "☕",
  "🍕",
  "🍔",
  "🍟",
  "🌭",
  "🥪",
  "🌮",
  "🌯",
  "🥙",
  "🍗",
  "🍖",
  "🥩",
  "🍤",
  "🍱",
  "🍛",
  "🍜",
  "🍝",
  "🍠",
  "🍢",
  "🍣",
  "🍰",
  "🎂",
  "🧁",
  "🍪",
  "🍩",
  "🍨",
  "🍦",
  "🥤",
  "🧃",
  "🧋",
  "🍺",
  "🍻",
  "🥂",
  "🍷",
  "🥃",
  "🍸",
  "🍹",
  "🧉",
  "🍶",
  "🥛",
  "🍎",
  "🍊",
  "🍋",
  "🍌",
  "🍉",
  "🍇",
  "🍓",
  "🫐",
  "🍈",
  "🍒",
  "🥗",
  "🥘",
  "🍲",
  "🥫",
  "🧂",
  "🧈",
  "🥖",
  "🥐",
  "🥯",
  "🍞",
];

const isImageSource = (value?: string): boolean => {
  if (!value) return false;
  const normalized = value.trim();
  if (!normalized) return false;
  return /^(https?:\/\/|\/|data:image\/|blob:)/i.test(normalized);
};

export const ProductFormModal = ({
  product,
  onClose,
}: ProductFormModalProps) => {
  const { createProduct, updateProduct, isCreating, isUpdating } =
    useProducts();
  const { categories } = useCategories();
  const { data: branches } = useGetBranchesQuery();
  const currentBranch = useAppSelector(selectCurrentBranch);
  const isEditing = !!product;
  const { data: branchInventory } = useGetBranchInventoryQuery(
    currentBranch?.id ?? 0,
    {
      skip: !currentBranch?.id || !product?.id,
    },
  );

  const currentBranchQuantity = useMemo(() => {
    if (!product?.id || !branchInventory) {
      return 0;
    }

    const inventoryRow = branchInventory.find(
      (item) => item.productId === product.id,
    );
    return inventoryRow?.quantity ?? 0;
  }, [product?.id, branchInventory]);

  const [formData, setFormData] = useState({
    name: product?.name || "",
    nameEn: product?.nameEn || "",
    description: product?.description || "",
    sku: product?.sku || "",
    barcode: product?.barcode || "",
    price: product?.price || 0,
    cost: product?.cost || 0,
    taxRate: product?.taxRate ?? null,
    taxInclusive: product?.taxInclusive ?? true,
    imageUrl: product?.imageUrl || "",
    categoryId: product?.categoryId || categories[0]?.id || 0,
    type: product?.type || ProductType.Physical,
    branchQuantity: 0,
    lowStockThreshold: product?.lowStockThreshold ?? 5,
    reorderPoint: product?.reorderPoint ?? null,
    isActive: product?.isActive ?? true,
  });

  const [showIconPicker, setShowIconPicker] = useState(false);
  const [branchStocks, setBranchStocks] = useState<
    Record<number, number | string>
  >({});
  const [useBranchSpecificStock, setUseBranchSpecificStock] = useState(false);

  // Initialize branch stocks with default quantity
  useEffect(() => {
    if (branches?.data && !isEditing) {
      const initialStocks: Record<number, number> = {};
      branches.data.forEach((branch) => {
        initialStocks[branch.id] = formData.branchQuantity;
      });
      setBranchStocks(initialStocks);
    }
  }, [branches?.data, formData.branchQuantity, isEditing]);

  useEffect(() => {
    if (!isEditing || !product || product.type !== ProductType.Physical) {
      return;
    }

    setFormData((prev) => ({
      ...prev,
      branchQuantity: currentBranchQuantity,
    }));
  }, [isEditing, product, currentBranchQuantity]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (isEditing && product) {
      // تحديث المنتج
      const baseUpdateData = {
        name: formData.name,
        nameEn: formData.nameEn,
        description: formData.description,
        sku: formData.sku,
        barcode: formData.barcode,
        price: formData.price,
        cost: formData.cost,
        taxRate: formData.taxRate ?? undefined,
        taxInclusive: formData.taxInclusive,
        imageUrl: formData.imageUrl,
        categoryId: formData.categoryId,
        type: formData.type,
        lowStockThreshold: formData.lowStockThreshold,
        reorderPoint: formData.reorderPoint ?? undefined,
        isActive: formData.isActive,
      };

      // Keep backend compatibility while frontend migrates away from deprecated naming.
      const updateData = {
        ...baseUpdateData,
        [LEGACY_CURRENT_BRANCH_FIELD]: formData.branchQuantity,
      } as UpdateProductRequest;

      await updateProduct(product.id, updateData);
    } else {
      // إنشاء منتج جديد
      const createData: CreateProductRequest = {
        name: formData.name,
        nameEn: formData.nameEn,
        description: formData.description,
        sku: formData.sku,
        barcode: formData.barcode,
        price: formData.price,
        cost: formData.cost,
        taxRate: formData.taxRate ?? undefined,
        taxInclusive: formData.taxInclusive,
        imageUrl: formData.imageUrl,
        categoryId: formData.categoryId,
        type: formData.type,
        initialBranchStock: formData.branchQuantity,
        lowStockThreshold: formData.lowStockThreshold,
        reorderPoint: formData.reorderPoint ?? undefined,
        branchStockQuantities: useBranchSpecificStock
          ? Object.fromEntries(
              Object.entries(branchStocks).map(([key, value]) => [
                key,
                typeof value === "string" ? parseInt(value) || 0 : value,
              ]),
            )
          : undefined,
      };
      await createProduct(createData);
    }

    onClose();
  };

  const isLoading = isCreating || isUpdating;

  return (
    <Modal
      isOpen={true}
      onClose={onClose}
      title={isEditing ? "تعديل المنتج" : "إضافة منتج جديد"}
      size="xl"
    >
      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="space-y-4">
          <h3 className="text-sm font-semibold text-gray-700 border-b pb-2">
            المعلومات الأساسية
          </h3>

          <div className="grid grid-cols-2 gap-4">
            <Input
              label="اسم المنتج (عربي) *"
              value={formData.name}
              onChange={(e) =>
                setFormData({ ...formData, name: e.target.value })
              }
              required
            />
            <Input
              label="اسم المنتج (إنجليزي)"
              value={formData.nameEn}
              onChange={(e) =>
                setFormData({ ...formData, nameEn: e.target.value })
              }
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              الوصف
            </label>
            <textarea
              value={formData.description}
              onChange={(e) =>
                setFormData({ ...formData, description: e.target.value })
              }
              className="w-full px-4 py-2.5 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
              rows={2}
              placeholder="وصف المنتج (اختياري)"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              التصنيف *
            </label>
            <div className="relative">
              <select
                value={formData.categoryId}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    categoryId: Number(e.target.value),
                  })
                }
                className="appearance-none w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white cursor-pointer hover:border-gray-400 transition-all duration-200 text-gray-700 font-medium shadow-sm"
                required
              >
                <option value="">اختر التصنيف</option>
                {categories.map((cat) => (
                  <option key={cat.id} value={cat.id}>
                    {cat.name}
                  </option>
                ))}
              </select>
              <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              نوع المنتج *
            </label>
            <div className="grid grid-cols-2 gap-3">
              <button
                type="button"
                onClick={() =>
                  setFormData({ ...formData, type: ProductType.Physical })
                }
                className={clsx(
                  "flex items-center gap-2 px-4 py-3 rounded-lg border-2 transition-all",
                  formData.type === ProductType.Physical
                    ? "border-primary-500 bg-primary-50 text-primary-700"
                    : "border-gray-200 hover:border-gray-300 text-gray-700",
                )}
              >
                <Package className="w-5 h-5" />
                <div className="text-right">
                  <div className="font-medium">منتج مادي</div>
                  <div className="text-xs opacity-75">يتم تتبع المخزون</div>
                </div>
              </button>

              <button
                type="button"
                onClick={() =>
                  setFormData({ ...formData, type: ProductType.Service })
                }
                className={clsx(
                  "flex items-center gap-2 px-4 py-3 rounded-lg border-2 transition-all",
                  formData.type === ProductType.Service
                    ? "border-secondary-500 bg-secondary-50 text-secondary-700"
                    : "border-gray-200 hover:border-gray-300 text-gray-700",
                )}
              >
                <Wrench className="w-5 h-5" />
                <div className="text-right">
                  <div className="font-medium">خدمة</div>
                  <div className="text-xs opacity-75">بدون مخزون</div>
                </div>
              </button>
            </div>
            <p className="text-xs text-gray-500 mt-2">
              {formData.type === ProductType.Physical
                ? "المنتجات المادية يتم تتبع مخزونها تلقائياً"
                : "الخدمات لا تحتاج لتتبع مخزون"}
            </p>
          </div>
        </div>

        {/* Icon Picker */}
        <div className="space-y-4">
          <h3 className="text-sm font-semibold text-gray-700 border-b pb-2">
            الأيقونة
          </h3>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">
              أيقونة المنتج
            </label>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => setShowIconPicker(!showIconPicker)}
                className="px-4 py-2 border border-gray-300 rounded-lg hover:bg-gray-50 flex items-center gap-2"
              >
                {formData.imageUrl ? (
                  isImageSource(formData.imageUrl) ? (
                    <img
                      src={formData.imageUrl}
                      alt="product-icon"
                      className="w-7 h-7 rounded object-cover"
                    />
                  ) : (
                    <span className="text-2xl">{formData.imageUrl}</span>
                  )
                ) : (
                  <ImageIcon className="w-5 h-5 text-gray-400" />
                )}
                <span className="text-sm">اختر أيقونة</span>
              </button>
              {formData.imageUrl && (
                <button
                  type="button"
                  onClick={() => setFormData({ ...formData, imageUrl: "" })}
                  className="px-3 py-2 text-red-600 hover:bg-red-50 rounded-lg"
                >
                  <X className="w-5 h-5" />
                </button>
              )}
            </div>

            {showIconPicker && (
              <div className="mt-2 p-3 border border-gray-200 rounded-lg bg-gray-50 grid grid-cols-10 gap-2 max-h-48 overflow-y-auto">
                {PRODUCT_ICONS.map((icon) => (
                  <button
                    key={icon}
                    type="button"
                    onClick={() => {
                      setFormData({ ...formData, imageUrl: icon });
                      setShowIconPicker(false);
                    }}
                    className={clsx(
                      "text-2xl p-2 rounded hover:bg-white transition-colors",
                      formData.imageUrl === icon &&
                        "bg-primary-100 ring-2 ring-primary-500",
                    )}
                  >
                    {icon}
                  </button>
                ))}
              </div>
            )}

            <Input
              label="أيقونة مخصصة (إيموجي أو رابط صورة)"
              value={formData.imageUrl}
              onChange={(e) =>
                setFormData({ ...formData, imageUrl: e.target.value })
              }
              placeholder="مثال: 🧃 أو https://example.com/icon.png"
            />
          </div>
        </div>

        {/* Pricing */}
        <div className="space-y-4">
          <h3 className="text-sm font-semibold text-gray-700 border-b pb-2">
            التسعير والضرائب
          </h3>

          <div className="grid grid-cols-2 gap-4">
            <Input
              label="سعر البيع *"
              type="number"
              min="0"
              step="0.01"
              value={numberToDisplay(formData.price)}
              onChange={(e) =>
                setFormData({
                  ...formData,
                  price: displayToNumber(e.target.value),
                })
              }
              placeholder="0.00"
              required
            />
            <Input
              label="سعر التكلفة"
              type="number"
              min="0"
              step="0.01"
              value={numberToDisplay(formData.cost)}
              onChange={(e) =>
                setFormData({
                  ...formData,
                  cost: displayToNumber(e.target.value),
                })
              }
              placeholder="0.00"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <Input
                label="معدل الضريبة (%)"
                type="number"
                min="0"
                max="100"
                step="0.01"
                value={formData.taxRate ?? ""}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    taxRate: e.target.value ? parseFloat(e.target.value) : null,
                  })
                }
                placeholder="استخدام الافتراضي"
              />
              <p className="text-xs text-gray-500 mt-1">
                اتركه فارغاً لاستخدام معدل الفرع الافتراضي
              </p>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">
                السعر شامل الضريبة؟
              </label>
              <div className="flex gap-4 mt-2">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    checked={formData.taxInclusive}
                    onChange={() =>
                      setFormData({ ...formData, taxInclusive: true })
                    }
                    className="w-4 h-4 text-primary-600"
                  />
                  <span className="text-sm">نعم (شامل)</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    checked={!formData.taxInclusive}
                    onChange={() =>
                      setFormData({ ...formData, taxInclusive: false })
                    }
                    className="w-4 h-4 text-primary-600"
                  />
                  <span className="text-sm">لا (غير شامل)</span>
                </label>
              </div>
            </div>
          </div>
        </div>

        {/* Codes */}
        <div className="space-y-4">
          <h3 className="text-sm font-semibold text-gray-700 border-b pb-2">
            الأكواد
          </h3>

          <div className="grid grid-cols-2 gap-4">
            <Input
              label="SKU"
              value={formData.sku}
              onChange={(e) =>
                setFormData({ ...formData, sku: e.target.value })
              }
              placeholder="كود المنتج"
            />
            <Input
              label="الباركود"
              value={formData.barcode}
              onChange={(e) =>
                setFormData({ ...formData, barcode: e.target.value })
              }
              placeholder="رقم الباركود"
            />
          </div>
        </div>

        {/* Inventory - Only for Physical Products */}
        {formData.type === ProductType.Physical && (
          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-gray-700 border-b pb-2 flex items-center gap-2">
              <Package className="w-4 h-4" />
              المخزون
            </h3>

            {!isEditing && (
              <div className="space-y-3">
                <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
                  <div className="flex items-start gap-3">
                    <div className="flex-shrink-0 w-8 h-8 bg-blue-100 rounded-full flex items-center justify-center">
                      <Package className="w-4 h-4 text-blue-600" />
                    </div>
                    <div className="flex-1">
                      <h4 className="text-sm font-semibold text-blue-900 mb-1">
                        توزيع المخزون على الفروع
                      </h4>
                      <p className="text-xs text-blue-700 mb-3">
                        الكمية المدخلة ستُضاف للفرع الحالي فقط. الفروع الأخرى
                        ستبدأ بصفر.
                      </p>
                      <div className="flex items-center gap-2">
                        <input
                          type="checkbox"
                          id="branchSpecific"
                          checked={useBranchSpecificStock}
                          onChange={(e) =>
                            setUseBranchSpecificStock(e.target.checked)
                          }
                          className="w-4 h-4 text-primary-600 rounded"
                        />
                        <label
                          htmlFor="branchSpecific"
                          className="text-sm font-medium text-blue-900 cursor-pointer"
                        >
                          تحديد كمية مخصصة لكل فرع
                        </label>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {!useBranchSpecificStock ? (
              <div className="grid grid-cols-3 gap-4">
                <Input
                  label="الكمية المتاحة *"
                  type="number"
                  min="0"
                  value={numberToDisplay(formData.branchQuantity)}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      branchQuantity: displayToNumber(e.target.value),
                    })
                  }
                  placeholder="0"
                  required
                />
                <Input
                  label="حد التنبيه"
                  type="number"
                  min="0"
                  value={formData.lowStockThreshold || ""}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      lowStockThreshold: parseInt(e.target.value) || 5,
                    })
                  }
                  placeholder="5"
                />
                <Input
                  label="نقطة إعادة الطلب"
                  type="number"
                  min="0"
                  value={formData.reorderPoint ?? ""}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      reorderPoint: e.target.value
                        ? parseInt(e.target.value)
                        : null,
                    })
                  }
                  placeholder="اختياري"
                />
              </div>
            ) : (
              <div className="space-y-3">
                <p className="text-sm text-gray-600">
                  حدد الكمية المبدئية لكل فرع:
                </p>
                <div className="grid grid-cols-2 gap-3 max-h-48 overflow-y-auto p-3 bg-gray-50 rounded-lg">
                  {branches?.data?.map((branch) => (
                    <div key={branch.id} className="flex items-center gap-2">
                      <label className="text-sm font-medium text-gray-700 flex-1">
                        {branch.name}
                      </label>
                      <input
                        type="number"
                        min="0"
                        value={branchStocks[branch.id] || ""}
                        onChange={(e) =>
                          setBranchStocks({
                            ...branchStocks,
                            [branch.id]: parseInt(e.target.value) || 0,
                          })
                        }
                        placeholder="0"
                        className="w-24 px-3 py-1.5 border border-gray-300 rounded-lg text-sm"
                      />
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}

        {/* Actions */}
        <div className="flex gap-3 pt-4 border-t">
          <Button
            type="button"
            variant="secondary"
            onClick={onClose}
            className="flex-1"
          >
            إلغاء
          </Button>
          <Button
            type="submit"
            variant="primary"
            isLoading={isLoading}
            className="flex-1"
          >
            {isEditing ? "حفظ التغييرات" : "إضافة المنتج"}
          </Button>
        </div>
      </form>
    </Modal>
  );
};
