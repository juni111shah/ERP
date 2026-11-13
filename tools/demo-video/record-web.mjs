// Records the web walkthrough: pricing page, registration of a new workspace, then every module in the seeded Demo workspace.
// Each scene lasts at least as long as its voice-over (work/voice/durations.json), so picture and voice stay together.
// Usage: node record-web.mjs [baseUrl]   (default http://localhost:5080, the API serving the built web app)
import { readFileSync } from 'node:fs';
import { WebRecorder, sleep } from './lib/recorder.mjs';
import { here } from './lib/media.mjs';

const BASE = process.argv[2] ?? process.env.BASE ?? 'http://localhost:5080';
const script = JSON.parse(readFileSync(here('script.json'), 'utf8'));
const voice = JSON.parse(readFileSync(here('work/voice/durations.json'), 'utf8'));
const VO_LEAD = 0.6, TAIL = 1.0;
const PASSWORD = 'DevOnly#2026';
const runTag = Date.now().toString(36).slice(-5);

const r = new WebRecorder('web');
const page = await r.launch();
const tid = (id) => page.getByTestId(id);

/** Runs one scene: marks it, shows its lower-third, plays the actions, then holds until the voice-over has finished. */
async function scene(id, actions, { label = true } = {}) {
  const s = script.web.scenes.find((x) => x.id === id);
  r.mark(id);
  const t0 = r.now();
  if (label) await r.lowerThird(s.label, 4500);
  try { await actions(); } catch (e) { console.log(`  ! ${id}: ${e.message.split('\n')[0]}`); }
  const need = VO_LEAD + voice[id].seconds + TAIL - (r.now() - t0);
  if (need > 0) await sleep(need * 1000);
}

/** Opens a page inside the app through the sidebar search (as a person would), or by address if that fails. */
async function go(term, path) {
  try {
    const box = tid('sidebar-search-input');
    if (!(await box.isVisible())) throw new Error('no sidebar search');
    await r.type(box, term, { delay: 55 });
    const exact = new RegExp('^\\s*' + term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '\\s*$', 'i');
    const hit = page.locator('[data-testid="sidebar"] li[data-testid^="nav-"]:visible').filter({ hasText: exact }).first();
    await hit.waitFor({ timeout: 3000 });
    await r.click(hit);
    await box.fill('').catch(() => {});
    const want = path.split('?')[0];
    await page.waitForURL((u) => u.pathname.startsWith(want), { timeout: 4000 }).catch(() => page.goto(BASE + path, { waitUntil: 'networkidle' }));
  } catch {
    await page.goto(BASE + path, { waitUntil: 'networkidle' });
  }
  await page.waitForLoadState('networkidle').catch(() => {});
  await sleep(900);
}

/** Opens a searchable drop-down, types to filter it and picks the first match. */
async function pick(testId, text) {
  await r.click(tid(testId)); await page.keyboard.type(text, { delay: 70 }); await sleep(700);
  await r.click(page.locator('.ant-select-item-option').first()); await sleep(500);
}

/** Signs in without on-screen theatrics (used while the video is frozen behind a title card). */
async function quietSignIn(email, wsCode) {
  await page.getByTestId('login-email').fill(email);
  await page.getByTestId('login-password').fill(PASSWORD);
  await page.getByTestId('login-password').press('Enter');
  const ws = page.getByTestId('workspace-' + wsCode);
  await ws.waitFor({ timeout: 10000 }).catch(() => {});
  if (await ws.count()) await ws.click();
  await page.waitForLoadState('networkidle').catch(() => {});
  await sleep(2500);
}

async function signIn(email, wsCode) {
  await r.type(tid('login-email'), email);
  await r.type(tid('login-password'), PASSWORD, { delay: 30 });
  await r.click(page.getByRole('button', { name: /^sign in$/i }).first());
  if (wsCode) { const ws = tid('workspace-' + wsCode); await ws.waitFor({ timeout: 10000 }); await sleep(800); await r.click(ws); }
  await page.waitForLoadState('networkidle').catch(() => {});
  await sleep(1500);
}

// ------------------------------------------------------------------------------------------------------------------------------
await page.goto(BASE + '/pricing', { waitUntil: 'networkidle' });
await sleep(1500);
await r.start();

await scene('w01-intro', async () => {
  await r.card({ kick: 'Client demo', title: 'HigherSquare <span>360</span>', sub: 'One ERP for your whole business' });
  await sleep(5200);
}, { label: false });
await r.uncard();

await scene('w02-pricing', async () => {
  await sleep(1500);
  await r.hover(tid('pricing-hero-scene'), 900); await sleep(1500);
  await r.scroll(950, 1600); await sleep(1200);
  const annual = tid('pricing-cycle-annual');
  if (await annual.count()) { await r.click(annual); await sleep(1200); await r.click(tid('pricing-cycle-monthly')); }
  await sleep(1000);
  await r.scroll(900, 1500); await sleep(1500);
  await r.scroll(1100, 1600); await sleep(1500);
  const mobile = tid('pricing-mobile');
  if (await mobile.count()) { await mobile.scrollIntoViewIfNeeded(); await sleep(2500); }
});

let newEmail = `ayesha.${runTag}@crescent.example.test`;
await scene('w03-register', async () => {
  await page.evaluate(() => scrollTo({ top: 0, behavior: 'smooth' })); await sleep(900);
  await r.click(tid('pricing-top-start'));
  await page.waitForLoadState('networkidle'); await sleep(1500);
  const annual = tid('register-cycle-annual');
  if (await annual.count()) { await r.click(annual); await sleep(900); await r.click(tid('register-cycle-monthly')); await sleep(500); }
  await r.hover(tid('plan-card-business'), 700); await sleep(900);
  await r.click(tid('plan-choose-business'));
  await sleep(1000);
  await r.type(tid('register-business'), 'Crescent Trading Co');
  await sleep(1200);   // the workspace code fills itself in and is checked
  await r.spotlight(tid('register-code'), 1300);
  await pick('register-industry', 'Wholesale');
  await pick('register-country', 'Pakistan');
  if (!(await tid('register-currency').innerText()).match(/[A-Z]{3}/)) await pick('register-currency', 'PKR');
  await r.type(tid('register-name'), 'Ayesha Khan');
  await r.type(tid('register-email'), newEmail, { delay: 22 });
  await r.type(tid('register-phone'), '+92 300 1234567', { delay: 30 });
  await r.type(tid('register-password'), 'Crescent#2026demo', { delay: 25 });
  await r.click(tid('register-terms'));
  await sleep(500);
  await r.click(tid('register-submit'));
  await sleep(3500);
});

await scene('w04-first-login', async () => {
  const signin = tid('register-result-signin');
  if (await signin.count()) { await r.click(signin); await page.waitForLoadState('networkidle'); }
  else await page.goto(BASE + '/auth/login', { waitUntil: 'networkidle' });
  await sleep(800);
  await r.type(tid('login-email'), newEmail, { delay: 22 });
  await r.type(tid('login-password'), 'Crescent#2026demo', { delay: 25 });
  await r.click(page.getByRole('button', { name: /^sign in$/i }).first());
  await page.waitForLoadState('networkidle').catch(() => {}); await sleep(3500);
});

await scene('w05-bridge', async () => {
  await r.card({ kick: 'A few months later', title: 'A workspace in <span>full swing</span>', sub: 'Real data, posted through the real workflows' });
  await sleep(1600);
  // Behind the (frozen) card: sign out and into the Demo workspace.
  r.freeze();
  await page.context().clearCookies();
  await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
  await page.goto(BASE + '/auth/login', { waitUntil: 'networkidle' });
  await quietSignIn('admin@demo.local', 'demo');
  await r.card({ kick: 'A few months later', title: 'A workspace in <span>full swing</span>', sub: 'Real data, posted through the real workflows' });
  await r.unfreeze(1300);
}, { label: false });
await r.uncard();

/** Glides over the first few rows of the main table, so the viewer's eye follows real data. */
async function skimRows(n = 3) {
  const rows = page.locator('tbody tr:visible');
  const count = Math.min(n, await rows.count().catch(() => 0));
  for (let i = 0; i < count; i++) { await r.hover(rows.nth(i), 450); await sleep(350); }
}
/** Opens a page, shows it for a moment, glides over its data. */
async function visit(term, path, { hold = 1600, rows = 3, scroll = 0 } = {}) {
  await go(term, path);
  await sleep(hold);
  if (rows) await skimRows(rows);
  if (scroll) { await r.scroll(scroll, 1200); await sleep(900); await r.scroll(-scroll, 900); }
}

await scene('w06-dashboard', async () => {
  await sleep(2500);
  const kpis = page.locator('erp-stat-card, .erp-stat-card').first();
  if (await kpis.count()) await r.spotlight(page.locator('.erp-stat-grid').first(), 2200);
  await r.scroll(700, 1500); await sleep(1500);
  await r.scroll(700, 1500); await sleep(1200);
  await r.scroll(-1400, 1200); await sleep(600);
  const search = page.getByPlaceholder(/search/i).first();
  if (await search.count()) { await r.type(search, 'Al Noor', { delay: 70 }); await sleep(2200); await page.keyboard.press('Escape'); }
});

let invoiceNo = null;
await scene('w07-sales', async () => {
  await visit('Quotes', '/sales/quotes', { hold: 1400, rows: 2 });
  await visit('Sales orders', '/sales/orders', { hold: 1200, rows: 2 });
  await go('Invoices', '/finance/invoices'); await sleep(1500);
  await r.spotlight(tid('invoice-stats'), 1800);
  await r.click(tid('invoice-new')); await sleep(1500);
  const party = tid('inv-party');
  await r.click(party); await page.keyboard.type('Al Noor', { delay: 80 }); await sleep(1500);
  const opt = page.locator('nz-option-item, .ant-select-item-option').first();
  await r.click(opt);
  await r.type(tid('inv-desc-0'), 'Office fit-out consultancy', { delay: 40 });
  const qty = tid('inv-qty-0').locator('input'); await r.click(qty); await qty.fill(''); await qty.pressSequentially('3', { delay: 80 });
  const price = tid('inv-price-0').locator('input'); await r.click(price); await price.fill(''); await price.pressSequentially('4500', { delay: 80 });
  // Tax on the first line, if the workspace has tax codes.
  const tax = page.locator('[data-testid="invoice-form"] nz-select').last();
  await r.click(tax); await sleep(700);
  const taxOpt = page.locator('.ant-select-item-option').first();
  if (await taxOpt.count()) await r.click(taxOpt); else await page.keyboard.press('Escape');
  await sleep(600);
  await r.spotlight(tid('inv-totals'), 1800);
  await r.click(tid('inv-issue')); await sleep(3000);
  const print = tid('success-print');
  invoiceNo = await page.locator('[data-testid="sheet-number"]').first().innerText().catch(() => null);
  if (await print.count()) { await r.hover(print); await sleep(1200); }
});

await scene('w08-receivables', async () => {
  const pay = tid('success-pay');
  if (await pay.count()) {
    await r.click(pay); await sleep(1800);
    const save = page.getByRole('button', { name: /^(save|record|post)/i }).last();
    await r.hover(save); await sleep(800); await r.click(save); await sleep(2500);
  } else {
    await visit('Receipts', '/finance/receipts', { rows: 3 });
  }
  await page.keyboard.press('Escape').catch(() => {});
  await go('Customers', '/parties?type=customer'); await sleep(1500);
  const row = page.locator('tbody tr:visible', { hasText: 'Al Noor' }).first();
  if (await row.count()) { await r.click(row); await sleep(2500); await r.scroll(600, 1200); await sleep(1500); }
});

await scene('w09-purchasing', async () => {
  await visit('Requisitions', '/purchasing/requisitions', { hold: 1100, rows: 2 });
  await visit('Purchase orders', '/purchasing/orders', { hold: 1100, rows: 2 });
  const po = page.locator('tbody tr:visible').first();
  if (await po.count()) { await r.click(po); await sleep(2500); await page.keyboard.press('Escape'); await sleep(600); }
  await visit('Goods receipts', '/purchasing/receipts', { hold: 1000, rows: 2 });
  await visit('Bills', '/finance/bills', { hold: 1000, rows: 2 });
  await visit('Stock', '/inventory/stock', { hold: 1400, rows: 4 });
});

await scene('w10-finance', async () => {
  await visit('Chart of accounts', '/finance/accounts', { hold: 1200, rows: 3 });
  await visit('Journals', '/finance/journals', { hold: 1000, rows: 2 });
  await go('Reports', '/finance/reports'); await sleep(2000);
  for (const name of [/profit|income/i, /balance sheet/i, /trial balance/i]) {
    const tab = page.getByRole('tab', { name }).or(page.getByRole('button', { name })).first();
    if (await tab.count()) { await r.click(tab); await sleep(2200); }
  }
  await visit('Budgets', '/finance/budgets', { hold: 1600, rows: 2 });
});

await scene('w11-approvals', async () => {
  await go('Approvals', '/approvals'); await sleep(1800);
  await skimRows(2);
  const approve = page.getByRole('button', { name: /^approve/i }).first();
  if (await approve.count()) { await r.hover(approve); await sleep(900); await r.click(approve); await sleep(1500);
    const ok = page.getByRole('button', { name: /^(approve|ok|confirm|yes)/i }).last(); if (await ok.isVisible().catch(() => false)) await r.click(ok); await sleep(1500); }
});

await scene('w12-hr', async () => {
  await visit('Employees', '/hr/employees', { hold: 1200, rows: 3 });
  await visit('Attendance', '/hr/attendance', { hold: 1000, rows: 2 });
  await visit('Leave', '/hr/leave', { hold: 1000, rows: 2 });
  await go('Payroll', '/hr/payroll'); await sleep(1500);
  const run = page.locator('tbody tr:visible').first();
  if (await run.count()) { await r.click(run); await sleep(2500); await r.scroll(600, 1200); await sleep(1200); }
});

await scene('w13-industry', async () => {
  // Real estate lives in its own workspace: switch to it behind a short title card.
  await r.card({ kick: 'Industry modules', title: 'Switch on what <span>your industry</span> needs', sub: 'Real estate · Projects · POS · CRM · Manufacturing · and more' });
  await sleep(2200);
  r.freeze();
  await page.context().clearCookies();
  await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
  await page.goto(BASE + '/auth/login', { waitUntil: 'networkidle' });
  await quietSignIn('admin@demo.local', 'demo-re');
  await page.goto(BASE + '/real-estate/properties', { waitUntil: 'networkidle' });
  await r.card({ kick: 'Industry modules', title: 'Switch on what <span>your industry</span> needs', sub: 'Real estate · Projects · POS · CRM · Manufacturing · and more' });
  await r.unfreeze(1000); await r.uncard();
  await r.lowerThird('Real estate', 3500);
  await skimRows(3);
  await visit('Leases', '/real-estate/leases', { hold: 1100, rows: 2 });
  // Back to the general Demo workspace for the other modules.
  r.freeze();
  await page.context().clearCookies();
  await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
  await page.goto(BASE + '/auth/login', { waitUntil: 'networkidle' });
  await quietSignIn('admin@demo.local', 'demo');
  await page.goto(BASE + '/projects/board', { waitUntil: 'networkidle' });
  await sleep(800);
  await r.unfreeze(400);
  await r.lowerThird('Projects', 3000); await sleep(2200);
  await visit('Timesheets', '/projects/timesheets', { hold: 1000, rows: 2 });
  await go('Terminal', '/pos/terminal'); await r.lowerThird('Point of sale', 3000); await sleep(1500);
  const tiles = page.locator('button:visible, .tile:visible').filter({ hasText: /\d/ });
  for (let i = 0; i < 2 && i < (await tiles.count()); i++) { await r.click(tiles.nth(i + 3)).catch(() => {}); await sleep(500); }
  await sleep(1200);
  await go('Opportunities', '/crm/opportunities'); await r.lowerThird('CRM', 3000); await sleep(2200);
  await go('Work orders', '/manufacturing/workorders'); await r.lowerThird('Manufacturing', 3000); await sleep(1500); await skimRows(2);
  await visit('Engagements', '/services/engagements', { hold: 1100, rows: 1 });
  await visit('Stores', '/retail/stores', { hold: 1000, rows: 1 });
  await visit('Routes', '/distribution/routes', { hold: 1000, rows: 1 });
  await visit('Asset register', '/assets/register', { hold: 1100, rows: 2 });
}, { label: false });

await scene('w14-control', async () => {
  await visit('Modules', '/admin/modules', { hold: 1800, rows: 0, scroll: 700 });
  await visit('Users', '/admin/users', { hold: 1200, rows: 3 });
  await visit('Roles', '/admin/roles', { hold: 1000, rows: 2 });
  await go('Security', '/account/security'); await sleep(2000);
  const lang = tid('lang-toggle');
  if (await lang.count()) { await r.click(lang); await sleep(1200); const ar = page.getByText(/العربية|Arabic/).first(); if (await ar.isVisible().catch(() => false)) await r.click(ar); await sleep(2500);
    await go('Dashboard', '/'); await sleep(2500); }
});

await scene('w15-outro', async () => {
  await r.card({ kick: 'HigherSquare 360', title: 'Your whole business, <span>in one place</span>', sub: 'Start your free trial today' });
  await sleep(6000);
}, { label: false });
await sleep(1500);
await r.stop();
console.log('Recorded work/rec/web.mp4');
process.exit(0);
