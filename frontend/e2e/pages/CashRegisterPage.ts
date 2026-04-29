import { Page, Locator, expect } from '@playwright/test';

/**
 * Cash Register Page Object
 * Handles cash register dashboard interactions
 */
export class CashRegisterPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly depositButton: Locator;
  readonly withdrawButton: Locator;
  readonly transferButton: Locator;
  readonly reconcileButton: Locator;
  readonly refreshButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: /الخزينة/i });
    this.depositButton = page.getByRole('button', { name: /إيداع/i });
    this.withdrawButton = page.getByRole('button', { name: /سحب/i });
    this.transferButton = page.getByRole('button', { name: /تحويل نقدي/i });
    this.reconcileButton = page.getByRole('button', { name: /مطابقة وإغلاق الشيفت/i });
    this.refreshButton = page.getByRole('button', { name: /تحديث/i });
  }

  async goto() {
    await this.page.goto('/cash-register');
    await expect(this.heading).toBeVisible();
  }

  async expectDepositButtonVisible() {
    await expect(this.depositButton).toBeVisible();
  }

  async expectWithdrawButtonVisible() {
    await expect(this.withdrawButton).toBeVisible();
  }

  async expectTransferButtonHidden() {
    await expect(this.transferButton).toBeHidden();
  }

  async expectReconcileButtonHidden() {
    await expect(this.reconcileButton).toBeHidden();
  }

  async expectTransferButtonVisible() {
    await expect(this.transferButton).toBeVisible();
  }

  async expectReconcileButtonVisible() {
    await expect(this.reconcileButton).toBeVisible();
  }
}
