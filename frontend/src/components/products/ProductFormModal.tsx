import { useState, useEffect, useMemo } from "react";
import {
  X,
  Image as ImageIcon,
  Package,
  Wrench,
  ChefHat,
  ChevronDown,
  ToggleRight,
  ToggleLeft,
  Eye,
  AlertTriangle,
  Scale,
} from "lucide-react";
import {
  Product,
  CreateProductRequest,
  UpdateProductRequest,
  ProductType,
  UnitOfMeasure,
} from "@/types/product.types";
import { useProducts, useCategories } from "@/hooks/useProducts";
import { useGetBranchesQuery } from "@/api/branchesApi";
import { useGetBranchInventoryQuery } from "@/api/inventoryApi";
import { useGetBatchesByProductQuery } from "@/api/productBatchApi";
import { useGetRecipeByProductIdQuery } from "@/api/recipesApi";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentBranch } from "@/store/slices/branchSlice";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import { numberToDisplay, displayToNumber } from "@/hooks/useNumberInput";
import clsx from "clsx";

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

const RAW_MATERIAL_UNITS = [
  { value: UnitOfMeasure.Kilogram, label: "كيلوجرام" },
  { value: UnitOfMeasure.Gram, label: "جرام" },
  { value: UnitOfMeasure.Liter, label: "لتر" },
  { value: UnitOfMeasure.Milliliter, label: "ملليلتر" },
  { value: UnitOfMeasure.Piece, label: "قطعة" },
  { value: UnitOfMeasure.Portion, label: "حصة" },
] as const;

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

  // جلب الباتشات للمنتج للتحقق من وجودها
  const { data: productBatchesResponse } = useGetBatchesByProductQuery(
    {
      productId: product?.id ?? 0,
      branchId: currentBranch?.id,
    },
    {
      skip: !product?.id || !currentBranch?.id || !isEditing,
    },
  );

  const productBatches = productBatchesResponse?.data ?? [];
  const hasActiveBatches = productBatches.length > 0;
  const nextBatch = productBatches.find((b) => b.status === "Active");
  const { data: recipeResponse } = useGetRecipeByProductIdQuery(
    product?.id ?? 0,
    {
      skip: !product?.id || !isEditing,
    },
  );
  const productRecipe = recipeResponse?.data ?? null;
  const hasRecipeCostSource =
    !!productRecipe && productRecipe.yieldQuantity > 0;
  const recipeUnitCost = hasRecipeCostSource
    ? productRecipe.totalCost / productRecipe.yieldQuantity
    : 0;
  const recipeUnitCostDisplay = hasRecipeCostSource
    ? recipeUnitCost.toFixed(2)
    : "لا توجد وصفة نشطة";
  const hasRecipeWithZeroCost =
    hasRecipeCostSource &&
    recipeUnitCost === 0 &&
    (productRecipe?.ingredients.length ?? 0) > 0;
  const isCostInputDisabled =
    isEditing && (hasActiveBatches || hasRecipeCostSource);

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
    price: product?.suggestedPrice || product?.price || 0, // استخدم suggestedPrice إذا كان موجوداً
    cost: nextBatch?.costPrice || product?.cost || 0, // استخدم تكلفة الباتش إذا كان موجوداً
    taxRate: product?.taxRate ?? null,
    taxInclusive: product?.taxInclusive ?? false,
    imageUrl: product?.imageUrl || "",
    categoryId: product?.categoryId || categories[0]?.id || 0,
    type: product?.type || ProductType.Physical,
    unit: product?.unit || UnitOfMeasure.Piece,
    isBatchTracked: product?.isBatchTracked ?? false,
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
  const isRawMaterial = formData.type === ProductType.RawMaterial;
  const managesDirectStock =
    formData.type === ProductType.Physical ||
    formData.type === ProductType.RawMaterial;

  useEffect(() => {
    if (isRawMaterial && !RAW_MATERIAL_UNITS.some((unit) => unit.value === formData.unit)) {
      setFormData((prev) => ({ ...prev, unit: UnitOfMeasure.Kilogram }));
      return;
    }

    if (!managesDirectStock && (formData.isBatchTracked || formData.branchQuantity !== 0)) {
      setFormData((prev) => ({
        ...prev,
        isBatchTracked: false,
        branchQuantity: 0,
      }));
    }

    if (isRawMaterial && formData.price !== 0) {
      setFormData((prev) => ({ ...prev, price: 0 }));
    }
  }, [
    formData.branchQuantity,
    formData.isBatchTracked,
    formData.price,
    formData.type,
    formData.unit,
    isRawMaterial,
    managesDirectStock,
  ]);

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
        price: isRawMaterial ? 0 : formData.price,
        cost: formData.cost,
        taxRate: formData.taxRate ?? undefined,
        taxInclusive: false,
        imageUrl: formData.imageUrl,
        categoryId: formData.categoryId,
        type: formData.type,
        unit: isRawMaterial ? formData.unit : UnitOfMeasure.Piece,
        isBatchTracked: managesDirectStock ? formData.isBatchTracked : false,
        lowStockThreshold: formData.lowStockThreshold,
        reorderPoint: formData.reorderPoint ?? undefined,
        isActive: formData.isActive,
      };

      await updateProduct(product.id, baseUpdateData as UpdateProductRequest);
    } else {
      // إنشاء منتج جديد
      const createData: CreateProductRequest = {
        name: formData.name,
        nameEn: formData.nameEn,
        description: formData.description,
        sku: formData.sku,
        barcode: formData.barcode,
        price: isRawMaterial ? 0 : formData.price,
        cost: formData.cost,
        taxRate: formData.taxRate ?? undefined,
        taxInclusive: false,
        imageUrl: formData.imageUrl,
        categoryId: formData.categoryId,
        type: formData.type,
        unit: isRawMaterial ? formData.unit : UnitOfMeasure.Piece,
        isBatchTracked: managesDirectStock ? formData.isBatchTracked : false,
        initialBranchStock: managesDirectStock ? formData.branchQuantity : 0,
        lowStockThreshold: formData.lowStockThreshold,
        reorderPoint: formData.reorderPoint ?? undefined,
        branchStockQuantities: managesDirectStock && useBranchSpecificStock
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
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <button
                type="button"
                onClick={() =>
                  setFormData((prev) => ({
                    ...prev,
                    type: ProductType.Physical,
                  }))
                }
                className={clsx(
                  "flex items-center gap-2 rounded-lg border-2 px-4 py-3 transition-all",
                  formData.type === ProductType.Physical
                    ? "border-primary-500 bg-primary-50 text-primary-700"
                    : "border-gray-200 text-gray-700 hover:border-gray-300",
                )}
              >
                <Package className="h-5 w-5" />
                <div className="text-right">
                  <div className="font-medium">منتج مادي</div>
                  <div className="text-xs opacity-75">يباع ويُخصم من مخزونه</div>
                </div>
              </button>

              <button
                type="button"
                onClick={() =>
                  setFormData((prev) => ({
                    ...prev,
                    type: ProductType.Manufactured,
                    isBatchTracked: false,
                    branchQuantity: 0,
                  }))
                }
                className={clsx(
                  "flex items-center gap-2 rounded-lg border-2 px-4 py-3 transition-all",
                  formData.type === ProductType.Manufactured
                    ? "border-secondary-500 bg-secondary-50 text-secondary-700"
                    : "border-gray-200 text-gray-700 hover:border-gray-300",
                )}
              >
                <ChefHat className="h-5 w-5" />
                <div className="text-right">
                  <div className="font-medium">منتج مصنّع</div>
                  <div className="text-xs opacity-75">يباع من الوصفة وليس من مخزون مباشر</div>
                </div>
              </button>

              <button
                type="button"
                onClick={() =>
                  setFormData((prev) => ({
                    ...prev,
                    type: ProductType.RawMaterial,
                    unit: RAW_MATERIAL_UNITS.some(
                      (unit) => unit.value === prev.unit,
                    )
                      ? prev.unit
                      : UnitOfMeasure.Kilogram,
                    price: 0,
                  }))
                }
                className={clsx(
                  "flex items-center gap-2 rounded-lg border-2 px-4 py-3 transition-all",
                  formData.type === ProductType.RawMaterial
                    ? "border-amber-500 bg-amber-50 text-amber-700"
                    : "border-gray-200 text-gray-700 hover:border-gray-300",
                )}
              >
                <Scale className="h-5 w-5" />
                <div className="text-right">
                  <div className="font-medium">مادة خام</div>
                  <div className="text-xs opacity-75">مخزون داخلي للوصفات فقط</div>
                </div>
              </button>

              <button
                type="button"
                onClick={() =>
                  setFormData((prev) => ({
                    ...prev,
                    type: ProductType.Service,
                    isBatchTracked: false,
                    branchQuantity: 0,
                  }))
                }
                className={clsx(
                  "flex items-center gap-2 rounded-lg border-2 px-4 py-3 transition-all",
                  formData.type === ProductType.Service
                    ? "border-gray-500 bg-gray-50 text-gray-700"
                    : "border-gray-200 text-gray-700 hover:border-gray-300",
                )}
              >
                <Wrench className="h-5 w-5" />
                <div className="text-right">
                  <div className="font-medium">خدمة</div>
                  <div className="text-xs opacity-75">تباع بدون مخزون</div>
                </div>
              </button>
            </div>
            <p className="mt-2 text-xs text-gray-500">
              {formData.type === ProductType.Physical &&
                "المنتجات المادية تُراجع على المخزون وتُخصم عند البيع."}
              {formData.type === ProductType.Manufactured &&
                "هذا المنتج له وصفة - مخزونه يُحسب من المكونات."}
              {formData.type === ProductType.RawMaterial &&
                "المادة الخام لا تظهر في الكاشير وتُستخدم داخل الوصفات فقط."}
              {formData.type === ProductType.Service &&
                "الخدمات تباع بدون تتبع مخزون."}
            </p>
          </div>

          {managesDirectStock && (
            <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
              <div>
                <p className="font-medium text-sm">تتبع دفعات المخزون</p>
                <p className="text-xs text-gray-500">
                  {formData.isBatchTracked
                    ? "يتم تتبع الدفعات والصلاحية (FEFO)"
                    : "لا يتم تتبع الدفعات (FIFO)"}
                </p>
              </div>
              <button
                type="button"
                onClick={() =>
                  setFormData({
                    ...formData,
                    isBatchTracked: !formData.isBatchTracked,
                  })
                }
                className={clsx(
                  "p-2 rounded-lg transition-colors",
                  formData.isBatchTracked
                    ? "bg-success-100 text-success-600"
                    : "bg-gray-200 text-gray-500",
                )}
              >
                {formData.isBatchTracked ? (
                  <ToggleRight className="w-7 h-7" />
                ) : (
                  <ToggleLeft className="w-7 h-7" />
                )}
              </button>
            </div>
          )}

          {isEditing && product && (
            <div className="flex justify-end">
              <a
                href={`/product-batches?productId=${product.id}`}
                className="text-sm text-primary-600 hover:underline inline-flex items-center gap-1"
                onClick={(e) => {
                  e.preventDefault();
                  window.location.href = `/product-batches?productId=${product.id}`;
                }}
              >
                <Eye className="w-4 h-4" />
                عرض دفعات هذا المنتج
              </a>
            </div>
          )}
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

          {/* تنبيه للمنتجات التي لها باتشات */}
          {isEditing && hasActiveBatches && (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-4">
              <div className="flex items-start gap-3">
                <div className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full bg-amber-100">
                  <AlertTriangle className="h-5 w-5 text-amber-700" />
                </div>
                <div className="flex-1 space-y-2">
                  <h4 className="text-sm font-semibold text-amber-900">
                    هذا المنتج له دفعات مخزون (Batches)
                  </h4>
                  <p className="text-xs leading-relaxed text-amber-800">
                    السعر المعروض أدناه هو <strong>سعر الباتش الحالي</strong> ({numberToDisplay(product.suggestedPrice)} ج.م).
                    لتغيير السعر، قم بتعديل سعر الباتش من شاشة إدارة الدفعات.
                    السعر الأساسي للمنتج هو {numberToDisplay(product.price)} ج.م.
                  </p>
                  {nextBatch && (
                    <div className="mt-2 rounded-lg bg-white p-3 border border-amber-200">
                      <p className="text-xs font-medium text-gray-700 mb-1">
                        الدفعة التي ستُستخدم في البيع القادم:
                      </p>
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-sm font-semibold text-gray-900">
                            {nextBatch.batchNumber || "بدون رقم"}
                          </p>
                          {nextBatch.expiryDate && (
                            <p className="text-xs text-gray-500">
                              ينتهي:{" "}
                              {new Date(nextBatch.expiryDate).toLocaleDateString(
                                "ar-EG",
                              )}
                            </p>
                          )}
                        </div>
                        <div className="text-left">
                          <p className="text-lg font-bold text-green-600">
                            {numberToDisplay(nextBatch.sellingPrice)} ج.م
                          </p>
                          <p className="text-xs text-gray-500">سعر البيع</p>
                        </div>
                      </div>
                    </div>
                  )}
                  <p className="text-xs text-amber-700 mt-2">
                    💡 <strong>للتحكم في الأسعار:</strong> قم بتعديل أسعار
                    الدفعات من{" "}
                    <a
                      href={`/product-batches?productId=${product?.id}`}
                      className="underline hover:text-amber-900 font-medium"
                      onClick={(e) => {
                        e.preventDefault();
                        window.location.href = `/product-batches?productId=${product?.id}`;
                      }}
                    >
                      شاشة إدارة الدفعات
                    </a>
                  </p>
                </div>
              </div>
            </div>
          )}

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {!isRawMaterial && (
              <div>
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
                  disabled={isEditing && hasActiveBatches}
                />
                {isEditing && hasActiveBatches && (
                  <p className="mt-1 flex items-center gap-1 text-xs text-amber-600">
                    <AlertTriangle className="h-3 w-3" />
                    معطّل لأن المنتج له دفعات مخزون
                  </p>
                )}
              </div>
            )}

            <div>
              <Input
                label="سعر التكلفة اليدوي"
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
                disabled={isCostInputDisabled}
              />
              <div className="mt-3">
                <Input
                  label="التكلفة المحسوبة من الوصفة"
                  type="text"
                  value={recipeUnitCostDisplay}
                  readOnly
                  disabled
                />
              </div>
              {hasRecipeWithZeroCost && (
                <p className="mt-1 text-xs leading-relaxed text-amber-600">
                  الوصفة موجودة، لكن تكلفة مكوناتها صفر. حدّث تكلفة المواد
                  الخام من المنتج أو من فاتورة شراء حتى تظهر تكلفة الوصفة.
                </p>
              )}
              <p className="mt-2 text-xs leading-relaxed text-gray-500">
                المستخدم حالياً:{" "}
                <strong className={hasRecipeCostSource ? "text-blue-700" : "text-green-700"}>
                  {hasRecipeCostSource ? "التكلفة المحسوبة من الوصفة" : "سعر التكلفة اليدوي"}
                </strong>
                . لاستخدام الوصفة أضف أو فعّل وصفة للمنتج. لاستخدام اليدوي
                عطّل أو احذف الوصفة.
              </p>
              {isEditing && hasActiveBatches && !hasRecipeCostSource && (
                <p className="mt-1 flex items-center gap-1 text-xs text-amber-600">
                  <AlertTriangle className="h-3 w-3" />
                  معطّل لأن المنتج له دفعات مخزون
                </p>
              )}
            </div>

            {isRawMaterial && (
              <div className="md:col-span-2">
                <label className="mb-1.5 block text-sm font-medium text-gray-700">
                  وحدة القياس *
                </label>
                <div className="relative">
                  <select
                    value={formData.unit}
                    onChange={(e) =>
                      setFormData((prev) => ({
                        ...prev,
                        unit: Number(e.target.value) as UnitOfMeasure,
                      }))
                    }
                    className="w-full appearance-none rounded-xl border border-gray-300 bg-white px-4 py-2.5 pl-10 pr-4 text-gray-700 shadow-sm transition-all duration-200 hover:border-gray-400 focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500"
                    required
                  >
                    {RAW_MATERIAL_UNITS.map((unit) => (
                      <option key={unit.value} value={unit.value}>
                        {unit.label}
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="pointer-events-none absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Codes */}
        <div className="space-y-4">
          <h3 className="text-sm font-semibold text-gray-700 border-b pb-2">
            الأكواد
          </h3>

          <div className="grid grid-cols-1 gap-4">
            {/* SKU مخفي حالياً - غير مفيد بدون Product Variants */}
            {/* <Input
              label="SKU"
              value={formData.sku}
              onChange={(e) =>
                setFormData({ ...formData, sku: e.target.value })
              }
              placeholder="كود المنتج"
            /> */}
            <div>
              <Input
                label="الباركود"
                value={formData.barcode}
                onChange={(e) =>
                  setFormData({ ...formData, barcode: e.target.value })
                }
                placeholder="رقم الباركود (يمكن المسح بجهاز الباركود)"
                autoComplete="off"
              />
              <p className="mt-1.5 text-xs text-gray-500 flex items-center gap-1.5">
                <Package className="w-3.5 h-3.5" />
                يمكنك استخدام جهاز الباركود للمسح المباشر في هذا الحقل
              </p>
            </div>
          </div>
        </div>

        {/* Inventory - Only for stocked products */}
        {managesDirectStock && (
          <div className="space-y-4">
            <h3 className="text-sm font-semibold text-gray-700 border-b pb-2 flex items-center gap-2">
              <Package className="w-4 h-4" />
              المخزون
            </h3>

            {isEditing ? (
              <div className="space-y-4">
                <div className="rounded-xl border border-amber-200 bg-amber-50 p-4">
                  <div className="flex items-start gap-3">
                    <div className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full bg-amber-100">
                      <Eye className="h-4 w-4 text-amber-700" />
                    </div>
                    <div className="space-y-1">
                      <h4 className="text-sm font-semibold text-amber-900">
                        تعديل الكمية يتم من شاشة المخزون
                      </h4>
                      <p className="text-xs leading-6 text-amber-800">
                        كمية المنتج الحالية أصبحت مرتبطة بمخزون الفرع. استخدم
                        حركات المخزون أو شاشة الجرد لتعديل الكمية الفعلية.
                      </p>
                    </div>
                  </div>
                </div>

                <div className="grid gap-4 md:grid-cols-3">
                  <div className="rounded-xl border border-gray-200 bg-gray-50 p-4">
                    <p className="text-xs font-medium text-gray-500">
                      الكمية الحالية في الفرع
                    </p>
                    <p className="mt-2 text-2xl font-semibold text-gray-900">
                      {numberToDisplay(currentBranchQuantity)}
                    </p>
                    {currentBranch?.name && (
                      <p className="mt-1 text-xs text-gray-500">
                        {currentBranch.name}
                      </p>
                    )}
                  </div>

                  <div className="grid gap-4 md:col-span-2 md:grid-cols-2">
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
                </div>
              </div>
            ) : (
              <>
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
              </>
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
