import { useEffect, useMemo, useState } from "react";
import { useAppSelector } from "@/store/hooks";
import { selectCurrentUser } from "@/store/slices/authSlice";
import {
  DevicePrintMode,
  DevicePrintPreferences,
  getDevicePrintModeStorageKey,
  readDevicePrintPreferences,
  saveDevicePrintPreferences,
} from "@/utils/devicePrintPreferences";

export const useDevicePrintPreferences = () => {
  const user = useAppSelector(selectCurrentUser);
  const userId = user?.id;

  const [preferences, setPreferences] = useState<DevicePrintPreferences>(() =>
    readDevicePrintPreferences(userId),
  );

  const storageKey = useMemo(
    () => getDevicePrintModeStorageKey(userId),
    [userId],
  );

  useEffect(() => {
    setPreferences(readDevicePrintPreferences(userId));
  }, [userId]);

  useEffect(() => {
    const handleStorageChange = (event: StorageEvent) => {
      if (event.key !== storageKey) {
        return;
      }

      setPreferences(readDevicePrintPreferences(userId));
    };

    window.addEventListener("storage", handleStorageChange);
    return () => window.removeEventListener("storage", handleStorageChange);
  }, [storageKey, userId]);

  const setPrintMode = (mode: DevicePrintMode) => {
    const next = saveDevicePrintPreferences(mode, userId);
    setPreferences(next);
  };

  return {
    preferences,
    printMode: preferences.mode,
    setPrintMode,
    userId,
  };
};
