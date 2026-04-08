import {
  ArrowUpLeft,
  BarChart3,
  Building2,
  Clock3,
  FileText,
  LogOut,
  Receipt,
  Store,
} from "lucide-react";
import { useNavigate } from "react-router-dom";
import { getAccessibleNavigationItems, navigationItems } from "@/components/layout/navigation";
import {
  buildVisibleModuleSections,
  reportGroups,
  type GroupTone,
} from "@/components/layout/navigationGroups";
import { useGetCurrentTenantQuery } from "@/api/branchesApi";
import { useAuth } from "@/hooks/useAuth";
import { usePermission } from "@/hooks/usePermission";
import { cn } from "@/lib/utils";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentBranch } from "@/store/slices/branchSlice";

export default function HomePage() {
  const navigate = useNavigate();
  const { user, logout, isAdmin, isSystemOwner } = useAuth();
  const { hasPermission } = usePermission();
  const currentBranch = useAppSelector(selectCurrentBranch);
  const { data: tenantResponse } = useGetCurrentTenantQuery(undefined, {
    skip: isSystemOwner || !user,
  });

  const accessibleItems = getAccessibleNavigationItems(navigationItems, {
    isAdmin,
    isSystemOwner,
    hasPermission,
  }).filter((item) => item.path !== "/home");

  const visibleModuleSections = buildVisibleModuleSections(accessibleItems);
  const canViewReports = !isSystemOwner && hasPermission("ReportsView");
  const visibleReportGroups = canViewReports ? reportGroups : [];

  const firstName = user?.name?.trim().split(/\s+/)[0] ?? "المستخدم";
  const storeName = tenantResponse?.data?.name ?? "TajerPro";
  const currentTime = new Date().toLocaleTimeString("ar-EG", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Africa/Cairo",
  });

  return (
    <div className="min-h-screen bg-[radial-gradient(circle_at_top,_rgba(37,99,235,0.10),_transparent_26%),radial-gradient(circle_at_bottom_left,_rgba(249,115,22,0.10),_transparent_22%),linear-gradient(180deg,_#f8fbff_0%,_#f9fafb_50%,_#fffaf5_100%)]">
      <div className="mx-auto max-w-7xl space-y-4 p-4 sm:p-6 lg:p-8">
        <section className="rounded-[1.8rem] border border-primary-100 bg-white/90 p-5 shadow-[0_20px_50px_-38px_rgba(37,99,235,0.30)] backdrop-blur">
          <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
            <div className="space-y-2">
              <div className="inline-flex items-center gap-2 rounded-full border border-primary-100 bg-primary-50 px-3 py-1 text-xs font-semibold text-primary-700">
                <FileText className="h-3.5 w-3.5" />
                <span>الرئيسية</span>
              </div>
              <h1 className="text-2xl font-bold text-gray-900 sm:text-3xl">
                أهلاً {firstName}
              </h1>
              <p className="text-sm text-gray-500">
                اختر القسم أو التقرير المطلوب مباشرة.
              </p>
            </div>

            <div className="flex w-full flex-wrap justify-end gap-2 md:w-auto md:max-w-[52%] md:self-start">
              <InfoChip label={storeName} icon={Store} tone="primary" />
              <InfoChip
                label={currentBranch?.name ?? "بدون فرع"}
                icon={Building2}
                tone="secondary"
              />
              <InfoChip label={currentTime} icon={Clock3} tone="primary" />
              <ActionChip
                label="تسجيل الخروج"
                icon={LogOut}
                tone="secondary"
                onClick={logout}
              />
            </div>
          </div>
        </section>

        <section className="space-y-3">
          <SectionHeader title="الأقسام" count={visibleModuleSections.length} />
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {visibleModuleSections.map((section) => (
              <CompactSectionCard
                key={section.id}
                title={section.title}
                icon={section.icon}
                tone={section.tone}
                items={section.items.map((item) => ({
                  label: item.label,
                  icon: item.icon,
                  onClick: () => navigate(item.path),
                }))}
              />
            ))}
          </div>
        </section>

        {visibleReportGroups.length > 0 && (
          <section className="space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <SectionHeader
                title="التقارير"
                count={visibleReportGroups.length}
              />
              <button
                type="button"
                onClick={() => navigate("/reports")}
                className="inline-flex items-center gap-2 rounded-full border border-primary-200 bg-primary-50 px-4 py-2 text-sm font-semibold text-primary-700 transition hover:border-primary-300 hover:bg-primary-100"
              >
                <BarChart3 className="h-4 w-4" />
                <span>مركز التقارير</span>
              </button>
            </div>

            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              {visibleReportGroups.map((group) => (
                <CompactSectionCard
                  key={group.id}
                  title={group.title}
                  icon={group.icon}
                  tone={group.tone}
                  items={group.items.map((item) => ({
                    label: item.label,
                    icon: item.icon,
                    onClick: () => navigate(item.path),
                  }))}
                />
              ))}
            </div>
          </section>
        )}
      </div>
    </div>
  );
}

function SectionHeader({
  title,
  count,
}: {
  title: string;
  count: number;
}) {
  return (
    <div className="flex items-center justify-between gap-3">
      <h2 className="text-lg font-bold text-gray-900 sm:text-xl">{title}</h2>
      <span className="rounded-full border border-gray-200 bg-white px-3 py-1 text-xs font-semibold text-gray-500">
        {count}
      </span>
    </div>
  );
}

function InfoChip({
  label,
  icon: Icon,
  tone,
}: {
  label: string;
  icon: typeof Building2;
  tone: "primary" | "secondary";
}) {
  const toneClasses =
    tone === "primary"
      ? "border-primary-100 bg-primary-50 text-primary-700"
      : "border-secondary-100 bg-secondary-50 text-secondary-700";

  return (
    <div
      className={cn(
        "inline-flex items-center gap-2 rounded-full border px-3 py-2 text-sm font-medium",
        toneClasses,
      )}
    >
      <Icon className="h-4 w-4" />
      <span>{label}</span>
    </div>
  );
}

function ActionChip({
  label,
  icon: Icon,
  tone,
  onClick,
}: {
  label: string;
  icon: typeof LogOut;
  tone: "primary" | "secondary";
  onClick: () => void;
}) {
  const toneClasses =
    tone === "primary"
      ? "border-primary-100 bg-primary-50 text-primary-700 hover:bg-primary-100"
      : "border-secondary-100 bg-secondary-50 text-secondary-700 hover:bg-secondary-100";

  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "inline-flex items-center gap-2 rounded-full border px-3 py-2 text-sm font-medium transition",
        toneClasses,
      )}
    >
      <Icon className="h-4 w-4" />
      <span>{label}</span>
    </button>
  );
}

function CompactSectionCard({
  title,
  icon: Icon,
  tone,
  items,
}: {
  title: string;
  icon: typeof Receipt;
  tone: GroupTone;
  items: Array<{
    label: string;
    icon: typeof Receipt;
    onClick: () => void;
  }>;
}) {
  const toneClasses =
    tone === "emerald"
      ? {
          frame: "border-emerald-100",
          icon: "border-emerald-200 bg-emerald-50 text-emerald-700",
          button:
            "border-emerald-100 bg-emerald-50/60 text-emerald-700 hover:bg-emerald-100",
        }
      : tone === "secondary"
        ? {
            frame: "border-secondary-100",
            icon: "border-secondary-200 bg-secondary-50 text-secondary-700",
            button:
              "border-secondary-100 bg-secondary-50/60 text-secondary-700 hover:bg-secondary-100",
          }
        : {
            frame: "border-primary-100",
            icon: "border-primary-200 bg-primary-50 text-primary-700",
            button:
              "border-primary-100 bg-primary-50/60 text-primary-700 hover:bg-primary-100",
          };

  return (
    <div
      className={cn(
        "rounded-[1.6rem] border bg-white/92 p-4 shadow-[0_16px_40px_-34px_rgba(15,23,42,0.35)]",
        toneClasses.frame,
      )}
    >
      <div className="mb-3 flex items-center gap-3">
        <div
          className={cn(
            "flex h-11 w-11 items-center justify-center rounded-2xl border",
            toneClasses.icon,
          )}
        >
          <Icon className="h-5 w-5" />
        </div>
        <h3 className="text-base font-bold text-gray-900">{title}</h3>
      </div>

      <div className="grid gap-2">
        {items.map((item) => (
          <button
            key={item.label}
            type="button"
            onClick={item.onClick}
            className={cn(
              "flex items-center justify-between rounded-2xl border px-3 py-2.5 text-sm font-medium transition",
              toneClasses.button,
            )}
          >
            <span className="flex items-center gap-2">
              <item.icon className="h-4 w-4" />
              <span>{item.label}</span>
            </span>
            <ArrowUpLeft className="h-4 w-4" />
          </button>
        ))}
      </div>
    </div>
  );
}
