// Browser recording for the demo: Chrome (headless) is captured frame by frame over the DevTools screencast and encoded live
// to a constant 30 fps H.264 file, so the quality is far better than Playwright's built-in VP8 video. The page gets a soft
// cursor with click ripples, animated title cards and lower-thirds. Scene starts are written to <name>.marks.json so the
// build can line the voice-over up with the picture.
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { ffmpeg, here } from './media.mjs';

const FPS = 30;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const logoSvg = readFileSync(here('../../frontend/public/highersquare-logo.svg'), 'utf8');
const emblemPng = readFileSync(here('../../frontend/public/highersquare-emblem.png')).toString('base64');

/** CSS + script injected into every page: cursor, ripple, title card and lower-third layers. */
const overlayInit = ({ emblem }) => {
  const install = () => {
    if (document.getElementById('demo-layer') || !document.body) return;
    const style = document.createElement('style');
    style.textContent = `
      #demo-layer{position:fixed;inset:0;pointer-events:none;z-index:2147483647;font-family:'Inter','Segoe UI',system-ui,sans-serif}
      #demo-cursor{position:fixed;left:0;top:0;width:28px;height:28px;transform:translate(-4px,-3px);transition:opacity .3s;filter:drop-shadow(0 2px 4px rgba(0,0,0,.35))}
      .demo-ripple{position:fixed;width:16px;height:16px;margin:-8px 0 0 -8px;border-radius:50%;border:3px solid #4FA077;background:rgba(79,160,119,.25);animation:demo-rip .6s ease-out forwards}
      @keyframes demo-rip{to{transform:scale(4.2);opacity:0}}
      #demo-l3{position:fixed;left:48px;top:40px;display:flex;align-items:center;gap:14px;padding:12px 26px 12px 14px;border-radius:999px;
        background:rgba(31,58,68,.92);color:#fff;font-size:24px;font-weight:600;letter-spacing:.2px;box-shadow:0 10px 30px rgba(0,0,0,.25);
        transform:translateX(-130%);transition:transform .7s cubic-bezier(.2,.9,.2,1)}
      #demo-l3.on{transform:none}
      #demo-l3 img{width:34px;height:34px;border-radius:8px}
      #demo-card{position:fixed;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:22px;opacity:0;transition:opacity .6s ease;
        background:radial-gradient(1200px 700px at 30% 20%,#2A8A93 0%,transparent 60%),radial-gradient(900px 600px at 80% 90%,#4FA077 0%,transparent 55%),#1F3A44;color:#fff;overflow:hidden}
      #demo-card.on{opacity:1}
      #demo-card .orb{position:absolute;border-radius:50%;filter:blur(2px);opacity:.18;background:#fff;animation:demo-float 9s ease-in-out infinite}
      @keyframes demo-float{50%{transform:translateY(-40px) scale(1.08)}}
      #demo-card .emb{width:150px;height:150px;border-radius:34px;box-shadow:0 20px 60px rgba(0,0,0,.35);opacity:0;transform:scale(.6) rotate(-8deg)}
      #demo-card.on .emb{animation:demo-pop .9s .15s cubic-bezier(.2,1.4,.3,1) forwards}
      @keyframes demo-pop{to{opacity:1;transform:none}}
      #demo-card h1{margin:0;font-size:76px;font-weight:800;letter-spacing:-1px;opacity:0;transform:translateY(30px)}
      #demo-card h1 span{color:#7FE0A8}
      #demo-card p{margin:0;font-size:32px;font-weight:500;color:#D7EFE5;opacity:0;transform:translateY(24px)}
      #demo-card .kick{font-size:20px;letter-spacing:6px;text-transform:uppercase;color:#9FD8C0;opacity:0}
      #demo-card.on h1{animation:demo-up .8s .45s cubic-bezier(.2,.9,.2,1) forwards}
      #demo-card.on p{animation:demo-up .8s .75s cubic-bezier(.2,.9,.2,1) forwards}
      #demo-card.on .kick{animation:demo-up .8s .2s ease forwards}
      #demo-card .bar{width:0;height:5px;border-radius:3px;background:linear-gradient(90deg,#4FA077,#7FE0A8)}
      #demo-card.on .bar{animation:demo-bar 1.2s .9s ease forwards}
      @keyframes demo-bar{to{width:220px}}
      @keyframes demo-up{to{opacity:1;transform:none}}
      #demo-spot{position:fixed;border-radius:14px;box-shadow:0 0 0 9999px rgba(15,30,36,.45),0 0 0 4px #4FA077;transition:all .6s cubic-bezier(.2,.9,.2,1);opacity:0}
      #demo-spot.on{opacity:1}`;
    document.head.appendChild(style);
    const layer = document.createElement('div');
    layer.id = 'demo-layer';
    layer.innerHTML = `<div id="demo-spot"></div><div id="demo-l3"><img src="data:image/png;base64,${emblem}"><span></span></div>
      <div id="demo-card"><div class="orb" style="width:420px;height:420px;left:-120px;top:-80px"></div>
      <div class="orb" style="width:300px;height:300px;right:-60px;bottom:-40px;animation-delay:-4s"></div>
      <img class="emb" src="data:image/png;base64,${emblem}"><div class="kick"></div><h1></h1><div class="bar"></div><p></p></div>
      <svg id="demo-cursor" viewBox="0 0 28 28"><path d="M3 2 L3 22 L8.5 17 L12.5 26 L16 24.5 L12 15.8 L19.5 15.8 Z" fill="#fff" stroke="#1F3A44" stroke-width="1.6" stroke-linejoin="round"/></svg>`;
    document.body.appendChild(layer);
    const cur = layer.querySelector('#demo-cursor');
    let saved = [960, 540]; try { saved = JSON.parse(sessionStorage.getItem('demo-cursor') || '[960,540]'); } catch {}
    cur.style.left = saved[0] + 'px'; cur.style.top = saved[1] + 'px';
    addEventListener('mousemove', (e) => { cur.style.left = e.clientX + 'px'; cur.style.top = e.clientY + 'px'; try { sessionStorage.setItem('demo-cursor', JSON.stringify([e.clientX, e.clientY])); } catch {} }, true);
    addEventListener('mousedown', (e) => {
      const r = document.createElement('div'); r.className = 'demo-ripple'; r.style.left = e.clientX + 'px'; r.style.top = e.clientY + 'px';
      layer.appendChild(r); setTimeout(() => r.remove(), 700);
    }, true);
  };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', install); else install();
  new MutationObserver(install).observe(document.documentElement, { childList: true, subtree: false });
};

export class WebRecorder {
  constructor(name, { width = 1920, height = 1080 } = {}) {
    this.name = name; this.width = width; this.height = height;
    this.dir = here('work/rec'); mkdirSync(this.dir, { recursive: true });
    this.marks = []; this.mouse = { x: width / 2, y: height / 2 };
  }

  async launch() {
    this.browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--hide-scrollbars', '--force-color-profile=srgb', '--font-render-hinting=none'] });
    this.context = await this.browser.newContext({ viewport: { width: this.width, height: this.height }, deviceScaleFactor: 1, locale: 'en-US', timezoneId: 'Asia/Karachi', colorScheme: 'light' });
    await this.context.addInitScript(overlayInit, { emblem: emblemPng });
    this.page = await this.context.newPage();
    this.page.setDefaultTimeout(20000);
    return this.page;
  }

  /** Starts capturing. Frames arrive only when the page changes; a 30 fps clock repeats the latest one in between. */
  async start() {
    const out = `${this.dir}/${this.name}.mp4`;
    this.enc = spawn(ffmpeg, ['-hide_banner', '-y', '-f', 'image2pipe', '-c:v', 'mjpeg', '-framerate', String(FPS), '-i', '-',
      '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '15', '-pix_fmt', 'yuv420p', '-r', String(FPS), out], { stdio: ['pipe', 'ignore', 'pipe'] });
    this.encErr = ''; this.enc.stderr.on('data', (d) => { this.encErr = (this.encErr + d).slice(-4000); });
    this.encDone = new Promise((r) => this.enc.on('close', r));
    this.cdp = await this.context.newCDPSession(this.page);
    this.latest = null;
    this.cdp.on('Page.screencastFrame', ({ data, sessionId }) => {
      this.latest = Buffer.from(data, 'base64');
      this.cdp.send('Page.screencastFrameAck', { sessionId }).catch(() => {});
    });
    await this.cdp.send('Page.startScreencast', { format: 'jpeg', quality: 92, maxWidth: this.width, maxHeight: this.height, everyNthFrame: 1 });
    while (!this.latest) await sleep(20);
    this.t0 = Date.now(); this.written = 0;
    this.ticker = setInterval(() => {
      const due = Math.floor(((Date.now() - this.t0) / 1000) * FPS);
      while (this.written < due) { this.enc.stdin.write(this.latest); this.written++; }
    }, 1000 / FPS / 2);
  }

  /** Seconds since recording started. */
  now() { return (Date.now() - this.t0) / 1000; }
  mark(id) { this.marks.push({ id, t: this.now() }); console.log(`  [${this.now().toFixed(1)}s] ${id}`); }

  async stop() {
    clearInterval(this.ticker);
    await this.cdp.send('Page.stopScreencast').catch(() => {});
    this.enc.stdin.end();
    const code = await this.encDone;
    if (code !== 0) throw new Error(this.encErr);
    writeFileSync(`${this.dir}/${this.name}.marks.json`, JSON.stringify({ duration: this.written / FPS, marks: this.marks }, null, 2));
    await this.browser.close();
  }

  // ---------- acting ----------

  /** Glides the cursor to a point with an ease-in-out curve. */
  async glide(x, y, ms = 700) {
    const { x: x0, y: y0 } = this.mouse; const steps = Math.max(8, Math.round(ms / 16));
    for (let i = 1; i <= steps; i++) {
      const t = i / steps; const e = t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
      await this.page.mouse.move(x0 + (x - x0) * e, y0 + (y - y0) * e); await sleep(ms / steps);
    }
    this.mouse = { x, y };
  }

  async hover(locator, ms = 700) {
    await locator.scrollIntoViewIfNeeded().catch(() => {});
    const b = await locator.boundingBox(); if (!b) throw new Error('No box for ' + locator);
    await this.glide(b.x + b.width / 2, b.y + Math.min(b.height / 2, 24), ms);
  }

  async click(locator, { pause = 250, ms = 700 } = {}) {
    await this.hover(locator, ms); await sleep(pause);
    await this.page.mouse.down(); await sleep(60); await this.page.mouse.up();
    await sleep(350);
  }

  async type(locator, text, { delay = 38 } = {}) {
    await this.click(locator, { pause: 120 });
    await locator.fill('');
    await locator.pressSequentially(text, { delay });
    await sleep(200);
  }

  /** Scrolls whatever is under the cursor, smoothly, by `dy` pixels. */
  async scroll(dy, ms = 1200) {
    const steps = Math.max(10, Math.round(ms / 25)); let done = 0;
    for (let i = 1; i <= steps; i++) {
      const t = i / steps; const e = t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
      const target = Math.round(dy * e); await this.page.mouse.wheel(0, target - done); done = target; await sleep(ms / steps);
    }
  }

  async wait(ms) { await sleep(ms); }

  // ---------- overlays ----------

  async card({ kick = '', title = '', sub = '' }) {
    await this.page.evaluate(({ kick, title, sub }) => {
      const c = document.getElementById('demo-card'); if (!c) return;
      c.querySelector('.kick').textContent = kick; c.querySelector('h1').innerHTML = title; c.querySelector('p').textContent = sub;
      c.classList.remove('on'); void c.offsetWidth; c.classList.add('on');
    }, { kick, title, sub });
  }
  async uncard() { await this.page.evaluate(() => document.getElementById('demo-card')?.classList.remove('on')); await sleep(650); }

  async lowerThird(text, holdMs = 4200) {
    await this.page.evaluate((text) => { const l = document.getElementById('demo-l3'); if (!l) return; l.querySelector('span').textContent = text; l.classList.add('on'); }, text);
    clearTimeout(this.l3Timer);
    this.l3Timer = setTimeout(() => this.page.evaluate(() => document.getElementById('demo-l3')?.classList.remove('on')).catch(() => {}), holdMs);
  }

  /** Dims the page around an element for a moment, to draw the eye. */
  async spotlight(locator, holdMs = 1800) {
    const b = await locator.boundingBox(); if (!b) return;
    await this.page.evaluate((b) => { const s = document.getElementById('demo-spot'); if (!s) return; const p = 10;
      Object.assign(s.style, { left: b.x - p + 'px', top: b.y - p + 'px', width: b.width + 2 * p + 'px', height: b.height + 2 * p + 'px' }); s.classList.add('on'); }, b);
    await sleep(holdMs);
    await this.page.evaluate(() => document.getElementById('demo-spot')?.classList.remove('on'));
    await sleep(400);
  }
}

export { sleep, logoSvg };
