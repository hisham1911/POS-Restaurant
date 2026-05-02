import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import {
  useCreateProductBatchMutation,
  useUpdateProductBatchMutation,
} from "@/api/productBatchApi";
import { useGetProductsQuery } from "@/api/productsApi";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import type { ApiResponse } from "@/types/api.types";
import type {
  CreateProductBatchRequest,
  ProductBatch,
} from "@/types/productBatch.types";

interface ProductBatchFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  productId?: number;
  batch?: ProductBatch | null;
}

const toDateInputValue = (value?: string) =>
  value ? new Date(value).toISOString().split("T")[0] : "";

const createInitialState = (
  productId?: number,
  batch?: ProductBatch | null,
): CreateProductBatchRequest => ({
  productId: batch?.productId ?? productId ?? 0,
  batchNumber: batch?.batchNumber ?? "",
  quantity: batch?.quantity ?? 1,
  expiryDate: toDateInputValue(batch?.expiryDate),
  productionDate: toDateInputValue(batch?.productionDate),
  costPrice: batch?.costPrice,
  sellingPrice: batch?.sellingPrice,
  supplierName: batch?.supplierName ?? "",
  notes: batch?.notes ?? "",
});

export const ProductBatchFormModal = ({
  isOpen,
  onClose,
  onSuccess,
  productId,
  batch,
}: ProductBatchFormModalProps) => {
  const isEditMode = Boolean(batch);
  const [formData, setFormData] = useState<CreateProductBatchRequest>(
    createInitialState(productId, batch),
  );

  const { data: productsResponse } = useGetProductsQuery({
    page: 1,
    pageSize: 200,
    isActive: true,
  });
  const batchTrackedProducts = useMemo(
    () =>
      (productsResponse?.data?.items ?? []).filter(
        (product) => product.trackInventory && product.isBatchTracked,
      ),
    [productsResponse?.data?.items],
  );

  const [createProductBatch, { isLoading }] = useCreateProductBatchMutation();
  const [updateProductBatch, { isLoading: isUpdating }] =
    useUpdateProductBatchMutation();

  useEffect(() => {
    if (!isOpen) return;
    setFormData(createInitialState(productId, batch));
  }, [batch, isOpen, productId]);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();

    try {
      if (isEditMode && batch) {
        await updateProductBatch({
          id: batch.id,
          data: {
            batchNumber: formData.batchNumber.trim(),
            expiryDate: formData.expiryDate,
            productionDate: formData.productionDate || undefined,
            sellingPrice: formData.sellingPrice,
            notes: formData.notes?.trim() || undefined,
          },
        }).unwrap();
      } else {
        await createProductBatch({
          ...formData,
          batchNumber: formData.batchNumber.trim(),
          productionDate: formData.productionDate || undefined,
          supplierName: formData.supplierName?.trim() || undefined,
          notes: formData.notes?.trim() || undefined,
        }).unwrap();
      }

      toast.success(isEditMode ? "تم تحديث الدفعة بنجاح" : "تم إنشاء الدفعة بنجاح");
      onSuccess();
      onClose();
    } catch (error) {
      const apiError = error as { data?: ApiResponse<unknown> };
      switch (apiError.data?.errorCode) {
        case "BATCH_NUMBER_DUPLICATE":
          toast.error("رقم الدفعة مستخدم بالفعل لهذا المنتج");
          break;
        case "PRODUCT_NOT_FOUND":
          toast.error("المنتج المحدد غير موجود");
          break;
        default:
          toast.error(isEditMode ? "تعذر تحديث الدفعة" : "تعذر إنشاء الدفعة");
          break;
      }
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={isEditMode ? "تعديل الدفعة" : "إضافة دفعة جديدة"}
      size="lg"
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-gray-700">
            المنتج
          </label>
          <select
            value={formData.productId}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                productId: Number(event.target.value),
              }))
            }
            className="w-full rounded-lg border border-gray-300 px-4 py-2.5 focus:border-transparent focus:ring-2 focus:ring-primary-500"
            required
            disabled={productId !== undefined || isEditMode}
          >
            <option value={0}>اختر المنتج</option>
            {batchTrackedProducts.map((product) => (
              <option key={product.id} value={product.id}>
                {product.name}
              </option>
            ))}
          </select>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Input
            label="رقم الدفعة"
            value={formData.batchNumber}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                batchNumber: event.target.value,
              }))
            }
            placeholder="BATCH-001"
            required
          />

          <Input
            label="تاريخ الصلاحية"
            type="date"
            value={formData.expiryDate}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                expiryDate: event.target.value,
              }))
            }
            required
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-3">
          <Input
            label="الكمية"
            type="number"
            min="1"
            value={formData.quantity}
            disabled={isEditMode}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                quantity: Number(event.target.value) || 0,
              }))
            }
            required
          />

          <Input
            label="سعر التكلفة"
            type="number"
            min="0"
            step="0.01"
            value={formData.costPrice ?? ""}
            disabled={isEditMode}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                costPrice: event.target.value
                  ? Number(event.target.value)
                  : undefined,
              }))
            }
          />

          <Input
            label="سعر البيع"
            type="number"
            min="0"
            step="0.01"
            value={formData.sellingPrice ?? ""}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                sellingPrice: event.target.value
                  ? Number(event.target.value)
                  : undefined,
              }))
            }
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Input
            label="تاريخ الإنتاج"
            type="date"
            value={formData.productionDate ?? ""}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                productionDate: event.target.value,
              }))
            }
          />

          <Input
            label="اسم المورد"
            value={formData.supplierName ?? ""}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                supplierName: event.target.value,
              }))
            }
          />
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-gray-700">
            ملاحظات
          </label>
          <textarea
            value={formData.notes ?? ""}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                notes: event.target.value,
              }))
            }
            rows={3}
            className="w-full rounded-lg border border-gray-300 px-4 py-2.5 focus:border-transparent focus:ring-2 focus:ring-primary-500"
            placeholder="ملاحظات اختيارية"
          />
        </div>

        <div className="flex gap-3 pt-4">
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
            isLoading={isLoading || isUpdating}
            className="flex-1"
          >
            {isEditMode ? "حفظ التعديلات" : "إنشاء الدفعة"}
          </Button>
        </div>
      </form>
    </Modal>
  );
};

export default ProductBatchFormModal;
