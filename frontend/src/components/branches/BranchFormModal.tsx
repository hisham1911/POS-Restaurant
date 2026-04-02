import { useState, useEffect } from "react";
import { X, Building2 } from "lucide-react";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import {
  useCreateBranchMutation,
  useUpdateBranchMutation,
} from "@/api/branchesApi";
import { Branch } from "@/types/branch.types";
import { toast } from "react-hot-toast";
import { Portal } from "@/components/common/Portal";
import { handleApiError } from "@/utils/errorHandler";

interface BranchFormModalProps {
  branch?: Branch;
  onClose: () => void;
}

export const BranchFormModal = ({ branch, onClose }: BranchFormModalProps) => {
  const isEditMode = !!branch;

  const [formData, setFormData] = useState({
    name: branch?.name || "",
    code: branch?.code || "",
    address: branch?.address || "",
    phone: branch?.phone || "",
    isActive: branch?.isActive ?? true,
  });

  const [createBranch, { isLoading: isCreating }] = useCreateBranchMutation();
  const [updateBranch, { isLoading: isUpdating }] = useUpdateBranchMutation();

  const isLoading = isCreating || isUpdating;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    // Validation
    if (!formData.name.trim()) {
      toast.error("اسم الفرع مطلوب");
      return;
    }

    if (!formData.code.trim()) {
      toast.error("كود الفرع مطلوب");
      return;
    }

    try {
      if (isEditMode) {
        await updateBranch({
          id: branch.id,
          data: formData,
        }).unwrap();

        toast.success("تم تحديث الفرع بنجاح");
        onClose();
      } else {
        await createBranch({
          name: formData.name,
          code: formData.code,
          address: formData.address || undefined,
          phone: formData.phone || undefined,
        }).unwrap();

        toast.success("تم إضافة الفرع بنجاح");
        onClose();
      }
    } catch (error) {
      toast.error(handleApiError(error));
    }
  };

  const handleChange = (field: string, value: string | boolean) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  return (
    <Portal>
      <div 
        className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/50"
        onClick={onClose}
      >
        <div
          className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] flex flex-col overflow-hidden"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b border-gray-200 flex-shrink-0">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-primary-100 rounded-xl flex items-center justify-center">
                <Building2 className="w-5 h-5 text-primary-600" />
              </div>
              <h2 className="text-xl font-bold text-gray-800">
                {isEditMode ? "تعديل الفرع" : "إضافة فرع جديد"}
              </h2>
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            >
              <X className="w-5 h-5 text-gray-500" />
            </button>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} className="p-6 space-y-6 overflow-y-auto flex-1">
            {/* Basic Info */}
            <div className="space-y-4">
              <h3 className="font-semibold text-gray-800">
                المعلومات الأساسية
              </h3>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Name */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    اسم الفرع <span className="text-red-500">*</span>
                  </label>
                  <Input
                    type="text"
                    value={formData.name}
                    onChange={(e) => handleChange("name", e.target.value)}
                    placeholder="مثال: الفرع الرئيسي"
                    required
                  />
                </div>

                {/* Code */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    كود الفرع <span className="text-red-500">*</span>
                  </label>
                  <Input
                    type="text"
                    value={formData.code}
                    onChange={(e) => handleChange("code", e.target.value)}
                    placeholder="مثال: BR001"
                    required
                    disabled={isEditMode}
                  />
                  {isEditMode && (
                    <p className="text-xs text-gray-500 mt-1">
                      لا يمكن تعديل الكود بعد الإنشاء
                    </p>
                  )}
                </div>
              </div>
            </div>

            {/* Contact Info */}
            <div className="space-y-4">
              <h3 className="font-semibold text-gray-800">معلومات الاتصال</h3>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* Phone */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    رقم الهاتف
                  </label>
                  <Input
                    type="tel"
                    value={formData.phone}
                    onChange={(e) => handleChange("phone", e.target.value)}
                    placeholder="مثال: 01234567890"
                  />
                </div>

                {/* Status (Edit mode only) */}
                {isEditMode && (
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-2">
                      الحالة
                    </label>
                    <div className="flex items-center gap-4 h-10">
                      <label className="flex items-center gap-2 cursor-pointer">
                        <input
                          type="radio"
                          name="isActive"
                          checked={formData.isActive}
                          onChange={() => handleChange("isActive", true)}
                          className="w-4 h-4 text-primary-600"
                        />
                        <span className="text-sm text-gray-700">نشط</span>
                      </label>
                      <label className="flex items-center gap-2 cursor-pointer">
                        <input
                          type="radio"
                          name="isActive"
                          checked={!formData.isActive}
                          onChange={() => handleChange("isActive", false)}
                          className="w-4 h-4 text-primary-600"
                        />
                        <span className="text-sm text-gray-700">غير نشط</span>
                      </label>
                    </div>
                  </div>
                )}
              </div>

              {/* Address */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  العنوان
                </label>
                <textarea
                  value={formData.address}
                  onChange={(e) => handleChange("address", e.target.value)}
                  placeholder="مثال: 123 شارع الجمهورية، القاهرة"
                  rows={3}
                  className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500 resize-none"
                />
              </div>
            </div>
          </form>

          {/* Actions */}
          <div className="flex gap-3 p-6 border-t border-gray-200 flex-shrink-0">
            <Button
              type="button"
              variant="outline"
              onClick={onClose}
              disabled={isLoading}
              className="flex-1"
            >
              إلغاء
            </Button>
            <Button
              type="submit"
              onClick={handleSubmit}
              variant="primary"
              disabled={isLoading}
              className="flex-1"
            >
              {isLoading
                ? "جاري الحفظ..."
                : isEditMode
                  ? "حفظ التعديلات"
                  : "إضافة الفرع"}
            </Button>
          </div>
        </div>
      </div>
    </Portal>
  );
};
