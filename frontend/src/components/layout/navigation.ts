import {
  Home,
  ShoppingCart,
  ClipboardList,
  Timer,
  Clock,
  Users,
  Package,
  FolderOpen,
  Truck,
  FileText,
  Boxes,
  Receipt,
  Wallet,
  Building2,
  Shield,
  BarChart3,
  HardDrive,
  Settings,
  type LucideIcon,
} from "lucide-react";

export type NavigationAudience = "all" | "non-system-owner" | "system-owner";

export interface NavigationSection {
  id: string;
  label: string;
  description: string;
  badge: string;
  accentClass: string;
  iconClass: string;
  buttonClass: string;
}

export interface NavigationItem {
  path: string;
  label: string;
  description: string;
  spotlight: string;
  icon: LucideIcon;
  sectionId: string;
  audience?: NavigationAudience;
  permission?: string;
  adminOnly?: boolean;
  systemOwnerOnly?: boolean;
  featured?: boolean;
  subItems?: Array<{ path: string; label: string }>;
}

interface NavigationAccessOptions {
  isAdmin: boolean;
  isSystemOwner: boolean;
  hasPermission: (permission: string) => boolean;
}

export const navigationSections: NavigationSection[] = [
  {
    id: "overview",
    label: "الانطلاقة",
    description: "محطة البداية التي تختصر لك الطريق لأهم مسارات النظام.",
    badge: "واجهة النظام",
    accentClass: "from-primary-500 via-primary-400 to-secondary-400",
    iconClass: "border-primary-200 bg-primary-50 text-primary-700",
    buttonClass:
      "border-primary-200 text-primary-700 hover:border-primary-300 hover:bg-primary-50",
  },
  {
    id: "operations",
    label: "التشغيل اليومي",
    description: "كل ما تحتاجه أثناء البيع، إدارة الطلبات، ومتابعة الورديات.",
    badge: "تشغيل مباشر",
    accentClass: "from-primary-600 via-primary-500 to-sky-400",
    iconClass: "border-primary-200 bg-primary-50 text-primary-700",
    buttonClass:
      "border-primary-200 text-primary-700 hover:border-primary-300 hover:bg-primary-50",
  },
  {
    id: "catalog",
    label: "المنتجات والبيانات",
    description: "تنظيم الأصناف، العملاء، الموردين، والمخزون من شاشة واحدة.",
    badge: "تشغيل تشغيلي",
    accentClass: "from-emerald-600 via-emerald-500 to-teal-400",
    iconClass: "border-emerald-200 bg-emerald-50 text-emerald-700",
    buttonClass:
      "border-emerald-200 text-emerald-700 hover:border-emerald-300 hover:bg-emerald-50",
  },
  {
    id: "finance",
    label: "المالية والتحليل",
    description: "متابعة الخزينة والمصروفات والتقارير لاتخاذ قرار أسرع.",
    badge: "رقابة ذكية",
    accentClass: "from-secondary-500 via-warning-500 to-secondary-300",
    iconClass: "border-secondary-200 bg-secondary-50 text-secondary-700",
    buttonClass:
      "border-secondary-200 text-secondary-700 hover:border-secondary-300 hover:bg-secondary-50",
  },
  {
    id: "admin",
    label: "الإدارة والتحكم",
    description: "مسارات التحكم في الإعدادات والمستخدمين والأمان والنسخ الاحتياطية.",
    badge: "تحكم إداري",
    accentClass: "from-primary-400 via-sky-300 to-secondary-300",
    iconClass: "border-primary-200 bg-primary-50 text-primary-700",
    buttonClass:
      "border-primary-200 text-primary-700 hover:border-primary-300 hover:bg-primary-50",
  },
  {
    id: "owner",
    label: "إدارة النظام",
    description: "لوحات المالك لمتابعة الشركات والمستخدمين على مستوى النظام كله.",
    badge: "صلاحية مالك النظام",
    accentClass: "from-secondary-500 via-primary-400 to-sky-300",
    iconClass: "border-secondary-200 bg-secondary-50 text-secondary-700",
    buttonClass:
      "border-secondary-200 text-secondary-700 hover:border-secondary-300 hover:bg-secondary-50",
  },
];

export const navigationItems: NavigationItem[] = [
  {
    path: "/home",
    label: "الرئيسية",
    description:
      "واجهة ترحيبية تجمع لك صفحات النظام المهمة حسب دورك وصلاحياتك الحالية.",
    spotlight: "الانطلاقة",
    icon: Home,
    sectionId: "overview",
    audience: "all",
    featured: true,
  },
  {
    path: "/pos",
    label: "نقطة البيع",
    description:
      "ابدأ البيع بسرعة، أضف المنتجات، وأتمم الطلبات من واجهة عملية مناسبة للشباك.",
    spotlight: "الأكثر استخدامًا",
    icon: ShoppingCart,
    sectionId: "operations",
    audience: "non-system-owner",
    permission: "PosSell",
    featured: true,
  },
  {
    path: "/orders",
    label: "الطلبات",
    description:
      "تابع حالة الطلبات المفتوحة والمكتملة وارجع لأي عملية بيع بسهولة.",
    spotlight: "متابعة التنفيذ",
    icon: ClipboardList,
    sectionId: "operations",
    audience: "non-system-owner",
    permission: "OrdersView",
  },
  {
    path: "/shift",
    label: "الوردية",
    description:
      "افتح ورديتك، راقب وقت العمل، وأغلقها بخطوات واضحة ومنظمة.",
    spotlight: "جاهزية الكاشير",
    icon: Timer,
    sectionId: "operations",
    audience: "non-system-owner",
  },
  {
    path: "/shifts-management",
    label: "إدارة الورديات",
    description:
      "مراجعة الورديات النشطة وإدارة الحالات الخاصة والتدخل عند الحاجة.",
    spotlight: "تحكم إداري",
    icon: Clock,
    sectionId: "operations",
    audience: "non-system-owner",
    permission: "ShiftsManage",
  },
  {
    path: "/customers",
    label: "العملاء",
    description:
      "احتفظ ببيانات العملاء وسجل تعاملاتهم ونقاطهم بشكل منظم وقابل للرجوع.",
    spotlight: "خدمة العملاء",
    icon: Users,
    sectionId: "catalog",
    audience: "non-system-owner",
    permission: "CustomersView",
  },
  {
    path: "/products",
    label: "المنتجات",
    description:
      "إدارة الأصناف والأسعار والبيانات الأساسية للمنتجات المعروضة للبيع.",
    spotlight: "كتالوج البيع",
    icon: Package,
    sectionId: "catalog",
    audience: "non-system-owner",
    permission: "ProductsView",
    featured: true,
  },
  {
    path: "/categories",
    label: "التصنيفات",
    description:
      "رتب المنتجات داخل مجموعات واضحة لتسهيل البحث والعرض داخل النظام.",
    spotlight: "تنظيم البيانات",
    icon: FolderOpen,
    sectionId: "catalog",
    audience: "non-system-owner",
    permission: "CategoriesView",
  },
  {
    path: "/suppliers",
    label: "الموردين",
    description:
      "متابعة الموردين وبيانات التواصل الخاصة بهم وربطهم بحركة الشراء.",
    spotlight: "سلسلة التوريد",
    icon: Truck,
    sectionId: "catalog",
    audience: "non-system-owner",
    adminOnly: true,
  },
  {
    path: "/purchase-invoices",
    label: "فواتير الشراء",
    description:
      "سجل مشترياتك وفواتير التوريد وراجع تفاصيل كل عملية شراء بدقة.",
    spotlight: "دورة الشراء",
    icon: FileText,
    sectionId: "catalog",
    audience: "non-system-owner",
    adminOnly: true,
  },
  {
    path: "/inventory",
    label: "المخزون",
    description:
      "راقب المخزون الفعلي لكل فرع وتحرك سريعًا قبل نفاد الأصناف المهمة.",
    spotlight: "مخزون الفروع",
    icon: Boxes,
    sectionId: "catalog",
    audience: "non-system-owner",
    permission: "InventoryView",
  },
  {
    path: "/expenses",
    label: "المصروفات",
    description:
      "سجل المصروفات اليومية ونظمها حسب النوع وطريقة الدفع والمرفقات.",
    spotlight: "ضبط المصروف",
    icon: Receipt,
    sectionId: "finance",
    audience: "non-system-owner",
    permission: "ExpensesView",
  },
  {
    path: "/cash-register",
    label: "الخزينة",
    description:
      "تابع حركات النقدية والمقبوضات والمصروفات ضمن لوحة خزينة مركزة.",
    spotlight: "حركة النقد",
    icon: Wallet,
    sectionId: "finance",
    audience: "non-system-owner",
    permission: "CashRegisterView",
    featured: true,
  },
  {
    path: "/reports",
    label: "التقارير",
    description:
      "استعرض مؤشرات المبيعات والمخزون والعملاء والموردين من مركز تقارير موحد.",
    spotlight: "رؤية أوسع",
    icon: BarChart3,
    sectionId: "finance",
    audience: "non-system-owner",
    permission: "ReportsView",
    featured: true,
  },
  {
    path: "/branches",
    label: "الفروع",
    description:
      "إدارة بيانات الفروع وربط التشغيل اليومي بكل فرع داخل المنظومة.",
    spotlight: "هيكلة التشغيل",
    icon: Building2,
    sectionId: "admin",
    audience: "non-system-owner",
    adminOnly: true,
  },
  {
    path: "/users",
    label: "إدارة المستخدمين",
    description:
      "تحكم في حسابات المستخدمين وأدوارهم وصلاحيات الوصول داخل النظام.",
    spotlight: "صلاحيات وأدوار",
    icon: Shield,
    sectionId: "admin",
    audience: "non-system-owner",
    adminOnly: true,
  },
  {
    path: "/audit",
    label: "سجل التدقيق",
    description:
      "راجع أثر العمليات والتعديلات الحساسة لتتبع التغييرات بثقة أكبر.",
    spotlight: "شفافية كاملة",
    icon: FileText,
    sectionId: "admin",
    audience: "non-system-owner",
    adminOnly: true,
  },
  {
    path: "/backup",
    label: "النسخ الاحتياطية",
    description:
      "أدر النسخ الاحتياطية والاسترجاع بخطوات آمنة للمحافظة على بياناتك.",
    spotlight: "أمان البيانات",
    icon: HardDrive,
    sectionId: "admin",
    audience: "non-system-owner",
    adminOnly: true,
  },
  {
    path: "/settings",
    label: "الإعدادات",
    description:
      "اضبط إعدادات النظام العامة والصلاحيات والسياسات بما يناسب تشغيلك.",
    spotlight: "تخصيص المنظومة",
    icon: Settings,
    sectionId: "admin",
    audience: "non-system-owner",
    adminOnly: true,
  },
  {
    path: "/owner/tenants",
    label: "إدارة الشركات",
    description:
      "أنشئ الشركات وتابع حالتها وابدأ تجهيز بيئات العمل على مستوى النظام.",
    spotlight: "لوحة المالك",
    icon: Building2,
    sectionId: "owner",
    audience: "system-owner",
    systemOwnerOnly: true,
    featured: true,
  },
  {
    path: "/owner/users",
    label: "مستخدمي النظام",
    description:
      "إدارة مستخدمي المالك ومراجعة الوصول والحسابات المرتبطة بالمنصة كلها.",
    spotlight: "رقابة مركزية",
    icon: Users,
    sectionId: "owner",
    audience: "system-owner",
    systemOwnerOnly: true,
  },
];

export const getAccessibleNavigationItems = (
  items: NavigationItem[],
  options: NavigationAccessOptions,
) =>
  items.filter((item) => {
    const audience = item.audience ?? "all";

    if (options.isSystemOwner && audience === "non-system-owner") {
      return false;
    }

    if (!options.isSystemOwner && audience === "system-owner") {
      return false;
    }

    if (item.systemOwnerOnly) {
      return options.isSystemOwner;
    }

    if (item.adminOnly) {
      return options.isAdmin;
    }

    if (item.permission) {
      return options.hasPermission(item.permission);
    }

    return true;
  });
