#!/usr/bin/env node
// Visits many screens in Arabic mode; fails when a page throws, returns an API error, or renders no Arabic text.
// Needs the API (seeded with tools/seed-demo.mjs) and `ng serve`, plus playwright-core and a Chromium.
//   WEB=http://localhost:4310 CHROME=/path/to/chrome PW_ROOT=/path/to/dir-with-node_modules node tools/smoke-i18n.mjs [outdir]
import { createRequire } from 'module';
const require = createRequire(process.env.PW_ROOT ? process.env.PW_ROOT + '/' : import.meta.url);
const { chromium } = require('playwright-core');

const WEB = process.env.WEB ?? 'http://localhost:4200';
const CHROME = process.env.CHROME ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const out = process.argv[2] ?? '.';
const EMAIL = process.env.SEED_OWNER ?? 'owner@demo.local';
const PASSWORD = process.env.SEED_PASSWORD ?? 'DevOnly#2026';
const PAGES = [
  '', 'parties', 'catalog/items', 'finance/accounts', 'finance/journals', 'finance/invoices', 'finance/budgets', 'finance/cheques', 'finance/bank',
  'admin/users', 'admin/audit', 'admin/documents', 'admin/import', 'approvals', 'reports', 'reports/rent-roll', 'reports/tax-summary',
  'real-estate/properties', 'real-estate/units', 'real-estate/leases', 'real-estate/renewals', 'real-estate/work-orders', 'real-estate/inspections', 'real-estate/leads', 'real-estate/deals',
  'hr/employees', 'hr/payroll', 'hr/reviews', 'inventory/stock', 'inventory/warehouses',
];

const arabic = /[؀-ۿ]/;
const browser = await chromium.launch({ executablePath: CHROME, args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
const T = (id) => page.locator(`[data-testid="${id}"]`);
let failures = 0;
const problems = [];
page.on('pageerror', (e) => problems.push('pageerror: ' + e.message));
page.on('response', (r) => { if (r.url().includes('/api/') && r.status() >= 500) problems.push(`${r.status()} ${r.url()}`); });

await page.goto(`${WEB}/auth/login`);
await T('login-email').fill(EMAIL); await T('login-password').fill(PASSWORD); await T('login-submit').click();
await T('workspace-demo-re').click();
await page.waitForURL(`${WEB}/`, { timeout: 15000 });
await T('lang-toggle').click();
await page.waitForTimeout(600);

for (const path of PAGES) {
  problems.length = 0;
  await page.goto(`${WEB}/${path}`);
  await page.waitForTimeout(1500);
  const main = await page.locator('nz-content').innerText().catch(() => '');
  // leftover English words in headings/buttons are reported (not fatal) to guide the next dictionary pass
  const english = main.split('\n').map((l) => l.trim()).filter((l) => /^[A-Z][A-Za-z ]{3,40}$/.test(l)).slice(0, 4);
  const ok = arabic.test(main) && problems.length === 0;
  console.log(`${ok ? 'ok  ' : 'FAIL'} /${path}${english.length ? '   English left: ' + english.join(' | ') : ''}${problems.length ? '   ' + problems.join('; ') : ''}`);
  if (!ok) failures++;
  if (path === '' || path === 'real-estate/leases') await page.screenshot({ path: `${out}/i18n-${path.replace(/\W+/g, '-') || 'home'}.png` });
}
await browser.close();
process.exit(failures ? 1 : 0);
