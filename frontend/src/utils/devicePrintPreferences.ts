export type DevicePrintMode = "auto" | "browser" | "bridge";

export interface DevicePrintPreferences {
  mode: DevicePrintMode;
  targetBridgeDeviceId?: string;
  updatedAt: string;
}

const DEFAULT_MODE: DevicePrintMode = "auto";
const STORAGE_PREFIX = "kasserpro.devicePrintMode";

const isValidMode = (value: unknown): value is DevicePrintMode =>
  value === "auto" || value === "browser" || value === "bridge";

const normalizeTargetBridgeDeviceId = (value: unknown): string | undefined => {
  if (typeof value !== "string") {
    return undefined;
  }

  const normalized = value.trim();
  return normalized.length > 0 ? normalized : undefined;
};

const buildStorageKey = (userId?: number) =>
  userId ? `${STORAGE_PREFIX}:${userId}` : `${STORAGE_PREFIX}:guest`;

const getDefaultPreferences = (): DevicePrintPreferences => ({
  mode: DEFAULT_MODE,
  targetBridgeDeviceId: undefined,
  updatedAt: new Date().toISOString(),
});

export const getDevicePrintModeStorageKey = (userId?: number) =>
  buildStorageKey(userId);

export const readDevicePrintPreferences = (
  userId?: number,
): DevicePrintPreferences => {
  if (typeof window === "undefined") {
    return getDefaultPreferences();
  }

  const storageKey = buildStorageKey(userId);

  try {
    const rawValue = window.localStorage.getItem(storageKey);
    if (!rawValue) {
      return getDefaultPreferences();
    }

    const parsed = JSON.parse(rawValue) as Partial<DevicePrintPreferences>;
    if (!isValidMode(parsed.mode)) {
      return getDefaultPreferences();
    }

    return {
      mode: parsed.mode,
      targetBridgeDeviceId: normalizeTargetBridgeDeviceId(
        parsed.targetBridgeDeviceId,
      ),
      updatedAt:
        typeof parsed.updatedAt === "string" && parsed.updatedAt.length > 0
          ? parsed.updatedAt
          : new Date().toISOString(),
    };
  } catch {
    return getDefaultPreferences();
  }
};

export const saveDevicePrintPreferences = (
  mode: DevicePrintMode,
  userId?: number,
): DevicePrintPreferences => {
  const current = readDevicePrintPreferences(userId);

  const next: DevicePrintPreferences = {
    ...current,
    mode,
    updatedAt: new Date().toISOString(),
  };

  if (typeof window === "undefined") {
    return next;
  }

  const storageKey = buildStorageKey(userId);

  try {
    window.localStorage.setItem(storageKey, JSON.stringify(next));
  } catch {
    // Keep runtime state only when storage is unavailable.
  }

  return next;
};

export const saveTargetBridgeDeviceId = (
  targetBridgeDeviceId: string | null | undefined,
  userId?: number,
): DevicePrintPreferences => {
  const current = readDevicePrintPreferences(userId);

  const next: DevicePrintPreferences = {
    ...current,
    targetBridgeDeviceId: normalizeTargetBridgeDeviceId(targetBridgeDeviceId),
    updatedAt: new Date().toISOString(),
  };

  if (typeof window === "undefined") {
    return next;
  }

  const storageKey = buildStorageKey(userId);

  try {
    window.localStorage.setItem(storageKey, JSON.stringify(next));
  } catch {
    // Keep runtime state only when storage is unavailable.
  }

  return next;
};

export const getPrintPreferenceHeaderValue = (userId?: number): string => {
  const mode = readDevicePrintPreferences(userId).mode;

  if (mode === "browser") {
    return "BrowserOnly";
  }

  if (mode === "bridge") {
    return "BridgeOnly";
  }

  return "Auto";
};

export const getTargetBridgeDeviceIdHeaderValue = (
  userId?: number,
): string | null => {
  return readDevicePrintPreferences(userId).targetBridgeDeviceId ?? null;
};
