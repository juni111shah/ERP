// Final edit: lays the voice-over on the recording at each scene mark, mixes in the music (looped, ducked under the voice,
// faded in and out), burns in captions and writes out/<name>.mp4.  Usage: node build.mjs web | mobile
import path from 'node:path';
import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { run, here, probeDuration } from './lib/media.mjs';
import { buildAss } from './lib/captions.mjs';

const which = process.argv[2] ?? 'web';
const script = JSON.parse(readFileSync(here('script.json'), 'utf8'));
// SRC=<folder> builds from a CI output folder (raw-<web|mobile>.mp4, <name>.marks.json, voice/) instead of work/.
const SRC = process.env.SRC;
const voiceDir = SRC ? `${SRC}/voice` : here('work/voice');
const voiceDurations = JSON.parse(readFileSync(`${voiceDir}/durations.json`, 'utf8'));
const settings = {
  web: { video: 'work/rec/web.mp4', marks: 'work/rec/web.marks.json', out: 'out/HigherSquare360-Web-Demo.mp4', caption: {} },
  mobile: { video: 'work/rec/mobile.mp4', marks: 'work/rec/mobile.marks.json', out: 'out/HigherSquare360-Mobile-Demo.mp4', caption: { align: 3, marginR: 110, marginL: 1000, marginV: 70, fontSize: 38 } },
}[which];
if (SRC) { settings.video = `${SRC}/raw-${which}.mp4`; settings.marks = `${SRC}/${which}.marks.json`; }
const abs = (p) => (path.isAbsolute(p) ? p : here(p));
const music = process.env.MUSIC ?? here('assets/music.mp3');
const VO_LEAD = 0.6;   // seconds between a scene starting on screen and its voice-over starting

const hasMusic = existsSync(music);
if (!hasMusic) console.warn(`No music file at ${music}; building with voice only (see README.md).`);
mkdirSync(here('out'), { recursive: true });
mkdirSync(here('work/build'), { recursive: true });

const { duration: recDuration, marks } = JSON.parse(readFileSync(abs(settings.marks), 'utf8'));
const duration = await probeDuration(abs(settings.video)).catch(() => recDuration);
const scenes = script[which].scenes;
const placed = scenes.map((s) => {
  const m = marks.find((x) => x.id === s.id);
  if (!m) throw new Error(`No mark for scene ${s.id} in ${settings.marks}`);
  return { ...s, start: m.t + (m.lead ?? VO_LEAD), duration: voiceDurations[s.id].seconds };
});

// Captions.
const assFile = here(`work/build/${which}.ass`);
writeFileSync(assFile, buildAss(placed.map((p) => ({ start: p.start, duration: p.duration, text: p.vo })), settings.caption));

// Inputs: 0 video, 1 music (looped), 2.. voice clips.
const args = ['-i', abs(settings.video), ...(hasMusic ? ['-stream_loop', '-1', '-i', music] : ['-f', 'lavfi', '-i', 'anullsrc=r=48000:cl=stereo'])];
placed.forEach((p) => args.push('-i', `${voiceDir}/${p.id}.mp3`));
const fadeOut = Math.max(0, duration - 1.2);
const voiceChains = placed.map((p, i) => `[${i + 2}:a]aresample=48000,aformat=channel_layouts=stereo,volume=1.35,adelay=${Math.round(p.start * 1000)}|${Math.round(p.start * 1000)}[v${i}]`);
const vlabels = placed.map((_, i) => `[v${i}]`).join('');
// The subtitles filter wants a path with escaped drive colon and forward slashes.
const assArg = assFile.replace(/\\/g, '/').replace(/^([A-Za-z]):/, '$1\\:');
const filter = [
  `[0:v]subtitles='${assArg}',fade=t=in:st=0:d=0.8,fade=t=out:st=${fadeOut.toFixed(2)}:d=1.2,format=yuv420p[vout]`,
  ...voiceChains,
  `${vlabels}amix=inputs=${placed.length}:normalize=0,atrim=0:${duration.toFixed(2)},apad=whole_dur=${duration.toFixed(2)}[voice]`,
  `[voice]asplit=2[voiceA][voiceKey]`,
  `[1:a]aresample=48000,aformat=channel_layouts=stereo,atrim=0:${duration.toFixed(2)},volume=0.30,afade=t=in:st=0:d=2,afade=t=out:st=${Math.max(0, duration - 3.5).toFixed(2)}:d=3.5[music]`,
  `[music][voiceKey]sidechaincompress=threshold=0.02:ratio=9:attack=30:release=600:makeup=1[ducked]`,
  `[ducked][voiceA]amix=inputs=2:normalize=0,alimiter=limit=0.95[aout]`,
].join(';');
args.push('-filter_complex', filter, '-map', '[vout]', '-map', '[aout]', '-t', duration.toFixed(2),
  '-c:v', 'libx264', '-preset', 'slow', '-crf', '19', '-maxrate', '3M', '-bufsize', '6M', '-profile:v', 'high', '-pix_fmt', 'yuv420p',
  '-c:a', 'aac', '-b:a', '192k', '-movflags', '+faststart', here(settings.out));
console.log(`Building ${settings.out} (${duration.toFixed(1)} s, ${placed.length} voice clips)...`);
await run(args);
console.log(`Done: ${here(settings.out)}`);
