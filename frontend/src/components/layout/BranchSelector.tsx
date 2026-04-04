import { useEffect } from "react";
import { Building2, ChevronDown } from "lucide-react";
import { useAppDispatch, useAppSelector } from "@/store/hooks";
import { useGetBranchesQuery } from "@/api/branchesApi";
import {
  setCurrentBranch,
  setBranches,
  selectCurrentBranch,
  selectBranches,
} from "@/store/slices/branchSlice";
import { selectCurrentUser } from "@/store/slices/authSlice";
import { baseApi } from "@/api/baseApi";
import { clearCart } from "@/store/slices/cartSlice";

const getBranchStorageKey = (userId?: number) =>
  userId ? `selectedBranchId:${userId}` : null;

const readSavedBranchId = (storageKey: string | null): number | null => {
  if (!storageKey) {
    return null;
  }

  try {
    const value = localStorage.getItem(storageKey);
    if (!value) {
      return null;
    }

    const parsed = Number(value);
    return Number.isInteger(parsed) ? parsed : null;
  } catch {
    return null;
  }
};

const saveSelectedBranchId = (storageKey: string | null, branchId: number) => {
  if (!storageKey) {
    return;
  }

  try {
    localStorage.setItem(storageKey, String(branchId));
  } catch {
    // Ignore storage errors and keep runtime state only.
  }
};

export const BranchSelector = () => {
  const dispatch = useAppDispatch();
  const currentBranch = useAppSelector(selectCurrentBranch);
  const branches = useAppSelector(selectBranches);
  const currentUser = useAppSelector(selectCurrentUser);
  const { data: branchesData, isLoading } = useGetBranchesQuery();
  const branchStorageKey = getBranchStorageKey(currentUser?.id);

  useEffect(() => {
    if (branchesData?.data) {
      const savedBranchId = readSavedBranchId(branchStorageKey);

      // Pass user's branchId to auto-select correct branch
      dispatch(
        setBranches({
          branches: branchesData.data,
          userBranchId: currentUser?.branchId,
          preferredBranchId: savedBranchId,
        }),
      );
    }
  }, [branchesData, dispatch, currentUser?.branchId, branchStorageKey]);

  useEffect(() => {
    if (currentBranch?.id) {
      saveSelectedBranchId(branchStorageKey, currentBranch.id);
    }
  }, [currentBranch?.id, branchStorageKey]);

  const handleBranchChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const branchId = parseInt(e.target.value);
    const branch = branches.find((b) => b.id === branchId);
    if (branch) {
      dispatch(setCurrentBranch(branch));
      saveSelectedBranchId(branchStorageKey, branch.id);

      // Prevent accidental checkout with items from the previous branch.
      dispatch(clearCart());
      // Invalidate all branch-sensitive caches when branch changes.
      dispatch(
        baseApi.util.invalidateTags([
          "Products",
          "Categories",
          "Orders",
          "Shifts",
          "Customers",
          "Inventory",
          "Suppliers",
          "PurchaseInvoice",
          "Reports",
          "Expense",
          "Expenses",
          "CashRegisterBalance",
          "CashRegisterTransactions",
        ]),
      );
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 px-3 py-2 bg-gray-100 rounded-lg animate-pulse">
        <Building2 className="w-4 h-4 text-gray-400" />
        <div className="h-4 w-20 bg-gray-200 rounded" />
      </div>
    );
  }

  // Cashiers can only see their assigned branch (no dropdown)
  const isCashier = currentUser?.role === "Cashier";

  // Show static branch name for cashiers or single-branch users
  if (isCashier || branches.length <= 1) {
    return (
      <div className="flex items-center gap-2 px-3 py-2 bg-gray-100 rounded-lg">
        <Building2 className="w-4 h-4 text-gray-500" />
        <span className="text-sm font-medium">
          {currentBranch?.name || "الفرع الرئيسي"}
        </span>
      </div>
    );
  }

  // Show dropdown for Admin/SystemOwner with multiple branches
  return (
    <div className="relative flex items-center gap-2 px-3 py-2 bg-gray-100 rounded-lg">
      <Building2 className="w-4 h-4 text-gray-500" />
      <select
        value={currentBranch?.id || ""}
        onChange={handleBranchChange}
        className="appearance-none bg-transparent text-sm font-medium pr-6 cursor-pointer focus:outline-none"
      >
        {branches.map((branch) => (
          <option key={branch.id} value={branch.id}>
            {branch.name}
          </option>
        ))}
      </select>
      <ChevronDown className="w-4 h-4 text-gray-400 absolute left-2 pointer-events-none" />
    </div>
  );
};
