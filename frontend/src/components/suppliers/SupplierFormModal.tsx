import { useState, useEffect } from "react";
import { X } from "lucide-react";
import { toast } from "sonner";
import {
  useCreateSupplierMutation,
  useUpdateSupplierMutation,
} from "../../api/suppliersApi";
import { Supplier, CreateSupplierRequest, UpdateSupplierRequest } from "../../types/supplier.types";
import { Modal } from "../common/Modal";
import { Button } from "../common/Button";
import { Input } from "../common/Input";
import { handleApiError } from "../../utils/errorHandler";

interface SupplierFormModalProps {
  supplier: Supplier | null;
  onClose: () => void;
}

export default function SupplierFormModal({
  supplier,
  onClose,
}: SupplierFormModalProps) {
  const isEditMode = !!supplier;

  const [formData, setFormData] = useState<UpdateSupplierRequest>({
    name: "",
    nameEn: "",
    phone: "",
    email: "",
    address: "",
    taxNumber: "",
    contactPerson: "",
    notes: "",
    isActive: true,
  });

  const [createSupplier, { isLoading: isCreating }] = useCreateSupplierMutation();
  const [updateSupplier, { isLoading: isUpdating }] = useUpdateSupplierMutation();

  useEffect(() => {
    if (supplier) {
      setFormData({
        name: supplier.name,
        nameEn: supplier.nameEn || "",
        phone: supplier.phone || "",
        email: supplier.email || "",
        address: supplier.address || "",
        taxNumber: supplier.taxNumber || "",
        contactPerson: supplier.contactPerson || "",
        notes: supplier.notes || "",
        isActive: supplier.isActive,
      });
    }
  }, [supplier]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    // Validation
    if (!formData.name.trim()) {
      toast.error("الاسم مطلوب");
      return;
    }

    try {
      if (isEditMode) {
        await updateSupplier({
          id: supplier.id,
          data: formData,
        }).unwrap();

        toast.success("تم تحديث المورد بنجاح");
        onClose();
      } else {
        const createData: CreateSupplierRequest = {
          name: formData.name,
          nameEn: formData.nameEn || undefined,
          phone: formData.phone || undefined,
          email: formData.email || undefined,
          address: formData.address || undefined,
          taxNumber: formData.taxNumber || undefined,
          contactPerson: formData.contactPerson || undefined,
          notes: formData.notes || undefined,
        };

        await createSupplier(createData).unwrap();

        toast.success("تم إضافة المورد بنجاح");
        onClose();
      }
    } catch (error) {
      toast.error(handleApiError(error));
    }
  };

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
  ) => {
    const { name, value, type } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? (e.target as HTMLInputElement).checked : value,
    }));
  };

  return (
    <Modal isOpen onClose={onClose} title={isEditMode ? "تعديل مورد" : "إضافة مورد"}>
      <form onSubmit={handleSubmit} className="space-y-4">
        {/* Name (Arabic) - Required */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            الاسم (عربي) <span className="text-red-500">*</span>
          </label>
          <Input
            type="text"
            name="name"
            value={formData.name}
            onChange={handleChange}
            placeholder="اسم المورد بالعربي"
            required
          />
        </div>

        {/* Name (English) */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            الاسم (إنجليزي)
          </label>
          <Input
            type="text"
            name="nameEn"
            value={formData.nameEn}
            onChange={handleChange}
            placeholder="اسم المورد بالإنجليزية"
          />
        </div>

        {/* Phone */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            رقم الهاتف
          </label>
          <Input
            type="tel"
            name="phone"
            value={formData.phone}
            onChange={handleChange}
            placeholder="01xxxxxxxxx"
          />
        </div>

        {/* Email */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            البريد الإلكتروني
          </label>
          <Input
            type="email"
            name="email"
            value={formData.email}
            onChange={handleChange}
            placeholder="مثال: supplier@example.com"
          />
        </div>

        {/* Address */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            العنوان
          </label>
          <Input
            type="text"
            name="address"
            value={formData.address}
            onChange={handleChange}
            placeholder="عنوان المورد"
          />
        </div>

        {/* Tax Number */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            الرقم الضريبي
          </label>
          <Input
            type="text"
            name="taxNumber"
            value={formData.taxNumber}
            onChange={handleChange}
            placeholder="الرقم الضريبي للمورد"
          />
        </div>

        {/* Contact Person */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            جهة الاتصال
          </label>
          <Input
            type="text"
            name="contactPerson"
            value={formData.contactPerson}
            onChange={handleChange}
            placeholder="اسم الشخص المسؤول"
          />
        </div>

        {/* Notes */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            ملاحظات
          </label>
          <textarea
            name="notes"
            value={formData.notes}
            onChange={handleChange}
            placeholder="ملاحظات إضافية"
            rows={3}
            className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          />
        </div>

        {/* Is Active (only in edit mode) */}
        {isEditMode && (
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isActive"
              name="isActive"
              checked={formData.isActive}
              onChange={handleChange}
              className="w-4 h-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
            />
            <label htmlFor="isActive" className="text-sm font-medium text-gray-700">
              مورد نشط
            </label>
          </div>
        )}

        {/* Actions */}
        <div className="flex gap-3 pt-4">
          <Button
            type="submit"
            disabled={isCreating || isUpdating}
            className="flex-1"
          >
            {isCreating || isUpdating
              ? "جاري الحفظ..."
              : isEditMode
              ? "تحديث"
              : "إضافة"}
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={onClose}
            disabled={isCreating || isUpdating}
            className="flex-1"
          >
            إلغاء
          </Button>
        </div>
      </form>
    </Modal>
  );
}
