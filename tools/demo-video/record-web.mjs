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
    const hit = page.locator(`[data-testid="sidebar"] a[href="${path}"]`).first();
    await hit.waitFor({ timeout: 3000 });
    await r.click(hit);
    await box.fill('').catch(() => {});
  } catch {
    await page.goto(BASE + path, { waitUntil: 'networkidle' });
  }
  await page.waitForLoadState('networkidle').catch(() => {});
  await sleep(900);
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
  await r.click(page.getByRole('button', { name: /^choose/i }).nth(1).or(page.getByRole('button', { name: /^choose/i }).first()).first());
  await sleep(1000);
  await r.type(tid('register-business'), 'Crescent Trading Co');
  await sleep(700);
  const code = tid('register-code');
  await code.fill(''); await code.pressSequentially(`crescent-${runTag}`, { delay: 45 });
  await sleep(600);
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
  await page.getByTestId('login-email').fill('admin@demo.local');
  await page.getByTestId('login-password').fill(PASSWORD);
  await page.getByTestId('login-password').press('Enter');
  await page.waitForLoadState('networkidle').catch(() => {}); await sleep(2500);
  await r.card({ kick: 'A few months later', title: 'A workspace in <span>full swing</span>', sub: 'Real data, posted through the real workflows' });
  await r.unfreeze(1300);
}, { label: false });
await r.uncard();

// TODO(after explore): dashboard, sales, receivables, purchasing, finance, approvals, HR, industry modules, control.

await scene('w15-outro', async () => {
  await r.card({ kick: 'HigherSquare 360', title: 'Your whole business, <span>in one place</span>', sub: 'Start your free trial today' });
  await sleep(6000);
}, { label: false });
await sleep(1500);
await r.stop();
console.log('Recorded work/rec/web.mp4');
process.exit(0);
