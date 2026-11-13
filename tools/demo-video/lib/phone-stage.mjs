// Renders the stage for the phone video with Chrome: a branded 1920x1080 background and a phone bezel PNG whose screen is a
// transparent hole. The emulator recording is laid between the two, so its square corners hide under the bezel.
import { chromium } from 'playwright';
import { readFileSync } from 'node:fs';
import { here } from './media.mjs';

export const STAGE = { width: 1920, height: 1080, screenX: 300, screenY: 66, screenW: 426, screenH: 946, bezel: 14 };

export async function renderStage(dir) {
  const emblem = readFileSync(here('../../frontend/public/highersquare-mark.svg')).toString('base64');
  const s = STAGE; const b = s.bezel;
  const browser = await chromium.launch({ channel: process.env.PW_CHANNEL ?? 'chrome' });
  const page = await browser.newPage({ viewport: { width: s.width, height: s.height } });
  const font = `font-family:'Inter','Segoe UI',system-ui,sans-serif`;
  await page.setContent(`<body style="margin:0;width:${s.width}px;height:${s.height}px;overflow:hidden;${font};
      background:radial-gradient(1100px 700px at 15% 10%,#2A8A93 0%,transparent 60%),radial-gradient(900px 600px at 90% 95%,#4FA077 0%,transparent 55%),#1F3A44">
    <div style="position:absolute;left:${s.screenX - b - 40}px;top:${s.screenY - b - 40}px;width:${s.screenW + 2 * b + 80}px;height:${s.screenH + 2 * b + 80}px;border-radius:90px;background:radial-gradient(closest-side,rgba(0,0,0,.35),transparent)"></div>
    <div style="position:absolute;left:880px;top:84px;display:flex;align-items:center;gap:16px;color:#fff">
      <img src="data:image/svg+xml;base64,${emblem}" style="width:58px;height:58px;border-radius:14px">
      <div style="font-size:34px;font-weight:800">HigherSquare <span style="color:#7FE0A8">360</span><div style="font-size:16px;font-weight:600;letter-spacing:4px;color:#9FD8C0;margin-top:2px">MOBILE APP</div></div>
    </div></body>`);
  await page.screenshot({ path: `${dir}/stage-bg.png` });
  await page.setContent(`<body style="margin:0;background:transparent">
    <div style="position:absolute;left:${s.screenX - b}px;top:${s.screenY - b}px;width:${s.screenW}px;height:${s.screenH}px;border:${b}px solid #0d1417;border-radius:${58}px;
      box-shadow:0 0 0 2px #3b4a50, 0 30px 80px rgba(0,0,0,.45)"></div>
    <div style="position:absolute;left:${s.screenX + s.screenW / 2 - 9}px;top:${s.screenY + 12}px;width:18px;height:18px;border-radius:50%;background:#0d1417"></div></body>`);
  await page.screenshot({ path: `${dir}/stage-bezel.png`, omitBackground: true });
  // Rounded mask for the screen recording (white = visible), used with ffmpeg alphamerge.
  await page.setViewportSize({ width: s.screenW, height: s.screenH });
  await page.setContent(`<body style="margin:0;background:#000"><div style="width:${s.screenW}px;height:${s.screenH}px;border-radius:${58 - b}px;background:#fff"></div></body>`);
  await page.screenshot({ path: `${dir}/stage-mask.png` });
  await browser.close();
}
