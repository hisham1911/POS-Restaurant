import { useState, useMemo, type ReactNode } from "react";
import clsx from "clsx";
import {
  X,
  Clock3,
  User,
  Wallet,
  Receipt,
  CreditCard,
  Banknote,
  ArrowUpCircle,
  CheckCircle2,
  ShoppingBag,
  AlertCircle,
  StickyNote,
  Printer,
} from "lucide-react";
import { Portal } from "@/components/common/Portal";
import { Loading } from "@/components/common/Loading";
import { useGetShiftByIdQuery, useGetShiftProductsSummaryQuery } from "@/api/shiftsApi";
import type { Shift, ShiftOrder } from "@/types/shift.types";
import { formatCurrency, formatDateTime } from "@/utils/formatters";

interface ShiftDetailsDrawerProps {
  shift: Shift | null;
  isOpen: boolean;
  onClose: () => void;
}

type TabType = "overview" | "orders";

interface StatCardProps {
  icon: ReactNode;
  label: string;
  value: string;
  valueClassName?: string;
}

interface DetailRowProps {
  label: string;
  value: React.ReactNode;
  tone?: "default" | "success" | "danger" | "primary";
}

const StatCard = ({ icon, label, value, valueClassName }: StatCardProps) => (
  <div className="text-center">
    <div className="mb-1 flex items-center justify-center gap-1 text-sm text-gray-500">
      {icon}
      {label}
    </div>
    <p className={clsx("text-lg font-bold text-gray-800", valueClassName)}>
      {value}
    </p>
  </div>
);

const DetailRow = ({ label, value, tone = "default" }: DetailRowProps) => (
  <div className="flex items-center justify-between gap-3 py-2">
    <span className="text-sm text-gray-600">{label}</span>
    <span
      className={clsx(
        "text-sm font-semibold",
        tone === "default" && "text-gray-900",
        tone === "success" && "text-green-600",
        tone === "danger" && "text-red-600",
        tone === "primary" && "text-primary-600",
      )}
    >
      {value}
    </span>
  </div>
);

const getStatusBadge = (status: string) => {
  const statusMap: Record<string, { label: string; color: string }> = {
    Completed: { label: "مكتمل", color: "bg-green-100 text-green-800" },
    Cancelled: { label: "ملغي", color: "bg-red-100 text-red-800" },
    Refunded: { label: "مسترجع", color: "bg-orange-100 text-orange-800" },
    PartiallyRefunded: {
      label: "مسترجع جزئيًا",
      color: "bg-orange-100 text-orange-800",
    },
    Pending: { label: "قيد الانتظار", color: "bg-yellow-100 text-yellow-800" },
    Draft: { label: "مسودة", color: "bg-gray-100 text-gray-800" },
  };

  const { label, color } = statusMap[status] || {
    label: status,
    color: "bg-gray-100 text-gray-800",
  };

  return (
    <span className={clsx("rounded-full px-2 py-1 text-xs font-medium", color)}>
      {label}
    </span>
  );
};

const getOrderTypeLabel = (order: ShiftOrder) => {
  const labels: Record<string, string> = {
    DineIn: "بيع",
    Takeaway: "بيع",
    Delivery: "توصيل",
    Return: "مرتجع",
  };

  if (!order.orderType) {
    return "—";
  }

  return labels[order.orderType] ?? order.orderType;
};

// Aggregate products from all orders
interface AggregatedProduct {
  productName: string;
  totalQuantity: number;
  totalAmount: number;
}

const aggregateProducts = (orders: ShiftOrder[]): AggregatedProduct[] => {
  const productMap = new Map<string, AggregatedProduct>();

  // Note: ShiftOrder doesn't include items, so we can't aggregate here
  // This would need to be fetched from the backend or passed separately
  // For now, we'll return empty array and document this limitation
  
  return Array.from(productMap.values()).sort((a, b) => b.totalQuantity - a.totalQuantity);
};

export const ShiftDetailsDrawer = ({
  shift,
  isOpen,
  onClose,
}: ShiftDetailsDrawerProps) => {
  const [activeTab, setActiveTab] = useState<TabType>("overview");

  // Print handler
  const handlePrint = () => {
    const shiftDetails = shiftResponse?.data ?? shift;
    if (!shiftDetails) return;

    const html = generateShiftReceiptHtml(shiftDetails, productsSummary);
    const printWindow = window.open("", "_blank", "width=350,height=700");
    if (printWindow) {
      printWindow.document.write(html);
      printWindow.document.close();
    }
  };

  const generateShiftReceiptHtml = (shiftData: Shift, products: typeof productsSummary): string => {
    const fmt = (n: number) => n.toFixed(2);
    
    const openedAt = shiftData.openedAt ? new Date(shiftData.openedAt).toLocaleString("ar-EG") : "—";
    const closedAt = shiftData.closedAt ? new Date(shiftData.closedAt).toLocaleString("ar-EG") : "—";
    
    const difference = shiftData.difference ?? 0;
    const hasDifference = difference !== 0;
    const isPositiveDifference = difference > 0;

    // Products rows
    const productsHtml = products?.length
      ? products
          .map(
            (p, i) => `
      <div class="product-row">
        <span class="product-rank">${i + 1}.</span>
        <span class="product-name">${p.productName}</span>
        <span class="product-qty">×${p.totalQuantity}</span>
        <span class="product-total">${fmt(p.totalAmount)}</span>
      </div>
    `,
          )
          .join("")
      : '<p class="no-data">لا توجد منتجات</p>';

    // Calculate totals
    const totalQuantity = products?.reduce((sum, p) => sum + p.totalQuantity, 0) ?? 0;
    const totalAmount = products?.reduce((sum, p) => sum + p.totalAmount, 0) ?? 0;

    return `<!DOCTYPE html>
<html dir="rtl" lang="ar">
<head>
  <meta charset="UTF-8">
  <title>تقرير الوردية #${shiftData.id}</title>
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
    .warning {
      background: #fff3cd;
      border: 1px solid #ffc107;
      padding: 4px;
      margin: 4px 0;
      font-size: 9px;
      text-align: center;
      border-radius: 2px;
    }
    @media print {
      body { padding: 0; }
      @page { margin: 2mm; size: 80mm auto; }
    }
  </style>
</head>
<body>
  <div class="header">
    <h1>📊 تقرير الوردية</h1>
    <h2>وردية #${shiftData.id}</h2>
    <div class="date">${shiftData.userName ?? "—"}</div>
  </div>

  <div class="double-line"></div>

  <!-- معلومات الوردية -->
  <div class="section-title">⏰ معلومات الوردية</div>
  <div class="row"><span>وقت الفتح</span><span class="value">${openedAt}</span></div>
  ${shiftData.closedAt ? `<div class="row"><span>وقت الإغلاق</span><span class="value">${closedAt}</span></div>` : ""}
  <div class="row"><span>الحالة</span><span class="value">${shiftData.closedAt ? "مغلقة" : "مفتوحة"}</span></div>

  <div class="line"></div>

  <!-- الأرصدة -->
  <div class="section-title">💰 الأرصدة</div>
  <div class="row"><span>رصيد الافتتاح</span><span class="value">${fmt(shiftData.openingBalance ?? 0)} ج.م</span></div>
  ${shiftData.closedAt ? `<div class="row"><span>رصيد الإغلاق</span><span class="value">${fmt(shiftData.closingBalance ?? 0)} ج.م</span></div>` : ""}
  ${shiftData.closedAt ? `<div class="row"><span>الرصيد المتوقع</span><span class="value">${fmt(shiftData.expectedBalance ?? 0)} ج.م</span></div>` : ""}
  ${hasDifference ? `<div class="row ${isPositiveDifference ? "highlight" : ""}"><span>${isPositiveDifference ? "فائض" : "عجز"}</span><span class="value" style="color:${isPositiveDifference ? "green" : "red"}">${isPositiveDifference ? "+" : ""}${fmt(difference)} ج.م</span></div>` : ""}

  <div class="line"></div>

  <!-- المبيعات -->
  <div class="section-title">💵 المبيعات</div>
  <div class="row"><span>إجمالي المبيعات</span><span class="value">${fmt(shiftData.totalSales ?? 0)} ج.م</span></div>
  <div class="row"><span>عدد الطلبات</span><span class="value">${shiftData.totalOrders ?? 0}</span></div>

  <div class="line"></div>

  <!-- طرق الدفع -->
  <div class="section-title">💳 طرق الدفع</div>
  <div class="row"><span>💵 نقدي</span><span class="value">${fmt(shiftData.totalCash ?? 0)} ج.م</span></div>
  <div class="row"><span>🏦 حساب بنكي</span><span class="value">${fmt(shiftData.totalBankAccount ?? 0)} ج.م</span></div>
  ${(shiftData.totalWallet ?? 0) > 0 ? `<div class="row"><span>📱 محفظة</span><span class="value">${fmt(shiftData.totalWallet ?? 0)} ج.م</span></div>` : ""}

  <div class="double-line"></div>

  <!-- المنتجات -->
  <div class="section-title">📦 ملخص المنتجات</div>
  ${productsHtml}
  ${products?.length ? `
  <div class="line"></div>
  <div class="row total">
    <span>الإجمالي</span>
    <span class="value">×${totalQuantity} = ${fmt(totalAmount)} ج.م</span>
  </div>
  ` : ""}
  <div class="double-line"></div>

  <div class="footer">
    <p>تم الطباعة: ${new Date().toLocaleString("ar-EG")}</p>
    <p>نظام نقاط البيع TajerPro</p>
  </div>

  <script>window.onload = function() { window.print(); }</script>
</body>
</html>`;
  };

  const shiftId = shift?.id ?? 0;
  const {
    data: shiftResponse,
    isLoading,
    isFetching,
  } = useGetShiftByIdQuery(shiftId, {
    skip: !isOpen || !shiftId,
  });

  const {
    data: productsSummaryResponse,
    isLoading: isLoadingProducts,
  } = useGetShiftProductsSummaryQuery(shiftId, {
    skip: !isOpen || !shiftId,
  });

  if (!isOpen || !shift) {
    return null;
  }

  const shiftDetails = shiftResponse?.data ?? shift;
  const productsSummary = productsSummaryResponse?.data ?? [];
  const orders = shiftDetails.orders ?? [];
  const difference = shiftDetails.difference ?? 0;
  const hasDifference = difference !== 0;
  const isPositiveDifference = difference > 0;
  const isBusy = isLoading || (isFetching && !shiftResponse?.data);

  return (
    <Portal>
      <div className="fixed inset-0 z-[100] bg-black/50" onClick={onClose} />

      <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
        <div
          className="flex max-h-[85vh] w-full max-w-4xl flex-col rounded-2xl bg-white shadow-xl"
          onClick={(event) => event.stopPropagation()}
        >
          <div className="flex items-center justify-between border-b p-6">
            <div className="flex items-center gap-4">
              <div className="flex h-14 w-14 items-center justify-center rounded-full bg-primary-100">
                <Wallet className="h-7 w-7 text-primary-600" />
              </div>
              <div>
                <h2 className="text-xl font-bold text-gray-800">
                  تفاصيل الوردية #{shiftDetails.id}
                </h2>
                <p className="mt-0.5 flex items-center gap-1 text-sm text-gray-500">
                  <User className="h-4 w-4" />
                  {shiftDetails.userName || "—"}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={handlePrint}
                className="flex items-center gap-2 rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-primary-700"
              >
                <Printer className="h-4 w-4" />
                طباعة التقرير
              </button>
              <span
                className={clsx(
                  "inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-semibold",
                  shiftDetails.isClosed
                    ? "bg-gray-100 text-gray-700"
                    : "bg-green-100 text-green-700",
                )}
              >
                {shiftDetails.isClosed ? "مغلقة" : "مفتوحة"}
              </span>
              {shiftDetails.isReconciled && (
                <span className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-3 py-1 text-xs font-semibold text-blue-700">
                  <CheckCircle2 className="h-3.5 w-3.5" />
                  تمت المراجعة
                </span>
              )}
              <button
                onClick={onClose}
                className="rounded-lg p-2 transition-colors hover:bg-gray-100"
              >
                <X className="h-5 w-5 text-gray-500" />
              </button>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4 border-b bg-gray-50 p-4 md:grid-cols-4">
            <StatCard
              icon={<ShoppingBag className="h-4 w-4" />}
              label="إجمالي الطلبات"
              value={(shiftDetails.totalOrdersCount ?? shiftDetails.totalOrders ?? 0).toString()}
            />
            <StatCard
              icon={<Receipt className="h-4 w-4" />}
              label="إجمالي المبيعات"
              value={formatCurrency(shiftDetails.totalSales ?? 0)}
              valueClassName="text-green-600"
            />
            <StatCard
              icon={<Banknote className="h-4 w-4" />}
              label="الرصيد الفعلي"
              value={formatCurrency(shiftDetails.closingBalance ?? 0)}
            />
            <StatCard
              icon={<AlertCircle className="h-4 w-4" />}
              label="الفرق"
              value={
                hasDifference
                  ? `${isPositiveDifference ? "+" : ""}${formatCurrency(difference)}`
                  : formatCurrency(0)
              }
              valueClassName={
                hasDifference
                  ? isPositiveDifference
                    ? "text-green-600"
                    : "text-red-600"
                  : "text-gray-800"
              }
            />
          </div>

          <div className="flex border-b">
            <button
              onClick={() => setActiveTab("overview")}
              className={clsx(
                "flex-1 py-3 text-sm font-medium transition-colors",
                activeTab === "overview"
                  ? "border-b-2 border-primary-600 text-primary-600"
                  : "text-gray-500 hover:text-gray-700",
              )}
            >
              الملخص المالي
            </button>
            <button
              onClick={() => setActiveTab("orders")}
              className={clsx(
                "flex-1 py-3 text-sm font-medium transition-colors",
                activeTab === "orders"
                  ? "border-b-2 border-primary-600 text-primary-600"
                  : "text-gray-500 hover:text-gray-700",
              )}
            >
              سجل الطلبات ({orders.length})
            </button>
          </div>

          <div className="flex-1 overflow-y-auto p-4">
            {isBusy ? (
              <div className="flex justify-center py-10">
                <Loading />
              </div>
            ) : activeTab === "overview" ? (
              <div className="space-y-4">
                <div className="rounded-2xl border border-gray-200 bg-white p-4">
                  <h3 className="mb-3 text-sm font-bold text-gray-900">
                    معلومات الوردية
                  </h3>
                  <div className="grid gap-3 md:grid-cols-2">
                    <div className="rounded-xl bg-gray-50 p-4">
                      <div className="mb-2 flex items-center gap-2 text-sm text-gray-500">
                        <Clock3 className="h-4 w-4" />
                        فتح الوردية
                      </div>
                      <p className="font-semibold text-gray-900">
                        {formatDateTime(shiftDetails.openedAt)}
                      </p>
                    </div>

                    <div className="rounded-xl bg-gray-50 p-4">
                      <div className="mb-2 flex items-center gap-2 text-sm text-gray-500">
                        <Clock3 className="h-4 w-4" />
                        إغلاق الوردية
                      </div>
                      <p className="font-semibold text-gray-900">
                        {shiftDetails.closedAt
                          ? formatDateTime(shiftDetails.closedAt)
                          : "—"}
                      </p>
                    </div>

                    <div className="rounded-xl bg-gray-50 p-4">
                      <div className="mb-2 flex items-center gap-2 text-sm text-gray-500">
                        <User className="h-4 w-4" />
                        الكاشير
                      </div>
                      <p className="font-semibold text-gray-900">
                        {shiftDetails.userName || "—"}
                      </p>
                    </div>

                    <div className="rounded-xl bg-gray-50 p-4">
                      <div className="mb-2 flex items-center gap-2 text-sm text-gray-500">
                        <CheckCircle2 className="h-4 w-4" />
                        حالة المراجعة
                      </div>
                      <p className="font-semibold text-gray-900">
                        {shiftDetails.isReconciled
                          ? `تمت${shiftDetails.reconciledAt ? ` في ${formatDateTime(shiftDetails.reconciledAt)}` : ""}`
                          : "لم تتم"}
                      </p>
                    </div>
                  </div>
                </div>

                <div className="rounded-2xl border border-gray-200 bg-white p-4">
                  <h3 className="mb-3 text-sm font-bold text-gray-900">
                    الملخص المالي
                  </h3>

                  <div className="divide-y divide-gray-100">
                    <DetailRow
                      label="رصيد الافتتاح"
                      value={formatCurrency(shiftDetails.openingBalance ?? 0)}
                    />
                    <DetailRow
                      label="إجمالي المبيعات"
                      value={formatCurrency(shiftDetails.totalSales ?? 0)}
                    />
                    <DetailRow
                      label="إجمالي المقبوض"
                      value={formatCurrency(shiftDetails.totalCollected ?? 0)}
                    />
                    <DetailRow
                      label="المبلغ الآجل"
                      value={formatCurrency(shiftDetails.deferredAmount ?? 0)}
                    />
                    <DetailRow
                      label="ديون مسددة"
                      value={formatCurrency(shiftDetails.totalDebtPayments ?? 0)}
                      tone={(shiftDetails.totalDebtPayments ?? 0) > 0 ? "success" : "default"}
                    />
                    <DetailRow
                      label="المصروفات"
                      value={formatCurrency(shiftDetails.totalExpenses ?? 0)}
                      tone={
                        (shiftDetails.totalExpenses ?? 0) > 0 ? "danger" : "default"
                      }
                    />
                    <DetailRow
                      label="المرتجعات"
                      value={formatCurrency(shiftDetails.totalRefunds ?? 0)}
                      tone={
                        (shiftDetails.totalRefunds ?? 0) > 0 ? "danger" : "default"
                      }
                    />
                    <DetailRow
                      label="الرصيد المتوقع"
                      value={formatCurrency(shiftDetails.expectedBalance ?? 0)}
                      tone="primary"
                    />
                    <DetailRow
                      label="الرصيد الفعلي"
                      value={formatCurrency(shiftDetails.closingBalance ?? 0)}
                    />
                    <DetailRow
                      label={
                        hasDifference
                          ? isPositiveDifference
                            ? "فائض"
                            : "عجز"
                          : "الفرق"
                      }
                      value={
                        hasDifference
                          ? `${isPositiveDifference ? "+" : ""}${formatCurrency(difference)}`
                          : formatCurrency(0)
                      }
                      tone={
                        hasDifference
                          ? isPositiveDifference
                            ? "success"
                            : "danger"
                          : "default"
                      }
                    />
                  </div>
                </div>

                <div className="rounded-2xl border border-gray-200 bg-white p-4">
                  <h3 className="mb-3 text-sm font-bold text-gray-900">
                    التحصيل حسب وسيلة الدفع
                  </h3>
                  <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
                    <div className="rounded-xl bg-green-50 p-3">
                      <div className="mb-1 flex items-center gap-1 text-xs text-green-700">
                        <Banknote className="h-3.5 w-3.5" />
                        نقدي
                      </div>
                      <p className="font-bold text-green-800">
                        {formatCurrency(shiftDetails.totalCash ?? 0)}
                      </p>
                    </div>
                    <div className="rounded-xl bg-blue-50 p-3">
                      <div className="mb-1 text-xs text-blue-700 flex items-center gap-1">
                        <CreditCard className="h-3 w-3" />
                        بنك
                      </div>
                      <p className="font-bold text-blue-800">
                        {formatCurrency(shiftDetails.totalBankAccount ?? 0)}
                      </p>
                    </div>
                    <div className="rounded-xl bg-amber-50 p-3">
                      <div className="mb-1 text-xs text-amber-700 flex items-center gap-1">
                        <Wallet className="h-3 w-3" />
                        محفظة
                      </div>
                      <p className="font-bold text-amber-800">
                        {formatCurrency(shiftDetails.totalWallet ?? 0)}
                      </p>
                    </div>
                  </div>
                </div>

                <div className="grid gap-4 md:grid-cols-2">
                  <div className="rounded-2xl border border-gray-200 bg-white p-4">
                    <h3 className="mb-3 text-sm font-bold text-gray-900">
                      ملخص الطلبات
                    </h3>
                    <div className="grid grid-cols-2 gap-3">
                      <div className="rounded-xl bg-green-50 p-3 text-center">
                        <p className="text-lg font-bold text-green-700">
                          {shiftDetails.completedOrdersCount ?? 0}
                        </p>
                        <p className="text-xs text-green-600">مكتملة</p>
                      </div>
                      <div className="rounded-xl bg-red-50 p-3 text-center">
                        <p className="text-lg font-bold text-red-700">
                          {shiftDetails.cancelledOrdersCount ?? 0}
                        </p>
                        <p className="text-xs text-red-600">ملغاة</p>
                      </div>
                      <div className="rounded-xl bg-orange-50 p-3 text-center">
                        <p className="text-lg font-bold text-orange-700">
                          {shiftDetails.refundedOrdersCount ?? 0}
                        </p>
                        <p className="text-xs text-orange-600">مسترجعة</p>
                      </div>
                      <div className="rounded-xl bg-blue-50 p-3 text-center">
                        <p className="text-lg font-bold text-blue-700">
                          {shiftDetails.creditOrdersCount ?? 0}
                        </p>
                        <p className="text-xs text-blue-600">آجلة</p>
                      </div>
                    </div>
                  </div>

                  {/* Debt Payments Card */}
                  {(shiftDetails.debtPaymentsCount ?? 0) > 0 && (
                    <div className="rounded-2xl border border-teal-200 bg-teal-50 p-4">
                      <h3 className="mb-3 flex items-center gap-2 text-sm font-bold text-teal-900">
                        <Receipt className="h-4 w-4" />
                        سداد الديون
                      </h3>
                      <div className="space-y-3">
                        <div className="rounded-xl bg-white p-3">
                          <div className="flex items-center justify-between">
                            <span className="text-xs text-gray-600">
                              إجمالي المبلغ
                            </span>
                            <span className="text-lg font-bold text-teal-700">
                              {formatCurrency(shiftDetails.totalDebtPayments ?? 0)}
                            </span>
                          </div>
                          <div className="mt-1 flex items-center justify-between">
                            <span className="text-xs text-gray-500">
                              عدد الدفعات
                            </span>
                            <span className="text-sm font-semibold text-gray-700">
                              {shiftDetails.debtPaymentsCount ?? 0}
                            </span>
                          </div>
                        </div>

                        <div className="grid grid-cols-2 gap-2">
                          {(shiftDetails.totalDebtPaymentsCash ?? 0) > 0 && (
                            <div className="rounded-lg bg-white p-2">
                              <div className="mb-1 flex items-center gap-1 text-xs text-green-700">
                                <Banknote className="h-3 w-3" />
                                نقدي
                              </div>
                              <p className="text-sm font-bold text-green-800">
                                {formatCurrency(shiftDetails.totalDebtPaymentsCash ?? 0)}
                              </p>
                            </div>
                          )}
                          {(shiftDetails.totalDebtPaymentsBankAccount ?? 0) > 0 && (
                            <div className="rounded-lg bg-white p-2">
                              <div className="mb-1 flex items-center gap-1 text-xs text-blue-700">
                                <CreditCard className="h-3 w-3" />
                                بنك
                              </div>
                              <p className="text-sm font-bold text-blue-800">
                                {formatCurrency(shiftDetails.totalDebtPaymentsBankAccount ?? 0)}
                              </p>
                            </div>
                          )}
                          {(shiftDetails.totalDebtPaymentsWallet ?? 0) > 0 && (
                            <div className="rounded-lg bg-white p-2">
                              <div className="mb-1 flex items-center gap-1 text-xs text-amber-700">
                                <Wallet className="h-3 w-3" />
                                محفظة
                              </div>
                              <p className="text-sm font-bold text-amber-800">
                                {formatCurrency(shiftDetails.totalDebtPaymentsWallet ?? 0)}
                              </p>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  )}

                  <div className="rounded-2xl border border-gray-200 bg-white p-4">
                    <h3 className="mb-3 text-sm font-bold text-gray-900">
                      ملاحظات إضافية
                    </h3>
                    <div className="space-y-3">
                      <div className="rounded-xl bg-gray-50 p-3">
                        <div className="mb-1 flex items-center gap-2 text-xs text-gray-500">
                          <StickyNote className="h-3.5 w-3.5" />
                          ملاحظات الإغلاق
                        </div>
                        <p className="text-sm font-medium text-gray-800">
                          {shiftDetails.notes || "لا توجد ملاحظات"}
                        </p>
                      </div>

                      {shiftDetails.isForceClosed && (
                        <div className="rounded-xl bg-red-50 p-3">
                          <p className="text-xs font-semibold text-red-700">
                            تم إغلاق الوردية بالقوة
                          </p>
                          <p className="mt-1 text-sm text-red-800">
                            {shiftDetails.forceCloseReason || "بدون سبب مسجل"}
                          </p>
                        </div>
                      )}

                      {shiftDetails.isHandedOver && (
                        <div className="rounded-xl bg-blue-50 p-3">
                          <p className="text-xs font-semibold text-blue-700">
                            تم تسليم الوردية
                          </p>
                          <p className="mt-1 text-sm text-blue-800">
                            {shiftDetails.handedOverFromUserName || "—"} إلى{" "}
                            {shiftDetails.handedOverToUserName || "—"}
                          </p>
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            ) : orders.length === 0 ? (
              <div className="py-12 text-center text-gray-500">
                <ShoppingBag className="mx-auto mb-3 h-12 w-12 text-gray-300" />
                <p>لا توجد طلبات في هذه الوردية</p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="bg-gray-50 text-gray-600">
                    <tr>
                      <th className="px-3 py-3 text-right font-medium">
                        رقم الطلب
                      </th>
                      <th className="px-3 py-3 text-right font-medium">
                        التاريخ
                      </th>
                      <th className="px-3 py-3 text-right font-medium">
                        النوع
                      </th>
                      <th className="px-3 py-3 text-right font-medium">
                        العميل
                      </th>
                      <th className="px-3 py-3 text-right font-medium">
                        الحالة
                      </th>
                      <th className="px-3 py-3 text-right font-medium">
                        الدفع
                      </th>
                      <th className="px-3 py-3 text-right font-medium">
                        الإجمالي
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {orders.map((order) => (
                      <tr key={order.id} className="transition-colors hover:bg-gray-50">
                        <td className="px-3 py-3">
                          <span className="rounded bg-gray-100 px-2 py-1 font-mono text-xs">
                            {order.orderNumber}
                          </span>
                        </td>
                        <td className="px-3 py-3 text-gray-600">
                          {formatDateTime(order.createdAt)}
                        </td>
                        <td className="px-3 py-3 text-gray-700">
                          {getOrderTypeLabel(order)}
                        </td>
                        <td className="px-3 py-3 text-gray-700">
                          {order.customerName || "عميل نقدي"}
                        </td>
                        <td className="px-3 py-3">{getStatusBadge(order.status)}</td>
                        <td className="px-3 py-3 text-gray-600">
                          {order.paymentMethod || "—"}
                        </td>
                        <td className="px-3 py-3 font-semibold text-gray-800">
                          {formatCurrency(order.total)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </div>
    </Portal>
  );
};

export default ShiftDetailsDrawer;
