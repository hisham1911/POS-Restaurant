import { Middleware } from "@reduxjs/toolkit";
import { logout } from "../slices/authSlice";

/**
 * Token Validation Middleware
 * Validates JWT token before every action to prevent stale token issues
 */
export const tokenValidationMiddleware: Middleware =
  (store) => (next) => (action) => {
    // Skip validation for logout action to prevent infinite loop
    if (
      typeof action === "object" &&
      action !== null &&
      "type" in action &&
      (action as { type?: string }).type === "auth/logout"
    ) {
      return next(action);
    }

    const state = store.getState();
    const { token, isAuthenticated } = state.auth;

    // Only validate if user is authenticated
    if (isAuthenticated && token) {
      try {
        // Decode JWT payload
        const parts = token.split(".");
        if (parts.length !== 3) {
          console.warn("[TokenValidation] Invalid JWT format - logging out");
          localStorage.removeItem("persist:auth");
          return next(logout());
        }

        const payload = JSON.parse(atob(parts[1]));
        const exp = payload.exp;

        if (exp) {
          const now = Math.floor(Date.now() / 1000);
          // Add 5 second buffer to prevent edge cases
          if (now >= exp - 5) {
            console.warn("[TokenValidation] JWT expired - logging out");
            localStorage.removeItem("persist:auth");
            return next(logout());
          }
        }
      } catch (e) {
        console.warn(
          "[TokenValidation] Failed to validate JWT - logging out",
          e,
        );
        localStorage.removeItem("persist:auth");
        return next(logout());
      }
    }

    return next(action);
  };
