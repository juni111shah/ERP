#!/usr/bin/env node
// Browser check of the shared stat card: visits the pages that show stat cards in English and Arabic, checks that a loading skeleton
// is replaced by real values without a manual refresh, that the page has no errors, and (Arabic) that the layout is RTL and numbers are bdi-isolated.
//
//   Real stack (needs the API + SQL Server and `node tools/seed-demo.mjs`):
//     WEB=http://localhost:4200 PW=<dir with playwright-core> node tools/smoke-stat-cards.mjs
//   No API (MOCK=1): every /api/v1 call is answered in the browser with fixtures, so only `ng serve` is needed:
//     MOCK=1 WEB=http://localhost:4292 PW=<dir with playwright-core> node tools/smoke-stat-cards.mjs
// Other env: CHROME (chromium path), SHOTS (screenshot folder), SLOW=ms (delay mocked list/summary answers to see the skeletons).
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';

const WEB = process.env.WEB ?? 'http://localhost:4200';
const MOCK = process.env.MOCK === '1';
const SLOW = Number(process.env.SLOW ?? 700);
const SHOTS = process.env.SHOTS ?? path.join(process.cwd(), 'stat-card-shots');
const PW = process.env.PW ?? process.cwd();
const CHROME = process.env.CHROME ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const { chromium } = createRequire(path.join(PW, 'package.json'))('playwright-core');
fs.mkdirSync(SHOTS, { recursive: true });

// [name, url, testid of the container holding the cards, minimum number of cards]
const PAGES = [
  ['overview', '/', 'kpi-grid', 4],
  ['invoices', '/finance/invoices', 'invoice-stats', 4],
  ['cheques', '/finance/cheques', 'cheque-kpis', 6],
  ['acquisitions', '/real-estate/acquisitions', 'acq-kpis', 4],
  ['renewals', '/real-estate/renewals', 'renewal-kpis', 3],
  ['inventory-stock', '/inventory/stock', 'stock-kpis', 3],
];

const ok = (data, meta) => ({ success: true, statusCode: 200, data, ...(meta ? { meta } : {}) });
const page1 = (n) => ({ page: 1, pageSize: 20, total: n, totalPages: 1, hasNext: false, hasPrevious: false });
const entity = { id: 'e1', code: 'DEMO', name: 'Demo Properties LLC', baseCurrency: 'AED' };
const workspace = { id: 'w1', code: 'demo-re', name: 'Demo Properties', industry: 'Real estate', industryCode: 'realestate' };
const me = {
  user: { id: 'u1', email: 'owner@demo.local', fullName: 'Workspace Owner', mustChangePassword: false },
  workspace: { ...workspace, countryCode: 'AE', baseCurrency: 'AED', timeZone: 'Asia/Dubai', defaultLocale: 'en' },
  workspaces: [workspace], permissions: ['*'], legalEntities: [entity], allLegalEntities: true,
  enabledModules: [{ key: 'realestate', uiRoute: 'real-estate' }, { key: 'inventory', uiRoute: 'inventory' }, { key: 'hr', uiRoute: 'hr' }],
};
const invoices = Array.from({ length: 6 }, (_, i) => ({ id: 'i' + i, number: `INV-${i + 1}`, isCreditNote: false, partyId: 'p', partyName: 'Tenant ' + i, invoiceDate: '2026-09-01', dueDate: i < 2 ? '2026-09-15' : '2026-12-01', total: 1000 * (i + 1), paid: i === 0 ? 1000 : 0, outstanding: i === 0 ? 0 : 1000 * (i + 1), status: i === 5 ? 'draft' : i === 0 ? 'paid' : 'issued', currencyCode: 'AED' }));
const dashboard = {
  asOf: '2026-10-23',
  kpis: [
    { key: 'revenue', label: 'Revenue', value: 2_450_000, format: 'money', caption: 'this month', order: 1, link: '/finance/invoices' },
    { key: 'occupancy', label: 'Occupancy', value: 87.5, format: 'percent', caption: '21 units', order: 2 },
    { key: 'receivables', label: 'Receivables', value: 123456, format: 'money', order: 3 },
    { key: 'cash', label: 'Cash on hand', value: 98765, format: 'money', order: 4 },
    ...Array.from({ length: 6 }, (_, i) => ({ key: 'extra' + i, label: 'Metric ' + i, value: 10 + i, format: 'number', order: 10 + i })),
  ],
  charts: [{ key: 'occupancy', title: 'Units by status', kind: 'bar', categories: ['Vacant', 'Occupied', 'Reserved'], series: [{ name: 'Units', values: [4, 17, 2] }], order: 1 }],
  alerts: [{ severity: 'info', category: 'Leases', urgency: 'upcoming', message: '3 leases end within 60 days', rank: 1 }],
};

function route(url, method) {
  const u = new URL(url);
  const p = u.pathname.replace('/api/v1', '');
  if (p === '/auth/refresh') return ok({ accessToken: 'mock', expiresIn: 3600, workspace, workspaces: [workspace], persistent: true });
  if (p === '/me') return ok(me);
  if (p === '/navigation/menu') return ok([]);
  if (p === '/notifications') return ok({ unread: 0, items: [] });
  if (p === '/dashboard') return { slow: true, body: ok(dashboard) };
  if (p === '/finance/invoices') return { slow: true, body: ok(invoices, page1(invoices.length)) };
  if (p === '/finance/cheques/summary') return { slow: true, body: ok({ inCustodyTotal: 120000, inCustodyCount: 5, maturedTotal: 30000, maturedCount: 2, next7DaysTotal: 45000, next30DaysTotal: 90000, depositedTotal: 15000, bouncedCount: 1 }) };
  if (p === '/m/realestate/acquisitions/overview') return { slow: true, body: ok({ consideration: 12_000_000, count: 1, downPayments: 2_400_000, financing: 7_200_000, outstanding: 7_000_000, unfunded: 2_400_000 }) };
  if (p === '/m/realestate/renewals') return { slow: true, body: ok({ asOf: '2026-10-23', withinDays: 120, rows: [], due: 3, draft: 1, renewed: 2 }) };
  if (p === '/m/inventory/stock') return { slow: true, body: ok({ rows: [{ warehouseId: 'w', warehouse: 'Main', itemId: 'i', sku: 'PRD-FILTER', item: 'AC filter', uom: 'pcs', quantity: 28, averageCost: 52, value: 1456 }], totalValue: 1456 }) };
  if (p === '/m/inventory/stock/low' || p === '/m/realestate/acquisitions/funding') return ok([]);
  if (/^\/(finance|m\/realestate|m\/inventory|legal-entities|parties|items)/.test(p) && method === 'GET') return ok([], page1(0));
  return ok(null);
}

const browser = await chromium.launch({ executablePath: CHROME, args: ['--no-sandbox'] });
const results = [];
const T = (page, id) => page.locator(`[data-testid="${id}"]`);

async function run(lang) {
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  await ctx.addInitScript((l) => { try { localStorage.setItem('erp.ui.prefs', JSON.stringify({ lang: l })); } catch { /* ignore */ } }, lang);
  const page = await ctx.newPage();
  const problems = [];
  page.on('pageerror', (e) => problems.push('pageerror: ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error' && !/favicon|Failed to load resource/.test(m.text())) problems.push('console: ' + m.text().slice(0, 160)); });
  page.on('response', (r) => { if (r.url().includes('/api/') && r.status() >= 500) problems.push(r.status() + ' ' + r.url()); });

  if (MOCK) {
    await page.route('**/api/v1/**', async (r) => {
      const hit = route(r.request().url(), r.request().method());
      const slow = hit && hit.slow;
      if (slow) await new Promise((res) => setTimeout(res, SLOW));
      await r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(slow ? hit.body : hit) });
    });
    await page.goto(`${WEB}/`); // the silent refresh restores the mocked session
  } else {
    await page.goto(`${WEB}/auth/login`);
    await T(page, 'login-email').fill('owner@demo.local');
    await T(page, 'login-password').fill('DevOnly#2026');
    await T(page, 'login-submit').click();
    await T(page, 'workspace-demo-re').click();
    await page.waitForURL(`${WEB}/`);
  }

  for (const [name, url, testId, min] of PAGES) {
    const label = `${lang}:${name}`;
    try {
      problems.length = 0;
      await page.goto(WEB + url);
      const box = T(page, testId);
      await box.waitFor({ timeout: 15000 });
      // first paint: either skeletons (loading) or already-final values — never an all-zero card
      const early = await page.locator(`[data-testid="${testId}"] .erp-stat-value`).allInnerTexts();
      if (early.some((v) => /^\s*0(\.0+)?\s*$/.test(v))) throw new Error('a card flashed a zero before its data arrived: ' + early.join(','));
      // the skeletons disappear on their own (no manual refresh) and real values show
      await page.waitForFunction((id) => !document.querySelector(`[data-testid="${id}"] [data-testid="stat-skeleton"]`) && !document.querySelector('[data-testid="dashboard-loading"]'), testId, { timeout: 20000 });
      const count = await page.locator(`[data-testid="${testId}"] .erp-stat-card`).count();
      if (count < min) throw new Error(`expected at least ${min} stat cards, found ${count}`);
      const values = await page.locator(`[data-testid="${testId}"] .erp-stat-value`).allInnerTexts();
      if (values.length && values.every((v) => v.trim() === '' || v.trim() === '—')) throw new Error('cards show no values');
      if (name === 'overview') {
        // charts render once the data has arrived (no blank panel)
        await page.locator('erp-simple-chart svg').first().waitFor({ timeout: 10000 });
      }
      if (lang === 'ar') {
        const dir = await page.evaluate(() => document.documentElement.dir);
        if (dir !== 'rtl') throw new Error('dir is not rtl');
        if ((await page.locator(`[data-testid="${testId}"] .erp-stat-value bdi`).count()) === 0) throw new Error('numbers are not bdi-isolated');
        const stripe = await page.locator(`[data-testid="${testId}"] .erp-stat-stripe`).first().evaluate((el) => { const r = el.getBoundingClientRect(); const p = el.parentElement.getBoundingClientRect(); return r.left - p.left > (p.right - r.right); }).catch(() => null);
        if (stripe === false) throw new Error('stripe is on the left in RTL (should mirror to the right)');
      }
      if (problems.length) throw new Error(problems.join('; '));
      await page.screenshot({ path: path.join(SHOTS, `${label.replace(':', '-')}.png`) });
      results.push([label, 'ok']);
    } catch (e) {
      await page.screenshot({ path: path.join(SHOTS, `FAIL-${label.replace(':', '-')}.png`) }).catch(() => {});
      results.push([label, 'FAIL ' + e.message.split('\n')[0]]);
    }
  }
  await ctx.close();
}

await run('en');
await run('ar');
await browser.close();
for (const [n, r] of results) console.log((r === 'ok' ? 'ok   ' : 'FAIL ') + n + (r === 'ok' ? '' : ' - ' + r));
process.exit(results.some(([, r]) => r !== 'ok') ? 1 : 0);
