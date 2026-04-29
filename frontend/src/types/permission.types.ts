export interface PermissionInfo {
  key: string;
  group: string;
  groupAr: string;
  description: string;
  descriptionAr: string;
  isDefault: boolean;
  isSensitive?: boolean;
}

export interface PermissionDto {
  id: number;
  name: string;
  label: string;
  description: string;
  isSensitive: boolean;
}

export interface PermissionGroupDto {
  groupName: string;
  permissions: PermissionDto[];
}

export interface UserPermissions {
  userId: number;
  userName: string;
  email: string;
  permissions: string[];
}

export interface UserPermissionsDto {
  userId: number;
  userName: string;
  role: string;
  isCustomized: boolean;
  permissions: string[];
  defaultPermissions: string[];
}

export interface UpdatePermissionsRequest {
  permissions: string[];
}

export interface UpdatePermissionsDto {
  userId: number;
  permissions: string[];
}
