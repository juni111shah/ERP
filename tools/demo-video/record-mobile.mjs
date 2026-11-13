// Records the phone walkthrough on a running Android emulator (one screen recording per scene), then lays it into a branded
// 1920x1080 stage with a phone bezel, animated side titles and title cards. Output: work/rec/mobile.mp4 + mobile.marks.json,
// which build.mjs turns into the finished video with voice, music and captions.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { find, tap, tapText, type, back, hideKeyboard, swipe, sleep, shell, record, nodes } from './lib/adb.mjs';
import { here, run, probeDuration } from './lib/media.mjs';
import { renderStage, STAGE } from './lib/phone-stage.mjs';
import { WebRecorder } from './lib/recorder.mjs';

const script = JSON.parse(readFileSync(here('script.json'), 'utf8'));
const voice = JSON.parse(readFileSync(here('work/voice/durations.json'), 'utf8'));
const VO_LEAD = 0.6, TAIL = 1.0, XF = 0.6;
const PKG = 'com.erp.erp_mobile';
const dir = here('work/rec'); mkdirSync(dir, { recursive: true });
const stageDir = here('work/stage'); mkdirSync(stageDir, { recursive: true });
const need = (id) => VO_LEAD + voice[id].seconds + TAIL;
const sceneOf = (id) => script.mobile.scenes.find((s) => s.id === id);

// ---------- title cards (recorded in Chrome) ----------
async function cardClip(id, card) {
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
  const file = `${dir}/phone-${id}.mp4`;
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

const scrollDown = (px = 900, ms = 700) => swipe(540, 1700, 540, 1700 - px, ms);
const scrollUp = (px = 900, ms = 500) => swipe(540, 700, 540, 700 + px, ms);
const tapIf = async (re, opts) => { const n = await find(re, { timeout: 2500, ...opts }); if (n) { await tap(n.x, n.y); await sleep(opts?.after ?? 1200); } return n; };

// Fresh start: clear the app so it opens on its intro, then launch it before recording.
await shell(`pm clear ${PKG}`).catch(() => {});
await shell('settings put system show_touches 1');
await shell(`monkey -p ${PKG} -c android.intent.category.LAUNCHER 1`);
await sleep(7000);

const clips = [];
clips.push({ id: 'm01-intro', file: await cardClip('m01-intro', { kick: 'Mobile app', title: 'HigherSquare <span>360</span>', sub: 'Your business, in your pocket' }), card: true });

clips.push({ id: 'm02-signin', file: await phoneScene('m02-signin', async () => {
  // The app's own animated intro, briefly, then sign in.
  await sleep(1500);
  for (let i = 0; i < 6 && !(await find(/Sign in to your workspace/, { timeout: 400 })); i++) {
    const skip = await find(/^Skip/i, { timeout: 600 });
    if (i >= 1 && skip) { await tap(skip.x, skip.y); await sleep(1500); break; }
    const next = await find(/^(Next|Continue|Get started)/i, { timeout: 800 });
    if (next) { await tap(next.x, next.y); await sleep(1600); } else { await swipe(900, 1300, 150, 1300, 350); await sleep(1400); }
  }
  await tapText(/^Email/, { cls: 'EditText' }); await type('admin@demo.local'); await hideKeyboard();
  await tapText(/^Password/, { cls: 'EditText' }); await type('DevOnly#2026'); await hideKeyboard();
  await sleep(500);
  await tapText(/^Sign in$/, { after: 4500 });
  for (let i = 0; i < 6; i++) { const t = await find(/^(Skip|Got it|Done|Not now|Close)/i, { timeout: 1200 }); if (!t) break; await tap(t.x, t.y); await sleep(1000); }
}) });

clips.push({ id: 'm03-overview', file: await phoneScene('m03-overview', async () => {
  await sleep(1800); await scrollDown(800, 900); await sleep(1600); await scrollDown(800, 900); await sleep(1600); await scrollUp(1700, 700); await sleep(800);
}) });

clips.push({ id: 'm04-documents', file: await phoneScene('m04-documents', async () => {
  await tapText(/^Invoices$/, { after: 1800 });
  const rows = (await nodes()).filter((x) => x.clickable && x.y > 500 && x.y < 2000 && x.w > 600).sort((a, b) => a.y - b.y);
  if (rows[0]) { await tap(rows[0].x, rows[0].y); await sleep(2500); await scrollDown(700, 800); await sleep(1200); await back(); }
  await tapText(/^Receipts$/, { after: 2000 });
  await tapText(/^Customers$/, { after: 1800 });
  const c = (await nodes()).filter((x) => x.clickable && x.y > 500 && x.y < 2000 && x.w > 600).sort((a, b) => a.y - b.y)[1];
  if (c) { await tap(c.x, c.y); await sleep(2500); await back(); }
}) });

clips.push({ id: 'm05-approvals', file: await phoneScene('m05-approvals', async () => {
  await tapText(/^More$/, { after: 1500 });
  await tapText(/^Approvals/i, { after: 2200 });
  const row = (await nodes()).filter((x) => x.clickable && x.y > 450 && x.y < 2000 && x.w > 600).sort((a, b) => a.y - b.y)[0];
  if (row) { await tap(row.x, row.y); await sleep(2500); await back(); }
  await back();
}) });

clips.push({ id: 'm06-myhr', file: await phoneScene('m06-myhr', async () => {
  await tapIf(/^More$/);
  await tapText(/^My HR|^Self.?service|^My leave/i, { after: 2200 });
  await scrollDown(700, 800); await sleep(1500);
  await tapIf(/payslip/i, { after: 2200 }); await back();
  await back();
}) });

clips.push({ id: 'm07-more', file: await phoneScene('m07-more', async () => {
  await tapIf(/^Overview$/);
  await tapIf(/otification/i, { after: 2200 }); await back();
  await tapText(/^More$/, { after: 1800 });
  await scrollDown(900, 1200); await sleep(1500); await scrollDown(900, 1200); await sleep(1500); await scrollUp(1800, 900);
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
    await run(['-loop', '1', '-i', `${stageDir}/stage-bg.png`, '-i', c.file, '-loop', '1', '-i', `${stageDir}/stage-bezel.png`, '-loop', '1', '-i', `${stageDir}/stage-mask.png`,
      '-filter_complex', `[1:v]fps=30,scale=${s.screenW}:${s.screenH}:flags=lanczos,format=rgba[sc];[3:v]scale=${s.screenW}:${s.screenH},format=gray[m];[sc][m]alphamerge[sm];` +
        `[0:v][sm]overlay=${s.screenX}:${s.screenY}:shortest=1[a];[a][2:v]overlay=0:0,format=yuv420p[v]`,
      '-map', '[v]', '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '15', '-r', '30', out]);
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
let ass = `[Script Info]\nScriptType: v4.00+\nPlayResX: 1920\nPlayResY: 1080\nScaledBorderAndShadow: yes\n\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n` +
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
