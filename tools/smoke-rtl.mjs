#!/usr/bin/env node
// Smoke test for language switching / RTL. Needs the API and `ng serve` running, and playwright-core + a Chromium.
//   WEB=http://localhost:4200 CHROME=/path/to/chrome PW=/path/to/node_modules/playwright-core node tools/smoke-rtl.mjs [screenshot.png]
import { createRequire } from 'module';
const require = createRequire(process.env.PW_ROOT ? process.env.PW_ROOT + '/' : import.meta.url);
const { chromium } = require('playwright-core');

const WEB = process.env.WEB ?? 'http://localhost:4200';
const CHROME = process.env.CHROME ?? '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const shot = process.argv[2] ?? 'rtl-dashboard.png';
const EMAIL = process.env.SEED_ADMIN ?? 'admin@demo.local';
const PASSWORD = process.env.SEED_PASSWORD ?? 'DevOnly#2026';

const browser = await chromium.launch({ executablePath: CHROME, args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1360, height: 860 } });
const T = (id) => page.locator(`[data-testid="${id}"]`);
const attr = (name) => page.evaluate((n) => document.documentElement.getAttribute(n), name);
let failures = 0;
const check = (name, ok, detail = '') => { console.log(`${ok ? 'ok  ' : 'FAIL'} ${name}${detail ? ' - ' + detail : ''}`); if (!ok) failures++; };

await page.goto(`${WEB}/auth/login`);
check('defaults to ltr/en', (await attr('dir')) === 'ltr' && (await attr('lang')) === 'en');
await T('login-email').fill(EMAIL); await T('login-password').fill(PASSWORD); await T('login-submit').click();
await page.waitForURL(`${WEB}/`, { timeout: 15000 });
await T('lang-toggle').waitFor();
await T('lang-toggle').click();
await page.waitForTimeout(800);
check('dir=rtl after switching to Arabic', (await attr('dir')) === 'rtl');
check('lang=ar', (await attr('lang')) === 'ar');
const title = await T('dashboard-title').innerText();
check('dashboard heading is Arabic', title.includes('مرحباً'), title);
const sider = await T('sidebar').boundingBox();
check('sidebar sits on the right', !!sider && sider.x > 600, `x=${sider?.x}`);
await page.screenshot({ path: shot });
await page.reload();
await T('lang-toggle').waitFor({ timeout: 15000 });
check('language persists after reload', (await attr('dir')) === 'rtl');
await T('lang-toggle').click();
await page.waitForTimeout(500);
check('back to ltr', (await attr('dir')) === 'ltr');
await browser.close();
process.exit(failures ? 1 : 0);
