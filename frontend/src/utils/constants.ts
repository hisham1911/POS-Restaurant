// حالات الطلب
export const ORDER_STATUS = {
  Draft: { label: "مسودة", color: "gray" },
  Pending: { label: "في الانتظار", color: "warning" },
  Completed: { label: "مكتمل", color: "success" },
  Cancelled: { label: "ملغي", color: "danger" },
  Refunded: { label: "مسترجع", color: "danger" },
  PartiallyRefunded: { label: "مسترجع جزئياً", color: "warning" },
} as const;

// أنواع الطلب
export const ORDER_TYPES = {
  DineIn: { label: "تناول في المكان", icon: "🍽️" },
  Takeaway: { label: "تيك أواي", icon: "🥡" },
  Delivery: { label: "توصيل", icon: "🚚" },
  Return: { label: "مرتجع", icon: "↩️" },
} as const;

// طرق الدفع
export const PAYMENT_METHODS = {
  Cash: { label: "نقدي", icon: "💵" },
  Card: { label: "بطاقة", icon: "💳" },
  Fawry: { label: "فوري", icon: "�" },
  BankTransfer: { label: "تحويل بنكي", icon: "🏦" },
} as const;

// صلاحيات المستخدمين
export const USER_ROLES = {
  Admin: { label: "مدير", color: "primary" },
  Cashier: { label: "كاشير", color: "gray" },
} as const;

// رسائل الأخطاء (mapped from backend ErrorCodes)
export const ERROR_MESSAGES: Record<string, string> = {
  // General
  NETWORK_ERROR: "حدث خطأ في الاتصال بالخادم",
  UNAUTHORIZED: "غير مصرح لك بالوصول",
  FORBIDDEN: "ليس لديك صلاحية لهذا الإجراء",
  NOT_FOUND: "العنصر غير موجود",
  INTERNAL_ERROR: "حدث خطأ في الخادم",
  SYSTEM_INTERNAL_ERROR: "حدث خطأ داخلي في النظام",
  VALIDATION_ERROR: "يرجى التحقق من البيانات المدخلة",
  CONFLICT: "تعارض في البيانات - يرجى تحديث الصفحة والمحاولة مرة أخرى",
  DUPLICATE_REQUEST: "تم إرسال هذا الطلب مسبقاً",

  // Order
  ORDER_NOT_FOUND: "الطلب غير موجود",
  ORDER_ALREADY_COMPLETED: "الطلب مكتمل بالفعل",
  ORDER_ALREADY_CANCELLED: "الطلب ملغي بالفعل",
  ORDER_INVALID_STATE_TRANSITION: "لا يمكن تغيير حالة الطلب إلى هذه الحالة",
  ORDER_EMPTY: "الطلب فارغ - أضف منتجات أولاً",
  ORDER_ITEM_NOT_FOUND: "عنصر الطلب غير موجود",
  ORDER_CANNOT_MODIFY: "لا يمكن تعديل هذا الطلب في حالته الحالية",
  ORDER_INVALID_QUANTITY: "الكمية غير صالحة",

  // Payment
  PAYMENT_INSUFFICIENT: "المبلغ المدفوع غير كافٍ",
  PAYMENT_INVALID_METHOD: "طريقة الدفع غير صالحة",
  PAYMENT_INVALID_AMOUNT: "مبلغ الدفع غير صالح",
  PAYMENT_EXCEEDS_DUE: "مبلغ الدفع يتجاوز المطلوب",

  // Product
  PRODUCT_NOT_FOUND: "المنتج غير موجود",
  PRODUCT_INACTIVE: "المنتج غير نشط",
  PRODUCT_OUT_OF_STOCK: "المنتج غير متوفر في المخزون",
  INSUFFICIENT_STOCK: "الكمية المطلوبة غير متوفرة في المخزون",

  // Shift
  SHIFT_NOT_FOUND: "الوردية غير موجودة",
  SHIFT_ALREADY_OPEN: "يوجد وردية مفتوحة بالفعل",
  SHIFT_ALREADY_CLOSED: "الوردية مغلقة بالفعل",
  NO_OPEN_SHIFT: "لا توجد وردية مفتوحة - يرجى فتح وردية أولاً",
  SHIFT_CONCURRENCY_CONFLICT:
    "تم تعديل الوردية من قبل مستخدم آخر - يرجى التحديث",
  SHIFT_FORCE_CLOSE_UNAUTHORIZED: "غير مصرح لك بالإغلاق القسري للوردية",
  SHIFT_USER_HAS_OPEN_SHIFT: "المستخدم لديه وردية مفتوحة بالفعل",

  // Customer
  CUSTOMER_CREDIT_LIMIT_EXCEEDED: "تم تجاوز حد الائتمان للعميل",

  // Cash Register
  CASH_REGISTER_INSUFFICIENT_BALANCE: "رصيد الخزينة غير كافٍ",
} as const;
