import { useMemo } from "react";
import { useAppSelector } from "../store/hooks";
import {
  selectCurrentUser,
  selectIsAdmin,
  selectIsSystemOwner,
} from "../store/slices/authSlice";
import { expandPermissions } from "@/utils/permissionImplications";

export const usePermission = () => {
  const user = useAppSelector(selectCurrentUser);
  const isAdmin = useAppSelector(selectIsAdmin);
  const isSystemOwner = useAppSelector(selectIsSystemOwner);
  const effectivePermissions = useMemo(
    () => expandPermissions(user?.permissions ?? []),
    [user?.permissions],
  );

  const hasPermission = (permission: string): boolean => {
    // Admin & SystemOwner have all permissions
    if (isAdmin || isSystemOwner) return true;
    if (!user?.permissions) return false;
    return effectivePermissions.includes(permission);
  };

  const hasAnyPermission = (permissions: string[]): boolean => {
    return permissions.some((p) => hasPermission(p));
  };

  return { hasPermission, hasAnyPermission };
};
