import { test, expect } from '@playwright/test';
import { LoginPage } from './pages/LoginPage';
import { CashRegisterPage } from './pages/CashRegisterPage';

/**
 * Cash Register Permissions E2E Test
 *
 * Scenario 3: Cashier (no CashRegisterTransfer permission)
 * → Transfer button should NOT appear on CashRegisterDashboard
 */
test.describe('Cash Register Permissions', () => {
  test.use({ storageState: undefined });

  test('Cashier should NOT see Transfer or Reconcile buttons', async ({ page, context }) => {
    const loginPage = new LoginPage(page);
    const cashRegisterPage = new CashRegisterPage(page);

    // Ensure clean state
    await context.clearCookies();
    await page.goto('/');
    await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });

    // 1. Login as Cashier
    await loginPage.goto();
    await loginPage.loginAsCashier();

    // 2. Navigate to Cash Register Dashboard
    await cashRegisterPage.goto();

    // 3. Verify basic buttons are visible
    await cashRegisterPage.expectDepositButtonVisible();
    await cashRegisterPage.expectWithdrawButtonVisible();

    // 4. Verify restricted buttons are hidden (Scenario 3)
    await cashRegisterPage.expectTransferButtonHidden();
    await cashRegisterPage.expectReconcileButtonHidden();
  });

  test('Admin SHOULD see Transfer and Reconcile buttons', async ({ page, context }) => {
    const loginPage = new LoginPage(page);
    const cashRegisterPage = new CashRegisterPage(page);

    // Ensure clean state
    await context.clearCookies();
    await page.goto('/');
    await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });

    // 1. Login as Admin
    await loginPage.goto();
    await loginPage.loginAsAdmin();

    // 2. Navigate to Cash Register Dashboard
    await cashRegisterPage.goto();

    // 3. Verify all buttons are visible for admin
    await cashRegisterPage.expectDepositButtonVisible();
    await cashRegisterPage.expectWithdrawButtonVisible();
    await cashRegisterPage.expectTransferButtonVisible();
    await cashRegisterPage.expectReconcileButtonVisible();
  });
});
