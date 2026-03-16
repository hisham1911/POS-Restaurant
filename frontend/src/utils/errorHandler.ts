import { toast } from "sonner";

// Error codes from backend
export const ERROR_CODES = {
  // General
  VALIDATION_ERROR: "VALIDATION_ERROR",
  NOT_FOUND: "NOT_FOUND",
  UNAUTHORIZED: "UNAUTHORIZED",
  FORBIDDEN: "FORBIDDEN",
  CONFLICT: "CONFLICT",
  INTERNAL_ERROR: "INTERNAL_ERROR",
  SYSTEM_INTERNAL_ERROR: "SYSTEM_INTERNAL_ERROR",
  DUPLICATE_REQUEST: "DUPLICATE_REQUEST",
  // Order
  ORDER_NOT_FOUND: "ORDER_NOT_FOUND",
  ORDER_ALREADY_COMPLETED: "ORDER_ALREADY_COMPLETED",
  ORDER_ALREADY_CANCELLED: "ORDER_ALREADY_CANCELLED",
  ORDER_INVALID_STATE_TRANSITION: "ORDER_INVALID_STATE_TRANSITION",
  ORDER_EMPTY: "ORDER_EMPTY",
  ORDER_ITEM_NOT_FOUND: "ORDER_ITEM_NOT_FOUND",
  ORDER_CANNOT_MODIFY: "ORDER_CANNOT_MODIFY",
  ORDER_INVALID_QUANTITY: "ORDER_INVALID_QUANTITY",
  // Payment
  PAYMENT_INSUFFICIENT: "PAYMENT_INSUFFICIENT",
  PAYMENT_INVALID_METHOD: "PAYMENT_INVALID_METHOD",
  PAYMENT_INVALID_AMOUNT: "PAYMENT_INVALID_AMOUNT",
  PAYMENT_EXCEEDS_DUE: "PAYMENT_EXCEEDS_DUE",
  // Product
  PRODUCT_NOT_FOUND: "PRODUCT_NOT_FOUND",
  PRODUCT_INACTIVE: "PRODUCT_INACTIVE",
  PRODUCT_OUT_OF_STOCK: "PRODUCT_OUT_OF_STOCK",
  INSUFFICIENT_STOCK: "INSUFFICIENT_STOCK",
  // Shift
  SHIFT_NOT_FOUND: "SHIFT_NOT_FOUND",
  SHIFT_ALREADY_OPEN: "SHIFT_ALREADY_OPEN",
  SHIFT_ALREADY_CLOSED: "SHIFT_ALREADY_CLOSED",
  NO_OPEN_SHIFT: "NO_OPEN_SHIFT",
  SHIFT_CONCURRENCY_CONFLICT: "SHIFT_CONCURRENCY_CONFLICT",
  SHIFT_FORCE_CLOSE_UNAUTHORIZED: "SHIFT_FORCE_CLOSE_UNAUTHORIZED",
  SHIFT_USER_HAS_OPEN_SHIFT: "SHIFT_USER_HAS_OPEN_SHIFT",
  // Customer
  CUSTOMER_NOT_FOUND: "CUSTOMER_NOT_FOUND",
  CUSTOMER_NOT_ACTIVE: "CUSTOMER_NOT_ACTIVE",
  CUSTOMER_CREDIT_LIMIT_EXCEEDED: "CUSTOMER_CREDIT_LIMIT_EXCEEDED",
  // Category
  CATEGORY_NOT_FOUND: "CATEGORY_NOT_FOUND",
  CATEGORY_HAS_PRODUCTS: "CATEGORY_HAS_PRODUCTS",
  // Cash Register
  CASH_REGISTER_INSUFFICIENT_BALANCE: "CASH_REGISTER_INSUFFICIENT_BALANCE",
} as const;

// Arabic error messages
export const ERROR_MESSAGES: Record<string, string> = {
  // General
  [ERROR_CODES.VALIDATION_ERROR]: "البيانات المدخلة غير صحيحة",
  [ERROR_CODES.NOT_FOUND]: "العنصر غير موجود",
  [ERROR_CODES.UNAUTHORIZED]: "يجب تسجيل الدخول أولاً",
  [ERROR_CODES.FORBIDDEN]: "ليس لديك صلاحية للقيام بهذا الإجراء",
  [ERROR_CODES.CONFLICT]: "تعارض في البيانات - يرجى تحديث الصفحة",
  [ERROR_CODES.INTERNAL_ERROR]: "حدث خطأ في الخادم",
  [ERROR_CODES.SYSTEM_INTERNAL_ERROR]: "حدث خطأ داخلي في النظام",
  [ERROR_CODES.DUPLICATE_REQUEST]: "تم إرسال هذا الطلب مسبقاً",
  // Order
  [ERROR_CODES.ORDER_NOT_FOUND]: "الطلب غير موجود",
  [ERROR_CODES.ORDER_ALREADY_COMPLETED]: "الطلب مكتمل بالفعل",
  [ERROR_CODES.ORDER_ALREADY_CANCELLED]: "الطلب ملغي بالفعل",
  [ERROR_CODES.ORDER_INVALID_STATE_TRANSITION]: "لا يمكن تغيير حالة الطلب",
  [ERROR_CODES.ORDER_EMPTY]: "لا يمكن إنشاء طلب فارغ",
  [ERROR_CODES.ORDER_ITEM_NOT_FOUND]: "عنصر الطلب غير موجود",
  [ERROR_CODES.ORDER_CANNOT_MODIFY]: "لا يمكن تعديل هذا الطلب",
  [ERROR_CODES.ORDER_INVALID_QUANTITY]: "الكمية المدخلة غير صحيحة",
  // Payment
  [ERROR_CODES.PAYMENT_INSUFFICIENT]: "المبلغ المدفوع غير كافٍ",
  [ERROR_CODES.PAYMENT_INVALID_METHOD]: "طريقة الدفع غير صالحة",
  [ERROR_CODES.PAYMENT_INVALID_AMOUNT]: "مبلغ الدفع غير صالح",
  [ERROR_CODES.PAYMENT_EXCEEDS_DUE]: "مبلغ الدفع يتجاوز المطلوب",
  // Product
  [ERROR_CODES.PRODUCT_NOT_FOUND]: "المنتج غير موجود",
  [ERROR_CODES.PRODUCT_INACTIVE]: "المنتج غير متاح حالياً",
  [ERROR_CODES.PRODUCT_OUT_OF_STOCK]: "المنتج غير متوفر في المخزون",
  [ERROR_CODES.INSUFFICIENT_STOCK]: "الكمية المتاحة غير كافية",
  // Shift
  [ERROR_CODES.SHIFT_NOT_FOUND]: "الوردية غير موجودة",
  [ERROR_CODES.SHIFT_ALREADY_OPEN]: "يوجد وردية مفتوحة بالفعل",
  [ERROR_CODES.SHIFT_ALREADY_CLOSED]: "الوردية مغلقة بالفعل",
  [ERROR_CODES.NO_OPEN_SHIFT]: "يجب فتح وردية أولاً قبل إنشاء طلبات",
  [ERROR_CODES.SHIFT_CONCURRENCY_CONFLICT]:
    "تم تعديل الوردية من مستخدم آخر - يرجى التحديث",
  [ERROR_CODES.SHIFT_FORCE_CLOSE_UNAUTHORIZED]: "غير مصرح لك بالإغلاق القسري",
  [ERROR_CODES.SHIFT_USER_HAS_OPEN_SHIFT]: "المستخدم لديه وردية مفتوحة بالفعل",
  // Customer
  [ERROR_CODES.CUSTOMER_NOT_FOUND]: "العميل غير موجود",
  [ERROR_CODES.CUSTOMER_NOT_ACTIVE]: "العميل غير نشط",
  [ERROR_CODES.CUSTOMER_CREDIT_LIMIT_EXCEEDED]: "تم تجاوز حد الائتمان للعميل",
  // Category
  [ERROR_CODES.CATEGORY_NOT_FOUND]: "التصنيف غير موجود",
  [ERROR_CODES.CATEGORY_HAS_PRODUCTS]: "لا يمكن حذف تصنيف يحتوي على منتجات",
  // Cash Register
  [ERROR_CODES.CASH_REGISTER_INSUFFICIENT_BALANCE]: "رصيد الخزينة غير كافٍ",
};

// Default error messages by status code
const STATUS_MESSAGES: Record<number, string> = {
  400: "طلب غير صحيح",
  401: "يجب تسجيل الدخول أولاً",
  403: "ليس لديك صلاحية للقيام بهذا الإجراء",
  404: "العنصر المطلوب غير موجود",
  409: "حدث تعارض في البيانات",
  500: "حدث خطأ في الخادم",
  503: "الخدمة غير متاحة حالياً",
};

interface ApiError {
  status?: number;
  data?: {
    message?: string;
    errorCode?: string;
    errors?: Record<string, string[]>;
  };
}

/**
 * Handle API errors and show user-friendly messages
 */
export function handleApiError(error: any): string {
  // Log error for debugging
  console.error("API Error:", error);

  // Check if it's an API error with response
  const apiError = error as ApiError;

  // Check for error code
  if (apiError.data?.errorCode) {
    const message = ERROR_MESSAGES[apiError.data.errorCode];
    if (message) {
      return message;
    }
  }

  // Check for custom message from backend
  if (apiError.data?.message) {
    return apiError.data.message;
  }

  // Check for validation errors
  if (apiError.data?.errors) {
    const firstError = Object.values(apiError.data.errors)[0];
    if (firstError && firstError.length > 0) {
      return firstError[0];
    }
  }

  // Check for status code
  if (apiError.status) {
    const message = STATUS_MESSAGES[apiError.status];
    if (message) {
      return message;
    }
  }

  // Network error
  if (
    error.message === "Network Error" ||
    error.message === "Failed to fetch"
  ) {
    return "فشل الاتصال بالخادم. يرجى التحقق من الاتصال بالإنترنت";
  }

  // Default error message
  return "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى";
}

/**
 * Show error toast with user-friendly message
 */
export function showErrorToast(error: any) {
  const message = handleApiError(error);
  toast.error(message);
}

/**
 * Log error for debugging (can be sent to error tracking service)
 */
export function logError(error: any, context?: string) {
  const timestamp = new Date().toISOString();
  const errorData = {
    timestamp,
    context,
    error: {
      message: error.message,
      stack: error.stack,
      ...error,
    },
  };

  console.error("Error Log:", errorData);

  // TODO: Send to error tracking service (e.g., Sentry)
  // if (process.env.NODE_ENV === "production") {
  //   sendToErrorTracking(errorData);
  // }
}
