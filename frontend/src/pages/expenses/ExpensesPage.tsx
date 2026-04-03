import { useState } from "react";
import { Link } from "react-router-dom";
import {
  Plus,
  Eye,
  Edit,
  Trash2,
  Receipt,
  Wallet,
  ChevronDown,
} from "lucide-react";
import {
  useGetExpensesQuery,
  useDeleteExpenseMutation,
} from "@/api/expensesApi";
import { useGetExpenseCategoriesQuery } from "@/api/expenseCategoriesApi";
import type { ExpenseStatus, ExpenseFilters } from "@/types/expense.types";
import { Button } from "@/components/common/Button";
import { Card } from "@/components/common/Card";
import { Loading } from "@/components/common/Loading";
import { formatCurrency, formatDateOnly } from "@/utils/formatters";
import { toast } from "sonner";

export function ExpensesPage() {
  const [filters, setFilters] = useState<ExpenseFilters>({
    pageNumber: 1,
    pageSize: 20,
  });

  const {
    data: expensesResponse,
    isLoading,
    error,
  } = useGetExpensesQuery(filters);
  const { data: categoriesResponse } = useGetExpenseCategoriesQuery();
  const [deleteExpense, { isLoading: isDeleting }] = useDeleteExpenseMutation();

  const expenses = expensesResponse?.data?.items || [];
  const totalCount = expensesResponse?.data?.totalCount || 0;
  const totalPages = expensesResponse?.data?.totalPages || 1;
  const categories = categoriesResponse?.data || [];
  const totalAmount = expenses.reduce(
    (sum, expense) => sum + expense.amount,
    0,
  );
  const paidCount = expenses.filter(
    (expense) => expense.status === "Paid",
  ).length;
  const draftCount = expenses.filter(
    (expense) => expense.status === "Draft",
  ).length;

  const handleFilterChange = <K extends keyof ExpenseFilters>(
    key: K,
    value: ExpenseFilters[K],
  ) => {
    setFilters((prev) => ({ ...prev, [key]: value, pageNumber: 1 }));
  };

  const handlePageChange = (page: number) => {
    setFilters((prev) => ({ ...prev, pageNumber: page }));
  };

  const handleDelete = async (id: number) => {
    if (window.confirm("هل أنت متأكد من حذف هذا المصروف؟")) {
      try {
        await deleteExpense(id).unwrap();
        toast.success("تم حذف المصروف بنجاح");
      } catch (error) {
        console.error("Failed to delete expense:", error);
        toast.error("فشل في حذف المصروف");
      }
    }
  };

  const getStatusBadge = (status: ExpenseStatus) => {
    const badges: Record<ExpenseStatus, string> = {
      Draft: "bg-gray-100 text-gray-700",
      Approved: "bg-blue-100 text-blue-800",
      Paid: "bg-green-100 text-green-800",
      Rejected: "bg-red-100 text-red-800",
    };
    const labels: Record<ExpenseStatus, string> = {
      Draft: "مسودة",
      Approved: "معتمد",
      Paid: "مدفوع",
      Rejected: "مرفوض",
    };
    return (
      <span
        className={`px-2 py-1 text-xs font-semibold rounded-full ${badges[status]}`}
      >
        {labels[status]}
      </span>
    );
  };

  if (isLoading) return <Loading />;
  if (error)
    return <div className="text-red-600">حدث خطأ في تحميل المصروفات</div>;

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
        <div className="mb-8">
          <div className="flex items-center gap-3 mb-2">
            <Wallet className="w-8 h-8 text-blue-600" />
            <h1 className="text-3xl font-bold text-gray-900">المصروفات</h1>
          </div>
          <p className="text-gray-600">
            إدارة مصروفات الشركة ومراجعة حالتها والموافقة على الدفع
          </p>
        </div>

        <div className="flex justify-end">
          <Link to="/expenses/new">
            <Button>
              <Plus className="w-4 h-4" />
              مصروف جديد
            </Button>
          </Link>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <Card className="border-blue-100">
            <div className="flex items-start justify-between">
              <div>
                <p className="text-sm text-gray-600">
                  إجمالي المصروفات (الصفحة الحالية)
                </p>
                <p className="text-2xl font-bold text-gray-900 mt-1">
                  {formatCurrency(totalAmount)}
                </p>
              </div>
              <div className="w-10 h-10 rounded-full bg-blue-100 flex items-center justify-center">
                <Wallet className="w-5 h-5 text-blue-600" />
              </div>
            </div>
          </Card>
          <Card className="border-green-100">
            <div className="flex items-start justify-between">
              <div>
                <p className="text-sm text-gray-600">مصروفات مدفوعة</p>
                <p className="text-2xl font-bold text-green-700 mt-1">
                  {paidCount}
                </p>
              </div>
              <div className="w-10 h-10 rounded-full bg-green-100 flex items-center justify-center">
                <Receipt className="w-5 h-5 text-green-600" />
              </div>
            </div>
          </Card>
          <Card className="border-amber-100">
            <div className="flex items-start justify-between">
              <div>
                <p className="text-sm text-gray-600">مصروفات مسودة</p>
                <p className="text-2xl font-bold text-amber-700 mt-1">
                  {draftCount}
                </p>
              </div>
              <div className="w-10 h-10 rounded-full bg-amber-100 flex items-center justify-center">
                <Receipt className="w-5 h-5 text-amber-600" />
              </div>
            </div>
          </Card>
        </div>

        <Card>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                التصنيف
              </label>
              <div className="relative">
                <select
                  value={filters.categoryId || ""}
                  onChange={(e) =>
                    handleFilterChange(
                      "categoryId",
                      e.target.value ? Number(e.target.value) : undefined,
                    )
                  }
                  className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
                >
                  <option value="">الكل</option>
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
              <label className="block text-sm font-medium text-gray-700 mb-1">
                الحالة
              </label>
              <div className="relative">
                <select
                  value={filters.status || ""}
                  onChange={(e) =>
                    handleFilterChange(
                      "status",
                      (e.target.value || undefined) as ExpenseFilters["status"],
                    )
                  }
                  className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
                >
                  <option value="">الكل</option>
                  <option value="Draft">مسودة</option>
                  <option value="Approved">معتمد</option>
                  <option value="Paid">مدفوع</option>
                  <option value="Rejected">مرفوض</option>
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                من تاريخ
              </label>
              <input
                type="date"
                value={filters.fromDate || ""}
                onChange={(e) =>
                  handleFilterChange("fromDate", e.target.value || undefined)
                }
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                إلى تاريخ
              </label>
              <input
                type="date"
                value={filters.toDate || ""}
                onChange={(e) =>
                  handleFilterChange("toDate", e.target.value || undefined)
                }
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>
        </Card>

        <Card padding="none">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    رقم المصروف
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    التصنيف
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    الوصف
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    المبلغ
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    التاريخ
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    الحالة
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    الإجراءات
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {expenses.length === 0 ? (
                  <tr>
                    <td
                      colSpan={7}
                      className="px-6 py-10 text-center text-sm text-gray-500"
                    >
                      لا توجد مصروفات مطابقة للفلاتر الحالية.
                    </td>
                  </tr>
                ) : (
                  expenses.map((expense) => (
                    <tr key={expense.id} className="hover:bg-gray-50">
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">
                        {expense.expenseNumber}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                        {expense.categoryName}
                      </td>
                      <td className="px-6 py-4 text-sm text-gray-900 max-w-sm truncate">
                        {expense.description}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-semibold text-gray-900">
                        {formatCurrency(expense.amount)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                        {formatDateOnly(expense.expenseDate)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        {getStatusBadge(expense.status)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-medium">
                        <div className="flex gap-3">
                          <Link
                            to={`/expenses/${expense.id}`}
                            className="text-blue-600 hover:text-blue-900"
                            title="عرض"
                          >
                            <Eye className="w-4 h-4" />
                          </Link>
                          {expense.status === "Draft" && (
                            <>
                              <Link
                                to={`/expenses/${expense.id}/edit`}
                                className="text-green-600 hover:text-green-900"
                                title="تعديل"
                              >
                                <Edit className="w-4 h-4" />
                              </Link>
                              <button
                                onClick={() => handleDelete(expense.id)}
                                disabled={isDeleting}
                                className="text-red-600 hover:text-red-900 disabled:opacity-50"
                                title="حذف"
                              >
                                <Trash2 className="w-4 h-4" />
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between px-6 py-4 border-t border-gray-200">
              <div className="text-sm text-gray-700">
                عرض {expenses.length} من {totalCount} مصروف
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  onClick={() =>
                    handlePageChange((filters.pageNumber || 1) - 1)
                  }
                  disabled={filters.pageNumber === 1}
                >
                  السابق
                </Button>
                <span className="px-4 py-2 text-sm text-gray-700">
                  صفحة {filters.pageNumber} من {totalPages}
                </span>
                <Button
                  variant="outline"
                  onClick={() =>
                    handlePageChange((filters.pageNumber || 1) + 1)
                  }
                  disabled={filters.pageNumber === totalPages}
                >
                  التالي
                </Button>
              </div>
            </div>
          )}
        </Card>

        {/* Help Section */}
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-6">
          <h3 className="text-lg font-semibold text-blue-900 mb-3">
            💡 نصائح إدارة المصروفات
          </h3>
          <ul className="space-y-2 text-sm text-blue-800">
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>المصروف الجديد:</strong> أضف مصروف جديد وحدد الفئة
                والمبلغ والوصف
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>الحالات:</strong> جميع المصروفات تبدأ كمسودة ثم تحتاج
                موافقة قبل الدفع
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>التصفية:</strong> استخدم الفلاتر للبحث حسب الفئة والحالة
                والتاريخ
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>التتبع:</strong> راقب إجمالي المصروفات والمبالغ المدفوعة
                والمعلقة
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>التدقيق:</strong> جميع المصروفات موثقة بالتاريخ
                والمستخدم والتغييرات
              </span>
            </li>
          </ul>
        </div>
      </div>
    </div>
  );
}
