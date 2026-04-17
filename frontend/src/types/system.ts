export interface CreateTenantRequest {
  tenantName: string;
  adminEmail: string;
  adminPassword: string;
  branchName: string;
}

export interface CreateTenantResponse {
  tenantId: number;
  tenantName: string;
  tenantSlug: string;
  branchId: number;
  branchName: string;
  adminUserId: number;
  adminEmail: string;
}

export interface SystemTenantSummary {
  id: number;
  name: string;
  slug: string;
  isActive: boolean;
  branchesCount: number;
  activeBranchesCount: number;
  usersCount: number;
  activeUsersCount: number;
  inactiveUsersCount: number;
  adminsCount: number;
  cashiersCount: number;
  currency: string;
  timezone: string;
  taxRate: number;
  isTaxEnabled: boolean;
  allowNegativeStock: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface SetTenantStatusRequest {
  isActive: boolean;
}

export interface SystemSeedRunResult {
  startedAtUtc: string;
  completedAtUtc: string;
  durationMs: number;
  inventorySynchronizationTriggered: boolean;
  preservedExistingData: boolean;
  seededTenantSlugs: string[];
  optionalWarnings: string[];
}
