import { NavLink, useLocation } from "react-router-dom";
import { ChevronDown, ChevronUp, type LucideIcon } from "lucide-react";
import clsx from "clsx";
import type { GroupTone } from "./navigationGroups";

interface SubItem {
  path: string;
  label: string;
}

interface NavItemWithSubmenuProps {
  label: string;
  icon: LucideIcon;
  subItems: SubItem[];
  tone?: GroupTone;
  isOpen: boolean;
  collapsed?: boolean;
  subItemCount?: number;
  onToggle: () => void;
  onItemClick?: () => void;
}

export const NavItemWithSubmenu = ({
  label,
  icon: Icon,
  subItems,
  tone = "primary",
  isOpen,
  collapsed = false,
  subItemCount,
  onToggle,
  onItemClick,
}: NavItemWithSubmenuProps) => {
  const location = useLocation();
  const isActive = subItems.some(
    (subItem) =>
      location.pathname === subItem.path ||
      location.pathname.startsWith(`${subItem.path}/`),
  );
  const toneClasses = getToneClasses(tone);
  const isHighlighted = isActive || isOpen;

  return (
    <div className="space-y-2">
      <button
        type="button"
        onClick={onToggle}
        title={collapsed ? label : undefined}
        aria-label={label}
        aria-expanded={!collapsed && isOpen}
        className={clsx(
          "w-full rounded-[1.25rem] border transition-all duration-200",
          collapsed
            ? "flex items-center justify-center px-2 py-2.5"
            : "flex items-center justify-between gap-3 px-3 py-3 text-start",
          isActive
            ? toneClasses.buttonActive
            : isOpen
              ? toneClasses.buttonOpen
              : "border-transparent bg-white/70 text-slate-700 hover:border-slate-200 hover:bg-white",
        )}
      >
        {collapsed ? (
          <div className="relative">
            <div
              className={clsx(
                "flex h-10 w-10 items-center justify-center rounded-2xl border",
                isHighlighted
                  ? toneClasses.iconHighlighted
                  : "border-slate-200 bg-slate-50 text-slate-500",
              )}
            >
              <Icon className="h-4.5 w-4.5" />
            </div>
            {isActive && (
              <span
                className={clsx(
                  "absolute -top-1 -end-1 h-2.5 w-2.5 rounded-full ring-2 ring-white",
                  toneClasses.dot,
                )}
              />
            )}
          </div>
        ) : (
          <>
            <div className="flex min-w-0 items-center gap-3">
              <div
                className={clsx(
                  "flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl border",
                  isHighlighted
                    ? toneClasses.iconHighlighted
                    : "border-slate-200 bg-slate-50 text-slate-500",
                )}
              >
                <Icon className="h-4.5 w-4.5" />
              </div>
              <div className="min-w-0 text-start">
                <div className="truncate text-sm font-semibold">{label}</div>
                {subItemCount !== undefined && (
                  <div className="mt-0.5 text-[11px] text-slate-400">
                    {subItemCount} صفحات
                  </div>
                )}
              </div>
            </div>
            <div className="flex items-center gap-2">
              {subItemCount !== undefined && (
                <span
                  className={clsx(
                    "rounded-full border px-2 py-0.5 text-[11px] font-semibold",
                    isHighlighted
                      ? toneClasses.countActive
                      : "border-slate-200 bg-white text-slate-500",
                  )}
                >
                  {subItemCount}
                </span>
              )}
              {isOpen ? (
                <ChevronUp className="h-4 w-4 shrink-0 text-slate-400" />
              ) : (
                <ChevronDown className="h-4 w-4 shrink-0 text-slate-400" />
              )}
            </div>
          </>
        )}
      </button>

      {!collapsed && isOpen && (
        <div className={clsx("me-3 space-y-1.5 border-s ps-3", toneClasses.rail)}>
          {subItems.map((subItem) => (
            <NavLink
              key={subItem.path}
              to={subItem.path}
              onClick={onItemClick}
              className={({ isActive: isSubItemActive }) =>
                clsx(
                  "flex items-center gap-2 rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors",
                  isSubItemActive
                    ? toneClasses.subItemActive
                    : toneClasses.subItemIdle,
                )
              }
            >
              {({ isActive: isSubItemActive }) => (
                <>
                  <span
                    className={clsx(
                      "h-1.5 w-1.5 rounded-full",
                      isSubItemActive ? toneClasses.dot : "bg-slate-300",
                    )}
                  />
                  <span className="truncate">{subItem.label}</span>
                </>
              )}
            </NavLink>
          ))}
        </div>
      )}
    </div>
  );
};

function getToneClasses(tone: GroupTone) {
  switch (tone) {
    case "secondary":
      return {
        buttonActive:
          "border-secondary-200 bg-secondary-50 text-secondary-800 shadow-sm",
        buttonOpen:
          "border-secondary-100 bg-secondary-50/70 text-secondary-800",
        iconHighlighted:
          "border-secondary-200 bg-white text-secondary-700 shadow-sm",
        countActive: "border-secondary-200 bg-white text-secondary-700",
        rail: "border-secondary-100",
        subItemActive:
          "border-secondary-200 bg-white text-secondary-700 shadow-sm",
        subItemIdle:
          "border-transparent text-slate-600 hover:border-secondary-100 hover:bg-secondary-50/80 hover:text-secondary-700",
        dot: "bg-secondary-500",
      };
    case "emerald":
      return {
        buttonActive:
          "border-emerald-200 bg-emerald-50 text-emerald-800 shadow-sm",
        buttonOpen: "border-emerald-100 bg-emerald-50/70 text-emerald-800",
        iconHighlighted:
          "border-emerald-200 bg-white text-emerald-700 shadow-sm",
        countActive: "border-emerald-200 bg-white text-emerald-700",
        rail: "border-emerald-100",
        subItemActive:
          "border-emerald-200 bg-white text-emerald-700 shadow-sm",
        subItemIdle:
          "border-transparent text-slate-600 hover:border-emerald-100 hover:bg-emerald-50/80 hover:text-emerald-700",
        dot: "bg-emerald-500",
      };
    case "primary":
    default:
      return {
        buttonActive:
          "border-primary-200 bg-primary-50 text-primary-800 shadow-sm",
        buttonOpen: "border-primary-100 bg-primary-50/70 text-primary-800",
        iconHighlighted:
          "border-primary-200 bg-white text-primary-700 shadow-sm",
        countActive: "border-primary-200 bg-white text-primary-700",
        rail: "border-primary-100",
        subItemActive:
          "border-primary-200 bg-white text-primary-700 shadow-sm",
        subItemIdle:
          "border-transparent text-slate-600 hover:border-primary-100 hover:bg-primary-50/80 hover:text-primary-700",
        dot: "bg-primary-500",
      };
  }
}
