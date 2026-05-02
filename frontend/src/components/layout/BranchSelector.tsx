import { useEffect, useRef, useState } from "react";
import { Building2, ChevronDown } from "lucide-react";
import { useAppDispatch, useAppSelector } from "@/store/hooks";
import { useGetBranchesQuery } from "@/api/branchesApi";
import {
  setCurrentBranch,
  selectCurrentBranch,
  selectBranches,
} from "@/store/slices/branchSlice";
import { selectCurrentUser } from "@/store/slices/authSlice";
import { clearCart } from "@/store/slices/cartSlice";
import {
  getBranchStorageKey,
  saveSelectedBranchId,
} from "@/utils/branchStorage";
import { usePermission } from "@/hooks/usePermission";

type BranchSelectorVariant = "header" | "chip";

export const BranchSelector = ({
  variant = "header",
}: {
  variant?: BranchSelectorVariant;
}) => {
  const dispatch = useAppDispatch();
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [isOpen, setIsOpen] = useState(false);
  const currentBranch = useAppSelector(selectCurrentBranch);
  const branches = useAppSelector(selectBranches);
  const currentUser = useAppSelector(selectCurrentUser);
  const { hasPermission } = usePermission();
  const canReadBranches =
    currentUser?.role !== "SystemOwner" && hasPermission("BranchesView");
  const { isLoading } = useGetBranchesQuery(undefined, {
    skip: !canReadBranches,
  });
  const branchStorageKey = getBranchStorageKey(currentUser?.id);
  const isChip = variant === "chip";
  const headerContainerClass =
    "inline-flex items-center gap-2 rounded-full border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-medium text-slate-700";
  const headerButtonClass =
    "inline-flex items-center gap-2 rounded-full border border-slate-200 bg-slate-50 px-3 py-2 text-sm font-medium text-slate-700 shadow-sm transition hover:border-slate-300 hover:bg-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-200";
  const headerIconClass = "h-4 w-4 text-slate-500";
  const headerPlaceholderClass = "h-4 w-20 rounded bg-slate-200";
  const headerMenuClass =
    "absolute left-0 top-full z-20 mt-2 min-w-[12rem] rounded-2xl border border-slate-200 bg-white/95 p-2 shadow-[0_20px_50px_-38px_rgba(15,23,42,0.35)] backdrop-blur";
  const headerItemClass =
    "flex w-full items-center justify-between gap-2 rounded-xl px-3 py-2 text-sm font-medium text-slate-600 transition hover:bg-slate-50";
  const headerActiveItemClass =
    "flex w-full items-center justify-between gap-2 rounded-xl bg-primary-50 px-3 py-2 text-sm font-semibold text-primary-700";
  const headerActiveDotClass = "h-2 w-2 rounded-full bg-primary-500";
  const headerChevronIdleClass = "text-slate-400";
  const headerChevronActiveClass = "text-slate-600";

  const chipContainerClass =
    "inline-flex items-center gap-2 rounded-full border border-secondary-100 bg-secondary-50 px-3 py-2 text-sm font-medium text-secondary-700 shadow-[0_10px_22px_-18px_rgba(15,23,42,0.35)]";
  const chipButtonClass =
    "inline-flex items-center gap-2 rounded-full border border-secondary-100 bg-secondary-50 px-3 py-2 text-sm font-medium text-secondary-700 shadow-[0_10px_22px_-18px_rgba(15,23,42,0.35)] transition hover:border-secondary-200 hover:bg-secondary-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-secondary-300";
  const chipIconClass = "h-4 w-4 text-secondary-600";
  const chipPlaceholderClass = "h-4 w-20 rounded bg-secondary-100";
  const chipMenuClass =
    "absolute left-0 top-full z-20 mt-2 min-w-[12rem] rounded-2xl border border-secondary-100 bg-white/95 p-2 shadow-[0_20px_50px_-38px_rgba(15,23,42,0.35)] backdrop-blur";
  const chipItemClass =
    "flex w-full items-center justify-between gap-2 rounded-xl px-3 py-2 text-sm font-medium text-slate-600 transition hover:bg-slate-50";
  const chipActiveItemClass =
    "flex w-full items-center justify-between gap-2 rounded-xl bg-secondary-50 px-3 py-2 text-sm font-semibold text-secondary-700";
  const chipActiveDotClass = "h-2 w-2 rounded-full bg-secondary-500";
  const chipChevronIdleClass = "text-secondary-400";
  const chipChevronActiveClass = "text-secondary-600";
  const currentBranchLabel =
    currentBranch?.name ||
    (currentUser?.role === "Cashier" ? "فرعك المحدد" : "بدون فرع");

  const variantStyles = isChip
    ? {
        container: chipContainerClass,
        button: chipButtonClass,
        icon: chipIconClass,
        placeholder: chipPlaceholderClass,
        menu: chipMenuClass,
        item: chipItemClass,
        activeItem: chipActiveItemClass,
        activeDot: chipActiveDotClass,
        chevronIdle: chipChevronIdleClass,
        chevronActive: chipChevronActiveClass,
      }
    : {
        container: headerContainerClass,
        button: headerButtonClass,
        icon: headerIconClass,
        placeholder: headerPlaceholderClass,
        menu: headerMenuClass,
        item: headerItemClass,
        activeItem: headerActiveItemClass,
        activeDot: headerActiveDotClass,
        chevronIdle: headerChevronIdleClass,
        chevronActive: headerChevronActiveClass,
      };

  useEffect(() => {
    if (!isOpen) return;

    const handleClickOutside = (event: MouseEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isChip, isOpen]);

  useEffect(() => {
    if (currentBranch?.id) {
      saveSelectedBranchId(branchStorageKey, currentBranch.id);
    }
  }, [currentBranch?.id, branchStorageKey]);

  const applyBranchSelection = (branchId: number) => {
    const branch = branches.find((b) => b.id === branchId);
    if (branch) {
      dispatch(setCurrentBranch(branch));
      saveSelectedBranchId(branchStorageKey, branch.id);

      // Prevent accidental checkout with items from the previous branch.
      dispatch(clearCart());
    }
  };

  const handleBranchChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    applyBranchSelection(parseInt(e.target.value));
  };

  const handleBranchSelect = (branchId: number) => {
    applyBranchSelection(branchId);
    setIsOpen(false);
  };

  if (isLoading) {
    return (
      <div className={`${variantStyles.container} animate-pulse`}>
        <Building2 className={variantStyles.icon} />
        <div className={variantStyles.placeholder} />
      </div>
    );
  }

  // Cashiers can only see their assigned branch (no dropdown)
  const isCashier = currentUser?.role === "Cashier";

  // Show static branch name for non-admin users or when there is only one branch.
  if (!canReadBranches || isCashier || branches.length <= 1) {
    return (
      <div className={variantStyles.container}>
        <Building2 className={variantStyles.icon} />
        <span className="max-w-[10rem] truncate text-sm font-medium">
          {currentBranchLabel}
        </span>
      </div>
    );
  }

  return (
    <div ref={containerRef} className="relative inline-flex">
      <button
        type="button"
        onClick={() => setIsOpen((current) => !current)}
        className={variantStyles.button}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
      >
        <Building2 className={variantStyles.icon} />
        <span className="max-w-[10rem] truncate text-sm font-medium">
          {currentBranchLabel}
        </span>
        <ChevronDown
          className={`h-4 w-4 transition ${
            isOpen
              ? `${variantStyles.chevronActive} rotate-180`
              : variantStyles.chevronIdle
          }`}
        />
      </button>

      {isOpen && (
        <div role="listbox" className={variantStyles.menu}>
          {branches.map((branch) => {
            const isActive = branch.id === currentBranch?.id;
            const itemClass = isActive
              ? variantStyles.activeItem
              : variantStyles.item;

            return (
              <button
                key={branch.id}
                type="button"
                onClick={() => handleBranchSelect(branch.id)}
                className={itemClass}
              >
                <span className="truncate">{branch.name}</span>
                {isActive && <span className={variantStyles.activeDot} />}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
};
