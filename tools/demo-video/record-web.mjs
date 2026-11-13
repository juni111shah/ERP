// Records the web walkthrough: pricing page, registration of a new workspace, then every module in the seeded Demo workspace.
// The narration is split into beats (script.json); a beat starts when its action starts on screen and never before the previous
// beat has finished speaking, so picture and voice stay together. Scene starts are written to work/rec/web.marks.json.
// Usage: node record-web.mjs [baseUrl]   (default http://localhost:4200, the web app with /api proxied to the API)
import { readFileSync } from 'node:fs';
import { WebRecorder, sleep } from './lib/recorder.mjs';
import { here } from './lib/media.mjs';

const BASE = process.argv[2] ?? process.env.BASE ?? 'http://localhost:4200';
const script = JSON.parse(readFileSync(here('script.json'), 'utf8'));
const voice = JSON.parse(readFileSync(here('work/voice/durations.json'), 'utf8'));
const VO_LEAD = 0.6, GAP = 0.35, TAIL = 0.9;
const PASSWORD = 'DevOnly#2026';
const runTag = Date.now().toString(36).slice(-5);
const CUSTOMER = 'Apex';

const r = new WebRecorder('web');
const page = await r.launch();
const tid = (id) => page.getByTestId(id);
/** Lets the page settle after a navigation, without waiting forever for a network that never goes quiet. */
const settle = (ms = 2500) => page.waitForLoadState('networkidle', { timeout: ms }).catch(() => {});
const open = async (path) => { await page.goto(BASE + path, { waitUntil: 'domcontentloaded' }); await settle(3500); };
const entry = (id) => { const s = script.web.scenes.find((x) => x.id === id); if (!s) throw new Error('Not in script.json: ' + id); return s; };

// ---------- narration timing ----------
let voFree = 0;        // recording time at which the current narration line ends
/** Starts a narration beat: waits for the previous line to finish, then marks this one (its voice starts VO_LEAD later). */
async function beat(id) {
  entry(id);
  const wait = voFree - r.now();
  if (wait > 0) await sleep(wait * 1000);
  r.mark(id);
  voFree = r.now() + VO_LEAD + voice[id].seconds + GAP;
}
/** A scene: its first beat, its lower-third, the actions, then a hold until the narration has finished. */
async function scene(id, actions) {
  await beat(id);
  const s = entry(id);
  if (s.label) await r.lowerThird(s.label, 4200);
  try { await actions(); } catch (e) { console.log(`  ! ${id}: ${e.message.split('\n')[0]}`); }
  const hold = voFree + TAIL - r.now();
  if (hold > 0) await sleep(hold * 1000);
}
/** Runs a step and keeps going if it fails (a missing button must not stop the whole recording). */
async function safely(name, fn) { try { await fn(); } catch (e) { console.log(`  ! ${name}: ${e.message.split('\n')[0]}`); } }

// ---------- moving around ----------
/** Opens a page through the sidebar search (as a person would), or by address if that fails. */
async function go(term, path) {
  try {
    const box = tid('sidebar-search-input');
    if (!(await box.isVisible())) throw new Error('no sidebar search');
    await r.type(box, term, { delay: 35 });
    const exact = new RegExp('^\\s*' + term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '\\s*$', 'i');
    const hit = page.locator('[data-testid="sidebar"] li[data-testid^="nav-"]:visible').filter({ hasText: exact }).first();
    await hit.waitFor({ timeout: 3000 });
    await r.click(hit, { pause: 150, ms: 500 });
    await box.fill('').catch(() => {});
    const want = path.split('?')[0];
    await page.waitForURL((u) => u.pathname.startsWith(want), { timeout: 4000 }).catch(() => open(path));
  } catch {
    await open(path);
  }
  await settle();
  await sleep(700);
}
/** Glides over the first few rows of the main table, so the eye follows real data. */
async function skimRows(n = 3) {
  const rows = page.locator('tbody tr:visible');
  const count = Math.min(n, await rows.count().catch(() => 0));
  for (let i = 0; i < count; i++) { await r.hover(rows.nth(i), 400); await sleep(250); }
}
/** Opens a page, shows it for a moment, glides over its data. */
async function visit(term, path, { hold = 1200, rows = 2, scroll = 0 } = {}) {
  await go(term, path);
  await sleep(hold);
  if (rows) await skimRows(rows);
  if (scroll) { await r.scroll(scroll, 1100); await sleep(700); await r.scroll(-scroll, 800); }
}
/** Opens a searchable drop-down, types to filter it and picks the first match. */
async function pick(testId, text) {
  await r.click(tid(testId), { pause: 120, ms: 500 }); await page.keyboard.type(text, { delay: 55 }); await sleep(600);
  await r.click(page.locator('.ant-select-item-option').first(), { pause: 120, ms: 400 }); await sleep(300);
}
/** Signs in without on-screen theatrics (used while the video is frozen behind a title card). */
async function quietSignIn(email, wsCode) {
  await tid('login-email').fill(email);
  await tid('login-password').fill(PASSWORD);
  await tid('login-password').press('Enter');
  const ws = tid('workspace-' + wsCode);
  await ws.waitFor({ timeout: 10000 }).catch(() => {});
  if (await ws.count()) await ws.click();
  await settle();
  await sleep(2500);
}

// ------------------------------------------------------------------------------------------------------------------------------
await open('/pricing');
await sleep(1500);
await r.start();

await scene('w01-intro', async () => {
  await r.card({ kick: 'Client demo', title: 'HigherSquare <span>360</span>', sub: 'One ERP for your whole business' });
  await sleep(4000);
});
await r.uncard();

await scene('w02-pricing', async () => {
  await sleep(1200);
  await r.hover(tid('pricing-hero-scene'), 900); await sleep(1800);
  await r.scroll(950, 1500);
  await beat('w02b-plans');
  await sleep(900);
  await safely('cycle', async () => { await r.click(tid('pricing-cycle-annual')); await sleep(1500); await r.click(tid('pricing-cycle-monthly')); });
  await r.scroll(450, 900); await sleep(1500);
  await safely('mobile', async () => { await tid('pricing-mobile').scrollIntoViewIfNeeded(); });
  await beat('w02c-mobile');
  await sleep(1500);
});

const newEmail = `ayesha.${runTag}@crescent.example.test`;
await scene('w03-register', async () => {
  await page.evaluate(() => scrollTo({ top: 0, behavior: 'smooth' })); await sleep(800);
  await r.click(tid('pricing-top-start'));
  await settle(); await sleep(1200);
  await r.hover(tid('plan-card-business'), 600); await sleep(800);
  await r.click(tid('plan-choose-business'));
  await sleep(700);
  await beat('w03b-business');
  await r.type(tid('register-business'), 'Crescent Trading Co', { delay: 45 });
  await sleep(900);   // the workspace code fills itself in and is checked
  await r.spotlight(tid('register-code'), 1200);
  await beat('w03c-country');
  await pick('register-industry', 'Wholesale');
  await pick('register-country', 'Pakistan');
  await safely('currency', async () => { if (!(await tid('register-currency').innerText()).match(/[A-Z]{3}/)) await pick('register-currency', 'PKR'); });
  await beat('w03d-contact');
  await r.type(tid('register-name'), 'Ayesha Khan', { delay: 35 });
  await r.type(tid('register-email'), newEmail, { delay: 8 });
  await r.type(tid('register-phone'), '+92 300 1234567', { delay: 18 });
  await r.type(tid('register-password'), 'Crescent#2026demo', { delay: 16 });
  await r.click(tid('register-terms'), { pause: 100, ms: 450 });
  await r.click(tid('register-submit'));
  await sleep(2200);
  await safely('result', async () => { await r.spotlight(page.getByText(/workspace is ready/i).first(), 1500); });
});

await scene('w04-first-login', async () => {
  const signin = tid('register-result-signin');
  if (await signin.count()) { await r.click(signin); await settle(); }
  else await open('/auth/login');
  await sleep(600);
  await r.type(tid('login-email'), newEmail, { delay: 12 });
  await r.type(tid('login-password'), 'Crescent#2026demo', { delay: 14 });
  await r.click(page.getByRole('button', { name: /^sign in$/i }).first());
  // The first sign-in to a brand-new workspace can take a while on a small server: hold the picture until the dashboard is there.
  r.freeze();
  await page.getByText(/Welcome, Ayesha Khan/).first().waitFor({ timeout: 30000 }).catch(() => {});
  await settle(); await sleep(800);
  await r.unfreeze(300);
  await safely('new kpis', async () => { await r.spotlight(tid('kpi-grid'), 2000); });
  await r.hover(page.getByText(/Welcome, Ayesha Khan/).first(), 700).catch(() => {});
  await sleep(1500);
});

const bridgeCard = { kick: 'A few months later', title: 'A workspace in <span>full swing</span>', sub: 'Real data, posted through the real workflows' };
await scene('w05-bridge', async () => {
  await r.card(bridgeCard);
  await sleep(1600);
  // Behind the (frozen) card: sign out and into the Demo workspace.
  r.freeze();
  await page.context().clearCookies();
  await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
  await open('/auth/login');
  await quietSignIn('admin@demo.local', 'demo');
  await r.card(bridgeCard);
  await r.unfreeze(1300);
});
await r.uncard();

await scene('w06-dashboard', async () => {
  await sleep(1500);
  await r.spotlight(tid('kpi-grid'), 2600);
  await beat('w06b-charts');
  await safely('ageing', async () => {
    await r.click(page.getByRole('button', { name: /^Receivables ageing$/ }).first()); await sleep(1800);
    await r.click(page.getByRole('button', { name: /^Billed vs collected$/ }).first()); await sleep(900);
  });
  await safely('alerts', async () => { await r.hover(page.getByText('Alerts and to-dos').first(), 700); });
  await sleep(900);
  await r.scroll(800, 1400); await sleep(1200);
  await r.scroll(-800, 1000);
  await beat('w06c-search');
  await safely('search', async () => {
    await r.click(tid('global-search-toggle')); await sleep(300);
    await page.keyboard.type(CUSTOMER, { delay: 110 });
    await sleep(1800);
    await r.hover(page.locator('[data-testid="search-hit"]').first(), 600);
    await sleep(900);
  });
  await page.keyboard.press('Escape'); await sleep(300);
});

await scene('w07-sales', async () => {
  await visit('Quotes', '/sales/quotes', { hold: 1000, rows: 2 });
  await go('Invoices', '/finance/invoices');
  await beat('w07b-invoices');
  await r.spotlight(tid('invoice-stats'), 2200);
  await beat('w07c-new');
  await r.click(tid('invoice-new')); await sleep(1200);
  await r.click(tid('inv-party')); await page.keyboard.type(CUSTOMER, { delay: 90 }); await sleep(1400);
  await r.click(page.locator('.ant-select-item-option').first()); await sleep(400);
  await r.type(tid('inv-desc-0'), 'Warehouse racking supply and installation', { delay: 22 });
  const qty = tid('inv-qty-0').locator('input'); await r.click(qty, { pause: 100, ms: 450 }); await qty.fill(''); await qty.pressSequentially('3', { delay: 80 });
  const price = tid('inv-price-0').locator('input'); await r.click(price, { pause: 100, ms: 450 }); await price.fill(''); await price.pressSequentially('12500', { delay: 70 });
  await safely('tax', async () => {
    await r.click(page.locator('[data-testid="invoice-form"] nz-select').last(), { pause: 100, ms: 450 }); await sleep(600);
    const opt = page.locator('.ant-select-item-option:visible').first();
    if (await opt.count()) await r.click(opt, { pause: 100, ms: 400 }); else await page.keyboard.press('Escape');
  });
  await sleep(400);
  await r.spotlight(tid('inv-totals'), 1600);
  await beat('w07d-issue');
  await r.click(tid('inv-issue')); await sleep(2800);
  await safely('print', async () => { await r.hover(tid('success-print'), 600); });
  await sleep(1500);
});

await scene('w08-receivables', async () => {
  await safely('pay', async () => {
    const pay = tid('success-pay');
    if (!(await pay.count())) return;
    await r.click(pay); await sleep(1800);
    const save = page.getByRole('button', { name: /^(save|record|post|receive)/i }).last();
    if (await save.isVisible().catch(() => false)) { await r.click(save); await sleep(2000); }
  });
  await page.keyboard.press('Escape').catch(() => {}); await sleep(300);
  await visit('Receipts', '/finance/receipts', { hold: 1000, rows: 2 });
  await beat('w08b-customer');
  await go('Customers', '/parties?type=customer');
  const row = page.locator('tbody tr:visible', { hasText: CUSTOMER }).first();
  if (await row.count()) { await r.click(row); await sleep(2400); await r.scroll(600, 1100); await sleep(1500); await r.scroll(-600, 800); }
});

await scene('w09-purchasing', async () => {
  await visit('Requisitions', '/purchasing/requisitions', { hold: 1200, rows: 1 });
  await beat('w09b-po');
  await visit('Purchase orders', '/purchasing/orders', { hold: 800, rows: 2 });
  await safely('po', async () => { await r.click(page.locator('tbody tr:visible').first()); await sleep(2600); });
  await beat('w09c-receive');
  await visit('Goods receipts', '/purchasing/receipts', { hold: 900, rows: 2 });
  await beat('w09d-stock');
  await visit('Stock on hand', '/inventory/stock', { hold: 1000, rows: 4 });
});

await scene('w10-finance', async () => {
  await visit('Chart of accounts', '/finance/accounts', { hold: 1000, rows: 0 });
  await visit('Journals', '/finance/journals', { hold: 900, rows: 3 });
  await beat('w10b-reports');
  await open('/finance/reports'); await sleep(1300);
  await safely('tb', async () => { await r.hover(tid('tb-table'), 600); });
  for (const name of ['Income statement', 'Balance sheet']) {
    await safely(name, async () => {
      await r.click(page.locator('button:visible').filter({ hasText: new RegExp('^\\s*' + name + '\\s*$') }).first()); await sleep(600);
      const run = tid('rep-run'); if (await run.isVisible().catch(() => false)) await r.click(run, { pause: 100, ms: 450 });
      await sleep(1800);
    });
  }
  await beat('w10c-budgets');
  await visit('Budgets', '/finance/budgets', { hold: 1300, rows: 2 });
});

await scene('w11-approvals', async () => {
  await go('Approvals', '/approvals');
  await safely('request', async () => {
    await r.click(tid('approval-new')); await sleep(1000);
    await r.type(tid('approval-subject'), 'New delivery van for the Doha route', { delay: 22 });
    await r.type(page.locator('textarea[formcontrolname="justification"]'), 'Two vans are due for replacement this quarter.', { delay: 14 });
    const amount = page.locator('[formcontrolname="amount"] input').first();
    await r.click(amount, { pause: 100, ms: 450 }); await amount.pressSequentially('85000', { delay: 70 });
    await beat('w11b-submit');
    await r.click(page.locator('.ant-drawer:visible').getByRole('button', { name: /^Submit$/ }).first()); await sleep(1600);
    await r.click(page.locator(':is(a,button,div,span,label):visible').filter({ hasText: /^\s*My requests\s*$/ }).last()); await sleep(1800);
  });
});

await scene('w12-hr', async () => {
  await visit('Employees', '/hr/employees', { hold: 1000, rows: 4 });
  await beat('w12b-leave');
  await visit('Leave requests', '/hr/leave', { hold: 800, rows: 3 });
  await beat('w12c-payroll');
  await go('Payroll', '/hr/payroll'); await sleep(900);
  await skimRows(3);
  await safely('run', async () => { await r.click(page.locator('tbody tr:visible').first()); await sleep(2400); await r.scroll(500, 1000); await sleep(1200); });
});

const industryCard = { kick: 'Industry modules', title: 'Switch on what <span>your industry</span> needs', sub: 'Real estate · Projects · POS · CRM · Manufacturing · and more' };
await scene('w13-industry', async () => {
  await r.card(industryCard);
  await sleep(2400);
  r.freeze();
  await open('/real-estate/properties');
  await sleep(1000);
  await r.card(industryCard);
  await r.unfreeze(1000); await r.uncard();
  await beat('w13b-realestate');
  await r.lowerThird('Real estate', 3200);
  await skimRows(3);
  await visit('Leases', '/real-estate/leases', { hold: 900, rows: 2 });
  await beat('w13c-projects');
  await go('Task board', '/projects/board'); await r.lowerThird('Projects', 3000); await sleep(1800);
  await visit('Timesheets', '/projects/timesheets', { hold: 800, rows: 2 });
  await beat('w13d-pos');
  await go('POS Terminal', '/pos/terminal'); await r.lowerThird('Point of sale', 3000); await sleep(1000);
  await safely('products', async () => { const p = page.getByText(/QAR [\d,]+\.\d\d/); for (let i = 0; i < 3; i++) { await r.hover(p.nth(i), 450); await sleep(350); } });
  await beat('w13e-crm');
  await go('Sales pipeline', '/crm/opportunities'); await r.lowerThird('CRM', 3000); await sleep(1800);
  await beat('w13f-manufacturing');
  await go('Bills of materials', '/manufacturing/boms'); await r.lowerThird('Manufacturing', 3000); await sleep(900); await skimRows(2);
  await open('/manufacturing/workorders'); await sleep(1100); await skimRows(2);
  await beat('w13g-more');
  await visit('Engagements', '/services/engagements', { hold: 700, rows: 0 });
  await visit('Stores', '/retail/stores', { hold: 700, rows: 0 });
  await visit('Routes', '/distribution/routes', { hold: 700, rows: 0 });
});

await scene('w14-control', async () => {
  await visit('Modules', '/admin/modules', { hold: 1200, rows: 0, scroll: 600 });
  await beat('w14b-users');
  await visit('Users', '/admin/users', { hold: 900, rows: 2 });
  await beat('w14c-security');
  await open('/account/security'); await sleep(1200);
  await safely('2fa', async () => { await r.spotlight(page.getByText(/Two-factor authentication/i).first(), 1600); });
  await beat('w14d-arabic');
  await safely('arabic', async () => {
    await r.click(tid('user-menu')); await sleep(700);
    await r.click(tid('lang-toggle')); await sleep(1500);
    await open('/'); await sleep(2500);
    await r.scroll(500, 1100); await sleep(800); await r.scroll(-500, 800);
  });
});

await scene('w15-outro', async () => {
  await r.card({ kick: 'HigherSquare 360', title: 'Your whole business, <span>in one place</span>', sub: 'Start your free trial today' });
  await sleep(5000);
});
await sleep(1200);
await r.stop();
console.log('Recorded work/rec/web.mp4');
process.exit(0);
