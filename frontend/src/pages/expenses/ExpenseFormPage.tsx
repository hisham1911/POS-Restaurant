import { useState, useEffect } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { ArrowRight, Save, Upload, ChevronDown } from "lucide-react";
import {
  useGetExpenseByIdQuery,
  useCreateExpenseMutation,
  useUpdateExpenseMutation,
  useUploadAttachmentMutation,
} from "../../api/expensesApi";
import { useGetExpenseCategoriesQuery } from "../../api/expenseCategoriesApi";
import { Button } from "../../components/common/Button";
import { Card } from "../../components/common/Card";
import { Loading } from "../../components/common/Loading";
import type {
  CreateExpenseRequest,
  UpdateExpenseRequest,
} from "../../types/expense.types";

export function ExpenseFormPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const isEditMode = !!id;

  const { data: expenseResponse, isLoading: isLoadingExpense } =
    useGetExpenseByIdQuery(Number(id), {
      skip: !isEditMode,
    });
  const { data: categoriesResponse, isLoading: isLoadingCategories } =
    useGetExpenseCategoriesQuery();
  const [createExpense, { isLoading: isCreating }] = useCreateExpenseMutation();
  const [updateExpense, { isLoading: isUpdating }] = useUpdateExpenseMutation();
  const [uploadAttachment] = useUploadAttachmentMutation();

  const expense = expenseResponse?.data;
  const categories = categoriesResponse?.data || [];

  type ExpenseFormData = Omit<
    CreateExpenseRequest | UpdateExpenseRequest,
    "amount"
  > & { amount: string | number };

  const [formData, setFormData] = useState<ExpenseFormData>({
    categoryId: 0,
    amount: "",
    description: "",
    expenseDate: new Date().toISOString().split("T")[0],
    notes: "",
    referenceNumber: "",
    beneficiary: "",
  });

  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);

  useEffect(() => {
    if (expense && isEditMode) {
      const updatedData: ExpenseFormData = {
        categoryId: expense.categoryId,
        amount: String(expense.amount),
        description: expense.description,
        expenseDate: expense.expenseDate.split("T")[0],
        notes: expense.notes || "",
        referenceNumber: expense.referenceNumber || "",
        beneficiary: expense.beneficiary || "",
      };
      setFormData(updatedData);
    }
  }, [expense, isEditMode]);

  const handleChange = (field: keyof typeof formData, value: any) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      setSelectedFiles(Array.from(e.target.files));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const numAmount = Number(formData.amount) || 0;

    // Validation
    if (!formData.categoryId) {
      alert("يرجى اختيار التصنيف");
      return;
    }
    if (numAmount <= 0) {
      alert("يرجى إدخال مبلغ صحيح");
      return;
    }
    if (!formData.description.trim()) {
      alert("يرجى إدخال الوصف");
      return;
    }

    try {
      let expenseId: number;

      const expenseData = {
        ...formData,
        amount: numAmount,
      };

      if (isEditMode) {
        const result = await updateExpense({
          id: Number(id),
          expense: expenseData,
        }).unwrap();
        expenseId = result.data!.id;
      } else {
        const result = await createExpense(expenseData).unwrap();
        expenseId = result.data!.id;

        // Upload attachments for new expense
        if (selectedFiles.length > 0) {
          for (const file of selectedFiles) {
            await uploadAttachment({ id: expenseId, file }).unwrap();
          }
        }
      }

      navigate(`/expenses/${expenseId}`);
    } catch (error) {
      console.error("Failed to save expense:", error);
      alert("حدث خطأ أثناء حفظ المصروف");
    }
  };

  if (isLoadingExpense || isLoadingCategories) return <Loading />;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Link to={isEditMode ? `/expenses/${id}` : "/expenses"}>
          <Button variant="outline">
            <ArrowRight className="w-4 h-4 ml-2" />
            رجوع
          </Button>
        </Link>
        <div>
          <h1 className="text-2xl font-bold text-gray-900">
            {isEditMode ? "تعديل المصروف" : "مصروف جديد"}
          </h1>
          <p className="text-gray-600 mt-1">
            {isEditMode ? "تعديل بيانات المصروف" : "إضافة مصروف جديد للنظام"}
          </p>
        </div>
      </div>

      {/* Form */}
      <form onSubmit={handleSubmit}>
        <Card>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Category */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                التصنيف <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <select
                  value={formData.categoryId}
                  onChange={(e) =>
                    handleChange("categoryId", Number(e.target.value))
                  }
                  className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
                  required
                >
                  <option value={0}>اختر التصنيف</option>
                  {categories.map((cat) => (
                    <option key={cat.id} value={cat.id}>
                      {cat.name}
                    </option>
                  ))}
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>

            {/* Amount */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                المبلغ (جنيه) <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                step="0.01"
                min="0"
                value={formData.amount}
                onChange={(e) => handleChange("amount", e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="0.00"
                required
              />
            </div>

            {/* Expense Date */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                تاريخ المصروف <span className="text-red-500">*</span>
              </label>
              <input
                type="date"
                value={formData.expenseDate}
                onChange={(e) => handleChange("expenseDate", e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                required
              />
            </div>

            {/* Vendor Name */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                المستفيد
              </label>
              <input
                type="text"
                value={formData.beneficiary}
                onChange={(e) => handleChange("beneficiary", e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="اسم المستفيد أو الجهة"
              />
            </div>

            {/* Receipt Number */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                رقم المرجع
              </label>
              <input
                type="text"
                value={formData.referenceNumber}
                onChange={(e) =>
                  handleChange("referenceNumber", e.target.value)
                }
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="رقم الإيصال أو الفاتورة"
              />
            </div>

            {/* Description */}
            <div className="md:col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                الوصف <span className="text-red-500">*</span>
              </label>
              <textarea
                value={formData.description}
                onChange={(e) => handleChange("description", e.target.value)}
                rows={3}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="وصف تفصيلي للمصروف..."
                required
              />
            </div>

            {/* Notes */}
            <div className="md:col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                ملاحظات
              </label>
              <textarea
                value={formData.notes}
                onChange={(e) => handleChange("notes", e.target.value)}
                rows={2}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="ملاحظات إضافية..."
              />
            </div>

            {/* File Upload (only for new expenses) */}
            {!isEditMode && (
              <div className="md:col-span-2">
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  المرفقات
                </label>
                <div className="flex items-center gap-2">
                  <input
                    type="file"
                    multiple
                    onChange={handleFileSelect}
                    className="hidden"
                    id="file-upload"
                    accept="image/*,.pdf,.doc,.docx"
                  />
                  <label
                    htmlFor="file-upload"
                    className="flex items-center gap-2 px-4 py-2 border border-gray-300 rounded-md cursor-pointer hover:bg-gray-50"
                  >
                    <Upload className="w-4 h-4" />
                    اختر ملفات
                  </label>
                  {selectedFiles.length > 0 && (
                    <span className="text-sm text-gray-600">
                      تم اختيار {selectedFiles.length} ملف
                    </span>
                  )}
                </div>
                {selectedFiles.length > 0 && (
                  <div className="mt-2 space-y-1">
                    {selectedFiles.map((file, index) => (
                      <div key={index} className="text-sm text-gray-600">
                        • {file.name} ({(file.size / 1024).toFixed(2)} KB)
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>

          {/* Actions */}
          <div className="flex gap-2 justify-end mt-6 pt-6 border-t border-gray-200">
            <Link to={isEditMode ? `/expenses/${id}` : "/expenses"}>
              <Button variant="outline">إلغاء</Button>
            </Link>
            <Button type="submit" disabled={isCreating || isUpdating}>
              <Save className="w-4 h-4 ml-2" />
              {isCreating || isUpdating ? "جاري الحفظ..." : "حفظ"}
            </Button>
          </div>
        </Card>
      </form>
    </div>
  );
}
