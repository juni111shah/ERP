// Walks the running web app and saves a screenshot plus a text dump (visible text, buttons, links, test ids) of every page,
// so the recording script can be written against what is really on screen. Output: work/explore-web/.
// Usage: node explore-web.mjs [baseUrl]   (default http://localhost:5080)
import { chromium } from 'playwright';
import { mkdirSync, writeFileSync } from 'node:fs';
import { here } from './lib/media.mjs';

const BASE = process.argv[2] ?? 'http://localhost:5080';
const out = here('work/explore-web'); mkdirSync(out, { recursive: true });
const browser = await chromium.launch({ channel: process.env.PW_CHANNEL ?? 'chrome' });
let n = 0;

async function dump(page, name) {
  await page.waitForTimeout(1800);
  const id = String(++n).padStart(3, '0') + '-' + name.replace(/[^a-z0-9]+/gi, '-').slice(0, 60);
  await page.screenshot({ path: `${out}/${id}.jpg`, type: 'jpeg', quality: 60 });
  const info = await page.evaluate(() => {
    const vis = (e) => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0; };
    const items = [...document.querySelectorAll('button,a,input,select,textarea,[role=tab],[role=button],[data-testid],nz-select')].filter(vis).slice(0, 220).map((e) => {
      const r = e.getBoundingClientRect();
      return `${e.tagName.toLowerCase()}${e.getAttribute('data-testid') ? '[' + e.getAttribute('data-testid') + ']' : ''}${e.getAttribute('href') ? ' href=' + e.getAttribute('href') : ''}${e.getAttribute('formcontrolname') ? ' fc=' + e.getAttribute('formcontrolname') : ''} "${(e.innerText || e.value || e.getAttribute('placeholder') || e.getAttribute('aria-label') || e.getAttribute('title') || '').trim().replace(/\s+/g, ' ').slice(0, 70)}" @${Math.round(r.x)},${Math.round(r.y)}`;
    });
    return { url: location.href, title: document.title, text: document.body.innerText.slice(0, 2500), items };
  });
  writeFileSync(`${out}/${id}.txt`, `${info.url}\n${info.title}\n\n${info.text}\n\n--- controls ---\n${info.items.join('\n')}\n`);
  console.log(id, info.url);
}

async function step(name, fn) { try { await fn(); } catch (e) { console.log('FAILED', name, e.message.split('\n')[0]); } }

// ---- signed out: pricing and registration ----
const ctx1 = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
const p1 = await ctx1.newPage();
const errors = []; const watch = (p) => p.on('console', (m) => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }).on('pageerror', (er) => errors.push('pageerror: ' + er.message.slice(0, 300)));
watch(p1);
await step('pricing', async () => {
  await p1.goto(BASE + '/pricing', { waitUntil: 'networkidle' });
  await dump(p1, 'pricing-top');
  for (let y = 1; y <= 7; y++) { await p1.mouse.wheel(0, 900); await dump(p1, 'pricing-scroll-' + y); }
});
await step('register', async () => {
  await p1.goto(BASE + '/register', { waitUntil: 'networkidle' });
  await dump(p1, 'register-plans');
  await p1.getByTestId('plan-choose-business').click();
  await dump(p1, 'register-form');
  await p1.getByTestId('register-business').fill('Crescent Trading Co');
  await p1.waitForTimeout(800);
  const pick = async (tidName, text, name) => {
    await p1.getByTestId(tidName).click(); await p1.keyboard.type(text, { delay: 40 }); await p1.waitForTimeout(800);
    await dump(p1, name);
    await p1.locator('.ant-select-item-option').first().click(); await p1.waitForTimeout(500);
  };
  await pick('register-industry', 'Wholesale', 'register-industry-open');
  await pick('register-country', 'Pakistan', 'register-country-open');
  if (!(await p1.getByTestId('register-currency').innerText()).match(/[A-Z]{3}/)) await pick('register-currency', 'PKR', 'register-currency-open');
  await p1.getByTestId('register-name').fill('Ayesha Khan');
  await p1.getByTestId('register-email').fill(`ayesha.${Date.now()}@crescent.example.test`);
  await p1.getByTestId('register-phone').fill('+92 300 1234567');
  await p1.getByTestId('register-password').fill('Crescent#2026demo');
  await p1.getByTestId('register-terms').click();
  await dump(p1, 'register-filled');
  await p1.getByTestId('register-submit').click();
  await p1.waitForTimeout(4000);
  await dump(p1, 'register-result');
});
await step('login page', async () => { await p1.goto(BASE + '/auth/login', { waitUntil: 'networkidle' }); await dump(p1, 'login'); });
await ctx1.close();

// ---- signed in as the demo admin: every page in the sidebar ----
async function signIn(page, email, wsCode) {
  await page.goto(BASE + '/auth/login', { waitUntil: 'networkidle' });
  await page.getByTestId('login-email').fill(email);
  await page.getByTestId('login-password').fill('DevOnly#2026');
  await page.getByTestId('login-password').press('Enter');
  await page.waitForTimeout(3000);
  if (wsCode) { const ws = page.getByTestId('workspace-' + wsCode); await ws.waitFor({ timeout: 8000 }).catch(() => {}); if (await ws.count()) { await dump(page, 'workspace-picker'); await ws.click(); await page.waitForTimeout(3000); } }
  await page.waitForLoadState('networkidle').catch(() => {});
}

for (const [email, ws, tag] of [['admin@demo.local', 'demo', 'demo'], ['owner@demo.local', 'demo-re', 're']]) {
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  watch(page);
  await step('sign in ' + tag, async () => {
    await signIn(page, email, ws);
    await dump(page, tag + '-home');
    // Open every collapsed sidebar group so all links are in the DOM.
    for (let k = 0; k < 3; k++) {
      for (const g of await page.locator('[data-testid^="nav-group-"][aria-expanded="false"], [data-testid="sidebar"] [aria-expanded="false"]').all()) await g.click().catch(() => {});
      await page.waitForTimeout(300);
    }
    await dump(page, tag + '-sidebar-open');
    const hrefs = [...new Set(await page.$eval('[data-testid="sidebar"] a[href], nav a[href], aside a[href]', (as) => as.map((a) => a.getAttribute('href')).filter((h) => h && h.startsWith('/') && !h.startsWith('//'))))];
    writeFileSync(`${out}/${tag}-links.txt`, hrefs.join('\n'));
    const limit = tag === 'demo' ? 140 : 30;
    for (const h of hrefs.slice(0, limit)) await step(h, async () => { await page.goto(BASE + h, { waitUntil: 'networkidle', timeout: 20000 }).catch(() => {}); await dump(page, tag + h); });
    for (const h of ['/approvals', '/account/security', '/admin/modules', '/finance/reports', '/pos/terminal', '/hr/payroll'])
      await step(h, async () => { await page.goto(BASE + h, { waitUntil: 'networkidle' }).catch(() => {}); await dump(page, tag + '-extra' + h); });
  });
  await ctx.close();
}
await browser.close();
console.log('Explore done:', n, 'pages');
