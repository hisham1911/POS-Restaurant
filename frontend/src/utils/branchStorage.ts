const buildBranchStorageKey = (userId?: number) =>
  userId ? `selectedBranchId:${userId}` : null;

export const getBranchStorageKey = (userId?: number) =>
  buildBranchStorageKey(userId);

export const readSavedBranchId = (storageKey: string | null): number | null => {
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

export const saveSelectedBranchId = (
  storageKey: string | null,
  branchId: number,
) => {
  if (!storageKey) {
    return;
  }

  try {
    localStorage.setItem(storageKey, String(branchId));
  } catch {
    // Ignore storage errors and keep runtime state only.
  }
};
