import { useState } from "react";
import { Link } from "react-router-dom";
import {
  ArrowRight,
  TrendingUp,
  TrendingDown,
  ListOrdered,
  Building2,
  ChevronDown,
} from "lucide-react";
import { useGetTransactionsQuery } from "../../api/cashRegisterApi";
import type {
  CashRegisterFilters,
  CashRegisterTransactionType,
} from "../../types/cashRegister.types";
import { Button } from "../../components/common/Button";
import { Card } from "../../components/common/Card";
import { Loading } from "../../components/common/Loading";
import { useAppSelector } from "../../store/hooks";
import { selectCurrentBranch } from "../../store/slices/branchSlice";
import { formatDateTimeFull } from "../../utils/formatters";

export function CashRegisterTransactionsPage() {
  const currentBranch = useAppSelector(selectCurrentBranch);
  const [filters, setFilters] = useState<CashRegisterFilters>({
    pageNumber: 1,
    pageSize: 20,
  });

  const {
    data: response,
    isLoading,
    error,
  } = useGetTransactionsQuery(
    { ...filters, branchId: currentBranch?.id },
    { skip: !currentBranch?.id },
  );

  const transactions = response?.data?.items || [];
  const totalCount = response?.data?.totalCount || 0;
  const totalPages = response?.data?.totalPages || 1;
  const incomingCount = transactions.filter((t) => t.amount >= 0).length;
  const outgoingCount = transactions.filter((t) => t.amount < 0).length;

  const handleFilterChange = (key: keyof CashRegisterFilters, value: any) => {
    setFilters((prev) => ({ ...prev, [key]: value, pageNumber: 1 }));
  };

  const handlePageChange = (page: number) => {
    setFilters((prev) => ({ ...prev, pageNumber: page }));
  };

  const getTransactionTypeLabel = (type: CashRegisterTransactionType) => {
    const labels: Record<CashRegisterTransactionType, string> = {
      Opening: "فتح وردية",
      Deposit: "إيداع",
      Withdrawal: "سحب",
      Sale: "مبيعات",
      Refund: "مرتجع",
      Expense: "مصروف",
      SupplierPayment: "دفع لمورد",
      Adjustment: "تسوية",
      Transfer: "تحويل",
    };
    return labels[type];
  };

  const getTransactionTypeBadge = (type: CashRegisterTransactionType) => {
    const badges: Record<CashRegisterTransactionType, string> = {
      Opening: "bg-blue-100 text-blue-800",
      Deposit: "bg-green-100 text-green-800",
      Withdrawal: "bg-red-100 text-red-800",
      Sale: "bg-green-100 text-green-800",
      Refund: "bg-red-100 text-red-800",
      Expense: "bg-red-100 text-red-800",
      SupplierPayment: "bg-red-100 text-red-800",
      Adjustment: "bg-yellow-100 text-yellow-800",
      Transfer: "bg-purple-100 text-purple-800",
    };
    return badges[type];
  };

  if (!currentBranch?.id) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <Loading />
      </div>
    );
  }

  if (isLoading) return <Loading />;
  if (error)
    return <div className="text-red-600">حدث خطأ في تحميل المعاملات</div>;

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
        <div className="mb-8">
          <div className="flex items-center gap-3 mb-2">
            <ListOrdered className="w-8 h-8 text-blue-600" />
            <h1 className="text-3xl font-bold text-gray-900">
              معاملات الخزينة
            </h1>
          </div>
          <p className="text-gray-600">سجل كامل لجميع المعاملات النقدية</p>
          {currentBranch && (
            <div className="mt-3 inline-flex items-center gap-2 px-4 py-2 bg-blue-50 border border-blue-200 rounded-lg">
              <Building2 className="w-4 h-4 text-blue-600" />
              <span className="text-sm font-medium text-blue-900">
                الفرع الحالي: {currentBranch.name}
              </span>
            </div>
          )}
        </div>

        <div className="flex justify-end">
          <Link to="/cash-register">
            <Button variant="outline">
              <ArrowRight className="w-4 h-4" />
              رجوع للخزينة
            </Button>
          </Link>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <Card className="border-blue-100">
            <p className="text-sm text-gray-600">المعاملات المعروضة</p>
            <p className="text-2xl font-bold text-gray-900 mt-1">
              {transactions.length}
            </p>
          </Card>
          <Card className="border-green-100">
            <p className="text-sm text-gray-600">عمليات دخول</p>
            <p className="text-2xl font-bold text-green-700 mt-1">
              {incomingCount}
            </p>
          </Card>
          <Card className="border-red-100">
            <p className="text-sm text-gray-600">عمليات خروج</p>
            <p className="text-2xl font-bold text-red-700 mt-1">
              {outgoingCount}
            </p>
          </Card>
        </div>

        <Card>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                نوع المعاملة
              </label>
              <div className="relative">
                <select
                  value={filters.type || ""}
                  onChange={(e) =>
                    handleFilterChange("type", e.target.value || undefined)
                  }
                  className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
                >
                  <option value="">الكل</option>
                  <option value="Opening">فتح وردية</option>
                  <option value="Deposit">إيداع</option>
                  <option value="Withdrawal">سحب</option>
                  <option value="Sale">مبيعات</option>
                  <option value="Refund">مرتجع</option>
                  <option value="Expense">مصروف</option>
                  <option value="SupplierPayment">دفع لمورد</option>
                  <option value="Adjustment">تسوية</option>
                  <option value="Transfer">تحويل</option>
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

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                رقم الوردية
              </label>
              <input
                type="number"
                value={filters.shiftId || ""}
                onChange={(e) =>
                  handleFilterChange(
                    "shiftId",
                    e.target.value ? Number(e.target.value) : undefined,
                  )
                }
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="رقم الوردية"
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
                    التاريخ والوقت
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    النوع
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    الوصف
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    المبلغ
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    الرصيد قبل
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    الرصيد بعد
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    المستخدم
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {transactions.length === 0 ? (
                  <tr>
                    <td
                      colSpan={7}
                      className="px-6 py-10 text-center text-sm text-gray-500"
                    >
                      لا توجد معاملات مطابقة للفلاتر الحالية.
                    </td>
                  </tr>
                ) : (
                  transactions.map((transaction) => (
                    <tr key={transaction.id} className="hover:bg-gray-50">
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                        {formatDateTimeFull(transaction.createdAt)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <span
                          className={`px-2 py-1 text-xs font-semibold rounded-full ${getTransactionTypeBadge(
                            transaction.type,
                          )}`}
                        >
                          {getTransactionTypeLabel(transaction.type)}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-sm text-gray-900 max-w-sm truncate">
                        {transaction.description}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="flex items-center gap-1">
                          {transaction.amount >= 0 ? (
                            <TrendingUp className="w-4 h-4 text-green-600" />
                          ) : (
                            <TrendingDown className="w-4 h-4 text-red-600" />
                          )}
                          <span
                            className={`text-sm font-bold ${
                              transaction.amount >= 0
                                ? "text-green-600"
                                : "text-red-600"
                            }`}
                          >
                            {transaction.amount >= 0 ? "+" : ""}
                            {transaction.amount.toFixed(2)} جنيه
                          </span>
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                        {transaction.balanceBefore.toFixed(2)} جنيه
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-semibold text-gray-900">
                        {transaction.balanceAfter.toFixed(2)} جنيه
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600">
                        {transaction.createdByUserName}
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
                عرض {transactions.length} من {totalCount} معاملة
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  onClick={() => handlePageChange(filters.pageNumber! - 1)}
                  disabled={filters.pageNumber === 1}
                >
                  السابق
                </Button>
                <span className="px-4 py-2 text-sm text-gray-700">
                  صفحة {filters.pageNumber} من {totalPages}
                </span>
                <Button
                  variant="outline"
                  onClick={() => handlePageChange(filters.pageNumber! + 1)}
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
            💡 نصائح استخدام سجل المعاملات
          </h3>
          <ul className="space-y-2 text-sm text-blue-800">
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>البحث والفلترة:</strong> يمكنك البحث حسب نوع المعاملة
                والتاريخ ورقم الوردية
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>الرصيد:</strong> الرصيد قبل وبعد يوضح تأثير كل معاملة
                على الرصيد
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>الدخول والخروج:</strong> له ألوان مختلفة (أخضر للدخول،
                أحمر للخروج)
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>التدقيق:</strong> جميع المعاملات موثقة باسم المستخدم
                والتاريخ والوقت الدقيق
              </span>
            </li>
            <li className="flex items-start gap-2">
              <span className="font-bold">•</span>
              <span>
                <strong>التصدير:</strong> يمكن استخدام الفلاتر لتحديد فترة معينة
                ثم حفظ التقرير
              </span>
            </li>
          </ul>
        </div>
      </div>
    </div>
  );
}
