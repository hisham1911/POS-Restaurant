import { useState } from "react";
import {
  Receipt,
  TrendingUp,
  Calendar,
  Loader2,
  AlertCircle,
  CreditCard,
  Banknote,
  PieChart,
  Info,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDateOnly } from "@/utils/formatters";
import { useGetExpensesReportQuery } from "@/api/financialReportsApi";

export const ExpensesReportPage = () => {
  const [fromDate, setFromDate] = useState(
    new Date(new Date().setDate(1)).toISOString().split("T")[0],
  );
  const [toDate, setToDate] = useState(new Date().toISOString().split("T")[0]);

  const { data, isLoading, isError, error } = useGetExpensesReportQuery({
    fromDate,
    toDate,
  });
  const report = data?.data;

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-primary-500" />
        <span className="mr-2 text-gray-600">جاري تحميل التقرير...</span>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="h-full flex items-center justify-center">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 text-red-500 mx-auto mb-4" />
          <p className="text-red-600">فشل في تحميل التقرير</p>
          <p className="text-gray-500 text-sm mt-2">
            {(error as any)?.data?.message || "حدث خطأ غير متوقع"}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="h-full overflow-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">تقرير المصروفات</h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "تحليل تفصيلي للمصروفات"}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <Calendar className="w-5 h-5 text-gray-400" />
            <input
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
              className="px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
            />
            <span className="text-gray-500">إلى</span>
            <input
              type="date"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
              className="px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500"
            />
          </div>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card className="bg-gradient-to-br from-red-50 to-red-100 border-red-200">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-red-500 rounded-xl flex items-center justify-center">
              <Receipt className="w-6 h-6 text-white" />
            </div>
            <div>
              <p className="text-sm text-red-700 font-medium">
                إجمالي المصروفات
              </p>
              <p className="text-2xl font-bold text-red-600">
                {formatCurrency(report?.totalExpenses || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-blue-100 rounded-xl flex items-center justify-center">
              <TrendingUp className="w-6 h-6 text-blue-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">عدد المصروفات</p>
              <p className="text-2xl font-bold text-gray-800">
                {report?.totalExpenseCount || 0}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-purple-100 rounded-xl flex items-center justify-center">
              <PieChart className="w-6 h-6 text-purple-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">متوسط المصروف</p>
              <p className="text-2xl font-bold text-gray-800">
                {formatCurrency(report?.averageExpenseAmount || 0)}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Payment Methods */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          المصروفات حسب طريقة الدفع
        </h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="flex items-center gap-3 p-4 bg-green-50 rounded-lg">
            <Banknote className="w-8 h-8 text-green-600" />
            <div>
              <p className="text-sm text-gray-600">نقدي</p>
              <p className="text-xl font-bold text-green-600">
                {formatCurrency(report?.cashExpenses || 0)}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-3 p-4 bg-blue-50 rounded-lg">
            <CreditCard className="w-8 h-8 text-blue-600" />
            <div>
              <p className="text-sm text-gray-600">بطاقة</p>
              <p className="text-xl font-bold text-blue-600">
                {formatCurrency(report?.cardExpenses || 0)}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-3 p-4 bg-gray-50 rounded-lg">
            <Receipt className="w-8 h-8 text-gray-600" />
            <div>
              <p className="text-sm text-gray-600">أخرى</p>
              <p className="text-xl font-bold text-gray-600">
                {formatCurrency(report?.otherExpenses || 0)}
              </p>
            </div>
          </div>
        </div>
      </Card>

      {/* Expenses by Category */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          المصروفات حسب الفئة
        </h3>
        <div className="space-y-4">
          {report?.expensesByCategory &&
          report.expensesByCategory.length > 0 ? (
            report.expensesByCategory.map((category) => (
              <div key={category.categoryId}>
                <div className="flex justify-between mb-2">
                  <div>
                    <span className="font-medium text-gray-700">
                      {category.categoryName}
                    </span>
                    <span className="text-sm text-gray-500 mr-2">
                      ({category.expenseCount} مصروف)
                    </span>
                  </div>
                  <span className="font-bold text-gray-800">
                    {formatCurrency(category.totalAmount)}
                  </span>
                </div>
                <div className="h-3 bg-gray-100 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-gradient-to-r from-red-500 to-red-600 rounded-full transition-all"
                    style={{ width: `${category.percentage}%` }}
                  />
                </div>
                <p className="text-xs text-gray-400 mt-1">
                  {category.percentage.toFixed(1)}% من إجمالي المصروفات
                </p>
              </div>
            ))
          ) : (
            <p className="text-gray-400 text-center py-8">
              لا توجد مصروفات في هذه الفترة
            </p>
          )}
        </div>
      </Card>

      {/* Top Expenses */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          أكبر 10 مصروفات
        </h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  #
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  التاريخ
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الفئة
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الوصف
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المستفيد
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  طريقة الدفع
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المبلغ
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.topExpenses && report.topExpenses.length > 0 ? (
                report.topExpenses.map((expense, index) => (
                  <tr key={expense.id} className="border-b hover:bg-gray-50">
                    <td className="px-4 py-3 text-gray-500">{index + 1}</td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {formatDateOnly(expense.date)}
                    </td>
                    <td className="px-4 py-3">
                      <span className="inline-flex px-2 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-700">
                        {expense.categoryName}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-700">
                      {expense.description}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {expense.recipientName || "-"}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">
                      {expense.paymentMethod}
                    </td>
                    <td className="px-4 py-3 font-semibold text-red-600">
                      {formatCurrency(expense.amount)}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={7}
                    className="px-4 py-8 text-center text-gray-400"
                  >
                    لا توجد مصروفات
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>

      {/* Daily Expenses Chart */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          المصروفات اليومية
        </h3>
        <div className="space-y-2 max-h-96 overflow-y-auto">
          {report?.dailyExpenses && report.dailyExpenses.length > 0 ? (
            report.dailyExpenses.map((day) => (
              <div
                key={day.date}
                className="flex items-center justify-between py-2 border-b border-gray-100"
              >
                <span className="text-gray-600">
                  {formatDateOnly(day.date)}
                </span>
                <div className="text-left">
                  <span className="font-medium text-gray-800">
                    {formatCurrency(day.amount)}
                  </span>
                  <span className="text-gray-400 text-sm mr-2">
                    ({day.count} مصروف)
                  </span>
                </div>
              </div>
            ))
          ) : (
            <p className="text-gray-400 text-center py-4">لا توجد بيانات</p>
          )}
        </div>
      </Card>
      {/* Info Card */}
      <Card className="bg-blue-50 border-blue-200">
        <div className="flex items-start gap-3">
          <Info className="w-5 h-5 text-blue-600 mt-0.5 flex-shrink-0" />
          <div className="flex-1">
            <h3 className="text-sm font-semibold text-blue-900 mb-2">
              معلومات التقرير
            </h3>
            <ul className="text-sm text-blue-800 space-y-1">
              <li>
                • <strong>إجمالي المصروفات:</strong> مجموع جميع المصروفات
                المسجلة خلال الفترة المحددة.
              </li>
              <li>
                • <strong>تصنيف المصروفات:</strong> يتم تقسيم المصروفات حسب
                النوع لتسهيل التحليل.
              </li>
              <li>
                • <strong>المصروفات اليومية:</strong> توزيع المصروفات على أيام
                الفترة لمعرفة أوقات الذروة.
              </li>
              <li>
                • <strong>نصيحة:</strong> راقب المصروفات دورياً وقارنها
                بالإيرادات لضمان الربحية.
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default ExpensesReportPage;
