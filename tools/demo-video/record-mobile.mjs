// Records the phone walkthrough on a running Android emulator (one screen recording per scene), then lays it into a branded
// 1920x1080 stage with a phone bezel, animated side titles and title cards. Output: work/rec/mobile.mp4 + mobile.marks.json,
// which build.mjs turns into the finished video with voice, music and captions.
import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { find, tap, tapText, type, swipe, sleep, shell, record, nodes, signIn } from './lib/adb.mjs';
import { here, run, probeDuration } from './lib/media.mjs';
import { renderStage, STAGE } from './lib/phone-stage.mjs';
import { WebRecorder } from './lib/recorder.mjs';

const script = JSON.parse(readFileSync(here('script.json'), 'utf8'));
const voice = JSON.parse(readFileSync(here('work/voice/durations.json'), 'utf8'));
const VO_LEAD = 0.6, TAIL = 1.0, XF = 0.6;
const PKG = 'com.erp.erp_mobile';
const dir = here('work/rec'); mkdirSync(dir, { recursive: true });
const stageDir = here('work/stage'); mkdirSync(stageDir, { recursive: true });
/** COMPOSE_ONLY=1 re-lays the clips already in work/rec without recording again. */
const COMPOSE_ONLY = !!process.env.COMPOSE_ONLY;
const need = (id) => VO_LEAD + voice[id].seconds + TAIL;
const sceneOf = (id) => script.mobile.scenes.find((s) => s.id === id);

// ---------- title cards (recorded in Chrome) ----------
async function cardClip(id, card) {
  if (COMPOSE_ONLY && existsSync(`${dir}/mcard-${id}.mp4`)) return `${dir}/mcard-${id}.mp4`;
  const r = new WebRecorder('mcard-' + id);
  const page = await r.launch();
  await page.setContent('<body style="margin:0;background:#1F3A44"></body>');
  await sleep(300);
  await r.start();
  await r.card(card);
  await sleep(need(id) * 1000 + 600);
  await r.stop();
  return `${dir}/mcard-${id}.mp4`;
}

// ---------- phone scenes (recorded on the emulator) ----------
async function phoneScene(id, actions) {
  console.log('scene', id);
  if (!COMPOSE_ONLY) await ensureApp();
  const file = `${dir}/phone-${id}.mp4`;
  if (COMPOSE_ONLY) return file;
  const rec = record('demo-' + id, file);
  await sleep(900);
  const t0 = Date.now();
  try { await actions(); } catch (e) { console.log(`  ! ${id}: ${e.message}`); }
  const left = need(id) * 1000 - (Date.now() - t0);
  if (left > 0) await sleep(left);
  await sleep(400);
  await rec.stop();
  return file;
}


// Fresh start: clear the app so it opens on its intro, then launch it before recording.
if (!COMPOSE_ONLY) {
  await shell(`pm clear ${PKG}`).catch(() => {});
  await shell('settings put system show_touches 1');
  await shell(`monkey -p ${PKG} -c android.intent.category.LAUNCHER 1`);
  await sleep(7000);
}

const clips = [];
/** Scrolls the current list until a node matching `re` is comfortably on screen; returns it (or null). */
async function scrollToFind(re, { tries = 8 } = {}) {
  for (let i = 0; i < tries; i++) {
    const n = await find(re, { timeout: 700 });
    if (n && n.y > 380 && n.y < 2000) return n;
    await swipe(540, 1750, 540, 1050, 650); await sleep(700);
  }
  return null;
}
/** The first list row (a wide, tall, clickable item) below the header, optionally containing `re`. */
async function row(re) {
  const list = (await nodes()).filter((x) => x.clickable && x.y > 480 && x.y < 2050 && x.w > 900 && x.h > 150 && (!re || re.test(`${x.text} ${x.desc}`)));
  return list.sort((a, b) => a.y - b.y)[0] ?? null;
}
const tapNode = async (n, after = 1200) => { if (n) { await tap(n.x, n.y); await sleep(after); } return n; };
/** Taps a bottom-bar tab (the More page has tiles with the same names higher up). */
async function navTab(name, after = 1600) {
  for (let i = 0; i < 6; i++) {
    const n = (await nodes()).find((x) => x.y > 2050 && new RegExp('^' + name + '$').test(`${x.text} ${x.desc}`.trim()));
    if (n) { await tap(n.x, n.y); await sleep(after); return n; }
    await sleep(600);
  }
  throw new Error('No bottom tab ' + name);
}
/** The app's own Back arrow (system Back on a root tab would leave the app). */
const appBack = async () => { const b = await find(/^Back$/, { timeout: 1500 }); if (b) { await tap(b.x, b.y); await sleep(1100); } };
/** Brings the app to the front again if something took it away. */
async function ensureApp() {
  const focus = await shell('dumpsys window | grep -E "mCurrentFocus|mFocusedApp"').catch(() => '');
  if (!focus.includes(PKG)) { await shell(`monkey -p ${PKG} -c android.intent.category.LAUNCHER 1`); await sleep(4000); }
}

clips.push({ id: 'm01-intro', file: await cardClip('m01-intro', { kick: 'Mobile app', title: 'HigherSquare <span>360</span>', sub: 'Your business, in your pocket' }), card: true });

clips.push({ id: 'm02-signin', file: await phoneScene('m02-signin', async () => {
  // Two pages of the app's own intro tour, then Skip.
  await sleep(1200);
  for (let i = 0; i < 2; i++) await tapNode(await find(/^Next$/, { timeout: 2500 }), 2000);
  await tapNode(await find(/^Skip$/, { timeout: 2000 }), 1500);
  await signIn('admin@demo.local', 'DevOnly#2026');
  await sleep(400);
  await tapText(/^Sign in$/, { after: 3000 });
  await tapNode(await find(/^Demo Workspace/, { timeout: 6000 }), 3000);
  // First-run tips: show one step, then skip the rest.
  await tapNode(await find(/^Next$/, { timeout: 2500 }), 1800);
  await tapNode(await find(/^Skip$/, { timeout: 1500 }), 1500);
}) });

clips.push({ id: 'm03-overview', file: await phoneScene('m03-overview', async () => {
  await sleep(1200);
  for (let i = 0; i < 2; i++) await tapNode(await find(/^Next$/, { timeout: 1500 }), 1500);
  await tapNode(await find(/^Finance \d+/, { timeout: 1500 }), 1800);
  await swipe(540, 1700, 540, 900, 800); await sleep(1500);
  await tapNode(await find(/^Line$/, { timeout: 1500 }), 1500);
  await swipe(540, 900, 540, 1800, 700); await sleep(600);
  await tapNode(await find(/^All \d+/, { timeout: 1500 }), 1200);
}) });

clips.push({ id: 'm04-documents', file: await phoneScene('m04-documents', async () => {
  await navTab('Invoices', 1800);
  await tapNode(await row(/INV-2026-0002/) ?? await row(/INV-/), 2400);
  await swipe(540, 1600, 540, 1000, 700); await sleep(1200);
  await appBack();
  await navTab('Receipts', 1800);
  await swipe(540, 1600, 540, 1000, 700); await sleep(1000);
  await navTab('Customers', 1600);
  await tapNode(await row(/Apex/), 2400);
  await swipe(540, 1600, 540, 1000, 700); await sleep(1000);
  await appBack();
}) });

clips.push({ id: 'm05-approvals', file: await phoneScene('m05-approvals', async () => {
  await navTab('More', 1500);
  await tapNode(await scrollToFind(/^Approvals$/), 2000);
  await tapNode(await find(/^My requests$/, { timeout: 1500 }), 1800);
  await tapNode(await row(), 2200);
  await appBack();
  await appBack();
}) });

clips.push({ id: 'm06-myhr', file: await phoneScene('m06-myhr', async () => {
  // HR through the app's search ("Search screens and actions"): employees, then the payroll runs.
  await ensureApp();
  await navTab('Overview', 1200);
  const openBySearch = async (text) => {
    await tapNode(await find(/^Search$/, { timeout: 2500 }), 1400);
    await type(text); await sleep(1600);
    const hit = (await nodes()).filter((x) => !x.cls.includes('EditText') && x.y > 330 && new RegExp('^' + text).test(`${x.text} ${x.desc}`.trim())).sort((p, q) => p.y - q.y)[0];
    await tapNode(hit, 2400);
  };
  await openBySearch('Employees');
  await swipe(540, 1600, 540, 1100, 700); await sleep(1000);
  await appBack();
  await openBySearch('Payroll');
  await tapNode(await row(), 2400);
  await swipe(540, 1600, 540, 1000, 700); await sleep(1200);
  await appBack(); await appBack();
}) });

clips.push({ id: 'm07-more', file: await phoneScene('m07-more', async () => {
  await ensureApp();
  await navTab('Overview', 1200);
  const bell = (await nodes()).find((x) => /^d+$/.test(x.text || x.desc) && x.y < 320);
  if (bell) { await tapNode(bell, 2600); await shell('input keyevent 4'); await sleep(1000); await ensureApp(); }
  await navTab('More', 1400);
  await swipe(540, 600, 540, 2000, 400); await sleep(500);
  for (let i = 0; i < 4; i++) { await swipe(540, 1800, 540, 900, 900); await sleep(900); }
  await tapNode(await scrollToFind(/^Properties$/, { tries: 2 }), 2200);
  await appBack();
}) });

clips.push({ id: 'm08-outro', file: await cardClip('m08-outro', { kick: 'HigherSquare 360', title: 'On the web, <span>and in your pocket</span>', sub: 'Android and iOS' }), card: true });

// ---------- compose ----------
await renderStage(stageDir);
const s = STAGE;
const parts = [];
for (const c of clips) {
  const out = `${dir}/part-${c.id}.mp4`;
  if (c.card) {
    await run(['-i', c.file, '-vf', 'fps=30,format=yuv420p', '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '15', '-an', out]);
  } else {
    const clipDur = await probeDuration(c.file);
    const target = need(c.id) + 3.5;
    const speed = Math.min(3, Math.max(1, clipDur / target));
    if (speed > 1.02) console.log(`  ${c.id}: ${clipDur.toFixed(1)} s sped up x${speed.toFixed(2)}`);
    await run(['-loop', '1', '-i', `${stageDir}/stage-bg.png`, '-i', c.file, '-loop', '1', '-i', `${stageDir}/stage-bezel.png`, '-loop', '1', '-i', `${stageDir}/stage-mask.png`,
      '-filter_complex', `[1:v]setpts=PTS/${speed.toFixed(4)},fps=30,scale=${s.screenW}:${s.screenH}:flags=lanczos,format=rgba[sc];[3:v]scale=${s.screenW}:${s.screenH},format=gray[m];[sc][m]alphamerge[sm];` +
        `[0:v][sm]overlay=${s.screenX}:${s.screenY}:shortest=1[a];[a][2:v]overlay=0:0,format=yuv420p[v]`,
      '-map', '[v]', '-t', (clipDur / speed).toFixed(3), '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '15', '-r', '30', out]);
  }
  parts.push({ ...c, out, dur: await probeDuration(out) });
}

// Chain with cross-fades; a scene's mark is when its clip starts showing.
let chain = '[0:v]'; let offset = 0; const marks = [{ id: parts[0].id, t: 0 }]; const fc = [];
for (let i = 1; i < parts.length; i++) {
  offset += parts[i - 1].dur - XF;
  const outLabel = i === parts.length - 1 ? '[vx]' : `[x${i}]`;
  fc.push(`${chain}[${i}:v]xfade=transition=fade:duration=${XF}:offset=${offset.toFixed(3)}${outLabel}`);
  chain = outLabel;
  marks.push({ id: parts[i].id, t: offset + XF / 2 });
}
const total = offset + parts[parts.length - 1].dur;

// Side titles: each phone scene's label slides in on the right.
const ts = (t) => { const h = Math.floor(t / 3600), m = Math.floor((t % 3600) / 60), sec = (t % 60).toFixed(2).padStart(5, '0'); return `${h}:${String(m).padStart(2, '0')}:${sec}`; };
let ass = `[Script Info]\nScriptType: v4.00+\nPlayResX: 1920\nPlayResY: 1080\nScaledBorderAndShadow: yes\nWrapStyle: 2\n\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n` +
  `Style: Side,Inter,68,&H00FFFFFF,&H00FFFFFF,&H00000000,&H00000000,1,0,0,0,100,100,0,0,1,0,0,7,0,0,0,1\nStyle: Kick,Inter,26,&H00C0D89F,&H00FFFFFF,&H00000000,&H00000000,1,0,0,0,100,100,6,0,1,0,0,7,0,0,0,1\n\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n`;
/** Breaks a long title into two lines near the middle. */
function wrap(t) { if (t.length <= 18) return t; const mid = t.length / 2; let best = -1; for (let i = 0; i < t.length; i++) if (t[i] === ' ' && (best < 0 || Math.abs(i - mid) < Math.abs(best - mid))) best = i; return best < 0 ? t : t.slice(0, best) + '\\N' + t.slice(best + 1); }
marks.forEach((m, i) => {
  const p = parts[i]; if (p.card) return;
  const start = m.t + 0.2, end = m.t + p.dur - XF;
  const n = script.mobile.scenes.findIndex((x) => x.id === m.id);
  ass += `Dialogue: 0,${ts(start)},${ts(end)},Kick,,0,0,0,,{\\fad(300,300)\\move(1010,410,960,410,0,500)}STEP ${n}\n`;
  ass += `Dialogue: 0,${ts(start + 0.15)},${ts(end)},Side,,0,0,0,,{\\fad(300,300)\\move(1010,460,960,460,0,600)}${sceneOf(m.id).label.replace(/,/g, ',').replace(/ (\S+)$/, '\\N$1')}\n`;
});
const assFile = `${dir}/mobile-side.ass`; writeFileSync(assFile, ass);
const assArg = assFile.replace(/\\/g, '/').replace(/^([A-Za-z]):/, '$1\\:');
const inputs = parts.flatMap((p) => ['-i', p.out]);
await run([...inputs, '-filter_complex', `${fc.join(';')};[vx]subtitles='${assArg}'[v]`, '-map', '[v]', '-c:v', 'libx264', '-preset', 'medium', '-crf', '16', '-pix_fmt', 'yuv420p', '-r', '30', `${dir}/mobile.mp4`]);
writeFileSync(`${dir}/mobile.marks.json`, JSON.stringify({ duration: total, marks: marks.map((m, i) => ({ ...m, lead: parts[i].card ? 0.9 : VO_LEAD })) }, null, 2));
console.log(`Recorded work/rec/mobile.mp4 (${total.toFixed(1)} s)`);
process.exit(0);
