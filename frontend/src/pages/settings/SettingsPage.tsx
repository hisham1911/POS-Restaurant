import { useState, useEffect } from "react";
import {
  Settings,
  Percent,
  ToggleLeft,
  ToggleRight,
  Save,
  Building2,
  Package,
  Receipt,
  Printer,
  Type,
  Phone,
  MessageSquare,
  Image,
  User,
  Upload,
  X,
  Wifi,
  WifiOff,
  Copy,
  Check,
  ShoppingCart,
  Sparkles,
  ChevronDown,
} from "lucide-react";
import {
  useGetCurrentTenantQuery,
  useUpdateCurrentTenantMutation,
  useUploadLogoMutation,
} from "@/api/branchesApi";
import { useGetSystemInfoQuery, useHealthQuery } from "@/api/systemApi";
import { useGetPrinterStatusQuery } from "@/api/printerApi";
import { useAppDispatch } from "@/store/hooks";
import { setTaxSettings } from "@/store/slices/cartSlice";
import { Button } from "@/components/common/Button";
import { Loading } from "@/components/common/Loading";
import { toast } from "sonner";
import clsx from "clsx";
import { usePOSMode } from "@/hooks/usePOSMode";
import { useDevicePrintPreferences } from "@/hooks/useDevicePrintPreferences";
import { extractApiData } from "@/utils/apiResponse";
import type { DevicePrintMode } from "@/utils/devicePrintPreferences";

const isPrivateIpv4Host = (host: string): boolean => {
  const parts = host.split(".");
  if (parts.length !== 4) {
    return false;
  }

  const octets = parts.map((part) => Number(part));
  if (octets.some((octet) => Number.isNaN(octet) || octet < 0 || octet > 255)) {
    return false;
  }

  const [first, second] = octets;
  return (
    first === 10 ||
    first === 127 ||
    (first === 192 && second === 168) ||
    (first === 172 && second >= 16 && second <= 31)
  );
};

const isLocalNetworkHost = (host: string): boolean => {
  if (!host) {
    return false;
  }

  const normalized = host.toLowerCase();

  if (
    normalized === "localhost" ||
    normalized === "::1" ||
    normalized === "0.0.0.0"
  ) {
    return true;
  }

  if (normalized.endsWith(".local")) {
    return true;
  }

  if (normalized.includes(":")) {
    return (
      normalized.startsWith("fe80:") ||
      normalized.startsWith("fc") ||
      normalized.startsWith("fd")
    );
  }

  if (isPrivateIpv4Host(normalized)) {
    return true;
  }

  // Hostnames without dots are usually local machine names inside LAN.
  return !normalized.includes(".");
};

const SHARED_PRINTER_ROUTING_MODE = "BranchWithFallback";

const DEVICE_PRINT_MODE_OPTIONS: Array<{
  value: DevicePrintMode;
  label: string;
  description: string;
}> = [
  {
    value: "auto",
    label: "تلقائي (Bridge ثم متصفح)",
    description:
      "يحاول الطباعة عبر تطبيق الطابعة أولًا، ولو غير متصل يحوّل تلقائيًا لطباعة المتصفح.",
  },
  {
    value: "browser",
    label: "متصفح فقط",
    description: "أي طباعة من هذا الجهاز ستفتح طباعة المتصفح مباشرة.",
  },
  {
    value: "bridge",
    label: "تطبيق الطابعة فقط",
    description:
      "أي طباعة من هذا الجهاز تعتمد على Bridge فقط ولن تتحول للمتصفح تلقائيًا.",
  },
];

export const SettingsPage = () => {
  const dispatch = useAppDispatch();
  const browserHost =
    typeof window !== "undefined" ? window.location.hostname : "";
  const isLanRuntime = isLocalNetworkHost(browserHost);
  const { data: tenantData, isLoading, refetch } = useGetCurrentTenantQuery();
  const [updateTenant, { isLoading: isUpdating }] =
    useUpdateCurrentTenantMutation();
  const [uploadLogo, { isLoading: isUploading }] = useUploadLogoMutation();

  // POS Mode
  const { mode, setMode } = usePOSMode();
  const {
    printMode,
    targetBridgeDeviceId,
    setPrintMode,
    setTargetBridgeDeviceId,
  } = useDevicePrintPreferences();
  const shouldCheckBridgeStatus = printMode !== "browser";
  const {
    data: printerStatusData,
    isLoading: isPrinterStatusLoading,
    isFetching: isPrinterStatusFetching,
  } = useGetPrinterStatusQuery(undefined, {
    skip: !shouldCheckBridgeStatus,
    pollingInterval: 10000,
  });

  // System Info & Network Status
  const { data: systemData } = useGetSystemInfoQuery(undefined, {
    skip: !isLanRuntime,
  });
  const {
    data: healthData,
    isError: isHealthError,
    isLoading: isHealthLoading,
    isFetching: isHealthFetching,
  } = useHealthQuery(undefined, {
    skip: !isLanRuntime,
  });
  const [urlCopied, setUrlCopied] = useState(false);

  const tenant = tenantData?.data;
  const printerStatus = printerStatusData?.data;
  const preferredBridgeDevice = printerStatus?.preferredDevice;
  const targetBridgeDevice = printerStatus?.targetDevice;
  const hasTargetSelection = Boolean(targetBridgeDeviceId);
  const isTargetBridgeConnected = printerStatus?.targetDeviceConnected === true;

  // Form state
  const [taxRate, setTaxRate] = useState<number>(0);
  const [isTaxEnabled, setIsTaxEnabled] = useState<boolean>(true);
  const [serviceChargeRate, setServiceChargeRate] = useState<number>(0);
  const [name, setName] = useState<string>("");
  const [nameEn, setNameEn] = useState<string>("");
  const [currency, setCurrency] = useState<string>("EGP");
  const [timezone, setTimezone] = useState<string>("Africa/Cairo");
  const [allowNegativeStock, setAllowNegativeStock] = useState<boolean>(false);
  const [expiryAlertDays, setExpiryAlertDays] = useState<number>(30);
  const [allowExpiredSales, setAllowExpiredSales] = useState<boolean>(false);

  // Receipt settings state
  const [receiptPaperSize, setReceiptPaperSize] = useState<string>("80mm");
  const [receiptCustomWidth, setReceiptCustomWidth] = useState<number>(280);
  const [receiptHeaderFontSize, setReceiptHeaderFontSize] =
    useState<number>(12);
  const [receiptBodyFontSize, setReceiptBodyFontSize] = useState<number>(9);
  const [receiptTotalFontSize, setReceiptTotalFontSize] = useState<number>(11);
  const [receiptShowBranchName, setReceiptShowBranchName] =
    useState<boolean>(true);
  const [receiptShowCashier, setReceiptShowCashier] = useState<boolean>(true);
  const [receiptShowThankYou, setReceiptShowThankYou] = useState<boolean>(true);
  const [receiptFooterMessage, setReceiptFooterMessage] = useState<string>("");
  const [receiptPhoneNumber, setReceiptPhoneNumber] = useState<string>("");
  const [receiptShowCustomerName, setReceiptShowCustomerName] =
    useState<boolean>(true);
  const [receiptShowLogo, setReceiptShowLogo] = useState<boolean>(true);
  const [logoUrl, setLogoUrl] = useState<string>("");

  // Print Routing settings state
  const [autoPrintOnSale, setAutoPrintOnSale] = useState<boolean>(true);
  const [autoPrintOnDebtPayment, setAutoPrintOnDebtPayment] =
    useState<boolean>(true);
  const [autoPrintDailyReports, setAutoPrintDailyReports] =
    useState<boolean>(false);

  // Initialize form with tenant data
  useEffect(() => {
    if (tenant) {
      setTaxRate(tenant.taxRate);
      setIsTaxEnabled(tenant.isTaxEnabled);
      setServiceChargeRate(tenant.serviceChargeRate ?? 0);
      setName(tenant.name);
      setNameEn(tenant.nameEn || "");
      setCurrency(tenant.currency);
      setTimezone(tenant.timezone);
      setAllowNegativeStock(tenant.allowNegativeStock ?? false);
      setExpiryAlertDays(tenant.expiryAlertDays ?? 30);
      setAllowExpiredSales(tenant.allowExpiredSales ?? false);
      // Receipt settings
      setReceiptPaperSize(tenant.receiptPaperSize || "80mm");
      setReceiptCustomWidth(tenant.receiptCustomWidth ?? 280);
      setReceiptHeaderFontSize(tenant.receiptHeaderFontSize ?? 12);
      setReceiptBodyFontSize(tenant.receiptBodyFontSize ?? 9);
      setReceiptTotalFontSize(tenant.receiptTotalFontSize ?? 11);
      setReceiptShowBranchName(tenant.receiptShowBranchName ?? true);
      setReceiptShowCashier(tenant.receiptShowCashier ?? true);
      setReceiptShowThankYou(tenant.receiptShowThankYou ?? true);
      setReceiptFooterMessage(tenant.receiptFooterMessage || "");
      setReceiptPhoneNumber(tenant.receiptPhoneNumber || "");
      setReceiptShowCustomerName(tenant.receiptShowCustomerName ?? true);
      setReceiptShowLogo(tenant.receiptShowLogo ?? true);
      setLogoUrl(tenant.logoUrl || "");
      // Print Routing settings
      setAutoPrintOnSale(tenant.autoPrintOnSale ?? true);
      setAutoPrintOnDebtPayment(tenant.autoPrintOnDebtPayment ?? true);
      setAutoPrintDailyReports(tenant.autoPrintDailyReports ?? false);
    }
  }, [tenant]);

  const handleSave = async () => {
    // Validate tax rate
    if (taxRate < 0 || taxRate > 100) {
      toast.error("نسبة الضريبة يجب أن تكون بين 0 و 100");
      return;
    }
    if (serviceChargeRate < 0 || serviceChargeRate > 100) {
      toast.error("Ù†Ø³Ø¨Ø© Ø±Ø³ÙˆÙ… Ø§Ù„Ø®Ø¯Ù…Ø© ÙŠØ¬Ø¨ Ø£Ù† ØªÙƒÙˆÙ† Ø¨ÙŠÙ† 0 Ùˆ 100");
      return;
    }
    try {
      const response = await updateTenant({
        name,
        nameEn: nameEn || undefined,
        logoUrl: logoUrl || undefined,
        currency,
        timezone,
        taxRate,
        isTaxEnabled,
        serviceChargeRate,
        allowNegativeStock,
        expiryAlertDays,
        allowExpiredSales,
        receiptPaperSize,
        receiptCustomWidth,
        receiptHeaderFontSize,
        receiptBodyFontSize,
        receiptTotalFontSize,
        receiptShowBranchName,
        receiptShowCashier,
        receiptShowThankYou,
        receiptFooterMessage: receiptFooterMessage || undefined,
        receiptPhoneNumber: receiptPhoneNumber || undefined,
        receiptShowCustomerName,
        receiptShowLogo,
        printRoutingMode: SHARED_PRINTER_ROUTING_MODE,
        autoPrintOnSale,
        autoPrintOnDebtPayment,
        autoPrintDailyReports,
      }).unwrap();

      extractApiData(
        response,
        "TENANT_UPDATE_EMPTY_RESPONSE",
        "تعذر حفظ الإعدادات",
      );

      dispatch(
        setTaxSettings({
          taxRate,
          isTaxEnabled,
          serviceChargeRate,
          allowNegativeStock,
        }),
      );
      toast.success("تم حفظ الإعدادات بنجاح");
      refetch();
    } catch {
      // baseApi.ts already shows error toast
    }
  };

  const copyUrl = () => {
    if (systemData?.data?.url) {
      navigator.clipboard.writeText(systemData.data.url);
      setUrlCopied(true);
      toast.success("تم نسخ الرابط");
      setTimeout(() => setUrlCopied(false), 2000);
    }
  };

  const healthStatus = healthData?.data?.status?.toLowerCase();
  const networkStatus: "checking" | "online" | "offline" =
    isHealthLoading || isHealthFetching
      ? "checking"
      : !isHealthError && healthStatus === "healthy"
        ? "online"
        : "offline";
  const isOnline = networkStatus === "online";
  const previewSubtotal = 100;
  const previewTaxAmount = isTaxEnabled
    ? (previewSubtotal * taxRate) / 100
    : 0;
  const previewServiceChargeAmount =
    serviceChargeRate > 0 ? (previewSubtotal * serviceChargeRate) / 100 : 0;
  const previewTotal =
    previewSubtotal + previewTaxAmount + previewServiceChargeAmount;

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <Loading />
      </div>
    );
  }

  return (
    <div className="h-full overflow-y-auto p-6">
      <div className="max-w-2xl mx-auto space-y-6">
        {/* Header */}
        <div className="flex items-center gap-3">
          <div className="w-12 h-12 bg-primary-100 rounded-xl flex items-center justify-center">
            <Settings className="w-6 h-6 text-primary-600" />
          </div>
          <div>
            <h1 className="text-2xl font-bold">إعدادات الشركة</h1>
            <p className="text-gray-500">
              إدارة إعدادات الضريبة والبيانات الأساسية
            </p>
          </div>
        </div>

        {/* System Network Info Card */}
        {isLanRuntime && systemData?.data && (
          <div className="bg-white rounded-xl shadow-sm border p-6 space-y-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2 text-lg font-semibold">
                {networkStatus === "online" ? (
                  <Wifi className="w-5 h-5 text-green-500" />
                ) : networkStatus === "checking" ? (
                  <Wifi className="w-5 h-5 text-yellow-500" />
                ) : (
                  <WifiOff className="w-5 h-5 text-red-500" />
                )}
                <span>معلومات الشبكة</span>
              </div>
              <div
                className={clsx(
                  "px-3 py-1 rounded-full text-sm font-medium",
                  networkStatus === "online"
                    ? "bg-green-100 text-green-700"
                    : networkStatus === "checking"
                      ? "bg-yellow-100 text-yellow-700"
                      : "bg-red-100 text-red-700",
                )}
              >
                {networkStatus === "online"
                  ? "متصل"
                  : networkStatus === "checking"
                    ? "جاري التحقق..."
                    : "غير متصل"}
              </div>
            </div>

            <div className="space-y-3">
              <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                <div>
                  <div className="text-sm text-gray-500">
                    عنوان للأجهزة الأخرى
                  </div>
                  <div className="font-mono text-sm font-medium mt-1" dir="ltr">
                    {systemData.data.url}
                  </div>
                </div>
                <button
                  onClick={copyUrl}
                  className="p-2 hover:bg-gray-200 rounded-lg transition-colors"
                  title="نسخ الرابط"
                >
                  {urlCopied ? (
                    <Check className="w-5 h-5 text-green-600" />
                  ) : (
                    <Copy className="w-5 h-5 text-gray-600" />
                  )}
                </button>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="p-3 bg-gray-50 rounded-lg">
                  <div className="text-sm text-gray-500">عنوان IP</div>
                  <div className="font-mono text-sm font-medium mt-1" dir="ltr">
                    {systemData.data.lanIp}
                  </div>
                </div>
                <div className="p-3 bg-gray-50 rounded-lg">
                  <div className="text-sm text-gray-500">المنفذ</div>
                  <div className="font-mono text-sm font-medium mt-1" dir="ltr">
                    {systemData.data.port}
                  </div>
                </div>
              </div>

              <div className="p-3 bg-blue-50 border border-blue-200 rounded-lg">
                <div className="text-sm text-blue-700">
                  📱 استخدم هذا العنوان على الموبايل، التابلت، أو أي جهاز آخر في
                  نفس الشبكة
                </div>
              </div>

              {networkStatus === "offline" && (
                <div className="p-3 bg-yellow-50 border border-yellow-200 rounded-lg">
                  <div className="text-sm text-yellow-700">
                    ⚠️ التطبيق يعمل في وضع عدم الاتصال. البيانات محلية ومتاحة.
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {/* POS Mode Settings Card */}
        <div className="bg-white rounded-xl shadow-sm border p-6 space-y-4">
          <div className="flex items-center gap-2 text-lg font-semibold">
            <ShoppingCart className="w-5 h-5 text-gray-500" />
            <span>وضع نقطة البيع</span>
          </div>

          <p className="text-sm text-gray-600">
            اختر الوضع المناسب لطريقة عملك. يمكنك التبديل بين الأوضاع في أي وقت.
          </p>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Cashier Mode */}
            <button
              onClick={() => {
                setMode("cashier");
                toast.success("تم التبديل إلى وضع الكاشير");
              }}
              className={clsx(
                "p-6 rounded-xl border-2 transition-all text-right",
                mode === "cashier"
                  ? "border-primary-500 bg-primary-50 shadow-md"
                  : "border-gray-200 hover:border-gray-300 bg-white",
              )}
            >
              <div className="flex items-start gap-3 mb-3">
                <div
                  className={clsx(
                    "p-3 rounded-lg",
                    mode === "cashier" ? "bg-primary-100" : "bg-gray-100",
                  )}
                >
                  <ShoppingCart
                    className={clsx(
                      "w-6 h-6",
                      mode === "cashier" ? "text-primary-600" : "text-gray-600",
                    )}
                  />
                </div>
                <div className="flex-1">
                  <h3 className="font-bold text-lg mb-1">وضع الكاشير</h3>
                  {mode === "cashier" && (
                    <span className="inline-block px-2 py-1 bg-primary-200 text-primary-800 text-xs rounded-full font-medium">
                      النشط حالياً
                    </span>
                  )}
                </div>
              </div>
              <ul className="space-y-2 text-sm text-gray-700">
                <li className="flex items-start gap-2">
                  <span className="text-primary-600 mt-0.5">✓</span>
                  <span>بطاقات كبيرة للمنتجات</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-primary-600 mt-0.5">✓</span>
                  <span>مناسب للمطاعم والمحلات الكبيرة</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-primary-600 mt-0.5">✓</span>
                  <span>تصميم مرئي وجذاب</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-primary-600 mt-0.5">✓</span>
                  <span>سهل الاستخدام</span>
                </li>
              </ul>
            </button>

            {/* Standard Mode */}
            <button
              onClick={() => {
                setMode("standard");
                toast.success("تم التبديل إلى الوضع الأساسي");
              }}
              className={clsx(
                "p-6 rounded-xl border-2 transition-all text-right",
                mode === "standard"
                  ? "border-blue-500 bg-blue-50 shadow-md"
                  : "border-gray-200 hover:border-gray-300 bg-white",
              )}
            >
              <div className="flex items-start gap-3 mb-3">
                <div
                  className={clsx(
                    "p-3 rounded-lg",
                    mode === "standard" ? "bg-blue-100" : "bg-gray-100",
                  )}
                >
                  <Sparkles
                    className={clsx(
                      "w-6 h-6",
                      mode === "standard" ? "text-blue-600" : "text-gray-600",
                    )}
                  />
                </div>
                <div className="flex-1">
                  <h3 className="font-bold text-lg mb-1">الوضع الأساسي</h3>
                  {mode === "standard" && (
                    <span className="inline-block px-2 py-1 bg-blue-200 text-blue-800 text-xs rounded-full font-medium">
                      النشط حالياً
                    </span>
                  )}
                </div>
              </div>
              <ul className="space-y-2 text-sm text-gray-700">
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 mt-0.5">✓</span>
                  <span>تصميم نظيف ومبتكر</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 mt-0.5">✓</span>
                  <span>قائمة منتجات مضغوطة</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 mt-0.5">✓</span>
                  <span>بحث سريع وذكي</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 mt-0.5">✓</span>
                  <span>مناسب للبيع السريع</span>
                </li>
              </ul>
            </button>
          </div>

          <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
            <div className="flex items-start gap-2 text-sm text-blue-800">
              <Sparkles className="w-5 h-5 mt-0.5 shrink-0" />
              <p>
                <strong>نصيحة:</strong> جرب كلا الوضعين واختر الأنسب لك. التغيير
                فوري ويظهر مباشرة في صفحة نقطة البيع.
              </p>
            </div>
          </div>
        </div>

        {/* Company Info Card */}
        <div className="bg-white rounded-xl shadow-sm border p-6 space-y-4">
          <div className="flex items-center gap-2 text-lg font-semibold">
            <Building2 className="w-5 h-5 text-gray-500" />
            <span>بيانات الشركة</span>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                اسم الشركة (عربي)
              </label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                placeholder="اسم الشركة"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                اسم الشركة (إنجليزي)
              </label>
              <input
                type="text"
                value={nameEn}
                onChange={(e) => setNameEn(e.target.value)}
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                placeholder="اسم الشركة بالإنجليزية"
                dir="ltr"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                العملة
              </label>
              <div className="relative">
                <select
                  value={currency}
                  onChange={(e) => setCurrency(e.target.value)}
                  className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
                >
                  <option value="EGP">جنيه مصري (EGP)</option>
                  <option value="SAR">ريال سعودي (SAR)</option>
                  <option value="AED">درهم إماراتي (AED)</option>
                  <option value="USD">دولار أمريكي (USD)</option>
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                المنطقة الزمنية
              </label>
              <div className="relative">
                <select
                  value={timezone}
                  onChange={(e) => setTimezone(e.target.value)}
                  className="w-full appearance-none pl-10 pr-4 py-2.5 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 hover:border-gray-400 transition-all duration-200 shadow-sm"
                >
                  <option value="Africa/Cairo">القاهرة (Africa/Cairo)</option>
                  <option value="Asia/Riyadh">الرياض (Asia/Riyadh)</option>
                  <option value="Asia/Dubai">دبي (Asia/Dubai)</option>
                </select>
                <ChevronDown className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400 pointer-events-none" />
              </div>
            </div>
          </div>
        </div>

        {/* Tax Settings Card */}
        <div className="bg-white rounded-xl shadow-sm border p-6 space-y-6">
          <div className="flex items-center gap-2 text-lg font-semibold">
            <Percent className="w-5 h-5 text-gray-500" />
            <span>إعدادات الضريبة</span>
          </div>

          {/* Tax Enable Toggle */}
          <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
            <div>
              <p className="font-medium">تفعيل الضريبة</p>
              <p className="text-sm text-gray-500">
                {isTaxEnabled
                  ? "الضريبة مفعلة - سيتم احتساب الضريبة على جميع الطلبات"
                  : "الضريبة معطلة - لن يتم احتساب أي ضريبة"}
              </p>
            </div>
            <button
              onClick={() => setIsTaxEnabled(!isTaxEnabled)}
              className={clsx(
                "p-2 rounded-lg transition-colors",
                isTaxEnabled
                  ? "bg-success-100 text-success-600"
                  : "bg-gray-200 text-gray-500",
              )}
            >
              {isTaxEnabled ? (
                <ToggleRight className="w-8 h-8" />
              ) : (
                <ToggleLeft className="w-8 h-8" />
              )}
            </button>
          </div>

          {/* Tax Rate Input */}
          <div
            className={clsx(!isTaxEnabled && "opacity-50 pointer-events-none")}
          >
            <label className="block text-sm font-medium text-gray-700 mb-2">
              نسبة الضريبة (%)
            </label>
            <div className="relative">
              <input
                type="number"
                min="0"
                max="100"
                step="0.01"
                value={taxRate}
                onChange={(e) => {
                  const nextTaxRate = parseFloat(e.target.value);
                  setTaxRate(Number.isNaN(nextTaxRate) ? 0 : nextTaxRate);
                }}
                disabled={!isTaxEnabled}
                className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500 text-lg"
                placeholder="0"
              />
              <span className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-400">
                %
              </span>
            </div>
            <p className="mt-2 text-sm text-gray-500">
              حدّد نسبة الضريبة الافتراضية الخاصة بمؤسستك، أو اتركها 0 عند عدم
              الاستخدام.
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              نسبة رسوم الخدمة (%)
            </label>
            <div className="relative">
              <input
                type="number"
                min="0"
                max="100"
                step="0.01"
                value={serviceChargeRate}
                onChange={(e) => {
                  const nextServiceChargeRate = parseFloat(e.target.value);
                  setServiceChargeRate(
                    Number.isNaN(nextServiceChargeRate)
                      ? 0
                      : nextServiceChargeRate,
                  );
                }}
                className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500 text-lg"
                placeholder="0"
              />
              <span className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-400">
                %
              </span>
            </div>
            <p className="mt-2 text-sm text-gray-500">
              رسوم اختيارية تضاف على صافي الطلب بعد الخصومات، ثم تظهر في معاينة
              السلة والإجمالي النهائي.
            </p>
          </div>

          {/* Tax Preview */}
          {(isTaxEnabled || serviceChargeRate > 0) && (
            <div className="p-4 bg-primary-50 rounded-lg border border-primary-200">
              <p className="text-sm font-medium text-primary-800 mb-2">
                معاينة الحساب (ضريبة مضافة):
              </p>
              <div className="text-sm text-primary-700 space-y-1">
                <p>• سعر المنتج (صافي بدون ضريبة): 100 ج.م</p>
                <p>
                  • قيمة الضريبة ({taxRate}%):{" "}
                  {((100 * taxRate) / 100).toFixed(2)} ج.م
                </p>
                <p>
                  • الإجمالي (شامل الضريبة):{" "}
                  {(100 + (100 * taxRate) / 100).toFixed(2)} ج.م
                </p>
              </div>
            </div>
          )}
        </div>

        {/* Inventory Settings Card */}
        <div className="bg-white rounded-xl shadow-sm border p-6 space-y-6">
          <div className="flex items-center gap-2 text-lg font-semibold">
            <Package className="w-5 h-5 text-gray-500" />
            <span>إعدادات المخزون</span>
          </div>

          {/* Allow Negative Stock Toggle */}
          <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
            <div>
              <p className="font-medium">
                السماح بالمخزون السالب (Sale below 0)
              </p>
              <p className="text-sm text-gray-500">
                {allowNegativeStock
                  ? "مسموح - يمكن البيع حتى لو كان المخزون صفر أو سالب"
                  : "غير مسموح - سيقوم النظام برفض البيع عند نفاد المخزون"}
              </p>
            </div>
            <button
              onClick={() => setAllowNegativeStock(!allowNegativeStock)}
              className={clsx(
                "p-2 rounded-lg transition-colors",
                allowNegativeStock
                  ? "bg-success-100 text-success-600"
                  : "bg-gray-200 text-gray-500",
              )}
            >
              {allowNegativeStock ? (
                <ToggleRight className="w-8 h-8" />
              ) : (
                <ToggleLeft className="w-8 h-8" />
              )}
            </button>
          </div>

          {!allowNegativeStock && (
            <div className="p-4 bg-amber-50 rounded-lg border border-amber-200">
              <p className="text-sm text-amber-800">
                <strong>تنبيه:</strong> عند إيقاف هذا الخيار، لن يتمكن الكاشير
                من إتمام عمليات البيع للمنتجات التي نفد مخزونها.
              </p>
            </div>
          )}

          {/* Expiry Alert Days */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              عدد أيام التنبيه قبل انتهاء الصلاحية
            </label>
            <div className="flex items-center gap-3">
              <input
                type="number"
                min="0"
                max="365"
                value={expiryAlertDays}
                onChange={(e) => setExpiryAlertDays(Number(e.target.value))}
                className="w-32 px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                dir="ltr"
              />
              <span className="text-sm text-gray-500">
                يوم (0 = إيقاف التنبيهات)
              </span>
            </div>
          </div>

          {/* Allow Expired Sales Toggle */}
          <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
            <div>
              <p className="font-medium">
                السماح ببيع المنتجات منتهية الصلاحية
              </p>
              <p className="text-sm text-gray-500">
                {allowExpiredSales
                  ? "مسموح - يمكن بيع الباتشات المنتهية الصلاحية"
                  : "غير مسموح - سيمنع النظام بيع الباتشات المنتهية"}
              </p>
            </div>
            <button
              onClick={() => setAllowExpiredSales(!allowExpiredSales)}
              className={clsx(
                "p-2 rounded-lg transition-colors",
                allowExpiredSales
                  ? "bg-success-100 text-success-600"
                  : "bg-gray-200 text-gray-500",
              )}
            >
              {allowExpiredSales ? (
                <ToggleRight className="w-8 h-8" />
              ) : (
                <ToggleLeft className="w-8 h-8" />
              )}
            </button>
          </div>
        </div>

        {/* Receipt Settings Card */}
        <div className="bg-white rounded-xl shadow-sm border p-6 space-y-6">
          <div className="flex items-center gap-2 text-lg font-semibold">
            <Receipt className="w-5 h-5 text-gray-500" />
            <span>إعدادات تنسيق الفاتورة</span>
          </div>

          {/* Paper Size */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              <div className="flex items-center gap-1.5">
                <Printer className="w-4 h-4" />
                مقاس الورق
              </div>
            </label>
            <div className="flex gap-3 mb-3">
              {[
                { value: "80mm", label: "80mm (عادي)" },
                { value: "58mm", label: "58mm (صغير)" },
                { value: "custom", label: "مخصص" },
              ].map((option) => (
                <button
                  key={option.value}
                  onClick={() => setReceiptPaperSize(option.value)}
                  className={clsx(
                    "flex-1 py-3 px-4 rounded-lg border-2 font-medium transition-all",
                    receiptPaperSize === option.value
                      ? "border-primary-500 bg-primary-50 text-primary-700"
                      : "border-gray-200 hover:border-gray-300 text-gray-600",
                  )}
                >
                  {option.label}
                </button>
              ))}
            </div>
            {/* Custom Width Input */}
            {receiptPaperSize === "custom" && (
              <div className="p-4 bg-primary-50 border border-primary-200 rounded-lg">
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  عرض الورق بالبيكسل (pixels)
                </label>
                <input
                  type="number"
                  min="200"
                  max="400"
                  value={receiptCustomWidth}
                  onChange={(e) =>
                    setReceiptCustomWidth(parseInt(e.target.value) || 280)
                  }
                  className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                  placeholder="280"
                />
                <p className="mt-2 text-xs text-gray-600">
                  القيمة الموصى بها: 280px (يمكنك التغيير حسب طابعتك)
                </p>
              </div>
            )}
          </div>

          {/* Font Sizes */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-3">
              <div className="flex items-center gap-1.5">
                <Type className="w-4 h-4" />
                أحجام الخطوط
              </div>
            </label>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div>
                <label className="block text-xs text-gray-500 mb-1">
                  خط العنوان
                </label>
                <div className="flex items-center gap-2">
                  <input
                    type="range"
                    min="8"
                    max="18"
                    value={receiptHeaderFontSize}
                    onChange={(e) =>
                      setReceiptHeaderFontSize(parseInt(e.target.value))
                    }
                    className="flex-1"
                  />
                  <span className="text-sm font-mono bg-gray-100 rounded px-2 py-1 min-w-[40px] text-center">
                    {receiptHeaderFontSize}
                  </span>
                </div>
              </div>
              <div>
                <label className="block text-xs text-gray-500 mb-1">
                  خط النص
                </label>
                <div className="flex items-center gap-2">
                  <input
                    type="range"
                    min="6"
                    max="14"
                    value={receiptBodyFontSize}
                    onChange={(e) =>
                      setReceiptBodyFontSize(parseInt(e.target.value))
                    }
                    className="flex-1"
                  />
                  <span className="text-sm font-mono bg-gray-100 rounded px-2 py-1 min-w-[40px] text-center">
                    {receiptBodyFontSize}
                  </span>
                </div>
              </div>
              <div>
                <label className="block text-xs text-gray-500 mb-1">
                  خط الإجمالي
                </label>
                <div className="flex items-center gap-2">
                  <input
                    type="range"
                    min="8"
                    max="16"
                    value={receiptTotalFontSize}
                    onChange={(e) =>
                      setReceiptTotalFontSize(parseInt(e.target.value))
                    }
                    className="flex-1"
                  />
                  <span className="text-sm font-mono bg-gray-100 rounded px-2 py-1 min-w-[40px] text-center">
                    {receiptTotalFontSize}
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* Toggles */}
          <div className="space-y-3">
            {[
              {
                label: "إظهار اسم الفرع",
                value: receiptShowBranchName,
                setter: setReceiptShowBranchName,
              },
              {
                label: "إظهار اسم الكاشير",
                value: receiptShowCashier,
                setter: setReceiptShowCashier,
              },
              {
                label: "إظهار اسم العميل",
                value: receiptShowCustomerName,
                setter: setReceiptShowCustomerName,
              },
              {
                label: "إظهار لوجو الشركة",
                value: receiptShowLogo,
                setter: setReceiptShowLogo,
              },
              {
                label: "إظهار رسالة شكراً في النهاية",
                value: receiptShowThankYou,
                setter: setReceiptShowThankYou,
              },
            ].map((toggle) => (
              <div
                key={toggle.label}
                className="flex items-center justify-between p-3 bg-gray-50 rounded-lg"
              >
                <span className="font-medium text-sm">{toggle.label}</span>
                <button
                  onClick={() => toggle.setter(!toggle.value)}
                  className={clsx(
                    "p-1 rounded-lg transition-colors",
                    toggle.value
                      ? "bg-success-100 text-success-600"
                      : "bg-gray-200 text-gray-500",
                  )}
                >
                  {toggle.value ? (
                    <ToggleRight className="w-7 h-7" />
                  ) : (
                    <ToggleLeft className="w-7 h-7" />
                  )}
                </button>
              </div>
            ))}
          </div>

          {/* Logo Upload */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              <div className="flex items-center gap-1.5">
                <Image className="w-4 h-4" />
                لوجو الشركة
              </div>
            </label>
            <div className="flex items-center gap-3">
              <label className="cursor-pointer inline-flex items-center gap-2 px-4 py-2 bg-primary-50 text-primary-700 border border-primary-200 rounded-lg hover:bg-primary-100 transition-colors">
                <Upload className="w-4 h-4" />
                {isUploading ? "جاري الرفع..." : "رفع صورة"}
                <input
                  type="file"
                  accept="image/png,image/jpeg,image/jpg,image/gif,image/webp,image/svg+xml"
                  className="hidden"
                  disabled={isUploading}
                  onChange={async (e) => {
                    const file = e.target.files?.[0];
                    if (!file) return;
                    if (file.size > 2 * 1024 * 1024) {
                      toast.error("حجم الملف يجب أن لا يتجاوز 2 ميجابايت");
                      return;
                    }
                    try {
                      const formData = new FormData();
                      formData.append("file", file);
                      const response = await uploadLogo(formData).unwrap();
                      const logoData = extractApiData(
                        response,
                        "TENANT_LOGO_UPLOAD_EMPTY_RESPONSE",
                        "تعذر رفع الشعار",
                      );

                      setLogoUrl(logoData.logoUrl);
                      toast.success("تم رفع الشعار بنجاح");
                      refetch();
                    } catch {
                      // baseApi.ts already shows error toast
                    }
                    e.target.value = "";
                  }}
                />
              </label>
              {logoUrl && (
                <button
                  type="button"
                  onClick={() => setLogoUrl("")}
                  className="inline-flex items-center gap-1 px-3 py-2 text-red-600 bg-red-50 border border-red-200 rounded-lg hover:bg-red-100 transition-colors text-sm"
                >
                  <X className="w-4 h-4" />
                  إزالة
                </button>
              )}
            </div>
            {logoUrl && (
              <div className="mt-3 flex items-center gap-3 p-3 bg-gray-50 rounded-lg border">
                <img
                  src={logoUrl}
                  alt="Logo Preview"
                  className="h-12 object-contain rounded"
                  onError={(e) => {
                    (e.target as HTMLImageElement).style.display = "none";
                  }}
                  onLoad={(e) => {
                    (e.target as HTMLImageElement).style.display = "block";
                  }}
                />
                <span className="text-xs text-gray-500">معاينة اللوجو</span>
              </div>
            )}
            <p className="mt-1 text-xs text-gray-400">
              PNG, JPG, GIF, WebP, SVG — حد أقصى 2 ميجابايت
            </p>
          </div>

          {/* Footer Message & Phone */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                <div className="flex items-center gap-1.5">
                  <MessageSquare className="w-4 h-4" />
                  رسالة أسفل الفاتورة
                </div>
              </label>
              <input
                type="text"
                value={receiptFooterMessage}
                onChange={(e) => setReceiptFooterMessage(e.target.value)}
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                placeholder="مثال: الرجاء الاحتفاظ بالفاتورة"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                <div className="flex items-center gap-1.5">
                  <Phone className="w-4 h-4" />
                  رقم هاتف المتجر
                </div>
              </label>
              <input
                type="text"
                value={receiptPhoneNumber}
                onChange={(e) => setReceiptPhoneNumber(e.target.value)}
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
                placeholder="01xxxxxxxxx"
                dir="ltr"
              />
            </div>
          </div>

          {/* Preview */}
          <div className="p-4 bg-gray-50 rounded-lg border border-gray-200">
            <p className="text-xs text-gray-500 mb-3 font-medium">
              معاينة الفاتورة:
            </p>
            <div
              className="mx-auto bg-white border border-dashed border-gray-300 p-4 space-y-2"
              style={{
                maxWidth:
                  receiptPaperSize === "80mm"
                    ? "302px"
                    : receiptPaperSize === "58mm"
                      ? "219px"
                      : `${receiptCustomWidth}px`,
                fontFamily: "Arial, sans-serif",
                direction: "rtl",
              }}
            >
              {/* Logo */}
              {receiptShowLogo && logoUrl && (
                <div className="text-center">
                  <img
                    src={logoUrl}
                    alt="Logo"
                    className="h-10 mx-auto object-contain"
                    onError={(e) => {
                      (e.target as HTMLImageElement).style.display = "none";
                    }}
                  />
                </div>
              )}
              {receiptShowBranchName && (
                <p
                  className="text-center font-bold"
                  style={{ fontSize: `${receiptHeaderFontSize}px` }}
                >
                  {name || "اسم المتجر"}
                </p>
              )}
              <div
                className="flex justify-between"
                style={{ fontSize: `${receiptBodyFontSize}px` }}
              >
                <span>فاتورة رقم</span>
                <span>ORD-001</span>
              </div>
              <p
                className="text-center"
                style={{ fontSize: `${receiptBodyFontSize}px` }}
              >
                {new Date().toLocaleDateString("ar-EG", {
                  timeZone: "Africa/Cairo",
                })}
              </p>
              <div className="border-t border-dashed border-gray-400 my-1" />
              {receiptShowCashier && (
                <div
                  className="flex justify-between"
                  style={{ fontSize: `${receiptBodyFontSize}px` }}
                >
                  <span>الكاشير: أحمد</span>
                  <span>الدفع: كاش</span>
                </div>
              )}
              {!receiptShowCashier && (
                <p style={{ fontSize: `${receiptBodyFontSize}px` }}>
                  الدفع: كاش
                </p>
              )}
              {receiptShowCustomerName && (
                <p style={{ fontSize: `${receiptBodyFontSize}px` }}>
                  العميل: محمد علي
                </p>
              )}
              <div className="border-t border-dashed border-gray-400 my-1" />
              <div
                className="flex justify-between"
                style={{ fontSize: `${receiptBodyFontSize}px` }}
              >
                <span>منتج تجريبي × 2</span>
                <span>100 ج.م</span>
              </div>
              <div className="border-t border-dashed border-gray-400 my-1" />
              <div
                className="flex justify-between"
                style={{ fontSize: `${receiptBodyFontSize}px` }}
              >
                <span>المجموع</span>
                <span>100.00 ج.م</span>
              </div>
              {isTaxEnabled && (
                <div
                  className="flex justify-between"
                  style={{ fontSize: `${receiptBodyFontSize}px` }}
                >
                  <span>الضريبة ({taxRate}%)</span>
                  <span>{previewTaxAmount.toFixed(2)} ج.م</span>
                </div>
              )}
              {serviceChargeRate > 0 && (
                <div
                  className="flex justify-between"
                  style={{ fontSize: `${receiptBodyFontSize}px` }}
                >
                  <span>رسوم الخدمة ({serviceChargeRate}%)</span>
                  <span>{previewServiceChargeAmount.toFixed(2)} ج.م</span>
                </div>
              )}
              <div className="border-t border-dashed border-gray-400 my-1" />
              <div
                className="flex justify-between font-bold"
                style={{ fontSize: `${receiptTotalFontSize}px` }}
              >
                <span>الإجمالي</span>
                <span>{previewTotal.toFixed(2)} ج.م</span>
              </div>
              <div className="border-t border-dashed border-gray-400 my-1" />
              <div
                className="flex justify-between"
                style={{ fontSize: `${receiptBodyFontSize}px` }}
              >
                <span>المبلغ المدفوع</span>
                <span>200.00 ج.م</span>
              </div>
              <div
                className="flex justify-between"
                style={{ fontSize: `${receiptBodyFontSize}px` }}
              >
                <span>الباقي</span>
                <span>{(200 - previewTotal).toFixed(2)} ج.م</span>
              </div>
              {receiptShowThankYou && (
                <p
                  className="text-center font-bold"
                  style={{ fontSize: `${receiptBodyFontSize}px` }}
                >
                  شكراً لزيارتكم ✨
                </p>
              )}
              {receiptFooterMessage && (
                <p
                  className="text-center"
                  style={{
                    fontSize: `${Math.max(receiptBodyFontSize - 1, 7)}px`,
                  }}
                >
                  {receiptFooterMessage}
                </p>
              )}
              {receiptPhoneNumber && (
                <p
                  className="text-center"
                  style={{
                    fontSize: `${Math.max(receiptBodyFontSize - 1, 7)}px`,
                  }}
                >
                  {receiptPhoneNumber}
                </p>
              )}
            </div>
          </div>
        </div>

        {/* Device-specific Print Settings */}
        <div className="bg-white rounded-xl shadow-sm border p-6 space-y-6">
          <div className="flex items-center gap-2 text-lg font-semibold">
            <Printer className="w-5 h-5 text-primary-600" />
            <span>إعدادات الطباعة لهذا الجهاز</span>
          </div>

          <div className="bg-slate-50 border border-slate-200 rounded-lg p-4 text-sm text-slate-700">
            هذه الإعدادات محفوظة على هذا الجهاز فقط. كل جهاز (موبايل، لابتوب،
            كمبيوتر) يمكنه اختيار طريقة الطباعة الخاصة به بشكل مستقل، وربطه
            بجهاز Bridge محدد إذا لزم.
          </div>

          <div className="space-y-3">
            {DEVICE_PRINT_MODE_OPTIONS.map((option) => {
              const isSelected = printMode === option.value;

              return (
                <button
                  key={option.value}
                  type="button"
                  onClick={() => setPrintMode(option.value)}
                  className={clsx(
                    "w-full rounded-lg border p-4 text-right transition-colors",
                    isSelected
                      ? "border-primary-300 bg-primary-50"
                      : "border-gray-200 bg-white hover:bg-gray-50",
                  )}
                >
                  <div className="flex items-center justify-between gap-3">
                    <span className="font-medium text-gray-900">
                      {option.label}
                    </span>
                    {isSelected ? (
                      <Check className="w-5 h-5 text-primary-600" />
                    ) : (
                      <span className="w-5 h-5 rounded-full border border-gray-300" />
                    )}
                  </div>
                  <p className="mt-2 text-sm text-gray-600">
                    {option.description}
                  </p>
                </button>
              );
            })}
          </div>

          {printMode === "browser" ? (
            <div className="rounded-lg border border-blue-200 bg-blue-50 p-4 text-sm text-blue-800">
              الطباعة الآن من المتصفح مباشرة على هذا الجهاز، ولا تحتاج اتصال
              Bridge.
            </div>
          ) : (
            <div className="space-y-3 rounded-lg border border-gray-200 bg-gray-50 p-4">
              <div className="space-y-2">
                <label className="text-sm font-medium text-gray-700">
                  جهاز Bridge المستهدف لهذا الجهاز
                </label>
                <div className="relative">
                  <select
                    value={targetBridgeDeviceId ?? ""}
                    onChange={(event) =>
                      setTargetBridgeDeviceId(event.target.value || null)
                    }
                    className="w-full appearance-none rounded-lg border border-gray-300 bg-white px-3 py-2 pl-9 text-sm text-gray-800 focus:border-primary-500 focus:outline-none"
                  >
                    <option value="">
                      تلقائي: أول جهاز Bridge متاح في نفس الفرع
                    </option>
                    {printerStatus?.devices?.map((device) => (
                      <option
                        key={`${device.deviceId}-${device.connectionId}`}
                        value={device.deviceId}
                      >
                        {device.deviceName || device.deviceId}
                        {device.printerName ? ` - ${device.printerName}` : ""}
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-500" />
                </div>
                <p className="text-xs text-gray-500">
                  {hasTargetSelection
                    ? "سيتم إرسال الطباعة من هذا الجهاز إلى جهاز Bridge المحدد فقط."
                    : "بدون تحديد جهاز، سيتم التوجيه تلقائيًا حسب حالة الاتصال داخل الفرع."}
                </p>
              </div>

              <div className="flex items-center justify-between gap-3">
                <div className="font-medium text-gray-800">
                  حالة اتصال Bridge
                </div>
                {isPrinterStatusLoading || isPrinterStatusFetching ? (
                  <div className="inline-flex items-center gap-2 rounded-full bg-amber-100 px-3 py-1 text-xs font-medium text-amber-700">
                    <Wifi className="w-3.5 h-3.5" />
                    <span>جاري التحقق...</span>
                  </div>
                ) : printerStatus?.bridgeAvailable ? (
                  <div className="inline-flex items-center gap-2 rounded-full bg-green-100 px-3 py-1 text-xs font-medium text-green-700">
                    <Wifi className="w-3.5 h-3.5" />
                    <span>
                      {hasTargetSelection ? "الجهاز المحدد متصل" : "متصل"}
                    </span>
                  </div>
                ) : (
                  <div className="inline-flex items-center gap-2 rounded-full bg-red-100 px-3 py-1 text-xs font-medium text-red-700">
                    <WifiOff className="w-3.5 h-3.5" />
                    <span>
                      {hasTargetSelection
                        ? "الجهاز المحدد غير متصل"
                        : "غير متصل"}
                    </span>
                  </div>
                )}
              </div>

              {hasTargetSelection ? (
                isTargetBridgeConnected && targetBridgeDevice ? (
                  <div className="rounded-lg border border-green-200 bg-green-50 p-3 text-sm text-green-800">
                    <p className="font-semibold">
                      الجهاز المحدد الذي سيستقبل الطباعة:
                    </p>
                    <p className="mt-1">
                      {targetBridgeDevice.deviceName ||
                        targetBridgeDevice.deviceId}
                    </p>
                    <p className="mt-1 text-xs text-green-700">
                      Device ID: {targetBridgeDevice.deviceId}
                      {targetBridgeDevice.printerName
                        ? ` • Printer: ${targetBridgeDevice.printerName}`
                        : ""}
                    </p>
                  </div>
                ) : (
                  <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                    جهاز Bridge المحدد ({targetBridgeDeviceId}) غير متصل حاليًا.
                    يمكنك تشغيله أو اختيار جهاز آخر.
                  </div>
                )
              ) : preferredBridgeDevice ? (
                <div className="rounded-lg border border-green-200 bg-green-50 p-3 text-sm text-green-800">
                  <p className="font-semibold">
                    الجهاز الذي سيستقبل الطباعة الآن:
                  </p>
                  <p className="mt-1">
                    {preferredBridgeDevice.deviceName ||
                      preferredBridgeDevice.deviceId}
                  </p>
                  <p className="mt-1 text-xs text-green-700">
                    Device ID: {preferredBridgeDevice.deviceId}
                    {preferredBridgeDevice.printerName
                      ? ` • Printer: ${preferredBridgeDevice.printerName}`
                      : ""}
                  </p>
                </div>
              ) : (
                <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                  لا يوجد جهاز Bridge متصل حاليًا. يمكنك تشغيل برنامج الطابعة
                  على أي جهاز لبدء الاستقبال.
                </div>
              )}

              {printerStatus?.devices?.length ? (
                <div className="space-y-2">
                  <p className="text-xs font-semibold text-gray-500">
                    الأجهزة المتصلة الآن ({printerStatus.devices.length})
                  </p>
                  {printerStatus.devices.map((device) => (
                    <div
                      key={device.connectionId}
                      className={clsx(
                        "rounded-md border bg-white p-2",
                        targetBridgeDeviceId === device.deviceId
                          ? "border-primary-300 ring-1 ring-primary-200"
                          : "border-gray-200",
                      )}
                    >
                      <div className="flex items-center justify-between gap-2">
                        <p className="text-sm font-medium text-gray-800">
                          {device.deviceName || device.deviceId}
                        </p>
                        {targetBridgeDeviceId === device.deviceId ? (
                          <span className="rounded-full bg-primary-100 px-2 py-0.5 text-[10px] font-medium text-primary-700">
                            مستهدف
                          </span>
                        ) : null}
                      </div>
                      <p className="text-xs text-gray-500">
                        ID: {device.deviceId}
                        {device.printerName
                          ? ` • Printer: ${device.printerName}`
                          : ""}
                      </p>
                    </div>
                  ))}
                </div>
              ) : null}
            </div>
          )}
        </div>

        {/* Print Routing Settings Card */}
        <div className="bg-white rounded-xl shadow-sm border p-6 space-y-6">
          <div className="flex items-center gap-2 text-lg font-semibold">
            <Printer className="w-5 h-5 text-primary-600" />
            <span>إعدادات الطباعة التلقائية</span>
          </div>

          {/* <div className="bg-primary-50 border border-primary-200 rounded-lg p-4 text-sm text-primary-900 space-y-2">
            <p className="font-semibold">وضع الطباعة ثابت تلقائياً:</p>
            <p>
              <strong>طابعة واحدة مشتركة</strong> لكل المستخدمين.
            </p>
            <p>
              1) شغّل برنامج الطابعة (Bridge App) على الجهاز الموصل بالطابعة.
            </p>
            <p>
              2) أي مستخدم من أي جهاز سيطبع على نفس الطابعة، سواء كان السيرفر
              محليًا أو عبر الإنترنت طالما جهاز الطابعة متصل بنفس الخادم.
            </p>
          </div> */}

          {/* Auto Print Toggles */}
          <div className="space-y-4">
            {/* Auto Print on Sale */}
            <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
              <div>
                <div className="font-medium">طباعة تلقائية عند البيع</div>
                <div className="text-sm text-gray-500">
                  طباعة الفاتورة تلقائياً عند إتمام البيع
                </div>
              </div>
              <button
                onClick={() => setAutoPrintOnSale(!autoPrintOnSale)}
                className="focus:outline-none"
              >
                {autoPrintOnSale ? (
                  <ToggleRight className="w-12 h-12 text-primary-600" />
                ) : (
                  <ToggleLeft className="w-12 h-12 text-gray-400" />
                )}
              </button>
            </div>

            {/* Auto Print on Debt Payment */}
            <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
              <div>
                <div className="font-medium">طباعة تلقائية عند دفع دين</div>
                <div className="text-sm text-gray-500">
                  طباعة إيصال تلقائياً عند دفع دين عميل
                </div>
              </div>
              <button
                onClick={() =>
                  setAutoPrintOnDebtPayment(!autoPrintOnDebtPayment)
                }
                className="focus:outline-none"
              >
                {autoPrintOnDebtPayment ? (
                  <ToggleRight className="w-12 h-12 text-primary-600" />
                ) : (
                  <ToggleLeft className="w-12 h-12 text-gray-400" />
                )}
              </button>
            </div>

            {/* Auto Print Daily Reports */}
            <div className="flex items-center justify-between p-4 bg-gray-50 rounded-lg">
              <div>
                <div className="font-medium">
                  طباعة تلقائية للتقارير اليومية
                </div>
                <div className="text-sm text-gray-500">
                  طباعة التقرير اليومي تلقائياً
                </div>
              </div>
              <button
                onClick={() => setAutoPrintDailyReports(!autoPrintDailyReports)}
                className="focus:outline-none"
              >
                {autoPrintDailyReports ? (
                  <ToggleRight className="w-12 h-12 text-primary-600" />
                ) : (
                  <ToggleLeft className="w-12 h-12 text-gray-400" />
                )}
              </button>
            </div>
          </div>

          {/* Info Box */}
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
            <div className="flex gap-3">
              <Wifi className="w-5 h-5 text-blue-600 flex-shrink-0 mt-0.5" />
              <div className="text-sm text-blue-800">
                <p className="font-medium mb-1">ملاحظة مهمة:</p>
                <p>
                  لتفعيل الطباعة، تأكد من تشغيل تطبيق الطابعة (Bridge App) على
                  الجهاز المطلوب. يمكنك تحميله من صفحة الإعدادات.
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Save Button */}
        <div className="flex justify-end">
          <Button
            variant="primary"
            size="lg"
            onClick={handleSave}
            isLoading={isUpdating}
            disabled={isUpdating}
            rightIcon={<Save className="w-5 h-5" />}
          >
            حفظ الإعدادات
          </Button>
        </div>
      </div>
    </div>
  );
};

export default SettingsPage;
