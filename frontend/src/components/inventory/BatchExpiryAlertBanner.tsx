import { AlertTriangle, AlertCircle, X, Eye } from "lucide-react";
import { useGetExpiryAlertsQuery } from "@/api/productBatchApi";
import { useState } from "react";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentUser } from "@/store/slices/authSlice";
import { usePermission } from "@/hooks/usePermission";
import { useNavigate } from "react-router-dom";

export const BatchExpiryAlertBanner = () => {
  const [dismissed, setDismissed] = useState(false);
  const user = useAppSelector(selectCurrentUser);
  const { hasPermission } = usePermission();
  const navigate = useNavigate();

  const canViewAlert = hasPermission("InventoryView");
  const branchId = user?.branchId;

  const { data } = useGetExpiryAlertsQuery(branchId ?? undefined, {
    skip: !canViewAlert,
    pollingInterval: 300000, // Refresh every 5 minutes
  });

  if (!canViewAlert || dismissed || !data?.data) {
    return null;
  }

  const summary = data.data;
  const totalAlerts = summary.alerts.length;
  const expiredCount = summary.expiredBatches;
  const nearExpiryCount = summary.nearExpiryBatches;

  if (totalAlerts === 0) return null;

  const isCritical = expiredCount > 0;
  const bgClass = isCritical
    ? "bg-red-50 border-red-200 text-red-700"
    : "bg-amber-50 border-amber-200 text-amber-700";
  const hoverClass = isCritical
    ? "text-red-600 hover:text-red-800 hover:bg-red-100"
    : "text-amber-600 hover:text-amber-800 hover:bg-amber-100";

  return (
    <div className={`${bgClass} border rounded-lg px-4 py-2 flex items-center justify-between gap-3 mb-4 animate-fade-in`}>
      <div className="flex items-center gap-2">
        {isCritical ? (
          <AlertCircle className="w-5 h-5 shrink-0" />
        ) : (
          <AlertTriangle className="w-5 h-5 shrink-0" />
        )}
        <span className="text-sm font-medium">
          {isCritical
            ? `تنبيه: ${expiredCount} باتش منتهي الصلاحية + ${nearExpiryCount} قريب من الانتهاء`
            : `تنبيه: ${nearExpiryCount} باتش قريب من الانتهاء`}
        </span>
      </div>
      <div className="flex items-center gap-1">
        <button
          onClick={() => navigate("/product-batches?status=NearExpiry")}
          className={`${hoverClass} p-1.5 rounded transition-colors flex items-center gap-1 text-xs`}
          aria-label="عرض التنبيهات"
          title="عرض التنبيهات"
        >
          <Eye className="w-3.5 h-3.5" />
          <span className="hidden sm:inline">عرض</span>
        </button>
        <button
          onClick={() => setDismissed(true)}
          className={`${hoverClass} p-1 rounded transition-colors`}
          aria-label="إخفاء التنبيه"
        >
          <X className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
};
