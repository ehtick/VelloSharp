#!/usr/bin/env node
import { chromium } from 'playwright';

const [,, url, screenshotPath] = process.argv;

if (!url) {
  console.error('Usage: node smoke.mjs <url> [screenshot-path]');
  process.exit(1);
}

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();

await page.goto(url, { waitUntil: 'networkidle' });
await page.waitForSelector('canvas');

const statusLocator = page.locator('.status-message').first();
await statusLocator.waitFor({ timeout: 5000 });

const statusText = (await statusLocator.textContent())?.trim() ?? '';
if (statusText.length === 0) {
  console.error('Status text could not be determined.');
  await browser.close();
  process.exit(2);
}

if (screenshotPath) {
  await page.screenshot({ path: screenshotPath, fullPage: false });
}

await browser.close();
console.log(`Status message: ${statusText}`);
