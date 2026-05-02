import { test, expect } from '@playwright/test';

test('Scenario 3: Cashier should NOT see Transfer/Reconcile buttons', async ({ page }) => {
  // Clear state
  await page.goto('/');
  await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
  await page.goto('/login');

  // Wait for login form
  await page.waitForSelector('input[type="email"]');

  // Login as cashier
  await page.locator('input[type="email"]').fill('ahmed@kasserpro.com');
  await page.locator('input[type="password"]').fill('123456');
  await page.getByRole('button', { name: /تسجيل الدخول/i }).click();

  // Wait for navigation to complete
  await page.waitForTimeout(3000);

  // Navigate to cash register
  await page.goto('/cash-register');
  await page.waitForTimeout(2000);

  // Verify Deposit and Withdraw are visible
  await expect(page.getByRole('button', { name: /إيداع/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /سحب/i })).toBeVisible();

  // Verify Transfer and Reconcile are NOT visible
  await expect(page.getByRole('button', { name: /تحويل نقدي/i })).toBeHidden();
  await expect(page.getByRole('button', { name: /مطابقة وإغلاق الشيفت/i })).toBeHidden();
});

test('Scenario 3: Admin SHOULD see Transfer/Reconcile buttons', async ({ page }) => {
  // Clear state
  await page.goto('/');
  await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
  await page.goto('/login');

  // Wait for login form
  await page.waitForSelector('input[type="email"]');

  // Login as admin
  await page.locator('input[type="email"]').fill('admin@kasserpro.com');
  await page.locator('input[type="password"]').fill('Admin@123');
  await page.getByRole('button', { name: /تسجيل الدخول/i }).click();

  // Wait for navigation
  await page.waitForTimeout(3000);

  // Navigate to cash register
  await page.goto('/cash-register');
  await page.waitForTimeout(2000);

  // Verify all buttons are visible
  await expect(page.getByRole('button', { name: /إيداع/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /سحب/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /تحويل نقدي/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /مطابقة وإغلاق الشيفت/i })).toBeVisible();
});
