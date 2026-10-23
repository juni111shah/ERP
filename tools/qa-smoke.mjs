#!/usr/bin/env node
// Browser QA over the screens added after the original admin/finance work. Drives the real UI with Playwright and reports
// console errors, page errors and HTTP 4xx/5xx. Usage:
//   WEB=http://localhost:4291 API=http://localhost:5091/api/v1 PW=<dir with playwright-core> node tools/qa-smoke.mjs
// Expects a seeded stack (node tools/seed-demo.mjs) and the API logging at Debug (the portal sign-in code is read from API_LOG).
import { createRequire } from 'module';
import crypto from 'crypto';
import fs from 'fs';

const WEB = process.env.WEB ?? 'http://localhost:4291';
const API = process.env.API ?? 'http://localhost:5091/api/v1';
const PW = process.env.PW ?? '/tmp/claude-0/-home-user-ERP/df25a78e-4c45-5605-87b7-c5a2c4848b71/scratchpad/pw';
const API_LOG = process.env.API_LOG ?? '/tmp/claude-0/-home-user-ERP/df25a78e-4c45-5605-87b7-c5a2c4848b71/scratchpad/qa/api.log';
const SHOTS = process.env.SHOTS ?? '/tmp/claude-0/-home-user-ERP/df25a78e-4c45-5605-87b7-c5a2c4848b71/scratchpad/qa/';
const CHROME = process.env.CHROME ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const { chromium } = createRequire(PW + '/')('playwright-core');

const u = (b) => b.data ?? b;
const api = async (method, path, body, token) => {
  const r = await fetch(API + path, { method, headers: { 'content-type': 'application/json', 'x-erp-client': 'web', ...(token ? { authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const t = await r.text();
  return t ? u(JSON.parse(t)) : null;
};
const sel = await api('POST', '/auth/login', { email: 'owner@demo.local', password: 'DevOnly#2026' });
const ws = sel.workspaces.find((w) => w.code === 'demo-re');
const session = await api('POST', '/auth/select-workspace', { selectionToken: sel.selectionToken, workspaceId: ws.id });
const token = session.accessToken;

const browser = await chromium.launch({ executablePath: CHROME, args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
page.setDefaultTimeout(10000);
let problems = [];
page.on('pageerror', (e) => problems.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error' && !/favicon|401|Failed to load resource/.test(m.text())) problems.push('console: ' + m.text().slice(0, 200)); });
page.on('response', (r) => { if (r.url().includes('/api/') && r.status() >= 400 && !(r.status() === 401 && r.url().includes('/auth/refresh'))) problems.push(`${r.status()} ${r.request().method()} ${r.url().replace(WEB, '')}`); });

let failed = 0;
const T = (id) => page.locator(`[data-testid="${id}"]`);
const ONLY = process.env.ONLY ? new RegExp(process.env.ONLY, 'i') : null;
const step = async (name, fn, expectProblems = []) => {
  if (ONLY && !ONLY.test(name) && name !== 'login') return;
  problems = [];
  try { await fn(); } catch (e) { failed++; console.log('FAIL ' + name + ': ' + e.message.split('\n')[0]); await page.screenshot({ path: SHOTS + 'qa-fail-' + name.replace(/\W+/g, '-') + '.png' }).catch(() => {}); return; }
  const real = problems.filter((p) => !expectProblems.some((x) => p.includes(x)));
  if (real.length) { failed++; console.log('FAIL ' + name + ' (problems): ' + real.slice(0, 4).join(' | ')); await page.screenshot({ path: SHOTS + 'qa-prob-' + name.replace(/\W+/g, '-') + '.png' }).catch(() => {}); }
  else console.log('ok   ' + name);
};
const go = async (path) => { await page.goto(WEB + path); await page.waitForLoadState('networkidle').catch(() => {}); };
async function pick(testid, text) {
  await T(testid).click();
  const o = text ? page.locator('.ant-select-item-option', { hasText: text }).first() : page.locator('.ant-select-item-option').first();
  await o.waitFor({ timeout: 8000 }); await o.click();
}
const fillNum = async (testid, v) => { const i = T(testid).locator('input'); await i.fill(String(v)); await i.blur(); };
const fillDate = async (testid, v) => { const i = T(testid).locator('input'); await i.fill(v); await i.press('Enter'); };
const toast = (text) => page.locator('.ant-message', { hasText: text }).first().waitFor({ timeout: 8000 });
const row = (text) => page.locator('tr', { hasText: text }).first();
const CYCLE = 'QA cycle ' + Date.now() % 100000;
const iso = (d) => d.toISOString().slice(0, 10);
const addDays = (n) => iso(new Date(Date.now() + n * 864e5));

// ---- login ---------------------------------------------------------------------------------------------------------------
await step('login', async () => {
  await go('/auth/login');
  await T('login-email').fill('owner@demo.local'); await T('login-password').fill('DevOnly#2026'); await T('login-submit').click();
  await T('workspace-demo-re').click(); await page.waitForURL(WEB + '/');
}, ['/auth/refresh']);

// ---- inventory -----------------------------------------------------------------------------------------------------------
await step('inventory: stock table, total and low-stock', async () => {
  await go('/inventory/stock');
  await T('stock-table').locator('tbody tr').first().waitFor();
  await T('stock-total').waitFor();
  await T('low-stock').waitFor();
});
await step('inventory: receive stock', async () => {
  await go('/inventory/stock');
  await T('stock-receive').click();
  await pick('f-warehouseId'); await pick('f-itemId');
  await fillNum('f-quantity', 10); await fillNum('f-unitCost', 60);
  await pick('f-offsetAccountId', '2000');
  await T('stock-form-save').click(); await toast('Saved');
});
await step('inventory: issue stock', async () => {
  await T('stock-issue').click();
  await pick('f-warehouseId'); await pick('f-itemId'); await fillNum('f-quantity', 2);
  await T('stock-form-save').click(); await toast('Saved');
});
await step('inventory: adjust stock', async () => {
  await T('stock-adjust').click();
  await pick('f-warehouseId'); await pick('f-itemId'); await fillNum('f-quantityDelta', -1);
  await T('stock-form-save').click(); await toast('Saved');
});
await step('inventory: transfer stock', async () => {
  await T('stock-transfer').click();
  await pick('f-fromWarehouseId', 'MAIN'); await pick('f-toWarehouseId', 'SITE'); await pick('f-itemId'); await fillNum('f-quantity', 3);
  await T('stock-form-save').click(); await toast('Saved');
});
await step('inventory: reorder level', async () => {
  await T('stock-policy').click();
  await pick('f-itemId'); await fillNum('f-reorderLevel', 5);
  await T('stock-form-save').click(); await toast('Saved');
});
await step('inventory: movements and warehouses', async () => {
  await go('/inventory/movements'); await page.locator('tbody tr').first().waitFor();
  await go('/inventory/warehouses'); await page.locator('td', { hasText: 'MAIN' }).first().waitFor();
  await T('wh-new').click(); await pick('f-legalEntityId');
  await T('f-code').fill('QA' + Math.floor(Math.random() * 900 + 100)); await T('f-name').fill('QA store');
  await T('wh-form-save').click(); await toast('Warehouse saved');
});

// ---- CRM -----------------------------------------------------------------------------------------------------------------
await step('crm: listings', async () => {
  await go('/real-estate/listings');
  await page.locator('tbody tr').first().waitFor();
  await T('listing-new').click();
  await pick('f-propertyId'); await T('f-title').fill('QA listing'); await fillNum('f-price', 99000);
  await T('listing-form-save').click(); await toast('saved');
  await row('QA listing').click();
  await T('listing-publish').click(); await toast('Listing');
});
await step('crm: leads create, move, convert', async () => {
  await go('/real-estate/leads');
  await T('lead-pipeline').waitFor();
  await T('lead-new').click();
  await pick('f-legalEntityId').catch(() => {});
  await T('f-name').fill('QA Lead'); await T('f-phone').fill('+971500000999');
  await T('lead-form-save').click(); await toast('saved');
  await row('QA Lead').click();
  await T('lead-next').click(); await toast('Lead');
  await row('QA Lead').click();
  await T('lead-next').click(); await toast('Lead');
  await row('QA Lead').click();
  await T('lead-next').click(); await toast('Lead');
  await row('QA Lead').click();
  await T('lead-convert').click(); await toast('Lead');
});

// ---- HR ------------------------------------------------------------------------------------------------------------------
await step('hr: review cycles and reviews', async () => {
  await go('/hr/review-cycles');
  await T('cycle-new').click();
  await T('f-name').fill(CYCLE);
  await fillDate('f-periodStart', `${new Date().getFullYear()}-07-01`); await fillDate('f-periodEnd', `${new Date().getFullYear()}-12-31`);
  await T('cycle-form-save').click(); await toast('saved');
  await go('/hr/reviews');
  await page.locator('tbody tr').first().waitFor();
  await T('review-new').click();
  await pick('review-employee'); await pick('review-cycle', CYCLE);
  await T('review-goals').fill('QA goals');
  await T('review-save').click(); await toast('Review');
});
await step('hr: payroll WPS button', async () => {
  await go('/hr/payroll');
  await page.locator('tbody tr').first().click();
  await T('payroll-title').waitFor();
  if (await T('payroll-post').isVisible().catch(() => false)) {
    await T('payroll-post').click();
    await page.locator('.ant-modal-confirm-btns .ant-btn-primary').click(); // "Post this payroll?" confirmation
    await page.waitForTimeout(1500);
  }
  await T('payroll-wps').waitFor({ timeout: 8000 });
  const [resp] = await Promise.all([page.waitForResponse((r) => r.url().includes('/wps-sif')), T('payroll-wps').click()]);
  // 200 (file) or 409 listing what is missing are both valid outcomes of a working button
  if (![200, 409].includes(resp.status())) throw new Error('wps-sif returned ' + resp.status());
}, ['409', 'wps-sif']);

// ---- inspections, renewals, preventive maintenance -----------------------------------------------------------------------
await step('inspections: create with checklist and complete', async () => {
  await go('/real-estate/inspections');
  await T('insp-new').click();
  await pick('insp-property');
  await T('insp-inspector').fill('QA');
  await T('insp-area-0').fill('Kitchen');
  await T('insp-save').click(); await toast('Inspection saved');
  await T('insp-complete').click(); await toast('Inspection completed');
});
await step('renewals: create drafts', async () => {
  await go('/real-estate/renewals');
  await T('renewal-table').locator('tbody tr').first().waitFor();
  await page.locator('[data-testid="renewal-table"] tbody tr label[nz-checkbox]:not(.ant-checkbox-wrapper-disabled)').first().click();
  await T('renewal-create').click(); await toast('draft renewal');
});
await step('preventive maintenance: new and run now', async () => {
  await go('/real-estate/preventive-maintenance');
  await T('pm-new').click();
  await pick('f-propertyId'); await T('f-title').fill('QA fire drill');
  await fillDate('f-nextDue', addDays(2));
  await T('pm-form-save').click(); await toast('saved');
  await T('pm-run').click(); await toast('work order');
});

// ---- budgets -------------------------------------------------------------------------------------------------------------
await step('budgets: grid save and vs-actual', async () => {
  await go('/finance/budgets');
  await T('budget-new').click();
  await T('budget-name').fill('QA budget ' + Date.now() % 10000);
  await pick('budget-add-account');
  const cell = page.locator('[data-testid="cell-0-0"] input'); await cell.fill('1000'); await cell.blur();
  await T('budget-save').click(); await toast('Budget saved');
  await page.getByRole('tab', { name: 'Budget vs actual' }).click();
  await T('budget-actual-table').waitFor();
});

// ---- document vault + attachments, import -----------------------------------------------------------------------------------
await step('documents: upload on a lease and see it in the vault', async () => {
  await go('/real-estate/leases');
  await page.locator('tbody tr').first().click();
  await T('lease-title').waitFor();
  fs.writeFileSync(SHOTS + 'qa-doc.txt', 'qa document');
  await T('attachment-file').setInputFiles(SHOTS + 'qa-doc.txt');
  await page.locator('input[placeholder^="Category"]').fill('QA licence');
  await T('attachment-upload').click(); await toast('Document uploaded');
  await T('attachments').locator('td', { hasText: 'qa-doc.txt' }).first().waitFor();
  await go('/admin/documents');
  await page.locator('td', { hasText: 'qa-doc.txt' }).first().waitFor();
});
await step('import: check then import parties', async () => {
  await go('/admin/import');
  await pick('import-type', 'Parties');
  const tag = Date.now() % 100000;
  fs.writeFileSync(SHOTS + 'qa-import.csv', `name,kind,roles\nQA Import ${tag},Company,Customer\n,Company,Customer\n`);
  await T('import-file').setInputFiles(SHOTS + 'qa-import.csv');
  await T('import-check').click();
  await T('import-errors').waitFor();
  await T('import-run').click(); await toast('imported');
});

// ---- account security (2FA) --------------------------------------------------------------------------------------------------
function totp(base32) {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  const bits = base32.replace(/\s/g, '').toUpperCase().replace(/=+$/, '').split('').map((c) => alphabet.indexOf(c).toString(2).padStart(5, '0')).join('');
  const key = Buffer.from(bits.match(/.{8}/g).map((b) => parseInt(b, 2)));
  const msg = Buffer.alloc(8); msg.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30000)));
  const h = crypto.createHmac('sha1', key).update(msg).digest(); const o = h[h.length - 1] & 15;
  return String(((h.readUInt32BE(o) & 0x7fffffff) % 1000000)).padStart(6, '0');
}
await step('account security: enable 2FA and see recovery codes', async () => {
  // use a throwaway admin user so the main QA login keeps working
  const adm = await api('POST', '/auth/login', { email: 'admin@demo.local', password: 'DevOnly#2026' });
  await go('/auth/login');
  await page.evaluate(() => { try { sessionStorage.clear(); } catch { /* ignore */ } });
  await T('user-menu').click().catch(() => {});
  await go('/account/security');
  await T('tf-begin').click();
  const secret = (await T('tf-secret').innerText()).trim();
  await T('tf-code').fill(totp(secret)); await T('tf-enable').click();
  await T('tf-recovery').waitFor({ timeout: 8000 });
  await T('tf-on').waitFor();
  // switch it off again so the seeded account stays usable
  await T('tf-password').fill('DevOnly#2026'); await T('tf-code').fill(totp(secret)); await T('tf-disable').click();
  await toast('off');
  void adm;
});

// ---- notification drawer, global search ----------------------------------------------------------------------------------------
await step('notifications drawer and global search', async () => {
  await go('/');
  await T('notifications-bell').click();
  await page.locator('.ant-drawer-title', { hasText: /Notifications|الإشعارات/ }).waitFor();
  await T('mark-all-read').click();
  await page.keyboard.press('Escape');
  await T('global-search-toggle').click(); // the header search is icon-only until clicked
  await T('global-search').click();
  await T('global-search').pressSequentially('Marina', { delay: 50 }); // type like a user; fill() does not exercise focus/blur the same way
  await T('search-hit').first().waitFor({ timeout: 8000 });
});

// ---- vendor portal -------------------------------------------------------------------------------------------------------------
await step('vendor portal: sign in with code, bills, statement, work orders', async () => {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const v = await ctx.newPage();
  v.on('pageerror', (e) => problems.push('vendor pageerror: ' + e.message));
  v.on('response', (r) => { if (r.url().includes('/api/') && r.status() >= 400) problems.push(`vendor ${r.status()} ${r.url().replace(WEB, '')}`); });
  const vt = (id) => v.locator(`[data-testid="${id}"]`);
  // give a vendor a work order so the portal has something to update
  const vendorEmail = 'coolair@vendor.example.com';
  const props = await api('GET', '/m/realestate/properties?pageSize=1', null, token);
  const propId = (props.items ?? props)[0].id;
  const vendors = await api('GET', '/parties?search=CoolAir', null, token);
  const vendorId = (vendors.items ?? vendors)[0].id;
  const wo = await api('POST', '/m/realestate/work-orders', { propertyId: propId, issue: 'QA vendor job', priority: 'normal', vendorPartyId: vendorId, estimatedCost: 100 }, token);
  await api('POST', `/m/realestate/work-orders/${wo.id}/status`, { status: 'assigned' }, token);
  await v.goto(WEB + '/portal/login');
  await vt('portal-workspace').fill('demo-re'); await vt('portal-vendor').click(); await vt('portal-email').fill(vendorEmail); await vt('portal-submit').click();
  await vt('portal-code').waitFor({ timeout: 8000 });
  await new Promise((r) => setTimeout(r, 600));
  const log = fs.readFileSync(API_LOG, 'utf8');
  const m = [...log.matchAll(/sign-in code is (\d{6})/g)].pop();
  if (!m) throw new Error('sign-in code not in API log');
  await vt('portal-code').fill(m[1]); await vt('portal-submit').click();
  await v.waitForURL(/\/portal/, { timeout: 8000 });
  await vt('vendor-bills').waitFor({ timeout: 8000 });
  await v.getByRole('tab', { name: /Payments/ }).click(); await vt('vendor-payments').waitFor();
  await v.getByRole('tab', { name: /Statement/ }).click(); await vt('vendor-balance').waitFor();
  await v.getByRole('tab', { name: /Work orders/ }).click(); await vt('vendor-orders').locator('tbody tr', { hasText: 'QA vendor job' }).waitFor({ timeout: 8000 });
  await ctx.close();
}, ['401']);

console.log(failed ? `\n${failed} step(s) failed` : '\nall steps passed');
await browser.close();
process.exit(failed ? 1 : 0);
