import { FormEvent, useEffect, useState } from "react";
import clsx from "clsx";
import { FolderOpen, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { useCreateCategoryMutation, useUpdateCategoryMutation } from "@/api/categoriesApi";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import type { ApiResponse } from "@/types/api.types";
import type { Category } from "@/types/category.types";

interface CategoryFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  category?: Category;
}

interface CategoryFormState {
  name: string;
  nameEn: string;
  description: string;
  imageUrl: string;
  sortOrder: number;
  isActive: boolean;
}

const CATEGORY_ICONS = [
  "🛒",
  "🥤",
  "🍞",
  "🥩",
  "🥦",
  "🍫",
  "🧴",
  "🧼",
  "🧻",
  "🧃",
  "🥫",
  "🍚",
  "🧂",
  "☕",
  "🍗",
  "🍟",
  "🧀",
  "🥛",
  "🍎",
  "🧺",
];

const createInitialState = (category?: Category): CategoryFormState => ({
  name: category?.name ?? "",
  nameEn: category?.nameEn ?? "",
  description: category?.description ?? "",
  imageUrl: category?.imageUrl ?? "",
  sortOrder: category?.sortOrder ?? 0,
  isActive: category?.isActive ?? true,
});

const isImageSource = (value?: string): boolean => {
  if (!value) return false;
  const normalized = value.trim();
  if (!normalized) return false;
  return /^(https?:\/\/|\/|data:image\/|blob:)/i.test(normalized);
};

export const CategoryFormModal = ({
  isOpen,
  onClose,
  onSuccess,
  category,
}: CategoryFormModalProps) => {
  const [formData, setFormData] = useState<CategoryFormState>(
    createInitialState(category),
  );
  const [showIconPicker, setShowIconPicker] = useState(false);

  const [createCategory, { isLoading: isCreating }] =
    useCreateCategoryMutation();
  const [updateCategory, { isLoading: isUpdating }] =
    useUpdateCategoryMutation();

  useEffect(() => {
    if (!isOpen) return;
    setFormData(createInitialState(category));
    setShowIconPicker(false);
  }, [category, isOpen]);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();

    try {
      const payload = {
        name: formData.name.trim(),
        nameEn: formData.nameEn.trim() || undefined,
        description: formData.description.trim() || undefined,
        imageUrl: formData.imageUrl.trim() || undefined,
        sortOrder: formData.sortOrder,
        isActive: formData.isActive,
      };

      if (category) {
        await updateCategory({
          id: category.id,
          data: payload,
        }).unwrap();
        toast.success("تم تحديث التصنيف بنجاح");
      } else {
        await createCategory(payload).unwrap();
        toast.success("تم إضافة التصنيف بنجاح");
      }

      onSuccess();
      onClose();
    } catch (error) {
      const apiError = error as { data?: ApiResponse<unknown> };
      switch (apiError.data?.errorCode) {
        case "CATEGORY_NAME_DUPLICATE":
          toast.error("يوجد تصنيف بنفس الاسم بالفعل");
          break;
        case "CATEGORY_NAME_REQUIRED":
          toast.error("اسم التصنيف مطلوب ولا يتجاوز 100 حرف");
          break;
        default:
          toast.error(category ? "تعذر تحديث التصنيف" : "تعذر إنشاء التصنيف");
          break;
      }
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={category ? "تعديل التصنيف" : "إضافة تصنيف جديد"}
      size="lg"
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="اسم التصنيف (عربي)"
          value={formData.name}
          onChange={(event) =>
            setFormData((prev) => ({ ...prev, name: event.target.value }))
          }
          placeholder="مثال: المشروبات"
          required
        />

        <Input
          label="اسم التصنيف (إنجليزي)"
          value={formData.nameEn}
          onChange={(event) =>
            setFormData((prev) => ({ ...prev, nameEn: event.target.value }))
          }
          placeholder="Beverages"
        />

        <div>
          <label className="mb-1.5 block text-sm font-medium text-gray-700">
            الوصف
          </label>
          <textarea
            value={formData.description}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                description: event.target.value,
              }))
            }
            placeholder="وصف اختياري للتصنيف..."
            rows={3}
            className="w-full rounded-lg border border-gray-300 px-4 py-2.5 focus:border-transparent focus:ring-2 focus:ring-primary-500"
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Input
            label="الترتيب"
            type="number"
            min="0"
            value={formData.sortOrder}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                sortOrder: Number(event.target.value) || 0,
              }))
            }
          />

          <div className="rounded-lg border border-gray-200 px-4 py-3">
            <div className="flex items-center justify-between">
              <label className="text-sm font-medium text-gray-700">
                حالة التصنيف
              </label>
              <button
                type="button"
                onClick={() =>
                  setFormData((prev) => ({
                    ...prev,
                    isActive: !prev.isActive,
                  }))
                }
                className={clsx(
                  "relative inline-flex h-6 w-11 items-center rounded-full transition-colors",
                  formData.isActive ? "bg-primary-600" : "bg-gray-300",
                )}
              >
                <span
                  className={clsx(
                    "inline-block h-4 w-4 rounded-full bg-white transition-transform",
                    formData.isActive ? "translate-x-6" : "translate-x-1",
                  )}
                />
              </button>
            </div>
            <p className="mt-2 text-sm text-gray-500">
              {formData.isActive ? "نشط" : "معطل"}
            </p>
          </div>
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-gray-700">
            أيقونة التصنيف
          </label>

          <div className="mb-3 flex gap-2">
            <button
              type="button"
              onClick={() => setShowIconPicker((prev) => !prev)}
              className="flex items-center gap-2 rounded-lg border border-gray-300 px-4 py-2 hover:bg-gray-50"
            >
              {formData.imageUrl ? (
                isImageSource(formData.imageUrl) ? (
                  <img
                    src={formData.imageUrl}
                    alt="category-icon"
                    className="h-7 w-7 rounded object-cover"
                  />
                ) : (
                  <span className="text-2xl">{formData.imageUrl}</span>
                )
              ) : (
                <FolderOpen className="h-5 w-5 text-gray-400" />
              )}
              <span className="text-sm">اختر أيقونة</span>
            </button>

            {formData.imageUrl && (
              <button
                type="button"
                onClick={() =>
                  setFormData((prev) => ({ ...prev, imageUrl: "" }))
                }
                className="rounded-lg px-3 py-2 text-red-600 hover:bg-red-50"
              >
                <Trash2 className="h-5 w-5" />
              </button>
            )}
          </div>

          {showIconPicker && (
            <div className="mb-3 grid max-h-44 grid-cols-10 gap-2 overflow-y-auto rounded-lg border border-gray-200 bg-gray-50 p-3">
              {CATEGORY_ICONS.map((icon) => (
                <button
                  key={icon}
                  type="button"
                  onClick={() => {
                    setFormData((prev) => ({ ...prev, imageUrl: icon }));
                    setShowIconPicker(false);
                  }}
                  className={clsx(
                    "rounded p-2 text-2xl transition-colors hover:bg-white",
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
            label="أيقونة مخصصة"
            value={formData.imageUrl}
            onChange={(event) =>
              setFormData((prev) => ({
                ...prev,
                imageUrl: event.target.value,
              }))
            }
            placeholder="🥤 أو https://example.com/icon.png"
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
            isLoading={isCreating || isUpdating}
            className="flex-1"
          >
            {category ? "تحديث" : "إضافة"}
          </Button>
        </div>
      </form>
    </Modal>
  );
};
