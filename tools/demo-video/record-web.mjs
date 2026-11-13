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
const CUSTOMER = 'Apex';

await scene('w06-dashboard', async () => {
  await sleep(2000);
  await r.spotlight(tid('kpi-grid'), 2400);
  const ageing = page.getByRole('button', { name: /^Receivables ageing$/ }).first();
  if (await ageing.count()) { await r.click(ageing); await sleep(1800); await r.click(page.getByRole('button', { name: /^Billed vs collected$/ }).first()); await sleep(1200); }
  await r.hover(page.getByText('Alerts and to-dos').first(), 800); await sleep(1200);
  await r.scroll(800, 1500); await sleep(1600);
  await r.scroll(-800, 1100); await sleep(600);
  const toggle = tid('global-search-toggle');
  if (await toggle.count()) { await r.click(toggle); await sleep(400); }
  await page.keyboard.type(CUSTOMER, { delay: 110 }).catch(() => {});
  await sleep(2400);
  await r.hover(page.locator('[data-testid="search-hit"]').first(), 600).catch(() => {});
  await sleep(1000);
  await page.keyboard.press('Escape'); await sleep(500);
});

await scene('w07-sales', async () => {
  await visit('Quotes', '/sales/quotes', { hold: 1300, rows: 2 });
  await visit('Sales orders', '/sales/orders', { hold: 1100, rows: 2 });
  await go('Invoices', '/finance/invoices'); await sleep(1300);
  await r.spotlight(tid('invoice-stats'), 1700);
  await r.click(tid('invoice-new')); await sleep(1500);
  await r.click(tid('inv-party')); await page.keyboard.type(CUSTOMER, { delay: 90 }); await sleep(1600);
  await r.click(page.locator('.ant-select-item-option').first()); await sleep(500);
  await r.type(tid('inv-desc-0'), 'Warehouse racking supply and installation', { delay: 35 });
  const qty = tid('inv-qty-0').locator('input'); await r.click(qty); await qty.fill(''); await qty.pressSequentially('3', { delay: 90 });
  const price = tid('inv-price-0').locator('input'); await r.click(price); await price.fill(''); await price.pressSequentially('12500', { delay: 80 });
  const tax = page.locator('[data-testid="invoice-form"] nz-select').last();
  await r.click(tax); await sleep(700);
  const taxOpt = page.locator('.ant-select-item-option:visible').first();
  if (await taxOpt.count()) await r.click(taxOpt); else await page.keyboard.press('Escape');
  await sleep(600);
  await r.spotlight(tid('inv-totals'), 1800);
  await r.click(tid('inv-issue')); await sleep(3200);
  const print = tid('success-print');
  if (await print.count()) { await r.hover(print); await sleep(1200); }
});

await scene('w08-receivables', async () => {
  const pay = tid('success-pay');
  if (await pay.count()) {
    await r.click(pay); await sleep(2000);
    const save = page.getByRole('button', { name: /^(save|record|post|receive)/i }).last();
    if (await save.isVisible().catch(() => false)) { await r.hover(save); await sleep(700); await r.click(save); await sleep(2500); }
  }
  await page.keyboard.press('Escape').catch(() => {}); await sleep(400);
  await visit('Receipts', '/finance/receipts', { hold: 1200, rows: 2 });
  await go('Customers', '/parties?type=customer'); await sleep(1300);
  const row = page.locator('tbody tr:visible', { hasText: CUSTOMER }).first();
  if (await row.count()) { await r.click(row); await sleep(2600); await r.scroll(600, 1200); await sleep(1500); await r.scroll(-600, 800); }
});

await scene('w09-purchasing', async () => {
  await visit('Requisitions', '/purchasing/requisitions', { hold: 1000, rows: 2 });
  await visit('Purchase orders', '/purchasing/orders', { hold: 1000, rows: 2 });
  const po = page.locator('tbody tr:visible').first();
  if (await po.count()) { await r.click(po); await sleep(2400); await page.keyboard.press('Escape'); await sleep(600); }
  await visit('Goods receipts', '/purchasing/receipts', { hold: 1000, rows: 2 });
  await visit('Bills', '/finance/bills', { hold: 1000, rows: 2 });
  await visit('Stock on hand', '/inventory/stock', { hold: 1300, rows: 4 });
});

await scene('w10-finance', async () => {
  await visit('Chart of accounts', '/finance/accounts', { hold: 1100, rows: 3 });
  await visit('Journals', '/finance/journals', { hold: 1000, rows: 2 });
  await page.goto(BASE + '/finance/reports', { waitUntil: 'networkidle' }); await sleep(1600);
  await r.hover(page.locator('[data-testid="tb-table"]').first(), 700); await sleep(1200);
  for (const name of ['Income statement', 'Balance sheet']) {
    const tab = page.getByRole('button', { name, exact: true }).first();
    if (await tab.count()) { await r.click(tab); await sleep(2400); }
  }
  await visit('Budgets', '/finance/budgets', { hold: 1500, rows: 2 });
});

await scene('w11-approvals', async () => {
  await go('Approvals', '/approvals'); await sleep(1500);
  const ask = page.getByRole('button', { name: /^Request approval$/ }).first();
  if (await ask.count()) {
    await r.click(ask); await sleep(1200);
    const dialog = page.locator('.ant-modal:visible, .ant-drawer:visible').last();
    const fieldsIn = dialog.locator('input:visible, textarea:visible');
    const n = await fieldsIn.count();
    const values = ['New delivery van for the Doha route', 'Two vans are due for replacement this quarter.', '85000'];
    for (let i = 0; i < Math.min(n, values.length); i++) await r.type(fieldsIn.nth(i), values[i], { delay: 30 });
    await sleep(500);
    await r.click(dialog.getByRole('button', { name: /^Submit$/ })); await sleep(1800);
    const mine = page.getByText(/^My requests$/).first(); if (await mine.count()) { await r.click(mine); await sleep(1800); }
  }
});

await scene('w12-hr', async () => {
  await visit('Employees', '/hr/employees', { hold: 1100, rows: 3 });
  await visit('Attendance', '/hr/attendance', { hold: 900, rows: 2 });
  await visit('Leave requests', '/hr/leave', { hold: 900, rows: 2 });
  await go('Payroll', '/hr/payroll'); await sleep(1300);
  await skimRows(3);
  const run = page.locator('tbody tr:visible').first();
  if (await run.count()) { await r.click(run); await sleep(2600); await r.scroll(600, 1200); await sleep(1200); }
});

await scene('w13-industry', async () => {
  const industryCard = { kick: 'Industry modules', title: 'Switch on what <span>your industry</span> needs', sub: 'Real estate · Projects · POS · CRM · Manufacturing · and more' };
  await r.card(industryCard);
  await sleep(2600);
  r.freeze();
  await page.goto(BASE + '/real-estate/properties', { waitUntil: 'networkidle' });
  await sleep(1200);
  await r.card(industryCard);
  await r.unfreeze(1200); await r.uncard();
  await r.lowerThird('Real estate', 3500);
  await skimRows(3);
  await visit('Leases', '/real-estate/leases', { hold: 1000, rows: 2 });
  await go('Task board', '/projects/board'); await r.lowerThird('Projects', 3000); await sleep(2200);
  await visit('Timesheets', '/projects/timesheets', { hold: 900, rows: 2 });
  await go('POS Terminal', '/pos/terminal'); await r.lowerThird('Point of sale', 3000); await sleep(1400);
  const products = page.getByText(/QAR [\d,]+\.\d\d/);
  for (let i = 0; i < 3 && i < (await products.count()); i++) { await r.hover(products.nth(i), 500); await sleep(400); }
  await sleep(800);
  await go('Sales pipeline', '/crm/opportunities'); await r.lowerThird('CRM', 3000); await sleep(2200);
  await go('Bills of materials', '/manufacturing/boms'); await r.lowerThird('Manufacturing', 3000); await sleep(1200); await skimRows(2);
  await page.goto(BASE + '/manufacturing/workorders', { waitUntil: 'networkidle' }); await sleep(1400); await skimRows(2);
  await visit('Engagements', '/services/engagements', { hold: 1000, rows: 1 });
  await visit('Stores', '/retail/stores', { hold: 900, rows: 1 });
  await visit('Routes', '/distribution/routes', { hold: 900, rows: 1 });
}, { label: false });

await scene('w14-control', async () => {
  await visit('Modules', '/admin/modules', { hold: 1800, rows: 0, scroll: 700 });
  await visit('Users', '/admin/users', { hold: 1100, rows: 3 });
  await visit('Roles', '/admin/roles', { hold: 900, rows: 2 });
  await page.goto(BASE + '/account/security', { waitUntil: 'networkidle' }); await sleep(2000);
  const lang = tid('lang-toggle');
  if (await lang.count()) {
    await r.click(lang); await sleep(1200);
    const ar = page.getByText(/العربية|Arabic/).first(); if (await ar.isVisible().catch(() => false)) await r.click(ar);
    await sleep(1500);
    await page.goto(BASE + '/', { waitUntil: 'networkidle' }); await sleep(3000);
  }
});

await scene('w15-outro', async () => {
  await r.card({ kick: 'HigherSquare 360', title: 'Your whole business, <span>in one place</span>', sub: 'Start your free trial today' });
  await sleep(6000);
}, { label: false });
await sleep(1500);
await r.stop();
console.log('Recorded work/rec/web.mp4');
process.exit(0);
