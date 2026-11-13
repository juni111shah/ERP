// Taps through the phone app on a running emulator and saves a screenshot plus the accessibility tree of every screen, so the
// recording script can be written against what is really there. Output: work/explore-mobile/.
import { mkdirSync, writeFileSync } from 'node:fs';
import { nodes, find, tap, tapText, type, back, hideKeyboard, screencap, swipe, sleep, describe, shell, signIn } from './lib/adb.mjs';
import { here, run } from './lib/media.mjs';

const out = here('work/explore-mobile'); mkdirSync(out, { recursive: true });
const PKG = 'com.erp.erp_mobile';
let n = 0;
async function dump(name) {
  await sleep(1500);
  const id = String(++n).padStart(3, '0') + '-' + name.replace(/[^a-z0-9]+/gi, '-').slice(0, 50);
  await screencap(`${out}/${id}.png`);
  await run(['-i', `${out}/${id}.png`, '-vf', 'scale=540:-1', '-q:v', '5', `${out}/${id}.jpg`]).catch(() => {});
  await import('node:fs').then((fs) => fs.rmSync(`${out}/${id}.png`, { force: true }));
  const list = await nodes();
  writeFileSync(`${out}/${id}.txt`, describe(list) + '\n');
  console.log(id);
  return list;
}
async function step(name, fn) { try { await fn(); } catch (e) { console.log('FAILED', name, e.message); await dump('failed-' + name).catch(() => {}); } }

await shell(`pm clear ${PKG}`).catch(() => {});
await shell(`monkey -p ${PKG} -c android.intent.category.LAUNCHER 1`);
await sleep(8000);
await dump('launch');

await step('intro', async () => {
  for (let i = 0; i < 10; i++) {
    if (await find(/Sign in to your workspace/, { timeout: 500 })) break;
    const btn = await find(/^(Next|Continue|Get started|Start|Done)/i, { timeout: 2000 }) ?? await find(/^Skip/i, { timeout: 500 });
    await dump('intro-' + i);
    if (!btn) { await swipe(900, 1300, 150, 1300, 300); continue; }
    await tap(btn.x, btn.y); await sleep(1200);
  }
});
await dump('login');

await step('sign in', async () => {
  await signIn('admin@alrayyan.qa', 'DevOnly#2026');
  await dump('login-filled');
  await tapText(/^Sign in$/);
  await sleep(5000);
  await dump('after-sign-in');
  const ws = await find(/Al Rayyan Trading/i, { timeout: 3000 });
  if (ws) { await tap(ws.x, ws.y); await sleep(4000); await dump('after-workspace'); }
  for (let i = 0; i < 8; i++) {
    const skip = await find(/^(Skip|Next|Got it|Done|Close|Finish|Not now)/i, { timeout: 1200 });
    if (!skip) break; await tap(skip.x, skip.y); await sleep(1200); await dump('tour-' + i);
  }
});

await step('overview', async () => { await dump('overview'); await swipe(540, 1800, 540, 700, 600); await dump('overview-scrolled'); await swipe(540, 700, 540, 1900, 400); });
for (const tab of ['Invoices', 'Receipts', 'Customers']) {
  await step(tab, async () => {
    await tapText(new RegExp('^' + tab + '$')); await dump(tab);
    const list = await nodes();
    const row = list.filter((x) => x.clickable && x.y > 500 && x.y < 2000 && x.w > 600).sort((a, b) => a.y - b.y)[0];
    if (row) { await tap(row.x, row.y); await dump(tab + '-detail'); await back(); }
  });
}
await step('more', async () => {
  await tapText(/^More$/); const first = await dump('more');
  writeFileSync(`${out}/more-labels.txt`, describe(first));
  for (let i = 0; i < 3; i++) { await swipe(540, 1900, 540, 800, 600); await dump('more-scroll-' + i); }
  await swipe(540, 600, 540, 2100, 300); await swipe(540, 600, 540, 2100, 300);
  const tiles = first.filter((x) => x.clickable && x.y > 300 && x.y < 2150 && (x.desc || x.text)).map((x) => (x.desc || x.text).split('\n')[0]).slice(0, 18);
  for (const t of tiles) await step('tile ' + t, async () => {
    const node = await find(new RegExp('^' + t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')), { timeout: 2000 }); if (!node) return;
    await tap(node.x, node.y); await dump('more-' + t);
    const list = await nodes();
    const sub = list.filter((x) => x.clickable && x.y > 400 && x.y < 2000 && x.w > 600).sort((a, b) => a.y - b.y)[0];
    if (sub) { await tap(sub.x, sub.y); await dump('more-' + t + '-open'); await back(); }
    await back();
  });
});
await step('notifications', async () => { await tapText(/^Overview$/); await tapText(/otification/i); await dump('notifications'); await back(); });
await step('profile', async () => { const p = await find(/profile|account|AD|admin/i, { timeout: 2000 }); if (p) { await tap(p.x, p.y); await dump('profile'); await back(); } });
console.log('Explore done:', n, 'screens');
process.exit(0);
