import type React from "react";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { useState, useEffect } from "react";
import { useAppSelector, useAppDispatch } from "./store/hooks";
import {
  selectCurrentUser,
  selectIsAuthenticated,
  selectIsAdmin,
  selectIsSystemOwner,
  selectToken,
  logout as logoutAction,
} from "./store/slices/authSlice";
import { clearBranch } from "./store/slices/branchSlice";
import { useGetCurrentShiftQuery } from "./api/shiftsApi";
import { ErrorBoundary } from "./components/ErrorBoundary";
import { MainLayout } from "./components/layout/MainLayout";
import { ShiftRecoveryModal } from "./components/shifts";
import { shiftPersistence } from "./utils/shiftPersistence";
import { usePermission } from "./hooks/usePermission";
import LoginPage from "./pages/auth/LoginPage";
import POSPage from "./pages/pos/POSPage";
import POSWorkspacePage from "./pages/pos/POSWorkspacePage";
import ProductsPage from "./pages/products/ProductsPage";
import CategoriesPage from "./pages/categories/CategoriesPage";
import OrdersPage from "./pages/orders/OrdersPage";
import ShiftPage from "./pages/shifts/ShiftPage";
import ShiftsManagementPage from "./pages/shifts/ShiftsManagementPage";
import CustomersPage from "./pages/customers/CustomersPage";
import SuppliersPage from "./pages/suppliers/SuppliersPage";
import { BranchesPage } from "./pages/branches/BranchesPage";
import DailyReportPage from "./pages/reports/DailyReportPage";
import SalesReportPage from "./pages/reports/SalesReportPage";
import InventoryReportsPage from "./pages/reports/InventoryReportsPage";
import ProfitLossReportPage from "./pages/reports/ProfitLossReportPage";
import ExpensesReportPage from "./pages/reports/ExpensesReportPage";
import TransferHistoryReportPage from "./pages/reports/TransferHistoryReportPage";
import TopCustomersReportPage from "./pages/reports/TopCustomersReportPage";
import CustomerDebtsReportPage from "./pages/reports/CustomerDebtsReportPage";
import CustomerActivityReportPage from "./pages/reports/CustomerActivityReportPage";
import ReportsDashboardPage from "./pages/reports/ReportsDashboardPage";
import CashierPerformanceReportPage from "./pages/reports/CashierPerformanceReportPage";
import ShiftDetailsReportPage from "./pages/reports/ShiftDetailsReportPage";
import SalesByEmployeeReportPage from "./pages/reports/SalesByEmployeeReportPage";
import ProductMovementReportPage from "./pages/reports/ProductMovementReportPage";
import ProfitableProductsReportPage from "./pages/reports/ProfitableProductsReportPage";
import SlowMovingProductsReportPage from "./pages/reports/SlowMovingProductsReportPage";
import CogsReportPage from "./pages/reports/CogsReportPage";
import SupplierPurchasesReportPage from "./pages/reports/SupplierPurchasesReportPage";
import SupplierDebtsReportPage from "./pages/reports/SupplierDebtsReportPage";
import SupplierPerformanceReportPage from "./pages/reports/SupplierPerformanceReportPage";
import AuditLogPage from "./pages/audit/AuditLogPage";
import SettingsPage from "./pages/settings/SettingsPage";
import PermissionsPage from "./pages/settings/PermissionsPage";
import UserManagementPage from "./pages/users/UserManagementPage";
import { PurchaseInvoicesPage } from "./pages/purchase-invoices/PurchaseInvoicesPage";
import { PurchaseInvoiceFormPage } from "./pages/purchase-invoices/PurchaseInvoiceFormPage";
import { PurchaseInvoiceDetailsPage } from "./pages/purchase-invoices/PurchaseInvoiceDetailsPage";
import { ExpensesPage } from "./pages/expenses/ExpensesPage";
import { ExpenseFormPage } from "./pages/expenses/ExpenseFormPage";
import { ExpenseDetailsPage } from "./pages/expenses/ExpenseDetailsPage";
import { CashRegisterDashboard } from "./pages/cash-register/CashRegisterDashboard";
import { CashRegisterTransactionsPage } from "./pages/cash-register/CashRegisterTransactionsPage";
import InventoryPage from "./pages/inventory/InventoryPage";
import TenantCreationPage from "./pages/owner/TenantCreationPage";
import SystemUsersPage from "./pages/system/SystemUsersPage";
import { BackupPage } from "./pages/backup/BackupPage";
import NotFound from "./pages/NotFound";

const ProtectedRoute = ({ children }: { children: React.ReactNode }) => {
  const isAuthenticated = useAppSelector(selectIsAuthenticated);
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <>{children}</>;
};

const AdminRoute = ({ children }: { children: React.ReactNode }) => {
  const isAdmin = useAppSelector(selectIsAdmin);
  if (!isAdmin) return <Navigate to="/pos" replace />;
  return <>{children}</>;
};

const PermissionRoute = ({
  children,
  permission,
}: {
  children: React.ReactNode;
  permission: string;
}) => {
  const { hasPermission } = usePermission();
  if (!hasPermission(permission)) return <Navigate to="/pos" replace />;
  return <>{children}</>;
};

const SystemOwnerRoute = ({ children }: { children: React.ReactNode }) => {
  const isSystemOwner = useAppSelector(selectIsSystemOwner);
  if (!isSystemOwner) return <Navigate to="/pos" replace />;
  return <>{children}</>;
};

const PublicRoute = ({ children }: { children: React.ReactNode }) => {
  const isAuthenticated = useAppSelector(selectIsAuthenticated);
  const user = useAppSelector(selectCurrentUser);
  if (isAuthenticated) {
    return (
      <Navigate
        to={user?.role === "SystemOwner" ? "/owner/tenants" : "/pos"}
        replace
      />
    );
  }
  return <>{children}</>;
};

const NonSystemOwnerRoute = ({ children }: { children: React.ReactNode }) => {
  const isSystemOwner = useAppSelector(selectIsSystemOwner);
  if (isSystemOwner) return <Navigate to="/owner/tenants" replace />;
  return <>{children}</>;
};

const AppRoutes = () => (
  <Routes>
    <Route
      path="/"
      element={
        <PublicRoute>
          <Navigate to="/login" replace />
        </PublicRoute>
      }
    />
    <Route
      path="/login"
      element={
        <PublicRoute>
          <LoginPage />
        </PublicRoute>
      }
    />
    <Route
      element={
        <ProtectedRoute>
          <MainLayout />
        </ProtectedRoute>
      }
    >
      <Route
        path="/pos"
        element={
          <NonSystemOwnerRoute>
            <POSPage />
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/pos-workspace"
        element={
          <NonSystemOwnerRoute>
            <POSWorkspacePage />
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/orders"
        element={
          <NonSystemOwnerRoute>
            <OrdersPage />
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/shift"
        element={
          <NonSystemOwnerRoute>
            <ShiftPage />
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/shifts-management"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <ShiftsManagementPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/customers"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="CustomersView">
              <CustomersPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/products"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ProductsView">
              <ProductsPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/categories"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="CategoriesView">
              <CategoriesPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/suppliers"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <SuppliersPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/purchase-invoices"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <PurchaseInvoicesPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/purchase-invoices/new"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <PurchaseInvoiceFormPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/purchase-invoices/:id"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <PurchaseInvoiceDetailsPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/purchase-invoices/:id/edit"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <PurchaseInvoiceFormPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/branches"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <BranchesPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <ReportsDashboardPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/daily"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <DailyReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/sales"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <SalesReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/inventory"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <InventoryReportsPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/profit-loss"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <ProfitLossReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/expenses"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <ExpensesReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/transfer-history"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <TransferHistoryReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/customers/top"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <TopCustomersReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/customers/debts"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <CustomerDebtsReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/customers/activity"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <CustomerActivityReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/employees/cashier-performance"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <CashierPerformanceReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/employees/shifts"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <ShiftDetailsReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/employees/sales"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <SalesByEmployeeReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/products/movement"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <ProductMovementReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/products/profitability"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <ProfitableProductsReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/products/slow"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <SlowMovingProductsReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/products/cogs"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <CogsReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/suppliers/purchases"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <SupplierPurchasesReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/suppliers/debts"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <SupplierDebtsReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/reports/suppliers/performance"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ReportsView">
              <SupplierPerformanceReportPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/audit"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <AuditLogPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/settings"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <SettingsPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/settings/permissions"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <PermissionsPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/users"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <UserManagementPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/backup"
        element={
          <NonSystemOwnerRoute>
            <AdminRoute>
              <BackupPage />
            </AdminRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/expenses"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ExpensesView">
              <ExpensesPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/expenses/new"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ExpensesCreate">
              <ExpenseFormPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/expenses/:id"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ExpensesView">
              <ExpenseDetailsPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/expenses/:id/edit"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="ExpensesCreate">
              <ExpenseFormPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/cash-register"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="CashRegisterView">
              <CashRegisterDashboard />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/cash-register/transactions"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="CashRegisterView">
              <CashRegisterTransactionsPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/inventory"
        element={
          <NonSystemOwnerRoute>
            <PermissionRoute permission="InventoryView">
              <InventoryPage />
            </PermissionRoute>
          </NonSystemOwnerRoute>
        }
      />
      <Route
        path="/owner/tenants"
        element={
          <SystemOwnerRoute>
            <TenantCreationPage />
          </SystemOwnerRoute>
        }
      />
      <Route
        path="/owner/users"
        element={
          <SystemOwnerRoute>
            <SystemUsersPage />
          </SystemOwnerRoute>
        }
      />
    </Route>
    <Route path="*" element={<NotFound />} />
  </Routes>
);

const App = () => {
  const isAuthenticated = useAppSelector(selectIsAuthenticated);
  const token = useAppSelector(selectToken);
  const dispatch = useAppDispatch();
  const { data: currentShiftData, isLoading: isLoadingShift } =
    useGetCurrentShiftQuery(undefined, {
      skip: !isAuthenticated,
    });
  const [showRecovery, setShowRecovery] = useState(false);
  const [recoveredShift, setRecoveredShift] = useState<any>(null);
  const [savedAt, setSavedAt] = useState<string>("");

  // Validate JWT token on app startup - prevents login/pos redirect loop
  // If token is expired or invalid, clear auth state immediately before any API call
  useEffect(() => {
    if (!isAuthenticated || !token) return;

    try {
      // Decode JWT payload (base64 middle section)
      const parts = token.split(".");
      if (parts.length !== 3) {
        // Invalid token format
        console.warn("Invalid JWT format - logging out");
        localStorage.removeItem("persist:auth");
        dispatch(logoutAction());
        return;
      }

      const payload = JSON.parse(atob(parts[1]));
      const exp = payload.exp;

      if (exp) {
        const now = Math.floor(Date.now() / 1000);
        if (now >= exp) {
          // Token expired
          console.warn("JWT expired - logging out");
          localStorage.removeItem("persist:auth");
          dispatch(logoutAction());
          return;
        }
      }
    } catch (e) {
      // Token is malformed - clear it
      console.warn("Failed to validate JWT - logging out", e);
      localStorage.removeItem("persist:auth");
      dispatch(logoutAction());
    }
  }, []); // Only run once on startup

  // CRITICAL: Clear branch state on app startup to prevent branch mismatch
  // This ensures that when a user logs in, they start with a clean branch state
  useEffect(() => {
    if (isAuthenticated) {
      // Clear persisted branch from localStorage
      try {
        localStorage.removeItem("persist:branch");
      } catch (e) {
        // ignore localStorage errors
      }
      // Clear branch state in Redux
      dispatch(clearBranch());
    }
  }, []); // Only run once on startup

  // Shift recovery disabled - was causing annoying popups on every app load
  // Clear any previously saved shift data on startup
  useEffect(() => {
    if (isAuthenticated) {
      shiftPersistence.clear();
    }
  }, [isAuthenticated]);

  // Shift auto-save disabled - was causing recovery modal popups
  // useEffect(() => {
  //   if (!isAuthenticated) return;
  //   const currentShift = currentShiftData?.data;
  //   if (currentShift && !currentShift.isClosed) {
  //     shiftPersistence.startAutoSave(() => currentShift);
  //   } else {
  //     shiftPersistence.stopAutoSave();
  //     if (currentShift?.isClosed) {
  //       shiftPersistence.clear();
  //     }
  //   }
  //   return () => shiftPersistence.stopAutoSave();
  // }, [isAuthenticated, currentShiftData]);

  const handleRestore = () => {
    // The shift is already in the backend, just close the modal
    // The user can continue working with the existing shift
    setShowRecovery(false);
    setRecoveredShift(null);
  };

  const handleDiscard = () => {
    shiftPersistence.clear();
    setShowRecovery(false);
    setRecoveredShift(null);
  };

  return (
    <ErrorBoundary>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </ErrorBoundary>
  );
};

export default App;
