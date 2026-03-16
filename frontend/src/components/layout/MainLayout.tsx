import { Outlet, NavLink } from "react-router-dom";
import { useAuth } from "@/hooks/useAuth";
import { usePermission } from "@/hooks/usePermission";
import {
  LogOut,
  User,
  Clock,
  Menu,
  ShoppingCart,
  Package,
  FolderOpen,
  ClipboardList,
  Timer,
  BarChart3,
  X,
  FileText,
  Settings,
  Users,
  Truck,
  Building2,
  Receipt,
  Wallet,
  Boxes,
  HardDrive,
  Shield,
  Pin,
  PinOff,
  type LucideIcon,
} from "lucide-react";
import { useState } from "react";
import clsx from "clsx";
import { BranchSelector } from "./BranchSelector";
import { NavItemWithSubmenu } from "./NavItemWithSubmenu";

const navItems: Array<{
  path: string;
  label: string;
  icon: LucideIcon;
  permission?: string;
  adminOnly?: boolean;
  systemOwnerOnly?: boolean;
  subItems?: Array<{ path: string; label: string }>;
}> = [
  {
    path: "/pos",
    label: "نقطة البيع",
    icon: ShoppingCart,
    permission: "PosSell",
  },
  {
    path: "/orders",
    label: "الطلبات",
    icon: ClipboardList,
    permission: "OrdersView",
  },
  { path: "/shift", label: "الوردية", icon: Timer }, // Available to all authenticated users
  {
    path: "/shifts-management",
    label: "إدارة الورديات",
    icon: Clock,
    permission: "ShiftsManage",
  },
  {
    path: "/customers",
    label: "العملاء",
    icon: Users,
    permission: "CustomersView",
  },
  {
    path: "/products",
    label: "المنتجات",
    icon: Package,
    permission: "ProductsView",
  },
  {
    path: "/categories",
    label: "التصنيفات",
    icon: FolderOpen,
    permission: "CategoriesView",
  },
  { path: "/suppliers", label: "الموردين", icon: Truck, adminOnly: true },
  {
    path: "/purchase-invoices",
    label: "فواتير الشراء",
    icon: FileText,
    adminOnly: true,
  },
  {
    path: "/inventory",
    label: "المخزون",
    icon: Boxes,
    permission: "InventoryView",
  },
  {
    path: "/expenses",
    label: "المصروفات",
    icon: Receipt,
    permission: "ExpensesView",
  },
  {
    path: "/cash-register",
    label: "الخزينة",
    icon: Wallet,
    permission: "CashRegisterView",
  },
  { path: "/branches", label: "الفروع", icon: Building2, adminOnly: true },
  {
    path: "/users",
    label: "إدارة المستخدمين",
    icon: Shield,
    adminOnly: true,
  },
  {
    path: "/reports",
    label: "التقارير",
    icon: BarChart3,
    permission: "ReportsView",
  },
  { path: "/audit", label: "سجل التدقيق", icon: FileText, adminOnly: true },
  {
    path: "/backup",
    label: "النسخ الاحتياطية",
    icon: HardDrive,
    adminOnly: true,
  },
  { path: "/settings", label: "الإعدادات", icon: Settings, adminOnly: true },
  {
    path: "/owner/tenants",
    label: "إدارة الشركات",
    icon: Building2,
    systemOwnerOnly: true,
  },
  {
    path: "/owner/users",
    label: "إدارة المستخدمين",
    icon: Users,
    systemOwnerOnly: true,
  },
];

export const MainLayout = () => {
  const { user, logout, isAdmin, isSystemOwner } = useAuth();
  const { hasPermission } = usePermission();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(true); // الافتراضي مقفول

  const currentTime = new Date().toLocaleTimeString("ar-EG", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Africa/Cairo",
  });

  const filteredNavItems = navItems.filter((item) => {
    // System owner only sees system owner items
    if (isSystemOwner) return !!item.systemOwnerOnly;

    // Hide system owner items from non-system owners
    if (item.systemOwnerOnly) return false;

    // Admin-only items (no permission check, just role check)
    if (item.adminOnly) return isAdmin;

    // Permission-based items
    if (item.permission) return hasPermission(item.permission);

    // Items without permission requirement (like /shift) are available to all
    return true;
  });

  return (
    <div className="h-screen flex w-full overflow-hidden">
      {/* Sidebar - Desktop */}
      <aside
        className={clsx(
          "hidden lg:flex bg-gray-900 text-white flex-col shrink-0 border-l border-gray-800 overflow-y-auto transition-all duration-300",
          sidebarCollapsed ? "w-20" : "w-64",
        )}
      >
        {/* Logo */}
        <div className="p-4 border-b border-gray-800">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-primary-600 rounded-lg flex items-center justify-center shrink-0">
              <span className="text-xl">🏪</span>
            </div>
            {!sidebarCollapsed && (
              <div>
                <h1 className="font-bold text-lg">TajerPro</h1>
                <p className="text-xs text-gray-400">نظام نقاط البيع</p>
              </div>
            )}
          </div>
        </div>

        {/* Toggle Button */}
        <div className="px-4 py-2">
          <button
            onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
            className="w-full flex items-center justify-center gap-2 px-3 py-2 bg-gray-800 hover:bg-gray-700 rounded-lg transition-colors"
            title={sidebarCollapsed ? "فتح القائمة" : "إغلاق القائمة"}
          >
            <Menu className="w-5 h-5" />
            {!sidebarCollapsed && <span className="text-sm">إغلاق</span>}
          </button>
        </div>

        {/* Navigation */}
        <nav className="flex-1 p-4 space-y-1 overflow-y-auto">
          {filteredNavItems.map((item) =>
            item.subItems ? (
              <NavItemWithSubmenu
                key={item.path}
                path={item.path}
                label={item.label}
                icon={item.icon}
                subItems={item.subItems}
              />
            ) : (
              <NavLink
                key={item.path}
                to={item.path}
                className={({ isActive }) =>
                  clsx(
                    "flex items-center gap-3 rounded-lg transition-colors",
                    sidebarCollapsed ? "px-3 py-3 justify-center" : "px-4 py-3",
                    isActive
                      ? "bg-primary-600 text-white"
                      : "text-gray-300 hover:bg-gray-800",
                  )
                }
                title={sidebarCollapsed ? item.label : undefined}
              >
                <item.icon className="w-5 h-5 shrink-0" />
                {!sidebarCollapsed && <span>{item.label}</span>}
              </NavLink>
            ),
          )}
        </nav>

        {/* User Info */}
        <div className="p-4 border-t border-gray-800">
          {!sidebarCollapsed && (
            <div className="flex items-center gap-3 mb-3">
              <div className="w-10 h-10 bg-gray-700 rounded-full flex items-center justify-center shrink-0">
                <User className="w-5 h-5" />
              </div>
              <div>
                <p className="font-medium">{user?.name}</p>
                <p className="text-xs text-gray-400">
                  {user?.role === "SystemOwner"
                    ? "مالك النظام"
                    : user?.role === "Admin"
                      ? "مدير"
                      : "كاشير"}
                </p>
              </div>
            </div>
          )}
          <button
            onClick={logout}
            className={clsx(
              "w-full flex items-center gap-2 bg-gray-800 hover:bg-gray-700 rounded-lg transition-colors",
              sidebarCollapsed
                ? "justify-center px-3 py-3"
                : "justify-center px-4 py-2",
            )}
            title={sidebarCollapsed ? "تسجيل الخروج" : undefined}
          >
            <LogOut className="w-4 h-4" />
            {!sidebarCollapsed && <span>تسجيل الخروج</span>}
          </button>
        </div>
      </aside>

      {/* Mobile Sidebar */}
      {sidebarOpen && (
        <div className="lg:hidden fixed inset-0 z-50">
          <div
            className="absolute inset-0 bg-black/50"
            onClick={() => setSidebarOpen(false)}
          />
          <aside className="absolute right-0 top-0 bottom-0 w-64 bg-gray-900 text-white flex flex-col animate-slide-in-right overflow-y-auto">
            {/* Close Button */}
            <div className="p-4 flex justify-between items-center border-b border-gray-800">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-primary-600 rounded-lg flex items-center justify-center">
                  <span className="text-xl">🏪</span>
                </div>
                <span className="font-bold">TajerPro</span>
              </div>
              <button
                onClick={() => setSidebarOpen(false)}
                className="p-2 hover:bg-gray-800 rounded-lg"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Navigation */}
            <nav className="flex-1 p-4 space-y-1 overflow-y-auto">
              {filteredNavItems.map((item) =>
                item.subItems ? (
                  <NavItemWithSubmenu
                    key={item.path}
                    path={item.path}
                    label={item.label}
                    icon={item.icon}
                    subItems={item.subItems}
                    onItemClick={() => setSidebarOpen(false)}
                  />
                ) : (
                  <NavLink
                    key={item.path}
                    to={item.path}
                    onClick={() => setSidebarOpen(false)}
                    className={({ isActive }) =>
                      clsx(
                        "flex items-center gap-3 px-4 py-3 rounded-lg transition-colors",
                        isActive
                          ? "bg-primary-600 text-white"
                          : "text-gray-300 hover:bg-gray-800",
                      )
                    }
                  >
                    <item.icon className="w-5 h-5" />
                    <span>{item.label}</span>
                  </NavLink>
                ),
              )}
            </nav>

            {/* Logout */}
            <div className="p-4 border-t border-gray-800">
              <button
                onClick={logout}
                className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-gray-800 hover:bg-gray-700 rounded-lg transition-colors"
              >
                <LogOut className="w-4 h-4" />
                <span>تسجيل الخروج</span>
              </button>
            </div>
          </aside>
        </div>
      )}

      {/* Main Content */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Header */}
        <header className="bg-white border-b px-4 py-3 flex items-center justify-between relative">
          <div className="flex items-center gap-4">
            <button
              onClick={() => setSidebarOpen(true)}
              className="lg:hidden p-2 hover:bg-gray-100 rounded-lg"
            >
              <Menu className="w-5 h-5" />
            </button>

            <div className="lg:hidden flex items-center gap-2">
              <div className="w-8 h-8 bg-primary-600 rounded-lg flex items-center justify-center">
                <span className="text-sm">🏪</span>
              </div>
              <span className="font-bold text-primary-600">TajerPro</span>
            </div>
          </div>

          <div className="flex items-center gap-4">
            <BranchSelector />

            <div className="hidden sm:flex items-center gap-2 text-gray-500">
              <Clock className="w-4 h-4" />
              <span className="text-sm">{currentTime}</span>
            </div>

            <div className="flex items-center gap-2 bg-gray-100 px-3 py-2 rounded-lg">
              <User className="w-4 h-4 text-gray-500" />
              <span className="text-sm font-medium hidden sm:inline">
                {user?.name}
              </span>
              {user?.role === "Admin" && (
                <span className="text-xs bg-primary-600 text-white px-2 py-0.5 rounded-full hidden sm:inline">
                  مدير
                </span>
              )}
              {user?.role === "SystemOwner" && (
                <span className="text-xs bg-primary-600 text-white px-2 py-0.5 rounded-full hidden sm:inline">
                  مالك النظام
                </span>
              )}
            </div>

            <button
              onClick={logout}
              className="hidden lg:flex items-center gap-2 px-3 py-2 text-gray-600 hover:bg-gray-100 rounded-lg transition-colors"
            >
              <LogOut className="w-4 h-4" />
              <span>خروج</span>
            </button>
          </div>
        </header>

        {/* Main Content */}
        <main className="flex-1 overflow-auto bg-gray-50">
          <Outlet />
        </main>
      </div>
    </div>
  );
};
