export interface User {
  id: number;
  name: string;
  email: string;
  role: "Admin" | "Cashier" | "SystemOwner";
  permissions: string[];
  branchId?: number;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
}
