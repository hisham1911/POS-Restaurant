import { Page, Locator, expect } from '@playwright/test';

/**
 * POS Page Object
 * Handles point of sale operations
 */
export class POSPage {
  readonly page: Page;
  readonly productGrid: Locator;
  readonly cart: Locator;
  readonly emptyCartMessage: Locator;
  readonly checkoutButton: Locator;
  readonly subtotalValue: Locator;
  readonly taxValue: Locator;
  readonly totalValue: Locator;
  readonly clearCartButton: Locator;

  // Payment Modal
  readonly paymentModal: Locator;
  readonly cashMethodButton: Locator;
  readonly cardMethodButton: Locator;
  readonly amountPaidDisplay: Locator;
  readonly completePaymentButton: Locator;
  readonly numpadButtons: Locator;
  readonly exactAmountButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.productGrid = page.locator('.grid').first();
    this.cart = page.locator('text=الطلب الحالي').locator('..').locator('..');
    this.emptyCartMessage = page.locator('text=السلة فارغة');
    this.checkoutButton = page.getByRole('button', { name: /الدفع/i });
    this.subtotalValue = page.locator('text=المجموع الفرعي').locator('..').locator('span').last();
    this.taxValue = page.locator('text=الضريبة').locator('..').locator('span').last();
    this.totalValue = page.locator('text=الإجمالي').locator('..').locator('span').last();
    this.clearCartButton = page.locator('text=إفراغ');

    // Payment Modal
    this.paymentModal = page.locator('text=الدفع').locator('..').locator('..');
    this.cashMethodButton = page.locator('text=نقدي').locator('..');
    this.cardMethodButton = page.locator('text=بطاقة').locator('..');
    this.amountPaidDisplay = page.locator('text=المبلغ المدفوع').locator('..').locator('p.text-3xl');
    this.completePaymentButton = page.getByRole('button', { name: /إتمام الدفع/i });
    this.numpadButtons = page.locator('.grid-cols-4 button');
    this.exactAmountButton = page.getByRole('button', { name: 'تمام', exact: true });
  }

  async goto() {
    await this.page.goto('/pos');
    // Wait for products to load
    await this.page.waitForLoadState('networkidle');
    await this.page.waitForTimeout(1000);
  }

  async selectProduct(productName: string) {
    const product = this.page.locator(`text=${productName}`).first();
    await product.click();
  }

  async selectProductByPrice(price: number) {
    // Find product card containing the price
    const productCard = this.page.locator(`text=${price}`).locator('..').locator('..');
    await productCard.click();
  }

  async getSubtotal(): Promise<string> {
    return await this.subtotalValue.textContent() || '0';
  }

  async getTax(): Promise<string> {
    return await this.taxValue.textContent() || '0';
  }

  async getTotal(): Promise<string> {
    return await this.totalValue.textContent() || '0';
  }

  async clearCart() {
    if (await this.clearCartButton.isVisible()) {
      await this.clearCartButton.click();
    }
  }

  async checkout() {
    await this.checkoutButton.click();
    await expect(this.page.locator('text=الإجمالي المطلوب')).toBeVisible();
  }

  async selectPaymentMethod(method: 'cash' | 'bankAccount' | 'wallet') {
    const methodMap = {
      cash: 'نقدي',
      bankAccount: 'حساب بنكي',
      wallet: 'محفظة'
    };
    await this.page.locator(`text=${methodMap[method]}`).locator('..').click();
  }

  async enterAmount(amount: number) {
    // Clear existing amount
    await this.page.getByRole('button', { name: 'C' }).click();
    // Enter new amount
    const amountStr = amount.toString();
    for (const digit of amountStr) {
      await this.page.getByRole('button', { name: digit, exact: true }).click();
    }
  }

  async payExactAmount() {
    await this.exactAmountButton.click();
  }

  async completePayment() {
    await this.completePaymentButton.click();
  }

  async expectPaymentSuccess() {
    // Wait for success toast
    const toast = this.page.locator('[data-sonner-toast]').filter({ hasText: /تم|الدفع|بنجاح/i });
    await expect(toast).toBeVisible({ timeout: 10000 });
  }

  async expectEmptyCart() {
    await expect(this.emptyCartMessage).toBeVisible({ timeout: 5000 });
  }

  async expectCheckoutDisabled() {
    // When cart is empty, checkout button should not be visible
    await expect(this.checkoutButton).not.toBeVisible();
  }

  async isCartEmpty(): Promise<boolean> {
    return await this.emptyCartMessage.isVisible();
  }
}
