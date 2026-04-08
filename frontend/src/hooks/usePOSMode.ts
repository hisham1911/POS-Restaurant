import { useState, useEffect } from "react";

export type POSMode = "cashier" | "standard";

const POS_MODE_KEY = "pos_mode";
const MOBILE_BREAKPOINT = 1024;

const getDefaultPOSMode = (): POSMode => {
  if (typeof window === "undefined") {
    return "cashier";
  }

  return window.innerWidth < MOBILE_BREAKPOINT ? "standard" : "cashier";
};

export const usePOSMode = () => {
  const [mode, setModeState] = useState<POSMode>(() => {
    if (typeof window === "undefined") {
      return "cashier";
    }

    const saved = localStorage.getItem(POS_MODE_KEY);
    if (saved === "cashier" || saved === "standard") {
      return saved;
    }

    return getDefaultPOSMode();
  });

  const setMode = (newMode: POSMode) => {
    setModeState(newMode);
    localStorage.setItem(POS_MODE_KEY, newMode);
  };

  useEffect(() => {
    // Listen for storage changes from other tabs
    const handleStorageChange = (e: StorageEvent) => {
      if (e.key === POS_MODE_KEY && e.newValue) {
        setModeState(e.newValue as POSMode);
      }
    };

    window.addEventListener("storage", handleStorageChange);
    return () => window.removeEventListener("storage", handleStorageChange);
  }, []);

  return { mode, setMode };
};
