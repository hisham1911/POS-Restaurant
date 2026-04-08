import { DebtPaymentDto } from "@/types/customer.types";
import { Order } from "@/types/order.types";
import { Tenant } from "@/types/tenant.types";

type ReceiptTenantSettings = Pick<
  Tenant,
  | "name"
  | "timezone"
  | "logoUrl"
  | "isTaxEnabled"
  | "receiptPaperSize"
  | "receiptCustomWidth"
  | "receiptHeaderFontSize"
  | "receiptBodyFontSize"
  | "receiptTotalFontSize"
  | "receiptShowBranchName"
  | "receiptShowCashier"
  | "receiptShowThankYou"
  | "receiptFooterMessage"
  | "receiptPhoneNumber"
  | "receiptShowCustomerName"
  | "receiptShowLogo"
>;

const DEFAULT_RECEIPT_SETTINGS: ReceiptTenantSettings = {
  name: "KasserPro Store",
  timezone: "Africa/Cairo",
  logoUrl: undefined,
  isTaxEnabled: true,
  receiptPaperSize: "80mm",
  receiptCustomWidth: 280,
  receiptHeaderFontSize: 12,
  receiptBodyFontSize: 9,
  receiptTotalFontSize: 11,
  receiptShowBranchName: true,
  receiptShowCashier: true,
  receiptShowThankYou: true,
  receiptFooterMessage: undefined,
  receiptPhoneNumber: undefined,
  receiptShowCustomerName: true,
  receiptShowLogo: true,
};

const PAYMENT_METHOD_LABELS: Record<string, string> = {
  Cash: "💵 كاش",
  cash: "💵 كاش",
  Card: "💳 بطاقة",
  card: "💳 بطاقة",
  Wallet: "📱 محفظة",
  wallet: "📱 محفظة",
  Fawry: "🧾 فوري",
  BankTransfer: "🏦 تحويل بنكي",
};

const escapeHtml = (value: string): string =>
  value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/\"/g, "&quot;")
    .replace(/'/g, "&#39;");

const formatMoney = (value: number): string => `${(value || 0).toFixed(2)} ج.م`;

const formatMoneyNoDecimals = (value: number): string =>
  `${(value || 0).toFixed(0)} ج.م`;

const formatReceiptDate = (
  value: string,
  timezone: string | undefined,
): string => {
  const dateValue = new Date(value);
  const options: Intl.DateTimeFormatOptions = {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: true,
  };

  if (timezone) {
    options.timeZone = timezone;
  }

  return dateValue.toLocaleString("ar-EG", options);
};

const resolveLogoUrl = (logoUrl?: string): string | undefined => {
  if (!logoUrl) {
    return undefined;
  }

  const normalized = logoUrl.trim();
  if (!normalized) {
    return undefined;
  }

  if (/^(https?:|data:|blob:)/i.test(normalized)) {
    return normalized;
  }

  if (typeof window !== "undefined") {
    return `${window.location.origin}${
      normalized.startsWith("/") ? normalized : `/${normalized}`
    }`;
  }

  return normalized;
};

const getPageWidth = (settings: ReceiptTenantSettings): string => {
  if (settings.receiptPaperSize === "58mm") {
    return "219px";
  }

  if (
    settings.receiptPaperSize === "custom" &&
    settings.receiptCustomWidth &&
    settings.receiptCustomWidth > 100
  ) {
    return `${settings.receiptCustomWidth}px`;
  }

  return "302px";
};

const getPrintPageSize = (settings: ReceiptTenantSettings): string =>
  settings.receiptPaperSize === "58mm" ? "58mm" : "80mm";

const normalizeReceiptSettings = (
  tenant?: Tenant | null,
): ReceiptTenantSettings => ({
  ...DEFAULT_RECEIPT_SETTINGS,
  ...(tenant || {}),
});

const openPrintWindow = (html: string): boolean => {
  const printWindow = window.open("", "_blank", "width=420,height=760");
  if (!printWindow) {
    return false;
  }

  printWindow.document.open();
  printWindow.document.write(html);
  printWindow.document.close();
  return true;
};

const buildBaseReceiptStyles = (settings: ReceiptTenantSettings): string => {
  const pageWidth = getPageWidth(settings);
  const printPageSize = getPrintPageSize(settings);

  return `
    * { box-sizing: border-box; }
    body {
      margin: 0 auto;
      padding: 20px;
      width: ${pageWidth};
      max-width: ${pageWidth};
      font-family: Arial, sans-serif;
      color: #111;
      direction: rtl;
      font-size: ${settings.receiptBodyFontSize}px;
      line-height: 1.4;
      print-color-adjust: exact;
      -webkit-print-color-adjust: exact;
    }
    .header {
      text-align: center;
      margin-bottom: 20px;
    }
    .logo {
      max-height: 60px;
      max-width: 60%;
      margin: 0 auto;
      display: block;
      object-fit: contain;
    }
    .title {
      font-size: ${settings.receiptHeaderFontSize}px;
      font-weight: bold;
      margin: 0;
    }
    .line {
      border-top: 1px dashed #000;
      margin: 10px 0;
    }
    .line-item {
      display: flex;
      justify-content: space-between;
      gap: 8px;
      margin: 5px 0;
      font-size: ${settings.receiptBodyFontSize}px;
    }
    .line-item span:last-child {
      text-align: left;
    }
    .total {
      font-weight: 700;
      font-size: ${settings.receiptTotalFontSize}px;
    }
    .center {
      text-align: center;
    }
    .footer-text {
      text-align: center;
      font-size: ${Math.max(settings.receiptBodyFontSize - 1, 7)}px;
      margin: 4px 0;
    }
    @page {
      margin: 2mm;
      size: ${printPageSize} auto;
    }
    @media print {
      body {
        padding: 0;
      }
    }
  `;
};

export const printOrderReceiptFallback = (
  order: Order,
  tenant?: Tenant | null,
): boolean => {
  const settings = normalizeReceiptSettings(tenant);
  const safeLogoUrl = resolveLogoUrl(settings.logoUrl);
  const paymentMethod = order.payments[0]?.method || "Cash";
  const paymentLabel = PAYMENT_METHOD_LABELS[paymentMethod] || paymentMethod;
  const receiptDate = order.completedAt || order.createdAt;
  const formattedReceiptDate = formatReceiptDate(
    receiptDate,
    settings.timezone,
  );
  const isRefund =
    order.orderType === "Return" ||
    order.status === "Refunded" ||
    order.status === "PartiallyRefunded";

  const netTotal = Math.abs(order.subtotal || 0);
  const taxAmount = Math.abs(order.taxAmount || 0);
  const totalAmount = Math.abs(order.total || 0);
  const amountPaid = Math.abs(order.amountPaid || 0);
  const changeAmount = Math.abs(order.changeAmount || 0);
  const amountDue = Math.abs(order.amountDue || 0);
  const discountAmount = Math.abs(netTotal - totalAmount + taxAmount);

  const itemsRows = order.items
    .map(
      (item) => `
        <div class="line-item">
          <span>${escapeHtml(item.productName)} × ${Math.abs(item.quantity)}</span>
          <span>${formatMoneyNoDecimals(Math.abs(item.total))}</span>
        </div>`,
    )
    .join("");

  const showTaxLine = settings.isTaxEnabled && taxAmount > 0;

  const html = `<!DOCTYPE html>
<html dir="rtl" lang="ar">
<head>
  <meta charset="UTF-8" />
  <title>فاتورة ${escapeHtml(order.orderNumber)}</title>
  <style>${buildBaseReceiptStyles(settings)}</style>
</head>
<body>
  <div class="header">
    ${
      settings.receiptShowLogo && safeLogoUrl
        ? `<img class="logo" src="${escapeHtml(safeLogoUrl)}" alt="Logo" onerror="this.style.display='none'" />`
        : ""
    }
    ${
      settings.receiptShowBranchName
        ? `<h2 class="title">${escapeHtml(order.branchName || settings.name || "KasserPro Store")}</h2>`
        : ""
    }
    <div class="line-item"><span>${isRefund ? "فاتورة إرجاع" : "فاتورة رقم"}</span><span>${escapeHtml(order.orderNumber)}</span></div>
    <p class="center">${formattedReceiptDate}</p>
  </div>

  <div class="line"></div>

  ${
    settings.receiptShowCashier
      ? `<div class="line-item"><span>الكاشير: ${escapeHtml(order.userName || "غير معروف")}</span><span>الدفع: ${escapeHtml(paymentLabel)}</span></div>`
      : `<div class="line-item"><span>الدفع: ${escapeHtml(paymentLabel)}</span><span></span></div>`
  }
  ${
    settings.receiptShowCustomerName && order.customerName
      ? `<div class="line-item"><span>العميل: ${escapeHtml(order.customerName)}</span><span></span></div>`
      : ""
  }

  <div class="line"></div>

  ${itemsRows}

  <div class="line"></div>

  <div class="line-item"><span>المجموع</span><span>${formatMoney(netTotal)}</span></div>
  ${
    showTaxLine
      ? `<div class="line-item"><span>الضريبة (${(order.taxRate || 0).toFixed(0)}%)</span><span>${formatMoney(taxAmount)}</span></div>`
      : ""
  }
  ${
    discountAmount > 0
      ? `<div class="line-item"><span>الخصم</span><span>-${formatMoney(discountAmount)}</span></div>`
      : ""
  }

  <div class="line"></div>

  <div class="line-item total"><span>الإجمالي</span><span>${formatMoney(totalAmount)}</span></div>

  ${
    amountPaid > 0
      ? `<div class="line"></div>
         <div class="line-item"><span>المبلغ المدفوع</span><span>${formatMoney(amountPaid)}</span></div>
         ${
           changeAmount > 0
             ? `<div class="line-item"><span>الباقي</span><span>${formatMoney(changeAmount)}</span></div>`
             : ""
         }
         ${
           amountDue > 0
             ? `<div class="line-item total"><span>المتبقي على العميل</span><span>${formatMoney(amountDue)}</span></div>`
             : ""
         }`
      : ""
  }

  ${
    settings.receiptShowThankYou
      ? `<p class="center"><strong>شكراً لزيارتكم ✨</strong></p>`
      : ""
  }
  ${
    settings.receiptFooterMessage
      ? `<p class="footer-text">${escapeHtml(settings.receiptFooterMessage)}</p>`
      : ""
  }
  ${
    settings.receiptPhoneNumber
      ? `<p class="footer-text">${escapeHtml(settings.receiptPhoneNumber)}</p>`
      : ""
  }

  <script>window.onload = function () { window.print(); };</script>
</body>
</html>`;

  return openPrintWindow(html);
};

interface DebtReceiptOptions {
  customerName?: string;
  branchName?: string;
  cashierName?: string;
  tenant?: Tenant | null;
}

export const printDebtPaymentReceiptFallback = (
  payment: DebtPaymentDto,
  options: DebtReceiptOptions,
): boolean => {
  const settings = normalizeReceiptSettings(options.tenant);
  const paymentLabel =
    PAYMENT_METHOD_LABELS[payment.paymentMethod] || payment.paymentMethod;
  const safeLogoUrl = resolveLogoUrl(settings.logoUrl);

  const html = `<!DOCTYPE html>
<html dir="rtl" lang="ar">
<head>
  <meta charset="UTF-8" />
  <title>إيصال سداد دين</title>
  <style>${buildBaseReceiptStyles(settings)}</style>
</head>
<body>
  <div class="header">
    ${
      settings.receiptShowLogo && safeLogoUrl
        ? `<img class="logo" src="${escapeHtml(safeLogoUrl)}" alt="Logo" onerror="this.style.display='none'" />`
        : ""
    }
    ${
      settings.receiptShowBranchName
        ? `<h2 class="title">${escapeHtml(options.branchName || settings.name || "KasserPro Store")}</h2>`
        : ""
    }
    <div class="line-item"><span>إيصال سداد دين</span><span>PAY-${payment.id}</span></div>
    <div class="line-item"><span>التاريخ</span><span>${formatReceiptDate(payment.createdAt, settings.timezone)}</span></div>
  </div>

  <div class="line"></div>

  <div class="line-item"><span>طريقة الدفع</span><span>${escapeHtml(paymentLabel)}</span></div>
  ${
    settings.receiptShowCashier
      ? `<div class="line-item"><span>المسجل بواسطة</span><span>${escapeHtml(options.cashierName || payment.recordedByUserName || "غير معروف")}</span></div>`
      : ""
  }
  ${
    settings.receiptShowCustomerName
      ? `<div class="line-item"><span>العميل</span><span>${escapeHtml(options.customerName || "غير محدد")}</span></div>`
      : ""
  }

  <div class="line"></div>

  <div class="line-item total"><span>المبلغ المسدد</span><span>${formatMoney(payment.amount)}</span></div>
  <div class="line-item"><span>الرصيد قبل</span><span>${formatMoney(payment.balanceBefore)}</span></div>
  <div class="line-item"><span>الرصيد بعد</span><span>${formatMoney(payment.balanceAfter)}</span></div>

  ${
    payment.referenceNumber
      ? `<div class="line-item"><span>المرجع</span><span>${escapeHtml(payment.referenceNumber)}</span></div>`
      : ""
  }
  ${
    payment.notes
      ? `<div class="line-item"><span>ملاحظات</span><span>${escapeHtml(payment.notes)}</span></div>`
      : ""
  }

  <div class="line"></div>

  ${
    settings.receiptShowThankYou
      ? `<p class="center"><strong>شكراً لكم</strong></p>`
      : ""
  }
  ${
    settings.receiptFooterMessage
      ? `<p class="footer-text">${escapeHtml(settings.receiptFooterMessage)}</p>`
      : ""
  }
  ${
    settings.receiptPhoneNumber
      ? `<p class="footer-text">${escapeHtml(settings.receiptPhoneNumber)}</p>`
      : ""
  }

  <script>window.onload = function () { window.print(); };</script>
</body>
</html>`;

  return openPrintWindow(html);
};
