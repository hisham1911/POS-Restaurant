import { useState } from "react";
import {
  Calendar,
  TrendingUp,
  ShoppingBag,
  DollarSign,
  Receipt,
  Package,
  CreditCard,
  Banknote,
  Loader2,
  AlertCircle,
  Clock,
  Users,
  AlertTriangle,
  Info,
  Printer,
} from "lucide-react";
import { Card } from "@/components/common/Card";
import { formatCurrency, formatDateTimeFull } from "@/utils/formatters";
import {
  useGetDailyReportQuery,
  usePrintDailyReportMutation,
} from "@/api/reportsApi";
import { toast } from "sonner";
import { DailyReport } from "@/types/report.types";

/**
 * Generate receipt-style HTML for the daily report (thermal printer layout)
 */
const generateDailyReportReceiptHtml = (
  report: DailyReport,
  dateStr: string,
): string => {
  const formattedDate = new Date(dateStr).toLocaleDateString("ar-EG", {
    year: "numeric",
    month: "long",
    day: "numeric",
    weekday: "long",
  });

  const fmt = (n: number) => n.toFixed(2);

  // Shifts rows
  const shiftsHtml = report.shifts?.length
    ? report.shifts
        .map(
          (s) => `
      <div class="shift-row">
        <div class="shift-name">${s.userName}${s.isForceClosed ? " ⚠️" : ""}</div>
        <div class="shift-details">
          <span>${s.totalOrders} طلب</span>
          <span>${fmt(s.totalSales)} ج.م</span>
        </div>
        <div class="shift-payments">
          <span>نقدي: ${fmt(s.totalCash)}</span>
          <span>إلكتروني: ${fmt(s.totalCard)}</span>
          ${s.totalFawry > 0 ? `<span>فوري: ${fmt(s.totalFawry)}</span>` : ""}
        </div>
      </div>
    `,
        )
        .join("")
    : '<p class="no-data">لا توجد ورديات</p>';

  // Top products rows
  const topProductsHtml = report.topProducts?.length
    ? report.topProducts
        .slice(0, 10)
        .map(
          (p, i) => `
      <div class="product-row">
        <span class="product-rank">${i + 1}.</span>
        <span class="product-name">${p.productName}</span>
        <span class="product-qty">×${p.quantitySold}</span>
        <span class="product-total">${fmt(p.totalSales)}</span>
      </div>
    `,
        )
        .join("")
    : '<p class="no-data">لا توجد منتجات</p>';

  return `<!DOCTYPE html>
<html dir="rtl" lang="ar">
<head>
  <meta charset="UTF-8">
  <title>التقرير اليومي - ${formattedDate}</title>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body {
      font-family: 'Arial', 'Tahoma', sans-serif;
      max-width: 302px;
      margin: 0 auto;
      padding: 10px;
      font-size: 11px;
      color: #000;
      direction: rtl;
    }
    .header {
      text-align: center;
      margin-bottom: 8px;
    }
    .header h1 {
      font-size: 14px;
      font-weight: bold;
      margin-bottom: 2px;
    }
    .header h2 {
      font-size: 12px;
      font-weight: bold;
      margin-bottom: 4px;
    }
    .header .date {
      font-size: 10px;
      color: #333;
    }
    .branch-name {
      font-size: 13px;
      font-weight: bold;
      margin-bottom: 4px;
    }
    .line {
      border-top: 1px dashed #000;
      margin: 6px 0;
    }
    .double-line {
      border-top: 2px solid #000;
      margin: 8px 0;
    }
    .section-title {
      font-size: 11px;
      font-weight: bold;
      text-align: center;
      margin: 6px 0 4px;
      background: #000;
      color: #fff;
      padding: 2px 0;
    }
    .row {
      display: flex;
      justify-content: space-between;
      margin: 3px 0;
      font-size: 10px;
    }
    .row.total {
      font-weight: bold;
      font-size: 12px;
    }
    .row.highlight {
      font-weight: bold;
      font-size: 11px;
    }
    .row .label { }
    .row .value { font-weight: bold; }
    .shift-row {
      border: 1px solid #ccc;
      border-radius: 3px;
      padding: 4px 6px;
      margin: 4px 0;
    }
    .shift-name {
      font-weight: bold;
      font-size: 10px;
      margin-bottom: 2px;
    }
    .shift-details, .shift-payments {
      display: flex;
      justify-content: space-between;
      font-size: 9px;
      color: #333;
    }
    .product-row {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 9px;
      margin: 2px 0;
    }
    .product-rank { width: 16px; }
    .product-name { flex: 1; }
    .product-qty { width: 30px; text-align: center; }
    .product-total { width: 55px; text-align: left; }
    .no-data {
      text-align: center;
      color: #999;
      font-size: 9px;
      padding: 6px 0;
    }
    .footer {
      text-align: center;
      margin-top: 10px;
      font-size: 9px;
      color: #666;
    }
    @media print {
      body { padding: 0; }
      @page { margin: 2mm; size: 80mm auto; }
    }
  </style>
</head>
<body>
  <div class="header">
    <h1>📊 التقرير اليومي</h1>
    ${report.branchName ? `<div class="branch-name">${report.branchName}</div>` : ""}
    <div class="date">${formattedDate}</div>
  </div>

  <div class="double-line"></div>

  <!-- ملخص الطلبات -->
  <div class="section-title">📋 ملخص الطلبات</div>
  <div class="row"><span>إجمالي الطلبات</span><span class="value">${report.totalOrders}</span></div>
  <div class="row"><span>الطلبات المكتملة</span><span class="value">${report.completedOrders}</span></div>
  ${report.cancelledOrders > 0 ? `<div class="row"><span>الطلبات الملغاة</span><span class="value">${report.cancelledOrders}</span></div>` : ""}

  <div class="line"></div>

  <!-- ملخص المبيعات -->
  <div class="section-title">💰 المبيعات</div>
  <div class="row"><span>إجمالي المبيعات</span><span class="value">${fmt(report.totalSales)} ج.م</span></div>
  <div class="row"><span>صافي المبيعات</span><span class="value">${fmt(report.netSales)} ج.م</span></div>
  ${report.totalDiscount > 0 ? `<div class="row"><span>الخصومات</span><span class="value">-${fmt(report.totalDiscount)} ج.م</span></div>` : ""}
  ${report.totalTax > 0 ? `<div class="row"><span>الضرائب</span><span class="value">${fmt(report.totalTax)} ج.م</span></div>` : ""}
  ${report.totalRefunds > 0 ? `<div class="row"><span>المرتجعات</span><span class="value" style="color:red">-${fmt(report.totalRefunds)} ج.م</span></div>` : ""}

  <div class="line"></div>

  <!-- طرق الدفع -->
  <div class="section-title">💳 طرق الدفع</div>
  <div class="row"><span>💵 نقدي</span><span class="value">${fmt(report.totalCash)} ج.م</span></div>
  <div class="row"><span>💳 بطاقة</span><span class="value">${fmt(report.totalCard)} ج.م</span></div>
  ${report.totalFawry > 0 ? `<div class="row"><span>📱 فوري</span><span class="value">${fmt(report.totalFawry)} ج.م</span></div>` : ""}
  ${report.totalOther > 0 ? `<div class="row"><span>أخرى</span><span class="value">${fmt(report.totalOther)} ج.م</span></div>` : ""}

  <div class="double-line"></div>

  <!-- الإجمالي الكبير -->
  <div class="row total">
    <span>💰 صافي الإيراد</span>
    <span class="value">${fmt(report.totalSales)} ج.م</span>
  </div>

  <div class="double-line"></div>

  <!-- الورديات -->
  ${
    report.shifts?.length
      ? `
  <div class="section-title">👥 الورديات (${report.totalShifts})</div>
  ${shiftsHtml}
  <div class="line"></div>
  `
      : ""
  }

  <!-- أعلى المنتجات -->
  ${
    report.topProducts?.length
      ? `
  <div class="section-title">🏆 أعلى المنتجات مبيعاً</div>
  ${topProductsHtml}
  <div class="line"></div>
  `
      : ""
  }

  <div class="footer">
    <p>تم الطباعة: ${new Date().toLocaleString("ar-EG")}</p>
    <p>TajerPro POS System</p>
  </div>

  <script>window.onload = function() { window.print(); }</script>
</body>
</html>`;
};

/**
 * Open print window with daily report receipt
 */
const printDailyReportLocally = (report: DailyReport, dateStr: string) => {
  const html = generateDailyReportReceiptHtml(report, dateStr);
  const printWindow = window.open("", "_blank", "width=350,height=700");
  if (printWindow) {
    printWindow.document.write(html);
    printWindow.document.close();
  }
};

export const DailyReportPage = () => {
  const [selectedDate, setSelectedDate] = useState(
    new Date().toISOString().split("T")[0],
  );

  const { data, isLoading, isError, error } =
    useGetDailyReportQuery(selectedDate);
  const [printDailyReport, { isLoading: isPrintingThermal }] =
    usePrintDailyReportMutation();
  const report = data?.data;

  /** طباعة عبر الطابعة الحرارية (BridgeApp) مع fallback للطباعة المحلية */
  const handleThermalPrint = async () => {
    if (!report) return;
    try {
      await printDailyReport(selectedDate).unwrap();
      toast.success("تم إرسال أمر الطباعة للطابعة الحرارية بنجاح");
    } catch {
      // Fallback: إذا فشل الإرسال للطابعة الحرارية، نطبع محلياً
      toast.info("جاري فتح نافذة الطباعة...");
      printDailyReportLocally(report, selectedDate);
    }
  };

  /** طباعة محلية (فتح نافذة طباعة بتصميم فاتورة) */
  const handleLocalPrint = () => {
    if (!report) return;
    printDailyReportLocally(report, selectedDate);
  };

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
          <h1 className="text-2xl font-bold text-gray-800">التقرير اليومي</h1>
          <p className="text-gray-500 mt-1">
            {report?.branchName || "ملخص المبيعات والإحصائيات"}
          </p>
        </div>
        <div className="flex items-center gap-3">
          {/* Print Buttons */}
          {report && (
            <div className="flex items-center gap-2">
              <button
                onClick={handleLocalPrint}
                className="flex items-center gap-2 px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 transition-colors text-sm font-medium shadow-sm"
                title="طباعة التقرير كفاتورة"
              >
                <Printer className="w-4 h-4" />
                طباعة التقرير
              </button>
              <button
                onClick={handleThermalPrint}
                disabled={isPrintingThermal}
                className="flex items-center gap-2 px-3 py-2 bg-gray-100 text-gray-700 rounded-lg hover:bg-gray-200 transition-colors text-sm disabled:opacity-50"
                title="إرسال للطابعة الحرارية"
              >
                <Receipt className="w-4 h-4" />
                {isPrintingThermal ? "جاري الإرسال..." : "طابعة حرارية"}
              </button>
            </div>
          )}
          <div className="flex items-center gap-2">
            <Calendar className="w-5 h-5 text-gray-400" />
            <input
              type="date"
              value={selectedDate}
              onChange={(e) => setSelectedDate(e.target.value)}
              className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
            />
          </div>
        </div>
      </div>

      {/* Info Banner */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <div className="flex items-start gap-3">
          <AlertCircle className="w-5 h-5 text-blue-600 mt-0.5 flex-shrink-0" />
          <div className="flex-1">
            <p className="text-sm text-blue-800 font-medium">
              💡 التقرير اليومي يعرض الورديات التي أُغلقت في هذا اليوم
            </p>
            <p className="text-xs text-blue-600 mt-1">
              الوردية التي تفتح في يوم وتغلق في اليوم التالي، تُحسب كاملة في
              تقرير يوم الإغلاق. مثال: وردية من 8 مساءً (15 يناير) → 4 صباحاً
              (16 يناير) تظهر في تقرير 16 يناير.
            </p>
          </div>
        </div>
      </div>

      {/* Shifts Section */}
      {report?.shifts && report.shifts.length > 0 && (
        <Card>
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-lg font-bold text-gray-800">
              الورديات المغلقة ({report.totalShifts})
            </h3>
            <Users className="w-5 h-5 text-gray-400" />
          </div>
          <div className="space-y-3">
            {report.shifts.map((shift) => (
              <div
                key={shift.shiftId}
                className="border border-gray-200 rounded-lg p-4 hover:bg-gray-50 transition-colors"
              >
                <div className="flex items-start justify-between mb-3">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 bg-primary-100 rounded-full flex items-center justify-center">
                      <Users className="w-5 h-5 text-primary-600" />
                    </div>
                    <div>
                      <p className="font-medium text-gray-800">
                        {shift.userName}
                      </p>
                      <p className="text-xs text-gray-500">
                        وردية #{shift.shiftId}
                      </p>
                    </div>
                  </div>
                  {shift.isForceClosed && (
                    <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-orange-100 text-orange-800">
                      <AlertTriangle className="w-3 h-3 ml-1" />
                      إغلاق قسري
                    </span>
                  )}
                </div>

                <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-3">
                  <div>
                    <p className="text-xs text-gray-500">وقت الفتح</p>
                    <p className="text-sm font-medium text-gray-700">
                      {formatDateTimeFull(shift.openedAt)}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">وقت الإغلاق</p>
                    <p className="text-sm font-medium text-gray-700">
                      {formatDateTimeFull(shift.closedAt)}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">الطلبات</p>
                    <p className="text-sm font-medium text-gray-700">
                      {shift.totalOrders}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-gray-500">الإجمالي</p>
                    <p className="text-sm font-medium text-primary-600">
                      {formatCurrency(shift.totalSales)}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-4 pt-3 border-t border-gray-100 flex-wrap">
                  <div className="flex items-center gap-2">
                    <Banknote className="w-4 h-4 text-green-600" />
                    <span className="text-sm text-gray-600">نقدي:</span>
                    <span className="text-sm font-medium text-green-600">
                      {formatCurrency(shift.totalCash)}
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    <CreditCard className="w-4 h-4 text-blue-600" />
                    <span className="text-sm text-gray-600">إلكتروني:</span>
                    <span className="text-sm font-medium text-blue-600">
                      {formatCurrency(shift.totalCard)}
                    </span>
                  </div>
                  {shift.totalFawry > 0 && (
                    <div className="flex items-center gap-2">
                      <span className="text-sm text-gray-600">فوري:</span>
                      <span className="text-sm font-medium text-purple-600">
                        {formatCurrency(shift.totalFawry)}
                      </span>
                    </div>
                  )}
                </div>

                {shift.forceCloseReason && (
                  <div className="mt-3 pt-3 border-t border-gray-100">
                    <p className="text-xs text-gray-500">سبب الإغلاق القسري:</p>
                    <p className="text-sm text-orange-700 mt-1">
                      {shift.forceCloseReason}
                    </p>
                  </div>
                )}
              </div>
            ))}
          </div>
        </Card>
      )}

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-primary-100 rounded-xl flex items-center justify-center">
              <ShoppingBag className="w-6 h-6 text-primary-600" />
            </div>
            <div>
              <p className="text-sm text-gray-500">الطلبات المكتملة</p>
              <p className="text-2xl font-bold text-gray-800">
                {report?.completedOrders || 0}
              </p>
              <p className="text-xs text-gray-400">
                من {report?.totalOrders || 0} طلب
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-success-50 rounded-xl flex items-center justify-center">
              <DollarSign className="w-6 h-6 text-success-500" />
            </div>
            <div>
              <p className="text-sm text-gray-500">إجمالي المبيعات</p>
              <p className="text-2xl font-bold text-gray-800">
                {formatCurrency(report?.totalSales || 0)}
              </p>
              <p className="text-xs text-gray-400">
                صافي: {formatCurrency(report?.netSales || 0)}
              </p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-red-50 rounded-xl flex items-center justify-center">
              <TrendingUp className="w-6 h-6 text-red-500" />
            </div>
            <div>
              <p className="text-sm text-gray-500">المرتجعات</p>
              <p className="text-2xl font-bold text-red-600">
                {formatCurrency(report?.totalRefunds || 0)}
              </p>
              <p className="text-xs text-gray-400">إجمالي المرتجعات</p>
            </div>
          </div>
        </Card>

        <Card>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 bg-warning-50 rounded-xl flex items-center justify-center">
              <Receipt className="w-6 h-6 text-warning-500" />
            </div>
            <div>
              <p className="text-sm text-gray-500">الضرائب</p>
              <p className="text-2xl font-bold text-gray-800">
                {formatCurrency(report?.totalTax || 0)}
              </p>
              <p className="text-xs text-gray-400">14% VAT</p>
            </div>
          </div>
        </Card>
      </div>

      {/* Payment Methods Row */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <div className="flex items-center gap-3">
            <Banknote className="w-8 h-8 text-green-600" />
            <div>
              <p className="text-sm text-gray-500">نقدي</p>
              <p className="text-xl font-bold text-green-600">
                {formatCurrency(report?.totalCash || 0)}
              </p>
            </div>
          </div>
        </Card>
        <Card>
          <div className="flex items-center gap-3">
            <CreditCard className="w-8 h-8 text-blue-600" />
            <div>
              <p className="text-sm text-gray-500">بطاقة</p>
              <p className="text-xl font-bold text-blue-600">
                {formatCurrency(report?.totalCard || 0)}
              </p>
            </div>
          </div>
        </Card>
        <Card>
          <div className="flex items-center gap-3">
            <Receipt className="w-8 h-8 text-orange-600" />
            <div>
              <p className="text-sm text-gray-500">فوري</p>
              <p className="text-xl font-bold text-orange-600">
                {formatCurrency(report?.totalFawry || 0)}
              </p>
            </div>
          </div>
        </Card>
        <Card>
          <div className="flex items-center gap-3">
            <TrendingUp className="w-8 h-8 text-purple-600" />
            <div>
              <p className="text-sm text-gray-500">الخصومات</p>
              <p className="text-xl font-bold text-purple-600">
                {formatCurrency(report?.totalDiscount || 0)}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Sales by Payment Method */}
        <Card>
          <h3 className="text-lg font-bold text-gray-800 mb-4">
            المبيعات حسب طريقة الدفع
          </h3>
          <div className="space-y-4">
            {[
              {
                label: "نقدي",
                value: report?.totalCash || 0,
                color: "bg-green-500",
              },
              {
                label: "بطاقة",
                value: report?.totalCard || 0,
                color: "bg-blue-500",
              },
              {
                label: "فوري",
                value: report?.totalFawry || 0,
                color: "bg-orange-500",
              },
            ].map((item) => {
              const total = report?.totalSales || 1;
              const percentage = (item.value / total) * 100;
              return (
                <div key={item.label}>
                  <div className="flex justify-between mb-1">
                    <span className="text-gray-600">{item.label}</span>
                    <span className="font-medium">
                      {formatCurrency(item.value)}
                    </span>
                  </div>
                  <div className="h-3 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className={`h-full ${item.color} rounded-full transition-all`}
                      style={{ width: `${Math.min(percentage, 100)}%` }}
                    />
                  </div>
                </div>
              );
            })}
          </div>
        </Card>

        {/* Hourly Sales */}
        <Card>
          <h3 className="text-lg font-bold text-gray-800 mb-4">
            المبيعات بالساعة
          </h3>
          <div className="space-y-2 max-h-64 overflow-y-auto">
            {report?.hourlySales?.length ? (
              report.hourlySales.map((hourData) => (
                <div
                  key={hourData.hour}
                  className="flex items-center justify-between py-2 border-b border-gray-100"
                >
                  <span className="text-gray-600">
                    {hourData.hour.toString().padStart(2, "0")}:00
                  </span>
                  <div className="text-left">
                    <span className="font-medium text-gray-800">
                      {formatCurrency(hourData.sales)}
                    </span>
                    <span className="text-gray-400 text-sm mr-2">
                      ({hourData.orderCount} طلب)
                    </span>
                  </div>
                </div>
              ))
            ) : (
              <p className="text-gray-400 text-center py-4">لا توجد بيانات</p>
            )}
          </div>
        </Card>
      </div>

      {/* Top Products */}
      <Card>
        <h3 className="text-lg font-bold text-gray-800 mb-4">
          أعلى المنتجات مبيعاً
        </h3>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  #
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  المنتج
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الكمية
                </th>
                <th className="px-4 py-3 text-right font-semibold text-gray-600">
                  الإجمالي
                </th>
              </tr>
            </thead>
            <tbody>
              {report?.topProducts?.length ? (
                report.topProducts.map((product, index) => (
                  <tr
                    key={product.productId}
                    className="border-b hover:bg-gray-50"
                  >
                    <td className="px-4 py-3 text-gray-500">{index + 1}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 bg-gray-100 rounded-lg flex items-center justify-center">
                          <Package className="w-4 h-4 text-gray-400" />
                        </div>
                        <span className="font-medium">
                          {product.productName}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {product.quantitySold}
                    </td>
                    <td className="px-4 py-3 font-semibold text-primary-600">
                      {formatCurrency(product.totalSales)}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td
                    colSpan={4}
                    className="px-4 py-8 text-center text-gray-400"
                  >
                    لا توجد منتجات مباعة
                  </td>
                </tr>
              )}
            </tbody>
          </table>
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
                • <strong>التقرير اليومي:</strong> يعرض ملخص الورديات المغلقة في
                اليوم المحدد
              </li>
              <li>
                • <strong>المبيعات:</strong> إجمالي العمليات المكتملة في كل
                وردية
              </li>
              <li>
                • <strong>الورديات:</strong> تُحسب في يوم الإغلاق (وليس الفتح)
              </li>
              <li>
                • <strong>التحصيل:</strong> النقدي والبطاقة وطرق الدفع الأخرى
              </li>
            </ul>
          </div>
        </div>
      </Card>
    </div>
  );
};

export default DailyReportPage;
