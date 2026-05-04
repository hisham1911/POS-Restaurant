import {
  createApi,
  fetchBaseQuery,
  FetchBaseQueryError,
  retry,
} from "@reduxjs/toolkit/query/react";
import type { RootState } from "../store";
import { toast } from "sonner";
import { ERROR_MESSAGES } from "../utils/constants";
import {
  getPrintPreferenceHeaderValue,
  getTargetBridgeDeviceIdHeaderValue,
} from "@/utils/devicePrintPreferences";

// Dynamic API URL resolution policy:
// 1) If VITE_API_URL is set, use it in all environments.
// 2) In local development, fallback to /api (Vite proxy).
// 3) In production, fallback to same-origin /api.
const getApiUrl = (): string => {
  const envApiUrl = import.meta.env.VITE_API_URL?.toString().trim();
  if (envApiUrl) {
    // Normalize trailing slash to avoid double-slash URL joins.
    return envApiUrl.replace(/\/+$/, "");
  }

  // If running in development with Vite dev server, use relative path.
  // Vite proxy will forward /api/* to the backend.
  if (import.meta.env.DEV) {
    return "/api";
  }

  // In production, use the current page's origin.
  // This keeps same-origin behavior when env override is not configured.
  return `${window.location.origin}/api`;
};

const API_URL = getApiUrl();

// API Response type from backend
interface ApiErrorResponse {
  success: boolean;
  message?: string;
  errorCode?: string;
}

const getLocalizedErrorMessage = (
  errorData: ApiErrorResponse | undefined,
  fallbackMessage = "حدث خطأ في الطلب",
): string => {
  if (!errorData) {
    return fallbackMessage;
  }

  if (errorData.errorCode && ERROR_MESSAGES[errorData.errorCode]) {
    // Special case: VALIDATION_ERROR may contain a specific business rule message from the server
    if (
      errorData.errorCode === "VALIDATION_ERROR" &&
      errorData.message?.trim()
    ) {
      return errorData.message;
    }
    return ERROR_MESSAGES[errorData.errorCode];
  }

  if (errorData.message) {
    return ERROR_MESSAGES[errorData.message] ?? errorData.message;
  }

  return fallbackMessage;
};

// Base query with auth header and branch header
const baseQuery = fetchBaseQuery({
  baseUrl: API_URL,
  prepareHeaders: (headers, { getState }) => {
    const state = getState() as RootState;
    const token = state.auth.token;
    const userId = state.auth.user?.id;
    const branchId = state.branch?.currentBranch?.id;

    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }
    if (branchId) {
      headers.set("X-Branch-Id", branchId.toString());
    }

    headers.set("X-Print-Preference", getPrintPreferenceHeaderValue(userId));

    const targetBridgeDeviceId = getTargetBridgeDeviceIdHeaderValue(userId);
    if (targetBridgeDeviceId) {
      headers.set("X-Target-Device-Id", targetBridgeDeviceId);
    }

    return headers;
  },
});

// Base query with global error handling and retry logic
const baseQueryWithReauth = retry(
  async (args, api, extraOptions) => {
    const result = await baseQuery(args, api, extraOptions);

    if (result.error) {
      const error = result.error as FetchBaseQueryError;

      // P0-7: NEVER retry mutations (POST, PUT, DELETE).
      // Retrying a payment or order completion can cause double-charges.
      // Only GET requests (queries) are safe to retry.
      const isMutation =
        typeof args === "object" &&
        args !== null &&
        "method" in args &&
        typeof (args as Record<string, unknown>).method === "string" &&
        ["POST", "PUT", "DELETE"].includes(
          ((args as Record<string, unknown>).method as string).toUpperCase(),
        );

      if (isMutation) {
        // Show error but do NOT retry
        if (error.status === "FETCH_ERROR") {
          toast.error("فشل الاتصال. تحقق من الشبكة وحاول يدوياً.");
        } else if (error.status === 500) {
          toast.error(
            "حدث خطأ في الخادم. لا تكرر العملية — تحقق من البيانات أولاً.",
          );
        } else if (
          error.status === 400 ||
          error.status === 403 ||
          error.status === 409
        ) {
          // Handle 400/403/409 errors for mutations
          const errorData = error.data as ApiErrorResponse | undefined;
          const message = getLocalizedErrorMessage(errorData);

          if (errorData?.errorCode === "NO_OPEN_SHIFT") {
            toast.error("يجب فتح وردية قبل إنشاء طلب");
          } else if (errorData?.errorCode === "SHIFT_CONCURRENCY_CONFLICT") {
            toast.error("تم تعديل الوردية من مستخدم آخر، يرجى تحديث الصفحة");
            api.dispatch({ type: "api/invalidateTags", payload: ["Shifts"] });
          } else if (errorData?.errorCode === "SHIFT_BRANCH_MISMATCH") {
            toast.error("الوردية لا تنتمي للفرع الحالي");
          } else if (errorData?.errorCode === "INSUFFICIENT_STOCK") {
            toast.error(message, { duration: 5000 });
            api.dispatch({ type: "api/invalidateTags", payload: ["Products"] });
          } else if (
            errorData?.errorCode === "CUSTOMER_CREDIT_LIMIT_EXCEEDED"
          ) {
            toast.error(message, { duration: 6000 });
          } else if (errorData?.errorCode === "CUSTOMER_NOT_ACTIVE") {
            toast.error(message);
          } else if (errorData?.errorCode === "PAYMENT_INSUFFICIENT") {
            toast.error(message);
          } else if (errorData?.errorCode === "PAYMENT_EXCEEDS_DUE") {
            toast.error(message);
          } else if (error.status === 409) {
            toast.error(
              message || "تم تعديل البيانات من مستخدم آخر - يرجى تحديث الصفحة",
            );
            api.dispatch({
              type: "api/invalidateTags",
              payload: ["Orders", "Customers", "Shifts"],
            });
          } else {
            toast.error(message);
          }
        }
        retry.fail(error);
        return result;
      }

      // --- Below: only applies to GET queries ---

      // Network error (offline) - retry query
      if (error.status === "FETCH_ERROR") {
        toast.error("لا يوجد اتصال بالإنترنت");
        // Retry will happen automatically
        return result;
      }

      // Timeout error - retry query
      if (error.status === "TIMEOUT_ERROR") {
        toast.error("انتهت مهلة الاتصال، حاول مرة أخرى");
        // Retry will happen automatically
        return result;
      }

      // 401 Unauthorized - Token expired (don't retry)
      if (error.status === 401) {
        // IMPORTANT: Clear localStorage BEFORE dispatching logout and redirecting
        // This prevents the redirect loop where redux-persist rehydrates stale auth state
        // before the logout action is flushed to localStorage
        try {
          localStorage.removeItem("persist:auth");
        } catch (e) {
          // ignore
        }
        api.dispatch({ type: "auth/logout" });
        window.location.href = "/login";
        retry.fail(error); // Don't retry auth errors
        return result;
      }

      // 403 Forbidden / 400 Bad Request / 409 Conflict - Show backend message (don't retry)
      if (
        error.status === 403 ||
        error.status === 400 ||
        error.status === 409
      ) {
        const errorData = error.data as ApiErrorResponse | undefined;
        const message = getLocalizedErrorMessage(errorData);

        // Handle specific error codes
        if (errorData?.errorCode === "NO_OPEN_SHIFT") {
          toast.error("يجب فتح وردية قبل إنشاء طلب");
        } else if (errorData?.errorCode === "SHIFT_CONCURRENCY_CONFLICT") {
          toast.error("تم تعديل الوردية من مستخدم آخر، يرجى تحديث الصفحة");
          // Invalidate shift cache to force refetch
          api.dispatch({ type: "api/invalidateTags", payload: ["Shifts"] });
        } else if (errorData?.errorCode === "SHIFT_BRANCH_MISMATCH") {
          toast.error("الوردية لا تنتمي للفرع الحالي");
        } else if (errorData?.errorCode === "INSUFFICIENT_STOCK") {
          toast.error(message, { duration: 5000 });
          // Invalidate products cache to get updated stock
          api.dispatch({ type: "api/invalidateTags", payload: ["Products"] });
        } else if (errorData?.errorCode === "CUSTOMER_CREDIT_LIMIT_EXCEEDED") {
          // Show detailed credit limit error
          toast.error(message, { duration: 6000 });
        } else if (errorData?.errorCode === "PAYMENT_INSUFFICIENT") {
          toast.error(message);
        } else if (errorData?.errorCode === "PAYMENT_EXCEEDS_DUE") {
          toast.error(message);
        } else if (error.status === 409) {
          // Concurrency conflict - show message and invalidate cache
          toast.error(
            message || "تم تعديل البيانات من مستخدم آخر - يرجى تحديث الصفحة",
          );
          // Invalidate all caches to force refetch
          api.dispatch({
            type: "api/invalidateTags",
            payload: ["Orders", "Customers", "Shifts"],
          });
        } else {
          toast.error(message);
        }
        retry.fail(error); // Don't retry client errors
        return result;
      }

      // 500 Server Error - retry query only
      if (error.status === 500) {
        toast.error("حدث خطأ في الخادم، حاول مرة أخرى");
        // Retry will happen automatically
        return result;
      }
    }

    return result;
  },
  {
    maxRetries: 3, // Retry up to 3 times
  },
);

// Create the base API
export const baseApi = createApi({
  reducerPath: "api",
  baseQuery: baseQueryWithReauth,
  tagTypes: [
    "Products",
    "Categories",
    "Orders",
    "Shifts",
    "User",
    "Users",
    "Branches",
    "Tenant",
    "AuditLogs",
    "Reports",
    "Customers",
    "Inventory",
    "Suppliers",
    "PurchaseInvoice",
    "ProductBatch",
    "DeliveryPerson",
    "DeliveryOrders",
    "StockTaking",
    "Expense",
    "Expenses",
    "ExpenseCategory",
    "ExpenseCategories",
    "CashRegister",
    "CashRegisterBalance",
    "CashRegisterTransactions",
    "Wallets",
    "WalletTransactions",
    "Backup",
    "Permissions",
    "UserPermissions",
    "SystemUsers",
    "PrinterStatus",
  ],
  // Enable automatic refetching on focus and reconnect
  refetchOnFocus: true,
  refetchOnReconnect: true,
  // Keep unused data in cache for 60 seconds
  keepUnusedDataFor: 60,
  endpoints: () => ({}),
});
