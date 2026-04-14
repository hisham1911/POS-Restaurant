import { NavLink, Outlet, useLocation } from "react-router-dom";
import { useEffect, useMemo, useState } from "react";
import clsx from "clsx";
import {
  ChevronLeft,
  ChevronRight,
  Clock,
  LogOut,
  Menu,
  User,
  X,
  type LucideIcon,
} from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { usePermission } from "@/hooks/usePermission";
import { BranchSelector } from "./BranchSelector";
import { NavItemWithSubmenu } from "./NavItemWithSubmenu";
import { PrinterConnectionBadge } from "@/components/printing/PrinterConnectionBadge";
import {
  getAccessibleNavigationItems,
  navigationItems,
} from "./navigation";
import {
  buildVisibleModuleSections,
  reportGroups,
  type GroupTone,
} from "./navigationGroups";

interface SidebarGroupItem {
  id: string;
  title: string;
  icon: LucideIcon;
  items: Array<{ path: string; label: string }>;
  kind: "modules" | "reports";
  tone: GroupTone;
}

const SIDEBAR_STORAGE_KEY = "kasserpro.sidebar.collapsed";

export const MainLayout = () => {
  const location = useLocation();
  const { user, logout, isAdmin, isSystemOwner } = useAuth();
  const { hasPermission } = usePermission();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState<boolean>(() => {
    if (typeof window === "undefined") return false;
    return window.localStorage.getItem(SIDEBAR_STORAGE_KEY) === "true";
  });
  const [openGroupId, setOpenGroupId] = useState<string | null>(null);
  const [mobileOpenGroupId, setMobileOpenGroupId] = useState<string | null>(
    null,
  );

  useEffect(() => {
    if (typeof window !== "undefined") {
      window.localStorage.setItem(
        SIDEBAR_STORAGE_KEY,
        sidebarCollapsed ? "true" : "false",
      );
    }
  }, [sidebarCollapsed]);

  const currentTime = new Date().toLocaleTimeString("ar-EG", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Africa/Cairo",
  });

  const filteredNavItems = getAccessibleNavigationItems(navigationItems, {
    isAdmin,
    isSystemOwner,
    hasPermission,
  });
  const homeItem = filteredNavItems.find((item) => item.path === "/home");
  const moduleGroups = buildVisibleModuleSections(
    filteredNavItems.filter((item) => item.path !== "/home"),
  );
  const visibleReportGroups =
    !isSystemOwner && hasPermission("ReportsView") ? reportGroups : [];

  const sidebarGroups: SidebarGroupItem[] = useMemo(
    () => [
      ...moduleGroups.map((group) => ({
        id: `modules-${group.id}`,
        title: group.title,
        icon: group.icon,
        items: group.items.map((item) => ({
          path: item.path,
          label: item.label,
        })),
        kind: "modules" as const,
        tone: group.tone,
      })),
      ...visibleReportGroups.map((group) => ({
        id: `reports-${group.id}`,
        title: group.title,
        icon: group.icon,
        items: group.items.map((item) => ({
          path: item.path,
          label: item.label,
        })),
        kind: "reports" as const,
        tone: group.tone,
      })),
    ],
    [moduleGroups, visibleReportGroups],
  );

  const activeGroupId =
    sidebarGroups.find((group) =>
      group.items.some(
        (item) =>
          location.pathname === item.path ||
          location.pathname.startsWith(`${item.path}/`),
      ),
    )?.id ?? null;

  useEffect(() => {
    if (activeGroupId) {
      setOpenGroupId(activeGroupId);
      setMobileOpenGroupId(activeGroupId);
    }
  }, [activeGroupId]);

  const desktopModuleGroups = sidebarGroups.filter(
    (group) => group.kind === "modules",
  );
  const desktopReportGroups = sidebarGroups.filter(
    (group) => group.kind === "reports",
  );

  const toggleDesktopGroup = (groupId: string) => {
    if (sidebarCollapsed) {
      setSidebarCollapsed(false);
      setOpenGroupId(groupId);
      return;
    }

    setOpenGroupId((current) => (current === groupId ? null : groupId));
  };

  const toggleMobileGroup = (groupId: string) => {
    setMobileOpenGroupId((current) => (current === groupId ? null : groupId));
  };

  const desktopSidebarClass = sidebarCollapsed ? "w-[5.75rem]" : "w-[19.5rem]";
  const userRoleLabel = getUserRoleLabel(user?.role);
  const userRoleBadgeClass = getUserRoleBadgeClass(user?.role);

  return (
    <div className="flex h-screen w-full overflow-hidden bg-slate-100/70">
      <aside
        className={clsx(
          "hidden shrink-0 flex-col border-l border-slate-200 bg-[linear-gradient(180deg,#f8fbff_0%,#ffffff_36%,#f8fafc_100%)] transition-[width] duration-300 lg:flex",
          desktopSidebarClass,
        )}
      >
        <div className="border-b border-slate-200/80 p-4">
          {sidebarCollapsed ? (
            <div className="flex justify-center">
              <button
                type="button"
                onClick={() => setSidebarCollapsed(false)}
                className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[linear-gradient(135deg,#2563eb_0%,#f97316_100%)] text-white shadow-[0_18px_30px_-24px_rgba(37,99,235,0.7)] transition-transform hover:scale-[1.03]"
                title="فتح السايد بار"
              >
                <span className="text-base font-black">T</span>
              </button>
            </div>
          ) : (
            <div className="rounded-[1.6rem] border border-primary-100 bg-white/95 p-3 shadow-[0_18px_40px_-34px_rgba(37,99,235,0.45)]">
              <div className="flex items-center gap-3">
                <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-[linear-gradient(135deg,#2563eb_0%,#f97316_100%)] text-white shadow-[0_18px_30px_-24px_rgba(37,99,235,0.7)]">
                  <span className="text-base font-black">T</span>
                </div>

                <div className="min-w-0 flex-1">
                  <h1 className="truncate text-base font-bold text-slate-900">
                    TajerPro
                  </h1>
                  <p className="text-xs text-slate-500">
                    تنقل أسرع بين صفحات النظام
                  </p>
                </div>

                <button
                  type="button"
                  onClick={() => setSidebarCollapsed(true)}
                  className="flex h-9 w-9 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-500 shadow-sm transition hover:border-primary-200 hover:bg-primary-50 hover:text-primary-700"
                  title="طي السايد بار"
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
              </div>
            </div>
          )}
        </div>

        <nav className="flex-1 space-y-5 overflow-y-auto p-4">
          {homeItem && (
            <NavLink
              to={homeItem.path}
              title={sidebarCollapsed ? homeItem.label : undefined}
              className={({ isActive }) =>
                clsx(
                  "group flex rounded-[1.35rem] border transition-all duration-200",
                  sidebarCollapsed
                    ? "items-center justify-center px-2 py-2.5"
                    : "items-center gap-3 px-3 py-3.5",
                  isActive
                    ? "border-primary-200 bg-primary-50 text-primary-800 shadow-sm"
                    : "border-transparent bg-white/75 text-slate-700 hover:border-slate-200 hover:bg-white",
                )
              }
            >
              {({ isActive }) => (
                <>
                  <div
                    className={clsx(
                      "relative flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl border",
                      isActive
                        ? "border-primary-200 bg-white text-primary-700 shadow-sm"
                        : "border-slate-200 bg-slate-50 text-slate-500",
                    )}
                  >
                    <homeItem.icon className="h-4.5 w-4.5" />
                    {sidebarCollapsed && isActive && (
                      <span className="absolute -top-1 -end-1 h-2.5 w-2.5 rounded-full bg-primary-500 ring-2 ring-white" />
                    )}
                  </div>

                  {!sidebarCollapsed && (
                    <div className="min-w-0 flex-1 text-start">
                      <div className="truncate text-sm font-semibold">
                        {homeItem.label}
                      </div>
                      <div className="mt-0.5 text-[11px] text-slate-400">
                        شاشة البداية
                      </div>
                    </div>
                  )}
                </>
              )}
            </NavLink>
          )}

          <SidebarGroupBlock
            title="الأقسام"
            groups={desktopModuleGroups}
            collapsed={sidebarCollapsed}
            openGroupId={openGroupId}
            onToggleGroup={toggleDesktopGroup}
          />

          <SidebarGroupBlock
            title="التقارير"
            groups={desktopReportGroups}
            collapsed={sidebarCollapsed}
            openGroupId={openGroupId}
            onToggleGroup={toggleDesktopGroup}
          />
        </nav>

        <div className="border-t border-slate-200/80 p-4">
          <div
            className={clsx(
              "mb-3 rounded-[1.4rem] border border-slate-200 bg-white/95 shadow-sm",
              sidebarCollapsed ? "p-2.5" : "p-3",
            )}
          >
            <div
              className={clsx(
                "flex items-center gap-3",
                sidebarCollapsed && "justify-center",
              )}
            >
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-slate-100 text-slate-600">
                <User className="h-4.5 w-4.5" />
              </div>

              {!sidebarCollapsed && (
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-semibold text-slate-900">
                    {user?.name}
                  </p>
                  <p className="mt-0.5 text-[11px] text-slate-400">
                    {userRoleLabel}
                  </p>
                </div>
              )}
            </div>
          </div>

          <button
            type="button"
            onClick={logout}
            title={sidebarCollapsed ? "تسجيل الخروج" : undefined}
            className={clsx(
              "flex w-full items-center justify-center gap-2 rounded-[1.15rem] border border-secondary-100 bg-secondary-50 text-secondary-700 transition hover:border-secondary-200 hover:bg-secondary-100",
              sidebarCollapsed ? "px-2 py-3" : "px-4 py-3",
            )}
          >
            <LogOut className="h-4 w-4" />
            {!sidebarCollapsed && (
              <span className="text-sm font-semibold">تسجيل الخروج</span>
            )}
          </button>
        </div>
      </aside>

      {sidebarOpen && (
        <div className="fixed inset-0 z-50 lg:hidden">
          <div
            className="absolute inset-0 bg-slate-900/35 backdrop-blur-sm"
            onClick={() => setSidebarOpen(false)}
          />

          <aside className="absolute end-0 top-0 bottom-0 flex w-80 max-w-[88vw] flex-col overflow-y-auto border-s border-slate-200 bg-[linear-gradient(180deg,#f8fbff_0%,#ffffff_36%,#f8fafc_100%)] text-slate-900 animate-slide-in-right">
            <div className="border-b border-slate-200/80 p-4">
              <div className="rounded-[1.5rem] border border-primary-100 bg-white/95 p-3 shadow-sm">
                <div className="flex items-center justify-between gap-3">
                  <div className="flex items-center gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-[linear-gradient(135deg,#2563eb_0%,#f97316_100%)] text-white">
                      <span className="text-base font-black">T</span>
                    </div>
                    <div>
                      <p className="font-bold text-slate-900">TajerPro</p>
                      <p className="text-xs text-slate-500">
                        تنقل منظم وسريع
                      </p>
                    </div>
                  </div>

                  <button
                    type="button"
                    onClick={() => setSidebarOpen(false)}
                    className="rounded-xl border border-slate-200 bg-white p-2 text-slate-500 shadow-sm transition hover:border-primary-200 hover:bg-primary-50 hover:text-primary-700"
                  >
                    <X className="h-5 w-5" />
                  </button>
                </div>
              </div>
            </div>

            <nav className="flex-1 space-y-5 overflow-y-auto p-4">
              {homeItem && (
                <NavLink
                  to={homeItem.path}
                  onClick={() => setSidebarOpen(false)}
                  className={({ isActive }) =>
                    clsx(
                      "flex items-center gap-3 rounded-[1.35rem] border px-3 py-3.5 transition-all duration-200",
                      isActive
                        ? "border-primary-200 bg-primary-50 text-primary-800 shadow-sm"
                        : "border-transparent bg-white/75 text-slate-700 hover:border-slate-200 hover:bg-white",
                    )
                  }
                >
                  {({ isActive }) => (
                    <>
                      <div
                        className={clsx(
                          "flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl border",
                          isActive
                            ? "border-primary-200 bg-white text-primary-700 shadow-sm"
                            : "border-slate-200 bg-slate-50 text-slate-500",
                        )}
                      >
                        <homeItem.icon className="h-4.5 w-4.5" />
                      </div>
                      <div className="min-w-0 flex-1 text-start">
                        <div className="truncate text-sm font-semibold">
                          {homeItem.label}
                        </div>
                        <div className="mt-0.5 text-[11px] text-slate-400">
                          شاشة البداية
                        </div>
                      </div>
                    </>
                  )}
                </NavLink>
              )}

              <SidebarGroupBlock
                title="الأقسام"
                groups={desktopModuleGroups}
                collapsed={false}
                openGroupId={mobileOpenGroupId}
                onToggleGroup={toggleMobileGroup}
                onItemClick={() => setSidebarOpen(false)}
              />

              <SidebarGroupBlock
                title="التقارير"
                groups={desktopReportGroups}
                collapsed={false}
                openGroupId={mobileOpenGroupId}
                onToggleGroup={toggleMobileGroup}
                onItemClick={() => setSidebarOpen(false)}
              />
            </nav>

            <div className="border-t border-slate-200/80 p-4">
              <div className="mb-3 rounded-[1.4rem] border border-slate-200 bg-white/95 p-3 shadow-sm">
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-full bg-slate-100 text-slate-600">
                    <User className="h-4.5 w-4.5" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-semibold text-slate-900">
                      {user?.name}
                    </p>
                    <span
                      className={clsx(
                        "mt-1 inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold",
                        userRoleBadgeClass,
                      )}
                    >
                      {userRoleLabel}
                    </span>
                  </div>
                </div>
              </div>

              <button
                type="button"
                onClick={logout}
                className="flex w-full items-center justify-center gap-2 rounded-[1.15rem] border border-secondary-100 bg-secondary-50 px-4 py-3 text-secondary-700 transition hover:border-secondary-200 hover:bg-secondary-100"
              >
                <LogOut className="h-4 w-4" />
                <span className="text-sm font-semibold">تسجيل الخروج</span>
              </button>
            </div>
          </aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="relative flex items-center justify-between border-b border-slate-200 bg-white/95 px-4 py-3">
          <div className="flex items-center gap-4">
            <button
              type="button"
              onClick={() => setSidebarOpen(true)}
              className="rounded-xl border border-slate-200 bg-white p-2 text-slate-600 shadow-sm transition hover:border-primary-200 hover:bg-primary-50 hover:text-primary-700 lg:hidden"
            >
              <Menu className="h-5 w-5" />
            </button>

            <div className="flex items-center gap-2 lg:hidden">
              <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-[linear-gradient(135deg,#2563eb_0%,#f97316_100%)] text-white">
                <span className="text-sm font-black">T</span>
              </div>
              <span className="font-bold text-primary-700">TajerPro</span>
            </div>
          </div>

          <div className="flex items-center gap-3 sm:gap-4">
            <BranchSelector />

            <div className="hidden md:block">
              <PrinterConnectionBadge />
            </div>

            <div className="hidden items-center gap-2 rounded-full border border-slate-200 bg-slate-50 px-3 py-2 text-slate-500 sm:flex">
              <Clock className="h-4 w-4" />
              <span className="text-sm">{currentTime}</span>
            </div>

            <div className="flex items-center gap-2 rounded-full border border-slate-200 bg-white px-3 py-2 shadow-sm">
              <User className="h-4 w-4 text-slate-500" />
              <span className="hidden text-sm font-medium text-slate-700 sm:inline">
                {user?.name}
              </span>
              <span
                className={clsx(
                  "hidden rounded-full border px-2 py-0.5 text-xs font-semibold sm:inline",
                  userRoleBadgeClass,
                )}
              >
                {userRoleLabel}
              </span>
            </div>

            <button
              type="button"
              onClick={logout}
              className="hidden items-center gap-2 rounded-full border border-secondary-100 bg-secondary-50 px-3 py-2 text-secondary-700 transition hover:border-secondary-200 hover:bg-secondary-100 lg:flex"
            >
              <LogOut className="h-4 w-4" />
              <span className="text-sm font-semibold">خروج</span>
            </button>
          </div>
        </header>

        <main className="flex-1 overflow-auto bg-slate-50">
          <Outlet />
        </main>
      </div>
    </div>
  );
};

function SidebarGroupBlock({
  title,
  groups,
  collapsed,
  openGroupId,
  onToggleGroup,
  onItemClick,
}: {
  title: string;
  groups: SidebarGroupItem[];
  collapsed: boolean;
  openGroupId: string | null;
  onToggleGroup: (groupId: string) => void;
  onItemClick?: () => void;
}) {
  if (groups.length === 0) return null;

  return (
    <div className="space-y-2">
      {!collapsed && (
        <div className="px-1">
          <p className="text-[11px] font-bold tracking-[0.16em] text-slate-400">
            {title}
          </p>
        </div>
      )}

      <div className="space-y-2">
        {groups.map((group) => (
          <NavItemWithSubmenu
            key={group.id}
            label={group.title}
            icon={group.icon}
            subItems={group.items}
            tone={group.tone}
            isOpen={openGroupId === group.id}
            collapsed={collapsed}
            subItemCount={group.items.length}
            onToggle={() => onToggleGroup(group.id)}
            onItemClick={onItemClick}
          />
        ))}
      </div>
    </div>
  );
}

function getUserRoleLabel(role?: string) {
  switch (role) {
    case "SystemOwner":
      return "مالك النظام";
    case "Admin":
      return "مدير";
    default:
      return "كاشير";
  }
}

function getUserRoleBadgeClass(role?: string) {
  switch (role) {
    case "SystemOwner":
      return "border-secondary-100 bg-secondary-50 text-secondary-700";
    case "Admin":
      return "border-primary-100 bg-primary-50 text-primary-700";
    default:
      return "border-emerald-100 bg-emerald-50 text-emerald-700";
  }
}
