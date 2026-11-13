// Renders the short social ads in ads.json: voice-over (Microsoft neural voice), footage clips cut from the recorded walkthroughs,
// every frame of the animated stage (stage.html) captured in headless Chrome, sound effects, music, and the final mix.
//   node ads/ads.mjs                 all ads
//   node ads/ads.mjs <ad id> ...     only these
//   STILLS=1.2,5,9 node ads/ads.mjs  only still frames at these seconds (work/ads/stills), for checking layouts
// Footage comes from WEB_SRC (default work/ci-web: raw-web.mp4 + web.marks.json) and MOBILE_SRC (default work/ci-mobile).
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { mkdir, readFile, writeFile, rm, rename, readdir } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
import path from 'node:path';
import { MsEdgeTTS, OUTPUT_FORMAT } from 'msedge-tts';
import { ffmpeg, run, probeDuration, here } from '../lib/media.mjs';

const conf = JSON.parse(await readFile(here('ads/ads.json'), 'utf8'));
const WEB = path.resolve(here(), process.env.WEB_SRC ?? 'work/ci-web');
const MOB = path.resolve(here(), process.env.MOBILE_SRC ?? 'work/ci-mobile');
const WORK = here('work/ads');
const OUT = here('out/ads');
const FPS = 30;
const only = process.argv.slice(2);
const stills = process.env.STILLS ? process.env.STILLS.split(',').map(Number) : null;
await mkdir(WORK, { recursive: true }); await mkdir(OUT, { recursive: true });

// ------------------------------------------------------------------------------------------------ voice
const voiceDir = path.join(WORK, 'voice'); await mkdir(voiceDir, { recursive: true });
const durFile = path.join(voiceDir, 'durations.json');
const durs = existsSync(durFile) ? JSON.parse(await readFile(durFile, 'utf8')) : {};
async function say(key, text) {
  const file = path.join(voiceDir, key + '.mp3');
  const fp = `${conf.voice}|${conf.rate}|${text}`;
  if (existsSync(file) && durs[key]?.fp === fp) return { file, seconds: durs[key].seconds };
  const tmp = path.join(voiceDir, 'tmp-' + key);
  for (let attempt = 1; ; attempt++) {
    try {
      const tts = new MsEdgeTTS();
      await tts.setMetadata(conf.voice, OUTPUT_FORMAT.AUDIO_24KHZ_96KBITRATE_MONO_MP3);
      await rm(tmp, { recursive: true, force: true }); await mkdir(tmp, { recursive: true });
      const { audioFilePath } = await tts.toFile(tmp, text, { rate: conf.rate });
      await rename(audioFilePath, file); tts.close?.(); break;
    } catch (e) {
      if (attempt >= 8) throw e;
      console.log(`  voice ${key}: ${String(e.message ?? e).slice(0, 80)}; retry in ${attempt * 10} s`);
      await new Promise((r) => setTimeout(r, attempt * 10000));
    }
  }
  await rm(tmp, { recursive: true, force: true });
  // Trim the service's leading and trailing silence so lines land exactly on their animation.
  const trimmed = file.replace('.mp3', '.wav');
  await run(['-i', file, '-af', 'silenceremove=start_periods=1:start_threshold=-45dB,areverse,silenceremove=start_periods=1:start_threshold=-45dB,areverse', trimmed]);
  durs[key] = { fp, seconds: await probeDuration(trimmed) };
  await writeFile(durFile, JSON.stringify(durs, null, 2));
  await new Promise((r) => setTimeout(r, 1500));
  return { file, seconds: durs[key].seconds };
}

// ------------------------------------------------------------------------------------------------ timeline per cut
const VO_LEAD = 0.25;   // voice starts this long after a scene's content appears
const cuts = {};
for (const [name, list] of Object.entries(conf.cuts)) {
  const scenes = [];
  for (const [i, s] of list.entries()) {
    const v = await say(`${name}-${i + 1}`, s.vo);
    const enter = s.in === 'wipe' || s.in === 'circle' ? 0.275 : 0;
    const dur = Math.max(s.min, enter + VO_LEAD + v.seconds + 0.45);
    scenes.push({ ...s, dur: +dur.toFixed(3), voFile: path.join(voiceDir, `${name}-${i + 1}.wav`), voAt: enter + VO_LEAD, voDur: v.seconds });
  }
  cuts[name] = scenes;
  console.log(`cut ${name}: ${scenes.map((s) => s.dur.toFixed(2)).join(' + ')} = ${scenes.reduce((a, s) => a + s.dur, 0).toFixed(2)} s`);
}

// ------------------------------------------------------------------------------------------------ footage clips (JPEG frame sequences)
const webMarks = JSON.parse(await readFile(path.join(WEB, 'web.marks.json'), 'utf8')).marks;
const mobMarks = JSON.parse(await readFile(path.join(MOB, 'mobile.marks.json'), 'utf8')).marks;
const clips = {};
for (const [name, c] of Object.entries(conf.clips)) {
  const dir = path.join(WORK, 'clips', name);
  let src, ss, vf, w, h;
  if (c.src === 'web') {
    const m = webMarks.find((x) => x.id === c.mark); if (!m) throw new Error('No mark ' + c.mark);
    src = path.join(WEB, 'raw-web.mp4'); ss = m.t + c.offset; vf = `fps=${FPS}`; w = 1920; h = 1080;
  } else {
    // Prefer the phone's own full-resolution screen recording when the CI run published it; else cut the screen out of the stage video.
    const rec = path.join(MOB, 'rec', `${c.scene}.mp4`);
    if (existsSync(rec)) { src = rec; ss = c.recOffset ?? c.offset; vf = `fps=${FPS},scale=720:-2`; w = 720; h = 1600; }
    else {
      const m = mobMarks.find((x) => x.id === c.scene); if (!m) throw new Error('No mark ' + c.scene);
      src = path.join(MOB, 'raw-mobile.mp4'); ss = m.t + c.offset; vf = `fps=${FPS},crop=426:946:300:66,scale=852:1892:flags=lanczos`; w = 852; h = 1892;
    }
  }
  const stamp = `${src}|${ss}|${c.dur}|${vf}`;
  const stampFile = path.join(dir, 'stamp.txt');
  if (!(existsSync(stampFile) && (await readFile(stampFile, 'utf8')) === stamp)) {
    await rm(dir, { recursive: true, force: true }); await mkdir(dir, { recursive: true });
    await run(['-ss', String(ss), '-t', String(c.dur), '-i', src, '-vf', vf, '-q:v', '3', path.join(dir, 'f%04d.jpg')]);
    await writeFile(stampFile, stamp);
  }
  const frames = (await readdir(dir)).filter((f) => f.endsWith('.jpg')).length;
  clips[name] = { url: pathToFileURL(dir).href, frames, w, h };
}
console.log('clips:', Object.entries(clips).map(([k, v]) => `${k}(${v.frames})`).join(' '));

// ------------------------------------------------------------------------------------------------ sound effects
const sfxDir = path.join(WORK, 'sfx'); await mkdir(sfxDir, { recursive: true });
const SFX = {
  whoosh: ['anoisesrc=d=0.75:c=pink:r=44100:a=0.7', 'highpass=f=350,lowpass=f=6500,afade=t=in:d=0.42:curve=exp,afade=t=out:st=0.42:d=0.33,volume=0.55'],
  'whoosh-soft': ['anoisesrc=d=0.5:c=pink:r=44100:a=0.6', 'highpass=f=600,lowpass=f=7000,afade=t=in:d=0.25:curve=exp,afade=t=out:st=0.25:d=0.25,volume=0.35'],
  pop: ["aevalsrc=exprs='0.55*sin(2*PI*(1100-3500*t)*t)*exp(-t*32)':d=0.16:s=44100", 'volume=1'],
  'pop-big': ["aevalsrc=exprs='0.7*sin(2*PI*(620-1300*t)*t)*exp(-t*14)':d=0.32:s=44100", 'volume=1'],
  boom: ["aevalsrc=exprs='0.95*sin(2*PI*(95-55*t)*t)*exp(-t*3.2)+0.25*sin(2*PI*190*t)*exp(-t*9)':d=1.4:s=44100", 'volume=1'],
  'boom-soft': ["aevalsrc=exprs='0.6*sin(2*PI*(95-55*t)*t)*exp(-t*3.5)':d=1.2:s=44100", 'volume=1'],
  shimmer: ["aevalsrc=exprs='0.11*(sin(2*PI*2093*t)+sin(2*PI*2637*t)*exp(-t*0.5)+sin(2*PI*3136*t)*exp(-t))*exp(-t*2.6)':d=1.3:s=44100", 'volume=1'],
  ding: ["aevalsrc=exprs='0.3*(sin(2*PI*1318.5*t)+0.6*sin(2*PI*1975.5*t))*exp(-t*4.5)':d=0.9:s=44100", 'volume=1'],
  click: ["aevalsrc=exprs='0.7*sin(2*PI*2600*t)*exp(-t*140)+0.3*sin(2*PI*900*t)*exp(-t*90)':d=0.06:s=44100", 'volume=1'],
  scratch: ['anoisesrc=d=0.35:c=white:r=44100:a=0.45', 'bandpass=f=2600:w=1800,afade=t=in:d=0.03,afade=t=out:st=0.06:d=0.29,volume=0.6'],
  hit: ["aevalsrc=exprs='0.95*sin(2*PI*(130-220*t)*t)*exp(-t*13)':d=0.45:s=44100", 'volume=1'],
};
for (const [k, [src, af]] of Object.entries(SFX)) {
  const f = path.join(sfxDir, k + '.wav');
  if (!existsSync(f)) await run(['-f', 'lavfi', '-i', src, '-af', af, '-ac', '2', f]);
}

// ------------------------------------------------------------------------------------------------ render
const browser = await chromium.launch({ channel: 'chrome', args: ['--disable-gpu-vsync', '--force-color-profile=srgb', '--hide-scrollbars'] });
const stage = pathToFileURL(here('ads/stage.html')).href;

async function openStage(ad) {
  const page = await browser.newPage({ viewport: { width: ad.w, height: ad.h }, deviceScaleFactor: 1 });
  await page.goto(stage);
  await page.evaluate(async () => {
    for (const f of ['400 20px Inter', '500 20px Inter', '600 20px Inter', '700 20px Inter', '800 20px Inter', '900 20px Inter', '700 20px "Noto Sans Arabic"']) await document.fonts.load(f, 'Aa العربية');
  });
  const info = await page.evaluate((c) => window.setup(c), { w: ad.w, h: ad.h, scenes: cuts[ad.cut].map(({ voFile, ...s }) => s), clips });
  return { page, info };
}

for (const ad of conf.ads) {
  if (only.length && !only.includes(ad.id)) continue;
  const { page, info } = await openStage(ad);
  if (stills) {
    const dir = path.join(WORK, 'stills'); await mkdir(dir, { recursive: true });
    for (const t of stills) { await page.evaluate((x) => window.render(x), t); await page.screenshot({ path: path.join(dir, `${ad.id}-${t}.jpg`), type: 'jpeg', quality: 85 }); }
    console.log(`${ad.id}: stills at ${stills.join(', ')} (total ${info.total.toFixed(2)} s, ${info.aspect})`);
    await page.close();
    continue;
  }
  const n = Math.round(info.total * FPS);
  const silent = path.join(WORK, ad.id + '.video.mp4');
  console.log(`${ad.id}: ${n} frames (${info.total.toFixed(2)} s, ${info.aspect})`);
  const enc = spawn(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-y', '-f', 'image2pipe', '-framerate', String(FPS), '-c:v', 'mjpeg', '-i', '-',
    '-c:v', 'libx264', '-preset', 'slow', '-crf', '17', '-pix_fmt', 'yuv420p', '-r', String(FPS), '-movflags', '+faststart', silent], { stdio: ['pipe', 'inherit', 'inherit'] });
  const done = new Promise((res, rej) => enc.on('close', (c) => (c === 0 ? res() : rej(new Error('encoder ' + c)))));
  const t0 = Date.now();
  for (let i = 0; i < n; i++) {
    await page.evaluate((x) => window.render(x), i / FPS);
    const buf = await page.screenshot({ type: 'jpeg', quality: 93 });
    if (!enc.stdin.write(buf)) await new Promise((r) => enc.stdin.once('drain', r));
    if (i % 150 === 0) process.stdout.write(`  ${i}/${n} (${((Date.now() - t0) / 1000).toFixed(0)} s)\n`);
  }
  enc.stdin.end(); await done; await page.close();

  // ---------------------------------------------------------------------------------------------- audio
  const total = info.total;
  const inputs = [], filters = [], vo = [], fx = [];
  let idx = 0;
  const music = conf.music;
  const hasMusic = existsSync(here(music.file));
  if (hasMusic) inputs.push('-ss', String(music.start), '-t', String(total + 0.5), '-i', here(music.file));
  else inputs.push('-f', 'lavfi', '-t', String(total + 0.5), '-i', 'anullsrc=r=44100:cl=stereo');
  const mIdx = idx++;
  let t = 0;
  for (const s of cuts[ad.cut]) {
    inputs.push('-i', s.voFile);
    const ms = Math.round((t + s.voAt) * 1000);
    filters.push(`[${idx}:a]aresample=44100,aformat=channel_layouts=stereo,adelay=${ms}|${ms},volume=1.5[v${idx}]`); vo.push(`[v${idx}]`); idx++;
    t += s.dur;
  }
  for (const e of info.events) {
    if (e.t > total) continue;
    inputs.push('-i', path.join(sfxDir, e.kind + '.wav'));
    const ms = Math.max(0, Math.round(e.t * 1000));
    filters.push(`[${idx}:a]aresample=44100,aformat=channel_layouts=stereo,adelay=${ms}|${ms}[x${idx}]`); fx.push(`[x${idx}]`); idx++;
  }
  filters.push(`${vo.join('')}amix=inputs=${vo.length}:normalize=0,apad=whole_dur=${total}[vo]`);
  filters.push(`[vo]asplit=2[vo1][vok]`);
  filters.push(`[${mIdx}:a]aresample=44100,aformat=channel_layouts=stereo,volume=${music.volume},afade=t=in:d=0.3,afade=t=out:st=${(total - 1.6).toFixed(2)}:d=1.5[m]`);
  filters.push(`[m][vok]sidechaincompress=threshold=0.03:ratio=5:attack=15:release=380:makeup=1[md]`);
  filters.push(`${fx.join('')}amix=inputs=${fx.length}:normalize=0,volume=0.6,apad=whole_dur=${total}[fx]`);
  filters.push(`[vo1][md][fx]amix=inputs=3:normalize=0,atrim=0:${total.toFixed(3)},loudnorm=I=-14:TP=-1.5:LRA=11,aresample=48000[a]`);
  const mixed = path.join(WORK, ad.id + '.audio.wav');
  await run([...inputs, '-filter_complex', filters.join(';'), '-map', '[a]', mixed]);
  const out = path.join(OUT, ad.id + '.mp4');
  await run(['-i', silent, '-i', mixed, '-map', '0:v', '-map', '1:a', '-c:v', 'copy', '-c:a', 'aac', '-b:a', '192k', '-shortest', '-movflags', '+faststart', out]);
  console.log(`  -> ${path.relative(here(), out)} in ${((Date.now() - t0) / 1000).toFixed(0)} s`);
}
await browser.close();
process.exit(0);
