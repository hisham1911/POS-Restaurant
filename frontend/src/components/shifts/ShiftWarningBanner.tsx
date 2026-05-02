import { AlertTriangle, Clock, XCircle } from "lucide-react";
import { ShiftWarning } from "@/types/shift.types";
import clsx from "clsx";

interface ShiftWarningBannerProps {
  warning: ShiftWarning;
  onClose?: () => void;
}

export const ShiftWarningBanner = ({ warning, onClose }: ShiftWarningBannerProps) => {
  if (!warning.shouldWarn) return null;

  const isCritical = warning.level === "Critical";
  const hoursText = Math.floor(warning.hoursOpen);
  const minutesText = Math.floor((warning.hoursOpen - hoursText) * 60);

  return (
    <div
      className={clsx(
        "rounded-lg p-4 mb-6 border-2 animate-pulse",
        isCritical
          ? "bg-red-50 border-red-500"
          : "bg-yellow-50 border-yellow-500"
      )}
    >
      <div className="flex items-start gap-3">
        <div
          className={clsx(
            "w-10 h-10 rounded-full flex items-center justify-center flex-shrink-0",
            isCritical ? "bg-red-100" : "bg-yellow-100"
          )}
        >
          {isCritical ? (
            <XCircle className="w-6 h-6 text-red-600" />
          ) : (
            <AlertTriangle className="w-6 h-6 text-yellow-600" />
          )}
        </div>

        <div className="flex-1">
          <div className="flex items-center gap-2 mb-1">
            <h3
              className={clsx(
                "font-bold text-lg",
                isCritical ? "text-red-800" : "text-yellow-800"
              )}
            >
              {isCritical ? "🚨 تحذير شديد" : "⚠️ تحذير"}
            </h3>
            <div className="flex items-center gap-1 text-sm text-gray-600">
              <Clock className="w-4 h-4" />
              <span>
                {hoursText} ساعة و {minutesText} دقيقة
              </span>
            </div>
          </div>

          <p
            className={clsx(
              "text-sm leading-relaxed",
              isCritical ? "text-red-700" : "text-yellow-700"
            )}
          >
            {warning.message}
          </p>

          {isCritical && (
            <div className="mt-3 p-3 bg-red-100 rounded-lg">
              <p className="text-sm text-red-800 font-medium">
                ⚡ يُرجى إغلاق الوردية الحالية وفتح وردية جديدة في أقرب وقت ممكن
                للحفاظ على دقة السجلات المالية.
              </p>
            </div>
          )}
        </div>

        {onClose && (
          <button
            onClick={onClose}
            className={clsx(
              "p-2 rounded-lg hover:bg-opacity-20 transition-colors flex-shrink-0",
              isCritical
                ? "hover:bg-red-600 text-red-600"
                : "hover:bg-yellow-600 text-yellow-600"
            )}
            aria-label="إغلاق التحذير"
            title="إغلاق التحذير"
          >
            <XCircle className="w-6 h-6" />
          </button>
        )}
      </div>
    </div>
  );
};
