import { useEffect, useRef } from "react";
import { useGetBranchesQuery } from "@/api/branchesApi";
import { baseApi } from "@/api/baseApi";
import { useAppDispatch, useAppSelector } from "@/store/hooks";
import { selectCurrentBranch, setBranches } from "@/store/slices/branchSlice";
import {
  selectCurrentUser,
  selectIsAuthenticated,
} from "@/store/slices/authSlice";
import { getBranchStorageKey, readSavedBranchId } from "@/utils/branchStorage";

const BRANCH_SENSITIVE_TAGS = [
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
] as const;

export const BranchStateSync = () => {
  const dispatch = useAppDispatch();
  const isAuthenticated = useAppSelector(selectIsAuthenticated);
  const currentUser = useAppSelector(selectCurrentUser);
  const currentBranch = useAppSelector(selectCurrentBranch);
  const previousBranchIdRef = useRef<number | null>(null);
  const branchStorageKey = getBranchStorageKey(currentUser?.id);
  const canReadBranches = isAuthenticated && currentUser?.role === "Admin";

  const { data: branchesData } = useGetBranchesQuery(undefined, {
    skip: !canReadBranches,
  });

  useEffect(() => {
    const branches = branchesData?.data;

    if (!canReadBranches || !branches) {
      return;
    }

    dispatch(
      setBranches({
        branches,
        userBranchId: currentUser?.branchId,
        preferredBranchId: readSavedBranchId(branchStorageKey),
      }),
    );
  }, [
    branchesData,
    branchStorageKey,
    currentUser?.branchId,
    canReadBranches,
    dispatch,
  ]);

  useEffect(() => {
    const branchId = currentBranch?.id ?? null;

    if (!branchId) {
      previousBranchIdRef.current = null;
      return;
    }

    if (previousBranchIdRef.current === branchId) {
      return;
    }

    previousBranchIdRef.current = branchId;
    dispatch(baseApi.util.invalidateTags([...BRANCH_SENSITIVE_TAGS]));
  }, [currentBranch?.id, dispatch]);

  return null;
};
