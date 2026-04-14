import clsx from "clsx";
import { Loader2, Printer, Wifi, WifiOff } from "lucide-react";
import { useGetPrinterStatusQuery } from "@/api/printerApi";
import { useDevicePrintPreferences } from "@/hooks/useDevicePrintPreferences";

const badgeBaseClassName =
  "inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium";

export const PrinterConnectionBadge = () => {
  const { printMode } = useDevicePrintPreferences();
  const shouldCheckBridge = printMode !== "browser";

  const { data, isLoading, isFetching } = useGetPrinterStatusQuery(undefined, {
    skip: !shouldCheckBridge,
    pollingInterval: 10000,
  });

  if (printMode === "browser") {
    return (
      <div
        className={clsx(
          badgeBaseClassName,
          "border-blue-200 bg-blue-50 text-blue-700",
        )}
        title="الوضع الحالي: طباعة المتصفح لهذا الجهاز"
      >
        <Printer className="h-3.5 w-3.5" />
        <span>طباعة المتصفح</span>
      </div>
    );
  }

  if (isLoading || isFetching) {
    return (
      <div
        className={clsx(
          badgeBaseClassName,
          "border-amber-200 bg-amber-50 text-amber-700",
        )}
        title="جاري التحقق من اتصال تطبيق الطابعة"
      >
        <Loader2 className="h-3.5 w-3.5 animate-spin" />
        <span>فحص الطابعة...</span>
      </div>
    );
  }

  const printerStatus = data?.data;
  const bridgeConnected = printerStatus?.bridgeAvailable === true;
  const preferredDevice = printerStatus?.preferredDevice;
  const preferredLabel =
    preferredDevice?.deviceName || preferredDevice?.deviceId;

  if (bridgeConnected) {
    return (
      <div
        className={clsx(
          badgeBaseClassName,
          "max-w-[17rem] border-green-200 bg-green-50 text-green-700",
        )}
        title={`Bridge متصل: ${preferredLabel || "Unknown Device"}`}
      >
        <Wifi className="h-3.5 w-3.5" />
        <span className="truncate">Bridge: {preferredLabel || "متصل"}</span>
      </div>
    );
  }

  return (
    <div
      className={clsx(
        badgeBaseClassName,
        printMode === "bridge"
          ? "border-red-200 bg-red-50 text-red-700"
          : "border-orange-200 bg-orange-50 text-orange-700",
      )}
      title="لا يوجد جهاز Bridge متصل"
    >
      <WifiOff className="h-3.5 w-3.5" />
      <span>Bridge غير متصل</span>
    </div>
  );
};
