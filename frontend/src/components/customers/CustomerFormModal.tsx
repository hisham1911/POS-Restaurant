import { useState, useEffect } from "react";
import { X, UserPlus, Edit } from "lucide-react";
import {
  Customer,
  CreateCustomerRequest,
  UpdateCustomerRequest,
} from "@/types/customer.types";
import {
  useCreateCustomerMutation,
  useUpdateCustomerMutation,
} from "@/api/customersApi";
import { Button } from "@/components/common/Button";
import { toast } from "sonner";
import { Portal } from "@/components/common/Portal";

interface CustomerFormModalProps {
  customer?: Customer | null;
  onClose: () => void;
  onSuccess?: () => void;
}

export const CustomerFormModal = ({
  customer,
  onClose,
  onSuccess,
}: CustomerFormModalProps) => {
  const isEditing = !!customer;

  const [formData, setFormData] = useState({
    phone: "",
    name: "",
    email: "",
    address: "",
    notes: "",
    creditLimit: "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const [createCustomer, { isLoading: isCreating }] =
    useCreateCustomerMutation();
  const [updateCustomer, { isLoading: isUpdating }] =
    useUpdateCustomerMutation();

  const isLoading = isCreating || isUpdating;

  useEffect(() => {
    if (customer) {
      setFormData({
        phone: customer.phone || "",
        name: customer.name || "",
        email: customer.email || "",
        address: customer.address || "",
        notes: customer.notes || "",
        creditLimit:
          customer.creditLimit > 0 ? customer.creditLimit.toString() : "",
      });
    }
  }, [customer]);

  const validateForm = () => {
    const newErrors: Record<string, string> = {};

    if (!formData.phone.trim()) {
      newErrors.phone = "رقم الهاتف مطلوب";
    } else if (!/^01[0125][0-9]{8}$/.test(formData.phone.trim())) {
      newErrors.phone = "رقم الهاتف غير صحيح";
    }

    if (formData.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
      newErrors.email = "البريد الإلكتروني غير صحيح";
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateForm()) return;

    try {
      if (isEditing && customer) {
        const updateData: UpdateCustomerRequest = {
          name: formData.name || undefined,
          email: formData.email || undefined,
          address: formData.address || undefined,
          notes: formData.notes || undefined,
          creditLimit: formData.creditLimit
            ? parseFloat(formData.creditLimit)
            : 0,
          rowVersion: customer.rowVersion, // Include for concurrency control
        };

        const result = await updateCustomer({
          id: customer.id,
          data: updateData,
        }).unwrap();
        if (result.success) {
          toast.success("تم تحديث بيانات العميل بنجاح");
          onSuccess?.();
          onClose();
        } else {
          toast.error(result.message || "فشل تحديث العميل");
        }
      } else {
        const createData: CreateCustomerRequest = {
          phone: formData.phone.trim(),
          name: formData.name || undefined,
          email: formData.email || undefined,
          address: formData.address || undefined,
          notes: formData.notes || undefined,
        };

        const result = await createCustomer(createData).unwrap();
        if (result.success) {
          toast.success("تم إضافة العميل بنجاح");
          onSuccess?.();
          onClose();
        } else {
          toast.error(result.message || "فشل إضافة العميل");
        }
      }
    } catch {
      toast.error(isEditing ? "فشل تحديث العميل" : "فشل إضافة العميل");
    }
  };

  return (
    <Portal>
      <div 
        className="fixed inset-0 z-[110] flex items-center justify-center p-4 bg-black/50"
        onClick={onClose}
      >
        <div 
          className="bg-white rounded-2xl shadow-2xl w-full max-w-md max-h-[90vh] flex flex-col overflow-hidden animate-scale-in"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b border-gray-200 flex-shrink-0">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-primary-100 rounded-xl flex items-center justify-center">
                {isEditing ? (
                  <Edit className="w-5 h-5 text-primary-600" />
                ) : (
                  <UserPlus className="w-5 h-5 text-primary-600" />
                )}
              </div>
              <h2 className="text-xl font-bold text-gray-800">
                {isEditing ? "تعديل العميل" : "إضافة عميل جديد"}
              </h2>
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            >
              <X className="w-5 h-5 text-gray-500" />
            </button>
          </div>

          <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto flex-1">
            {/* Phone */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                رقم الهاتف <span className="text-danger-500">*</span>
              </label>
              <input
                type="tel"
                value={formData.phone}
                onChange={(e) =>
                  setFormData({ ...formData, phone: e.target.value })
                }
                placeholder="01xxxxxxxxx"
                disabled={isEditing}
                className={`w-full px-4 py-2.5 border rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500 ${
                  isEditing ? "bg-gray-100 cursor-not-allowed" : ""
                } ${errors.phone ? "border-danger-500" : "border-gray-300"}`}
                dir="ltr"
              />
              {errors.phone && (
                <p className="text-danger-500 text-sm mt-1">{errors.phone}</p>
              )}
            </div>

            {/* Name */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                الاسم
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) =>
                  setFormData({ ...formData, name: e.target.value })
                }
                placeholder="اسم العميل"
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
              />
            </div>

            {/* Email */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                البريد الإلكتروني
              </label>
              <input
                type="email"
                value={formData.email}
                onChange={(e) =>
                  setFormData({ ...formData, email: e.target.value })
                }
                placeholder="email@example.com"
                className={`w-full px-4 py-2.5 border rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500 ${
                  errors.email ? "border-danger-500" : "border-gray-300"
                }`}
                dir="ltr"
              />
              {errors.email && (
                <p className="text-danger-500 text-sm mt-1">{errors.email}</p>
              )}
            </div>

            {/* Address */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                العنوان
              </label>
              <input
                type="text"
                value={formData.address}
                onChange={(e) =>
                  setFormData({ ...formData, address: e.target.value })
                }
                placeholder="عنوان العميل"
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
              />
            </div>

            {/* Notes */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                ملاحظات
              </label>
              <textarea
                value={formData.notes}
                onChange={(e) =>
                  setFormData({ ...formData, notes: e.target.value })
                }
                placeholder="ملاحظات إضافية..."
                rows={2}
                className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500 resize-none"
              />
            </div>

            {/* Credit Limit */}
            {isEditing && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  حد الائتمان (ج.م)
                </label>
                <input
                  type="number"
                  value={formData.creditLimit === "0" ? "" : formData.creditLimit}
                  onChange={(e) =>
                    setFormData({ ...formData, creditLimit: e.target.value })
                  }
                  placeholder="0 = بدون حد"
                  min="0"
                  step="0.01"
                  className="w-full px-4 py-2.5 border border-gray-300 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                  dir="ltr"
                />
                <p className="text-xs text-gray-500 mt-1">
                  اترك 0 للسماح بائتمان غير محدود
                </p>
              </div>
            )}
          </form>

          {/* Actions */}
          <div className="flex gap-3 p-6 border-t border-gray-200 flex-shrink-0">
            <Button
              type="button"
              variant="secondary"
              onClick={onClose}
              className="flex-1"
              disabled={isLoading}
            >
              إلغاء
            </Button>
            <Button
              type="submit"
              onClick={handleSubmit}
              variant="primary"
              isLoading={isLoading}
              className="flex-1"
            >
              {isEditing ? "حفظ التعديلات" : "إضافة العميل"}
            </Button>
          </div>
        </div>
      </div>
    </Portal>
  );
};
