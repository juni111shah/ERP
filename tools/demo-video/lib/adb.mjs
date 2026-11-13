// Drives an Android device/emulator over adb: find elements by their accessibility text (uiautomator), tap, type, swipe,
// take screenshots and record the screen.
import { spawn, execFile } from 'node:child_process';
import { promisify } from 'node:util';
import path from 'node:path';

const pexec = promisify(execFile);
const ADB = process.env.ADB ?? (process.env.ANDROID_HOME ? path.join(process.env.ANDROID_HOME, 'platform-tools', process.platform === 'win32' ? 'adb.exe' : 'adb') : 'adb');
export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
export const adb = async (...args) => (await pexec(ADB, args, { maxBuffer: 64 * 1024 * 1024, encoding: 'buffer' })).stdout;
export const shell = async (cmd) => (await adb('shell', cmd)).toString();

/** Every node on screen: { text, desc, hint, cls, clickable, x, y, w, h } (x/y = centre). */
export async function nodes() {
  for (let attempt = 0; attempt < 4; attempt++) {
    try {
      const xml = (await adb('exec-out', 'uiautomator', 'dump', '--compressed', '/dev/tty')).toString();
      const list = [];
      for (const m of xml.matchAll(/<node ([^>]+?)\/?>/g)) {
        const a = Object.fromEntries([...m[1].matchAll(/([\w-]+)="([^"]*)"/g)].map((x) => [x[1], x[2].replace(/&#10;/g, '\n').replace(/&amp;/g, '&').replace(/&quot;/g, '"').replace(/&apos;/g, "'")]));
        const b = a.bounds?.match(/\[(\d+),(\d+)\]\[(\d+),(\d+)\]/); if (!b) continue;
        const [x1, y1, x2, y2] = b.slice(1).map(Number);
        list.push({ text: a.text ?? '', desc: a['content-desc'] ?? '', hint: a.hint ?? '', cls: a.class ?? '', clickable: a.clickable === 'true', scrollable: a.scrollable === 'true', x: (x1 + x2) >> 1, y: (y1 + y2) >> 1, w: x2 - x1, h: y2 - y1 });
      }
      if (list.length) return list;
    } catch { /* uiautomator can fail while the screen is animating */ }
    await sleep(700);
  }
  return [];
}

const label = (n) => `${n.text} ${n.desc} ${n.hint}`.trim();

/** The first node whose text, description or hint matches. */
export async function find(re, { timeout = 8000, cls } = {}) {
  const until = Date.now() + timeout;
  do {
    const hit = (await nodes()).find((n) => re.test(label(n)) && n.w > 0 && (!cls || n.cls.includes(cls)));
    if (hit) return hit;
    await sleep(500);
  } while (Date.now() < until);
  return null;
}

/** Text fields on screen, top to bottom (Flutter fields often carry no label for uiautomator). */
export async function fields() { return (await nodes()).filter((n) => n.cls.includes('EditText')).sort((p, q) => p.y - q.y); }
/** Signs in on the app's sign-in screen. */
export async function signIn(email, password) {
  let f = await fields();
  for (let i = 0; i < 10 && f.length < 2; i++) { await sleep(700); f = await fields(); }
  if (f.length < 2) throw new Error('No sign-in fields');
  await tap(f[0].x, f[0].y); await sleep(400);
  if (f[0].text) { await shell('input keyevent KEYCODE_MOVE_END'); await shell('input keyevent ' + Array(f[0].text.length + 2).fill(67).join(' ')); }
  await type(email); await sleep(300);
  await tap(f[1].x, f[1].y); await sleep(400); await type(password); await sleep(300);
}

export async function tap(x, y) { await shell(`input tap ${Math.round(x)} ${Math.round(y)}`); }
export async function tapText(re, opts = {}) {
  const n = await find(re, opts); if (!n) throw new Error(`Not on screen: ${re}`);
  await tap(n.x, n.y); await sleep(opts.after ?? 900); return n;
}
export async function type(text) { await shell(`input text ${JSON.stringify(text.replace(/ /g, '%s'))}`); }
export async function swipe(x1, y1, x2, y2, ms = 500) { await shell(`input swipe ${x1} ${y1} ${x2} ${y2} ${ms}`); }
export async function back() { await shell('input keyevent 4'); await sleep(900); }
export async function hideKeyboard() {
  const ime = await shell('dumpsys input_method').catch(() => '');
  if (/mInputShown=true/.test(ime)) { await shell('input keyevent 4'); await sleep(500); }
}
export async function screencap(file) { const png = await adb('exec-out', 'screencap', '-p'); const { writeFileSync } = await import('node:fs'); writeFileSync(file, png); }

/** Starts `adb shell screenrecord` into /sdcard/<name>.mp4 (at most 175 s). Returns stop(), which pulls the file to `localFile`. */
export function record(name, localFile, { bitRate = 16000000 } = {}) {
  const remote = `/sdcard/${name}.mp4`;
  const p = spawn(ADB, ['shell', 'screenrecord', '--bit-rate', String(bitRate), '--time-limit', '175', remote], { stdio: 'ignore' });
  const done = new Promise((r) => p.on('close', r));
  return {
    async stop() {
      await shell('pkill -INT screenrecord').catch(() => {});
      await Promise.race([done, sleep(8000)]);
      await sleep(1200);
      await adb('pull', remote, localFile);
      await shell(`rm ${remote}`).catch(() => {});
    },
  };
}

export const describe = (list) => list.filter((n) => label(n)).map((n) => `${n.cls.split('.').pop()}${n.clickable ? '*' : ''} "${label(n).replace(/\s+/g, ' ').slice(0, 80)}" @${n.x},${n.y} ${n.w}x${n.h}`).join('\n');
