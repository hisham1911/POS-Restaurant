import {
  Activity,
  AlertTriangle,
  ArrowRightLeft,
  ArrowUpDown,
  Award,
  BarChart3,
  Boxes,
  Building2,
  Calculator,
  ClipboardList,
  Clock,
  CreditCard,
  FileText,
  FolderOpen,
  Package,
  Receipt,
  Shield,
  ShoppingCart,
  Star,
  Timer,
  TrendingDown,
  Truck,
  UserCheck,
  Users,
  Wallet,
  type LucideIcon,
} from "lucide-react";
import type { NavigationItem } from "./navigation";

export type GroupTone = "primary" | "secondary" | "emerald";

export interface ModuleSectionConfig {
  id: string;
  title: string;
  icon: LucideIcon;
  paths: string[];
  tone: GroupTone;
}

export interface VisibleModuleSection extends ModuleSectionConfig {
  items: NavigationItem[];
}

export interface ReportLinkItem {
  label: string;
  path: string;
  icon: LucideIcon;
}

export interface ReportGroup {
  id: string;
  title: string;
  icon: LucideIcon;
  tone: GroupTone;
  items: ReportLinkItem[];
}

export const moduleSections: ModuleSectionConfig[] = [
  {
    id: "sales",
    title: "البيع والتشغيل",
    icon: ShoppingCart,
    paths: [
      "/pos",
      "/orders",
      "/delivery/operations",
      "/delivery-persons",
      "/shift",
      "/shifts-management",
    ],
    tone: "primary",
  },
  {
    id: "products",
    title: "الأصناف والمخزون",
    icon: Package,
    paths: ["/products", "/categories", "/inventory"],
    tone: "emerald",
  },
  {
    id: "people",
    title: "العملاء والموردون",
    icon: Users,
    paths: ["/customers", "/suppliers", "/purchase-invoices"],
    tone: "secondary",
  },
  {
    id: "finance",
    title: "الخزينة والمصروفات",
    icon: Wallet,
    paths: ["/cash-register", "/expenses"],
    tone: "secondary",
  },
  {
    id: "admin",
    title: "الإدارة والتحكم",
    icon: Shield,
    paths: ["/branches", "/users", "/settings", "/backup", "/audit"],
    tone: "primary",
  },
  {
    id: "owner",
    title: "إدارة النظام",
    icon: Building2,
    paths: ["/owner/tenants", "/owner/users"],
    tone: "secondary",
  },
];

export const reportGroups: ReportGroup[] = [
  {
    id: "sales",
    title: "تقارير المبيعات",
    icon: BarChart3,
    tone: "primary",
    items: [
      { label: "اليومي", path: "/reports/daily", icon: BarChart3 },
      { label: "المبيعات", path: "/reports/sales", icon: ClipboardList },
      {
        label: "الأرباح والخسائر",
        path: "/reports/profit-loss",
        icon: CreditCard,
      },
      { label: "المصروفات", path: "/reports/expenses", icon: Receipt },
    ],
  },
  {
    id: "inventory",
    title: "تقارير المخزون",
    icon: Boxes,
    tone: "emerald",
    items: [
      { label: "المخزون", path: "/reports/inventory", icon: Boxes },
      {
        label: "التحويلات",
        path: "/reports/transfer-history",
        icon: ArrowRightLeft,
      },
      {
        label: "حركة المنتجات",
        path: "/reports/products/movement",
        icon: ArrowUpDown,
      },
      {
        label: "المنتجات الراكدة",
        path: "/reports/products/slow",
        icon: TrendingDown,
      },
    ],
  },
  {
    id: "products",
    title: "تقارير المنتجات",
    icon: FolderOpen,
    tone: "secondary",
    items: [
      {
        label: "الربحية",
        path: "/reports/products/profitability",
        icon: Star,
      },
      {
        label: "تكلفة البضاعة",
        path: "/reports/products/cogs",
        icon: Calculator,
      },
    ],
  },
  {
    id: "customers",
    title: "تقارير العملاء",
    icon: Users,
    tone: "primary",
    items: [
      { label: "أفضل العملاء", path: "/reports/customers/top", icon: Users },
      {
        label: "ديون العملاء",
        path: "/reports/customers/debts",
        icon: AlertTriangle,
      },
      {
        label: "نشاط العملاء",
        path: "/reports/customers/activity",
        icon: Activity,
      },
    ],
  },
  {
    id: "employees",
    title: "تقارير الموظفين",
    icon: UserCheck,
    tone: "secondary",
    items: [
      {
        label: "أداء الكاشير",
        path: "/reports/employees/cashier-performance",
        icon: UserCheck,
      },
      {
        label: "تفاصيل الورديات",
        path: "/reports/employees/shifts",
        icon: Clock,
      },
      {
        label: "المبيعات حسب الموظف",
        path: "/reports/employees/sales",
        icon: Receipt,
      },
    ],
  },
  {
    id: "suppliers",
    title: "تقارير الموردين",
    icon: Truck,
    tone: "emerald",
    items: [
      {
        label: "مشتريات الموردين",
        path: "/reports/suppliers/purchases",
        icon: FileText,
      },
      {
        label: "ديون الموردين",
        path: "/reports/suppliers/debts",
        icon: CreditCard,
      },
      {
        label: "أداء الموردين",
        path: "/reports/suppliers/performance",
        icon: Award,
      },
    ],
  },
];

export const buildVisibleModuleSections = (
  accessibleItems: NavigationItem[],
): VisibleModuleSection[] => {
  const itemsByPath = new Map(accessibleItems.map((item) => [item.path, item]));

  return moduleSections
    .map((section) => ({
      ...section,
      items: section.paths
        .map((path) => itemsByPath.get(path))
        .filter((item): item is NavigationItem => item !== undefined),
    }))
    .filter((section) => section.items.length > 0);
};
