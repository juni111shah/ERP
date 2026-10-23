#!/usr/bin/env node
// Browser smoke for: the record History tab, the AR ageing filters and the expanding header search.
// The API is MOCKED with Playwright routes, so it needs only `ng serve` (no SQL Server).
//   WEB=http://localhost:4293 PW=<dir with playwright-core> CHROME=<chromium path> node tools/smoke-history.mjs [outdir]
import { createRequire } from 'module';
const WEB = process.env.WEB ?? 'http://localhost:4200';
const PW = process.env.PW ?? '/tmp/claude-0/-home-user-ERP/df25a78e-4c45-5605-87b7-c5a2c4848b71/scratchpad/pw';
const CHROME = process.env.CHROME ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const out = process.argv[2] ?? '.';
const { chromium } = createRequire(PW + '/package.json')('playwright-core');

const ws = { id: 'w1', code: 'demo', name: 'Demo' };
const me = {
  user: { id: 'u1', email: 'a@demo.local', fullName: 'Admin', mustChangePassword: false },
  workspace: { ...ws, countryCode: 'AE', baseCurrency: 'AED', timeZone: 'Asia/Dubai', defaultLocale: 'en' }, workspaces: [ws],
  permissions: ['core.audit.read', 'finance.invoices.read', 'finance.reports.read', 'core.parties.read'], legalEntities: [{ id: 'e1', code: 'E1', name: 'Entity One', baseCurrency: 'AED' }], allLegalEntities: true,
  enabledModules: [],
};
const invoice = {
  id: 'inv1', number: 'INV-1', isCreditNote: false, legalEntityId: 'e1', partyId: 'p1', partyName: 'Acme', invoiceDate: '2026-06-01', dueDate: '2026-07-01', currencyCode: 'AED', net: 100, tax: 5, total: 105, paid: 0,
  status: 'issued', journalEntryId: null, lines: [{ id: 'l1', seq: 1, description: 'Work', quantity: 1, price: 100, taxAmount: 5, net: 100 }],
};
const history = [
  { id: 'a2', occurredAt: '2026-06-02T10:00:00Z', userId: 'u1', userName: 'Admin', action: 'Updated', changes: [{ field: 'DueDate', old: '2026-07-01', new: '2026-07-15' }, { field: 'Iban', old: '*******************3456', new: '*******************9999' }] },
  { id: 'a1', occurredAt: '2026-06-01T09:00:00Z', userId: 'u1', userName: 'Admin', action: 'Created', changes: [{ field: 'Number', old: null, new: 'INV-1' }] },
];
let ageingQuery = '';
const ok = (data, meta) => JSON.stringify({ success: true, statusCode: 200, data, ...(meta ? { meta } : {}) });

const browser = await chromium.launch({ executablePath: CHROME, args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1360, height: 900 } });
const problems = [];
page.on('pageerror', (e) => problems.push('pageerror: ' + e.message));
await page.route('**/api/v1/**', async (route) => {
  const url = new URL(route.request().url());
  const path = url.pathname.replace('/api/v1', '');
  const json = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body });
  if (path === '/auth/refresh') return json(ok({ accessToken: 't', expiresIn: 300, workspace: ws, workspaces: [ws], persistent: false }));
  if (path === '/me') return json(ok(me));
  if (path === '/navigation/menu') return json(ok([]));
  if (path === '/notifications') return json(ok({ unread: 0, items: [] }));
  if (path === '/search') return json(ok({ hits: url.searchParams.get('q') === 'Acme' ? [{ category: 'Parties', title: 'Acme', subtitle: 'customer', link: '/parties' }] : [] }));
  if (path === '/finance/invoices/inv1') return json(ok(invoice));
  if (path === '/audit/record') return json(ok(history, { total: 2, page: 1, pageSize: 20, totalPages: 1 }));
  if (path === '/legal-entities/e1') return json(ok({ id: 'e1', code: 'E1', name: 'Entity One', baseCurrency: 'AED', isTaxRegistered: true, taxRegistrationNo: '100123456700003' }));
  if (path === '/legal-entities') return json(ok([{ id: 'e1', code: 'E1', name: 'Entity One' }], { total: 1, page: 1, pageSize: 100, totalPages: 1 }));
  if (path === '/business-units') return json(ok([{ id: 'b1', legalEntityId: 'e1', code: 'BR', name: 'Retail branch', isActive: true }], { total: 1, page: 1, pageSize: 200, totalPages: 1 }));
  if (path === '/finance/reports/ar-ageing') {
    ageingQuery = url.search;
    const row = { partyId: 'b1', partyName: 'Retail branch', current: 0, days1To30: 300, days31To60: 0, days61To90: 0, over90: 0, total: 300 };
    return json(ok({ asOf: '2026-08-01', rows: [row], totals: { ...row, partyName: 'Total' } }));
  }
  if (route.request().method() === 'GET') return json(ok([]));
  return json(ok({}));
});

let failed = 0;
const step = async (name, fn) => { try { await fn(); console.log('ok   ' + name); } catch (e) { failed++; console.log('FAIL ' + name + ': ' + e.message.split('\n')[0]); await page.screenshot({ path: `${out}/fail-${name.replace(/\W+/g, '-')}.png` }); } };
const T = (id) => page.locator(`[data-testid="${id}"]`);

await step('invoice History tab lists entries newest first with old to new', async () => {
  await page.goto(WEB + '/finance/invoices/inv1');
  await page.getByRole('tab', { name: 'History' }).click();
  await T('history-entry').first().waitFor({ timeout: 8000 });
  const text = await page.locator('erp-record-history').innerText();
  if (!/Updated[\s\S]*Created/.test(text)) throw new Error('entries not newest first: ' + text.slice(0, 120));
  if (!text.includes('2026-07-01') || !text.includes('2026-07-15')) throw new Error('old → new missing');
  if (text.includes('AE07033123456789012')) throw new Error('unmasked value shown');
  await page.screenshot({ path: `${out}/history.png` });
});

await step('History tab is hidden without core.audit.read', async () => {
  me.permissions = me.permissions.filter((p) => p !== 'core.audit.read');
  await page.goto(WEB + '/finance/invoices/inv1');
  await page.getByRole('tab', { name: 'Documents' }).waitFor({ timeout: 8000 });
  if (await page.getByRole('tab', { name: 'History' }).count()) throw new Error('History tab visible');
  me.permissions.push('core.audit.read');
});

await step('AR ageing: group by business unit and filters are sent', async () => {
  await page.goto(WEB + '/finance/reports');
  await page.getByRole('tab', { name: 'Receivables ageing' }).click();
  await T('rep-groupby').click();
  await page.locator('.ant-select-item-option', { hasText: 'Group by business unit' }).click();
  await T('rep-bu').click();
  await page.locator('.ant-select-item-option', { hasText: 'Retail branch' }).click();
  await T('rep-source').fill('module.demo');
  await T('rep-run').click();
  await T('ar-table').waitFor({ timeout: 8000 });
  if (!(await T('ar-table').innerText()).includes('Retail branch')) throw new Error('grouped row missing');
  for (const part of ['groupBy=businessUnit', 'businessUnitId=b1', 'sourceType=module.demo']) if (!ageingQuery.includes(part)) throw new Error(`query lacks ${part}: ${ageingQuery}`);
});

await step('header search is icon-only, expands on click and shows results', async () => {
  await page.goto(WEB + '/');
  const toggle = T('global-search-toggle');
  await toggle.waitFor({ timeout: 8000 });
  const input = T('global-search');
  const host = page.locator('erp-global-search erp-expand-search');
  if ((await host.getAttribute('class'))?.includes('is-open')) throw new Error('search already expanded before click');
  const closedWidth = (await host.boundingBox()).width;
  await toggle.click();
  await page.waitForFunction(() => document.querySelector('erp-global-search erp-expand-search')?.classList.contains('is-open'));
  await page.waitForTimeout(500);
  if ((await host.boundingBox()).width < closedWidth * 3) throw new Error('search did not expand');
  await input.pressSequentially('Acme', { delay: 40 });
  await T('search-hit').first().waitFor({ timeout: 8000 });
});

await step('expanding search mirrors in Arabic (rtl)', async () => {
  await page.evaluate(() => { try { localStorage.setItem('erp.ui.prefs', JSON.stringify({ lang: 'ar', dir: 'rtl' })); } catch { /* ignore */ } });
  await page.goto(WEB + '/');
  await page.waitForTimeout(800);
  const dir = await page.evaluate(() => document.documentElement.dir);
  if (dir !== 'rtl') throw new Error('dir=' + dir);
  await T('global-search-toggle').waitFor({ timeout: 8000 });
});

console.log('problems:', problems);
await browser.close();
process.exit(failed ? 1 : 0);
