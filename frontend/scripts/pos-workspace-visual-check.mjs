import { chromium, devices } from "@playwright/test";
import fs from "node:fs/promises";
import path from "node:path";

const baseUrl = "http://localhost:3000";
const outputDir = path.resolve("artifacts", "pos-workspace-check");

const credentials = {
  email: "admin@kasserpro.com",
  password: "Admin@123",
};

const TEXT = {
  catalog: "\u0627\u0644\u0643\u062a\u0627\u0644\u0648\u062c",
  customer: "\u0625\u0636\u0627\u0641\u0629 \u0639\u0645\u064a\u0644 \u0644\u0644\u0641\u0627\u062a\u0648\u0631\u0629",
  searchProduct: "\u0642\u0631\u0627\u0642\u064a\u0634",
};

async function ensureDir() {
  await fs.mkdir(outputDir, { recursive: true });
}

async function clickButtonByText(page, text) {
  await page.evaluate((label) => {
    const button = Array.from(document.querySelectorAll("button")).find((item) =>
      item.textContent?.includes(label),
    );

    if (!button) {
      throw new Error(`Button not found: ${label}`);
    }

    button.click();
  }, text);
}

async function fillSearch(page, value) {
  await page.locator('input[placeholder*="ابحث"]').first().fill(value);
}

async function scrollPrimaryPane(page, amount) {
  await page.evaluate((nextScrollTop) => {
    const candidates = Array.from(document.querySelectorAll("div")).filter(
      (element) => {
        const style = window.getComputedStyle(element);
        return (
          ["auto", "scroll"].includes(style.overflowY) &&
          element.scrollHeight > element.clientHeight + 40
        );
      },
    );

    const target = candidates.sort(
      (first, second) =>
        second.scrollHeight - second.clientHeight - (first.scrollHeight - first.clientHeight),
    )[0];

    if (target) {
      target.scrollTop = nextScrollTop;
    }
  }, amount);
}

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: "networkidle" });
  await page.locator('input[type="email"]').fill(credentials.email);
  await page.locator('input[type="password"]').fill(credentials.password);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/(pos|shift|settings|pos-workspace)/, {
    timeout: 20000,
  });
}

async function openWorkspace(page) {
  await page.evaluate(() => localStorage.setItem("pos_mode", "standard"));
  await page.goto(`${baseUrl}/pos-workspace`, { waitUntil: "networkidle" });
  await page.waitForTimeout(2000);
}

async function captureDesktop(browser) {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 960 },
    locale: "ar-EG",
    timezoneId: "Africa/Cairo",
  });
  const page = await context.newPage();

  await login(page);
  await openWorkspace(page);

  await page.screenshot({
    path: path.join(outputDir, "desktop-landing.png"),
    fullPage: true,
  });

  await clickButtonByText(page, TEXT.catalog);
  await page.waitForTimeout(1200);
  await page.screenshot({
    path: path.join(outputDir, "desktop-catalog.png"),
    fullPage: true,
  });

  const storageState = await context.storageState();
  await context.close();
  return storageState;
}

async function captureMobile(browser, storageState) {
  const context = await browser.newContext({
    ...devices["Pixel 7"],
    locale: "ar-EG",
    timezoneId: "Africa/Cairo",
    storageState,
  });
  const page = await context.newPage();

  await page.goto(baseUrl, { waitUntil: "domcontentloaded" });
  await page.evaluate(() => localStorage.setItem("pos_mode", "standard"));
  await openWorkspace(page);

  await page.screenshot({
    path: path.join(outputDir, "mobile-landing.png"),
    fullPage: true,
  });

  await fillSearch(page, TEXT.searchProduct);
  await page.waitForTimeout(1200);
  await page.screenshot({
    path: path.join(outputDir, "mobile-search-results.png"),
    fullPage: true,
  });

  await page.locator(`button:has-text("${TEXT.searchProduct}")`).first().click();
  await page.waitForTimeout(1000);
  await page.screenshot({
    path: path.join(outputDir, "mobile-with-cart.png"),
    fullPage: true,
  });

  await clickButtonByText(page, TEXT.customer);
  await page.waitForTimeout(900);
  await scrollPrimaryPane(page, 900);
  await page.waitForTimeout(400);
  await page.screenshot({
    path: path.join(outputDir, "mobile-customer-section.png"),
    fullPage: false,
  });

  await page.locator('button:has-text("الدفع")').last().click();
  await page.waitForTimeout(900);
  await scrollPrimaryPane(page, 1600);
  await page.waitForTimeout(400);
  await page.screenshot({
    path: path.join(outputDir, "mobile-payment-section.png"),
    fullPage: false,
  });

  await clickButtonByText(page, TEXT.catalog);
  await page.waitForTimeout(1200);
  await page.screenshot({
    path: path.join(outputDir, "mobile-catalog.png"),
    fullPage: true,
  });

  await context.close();
}

async function main() {
  await ensureDir();
  const browser = await chromium.launch({ headless: true });

  try {
    const storageState = await captureDesktop(browser);
    await captureMobile(browser, storageState);
    console.log(`Saved screenshots to ${outputDir}`);
  } finally {
    await browser.close();
  }
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
