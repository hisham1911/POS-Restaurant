import { ReactNode } from "react";
import { Trash2, AlertTriangle } from "lucide-react";
import { Portal } from "./Portal";
import { Button } from "./Button";

interface ConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
  title: string;
  description: string;
  warning?: string;
  confirmText?: string;
  cancelText?: string;
  isLoading?: boolean;
  icon?: ReactNode;
  variant?: "danger" | "warning" | "primary";
}

export const ConfirmDialog = ({
  open,
  onOpenChange,
  onConfirm,
  title,
  description,
  warning,
  confirmText = "تأكيد",
  cancelText = "إلغاء",
  isLoading = false,
  icon,
  variant = "danger",
}: ConfirmDialogProps) => {
  if (!open) return null;

  const iconBgColor =
    variant === "warning"
      ? "bg-amber-100"
      : variant === "primary"
        ? "bg-blue-100"
        : "bg-danger-100";
  const iconTextColor =
    variant === "warning"
      ? "text-amber-600"
      : variant === "primary"
        ? "text-blue-600"
        : "text-danger-600";

  return (
    <Portal>
      <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-[100] p-4">
        <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm animate-scale-in p-6">
          <div className="text-center mb-6">
            <div
              className={`w-16 h-16 ${iconBgColor} rounded-full flex items-center justify-center mx-auto mb-4`}
            >
              {icon ||
                (variant === "warning" ? (
                  <AlertTriangle className={`w-8 h-8 ${iconTextColor}`} />
                ) : (
                  <Trash2 className={`w-8 h-8 ${iconTextColor}`} />
                ))}
            </div>
            <h3 className="text-lg font-bold text-gray-800 mb-2">{title}</h3>
            <p className="text-gray-500">{description}</p>
            {warning && (
              <p className="text-sm text-danger-500 mt-2">{warning}</p>
            )}
          </div>
          <div className="flex gap-3">
            <Button
              variant="secondary"
              onClick={() => onOpenChange(false)}
              className="flex-1"
              disabled={isLoading}
            >
              {cancelText}
            </Button>
            <Button
              variant={variant === "danger" ? "danger" : "primary"}
              onClick={onConfirm}
              isLoading={isLoading}
              className="flex-1"
            >
              {confirmText}
            </Button>
          </div>
        </div>
      </div>
    </Portal>
  );
};
