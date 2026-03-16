import { createSlice, PayloadAction } from "@reduxjs/toolkit";
import type { Branch } from "../../types/branch.types";

interface BranchState {
  currentBranch: Branch | null;
  branches: Branch[];
}

const initialState: BranchState = {
  currentBranch: null,
  branches: [],
};

interface SetBranchesPayload {
  branches: Branch[];
  userBranchId?: number | null;
}

const branchSlice = createSlice({
  name: "branch",
  initialState,
  reducers: {
    setCurrentBranch: (state, action: PayloadAction<Branch | null>) => {
      state.currentBranch = action.payload;
    },
    setBranches: (state, action: PayloadAction<Branch[] | SetBranchesPayload>) => {
      // Support both old and new payload formats
      const branches = Array.isArray(action.payload) 
        ? action.payload 
        : action.payload.branches;
      const userBranchId = Array.isArray(action.payload) 
        ? null 
        : action.payload.userBranchId;

      state.branches = branches;
      
      // Auto-select branch logic:
      // 1. If user has a specific branchId (Cashier), select that branch
      // 2. Otherwise, select first branch (Admin/SystemOwner)
      if (!state.currentBranch && branches.length > 0) {
        if (userBranchId) {
          // Find user's assigned branch
          const userBranch = branches.find(b => b.id === userBranchId);
          state.currentBranch = userBranch || branches[0];
        } else {
          // No specific branch - select first one
          state.currentBranch = branches[0];
        }
      }
    },
    clearBranch: (state) => {
      state.currentBranch = null;
      state.branches = [];
    },
  },
});

export const { setCurrentBranch, setBranches, clearBranch } = branchSlice.actions;

// Selectors
export const selectCurrentBranch = (state: { branch: BranchState }) => state.branch.currentBranch;
export const selectBranches = (state: { branch: BranchState }) => state.branch.branches;

export default branchSlice.reducer;
