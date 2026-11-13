// Turns every voice-over line in script.json into an MP3 with a Microsoft neural voice and writes work/voice/durations.json.
// Only the script text is sent to Microsoft's speech service. Usage: node voice.mjs [web|mobile] (default both).
import { MsEdgeTTS, OUTPUT_FORMAT } from 'msedge-tts';
import { mkdir, readFile, writeFile, rename, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { probeDuration, here } from './lib/media.mjs';

const script = JSON.parse(await readFile(here('script.json'), 'utf8'));
const only = process.argv[2];
const outDir = here('work/voice');
await mkdir(outDir, { recursive: true });
const durationsFile = `${outDir}/durations.json`;
const durations = existsSync(durationsFile) ? JSON.parse(await readFile(durationsFile, 'utf8')) : {};

for (const video of ['web', 'mobile']) {
  if (only && only !== video) continue;
  for (const scene of script[video].scenes) {
    const file = `${outDir}/${scene.id}.mp3`;
    const key = `${scene.id}`;
    const fingerprint = `${script.voice}|${script.rate}|${scene.vo}`;
    if (existsSync(file) && durations[key]?.fingerprint === fingerprint) continue;
    const tmp = `${outDir}/tmp-${scene.id}`;
    for (let attempt = 1; ; attempt++) {
      try {
        const tts = new MsEdgeTTS();
        await tts.setMetadata(script.voice, OUTPUT_FORMAT.AUDIO_24KHZ_96KBITRATE_MONO_MP3);
        await rm(tmp, { recursive: true, force: true });
        await mkdir(tmp, { recursive: true });
        const { audioFilePath } = await tts.toFile(tmp, scene.vo, { rate: script.rate });
        await rename(audioFilePath, file);
        tts.close?.();
        break;
      } catch (e) {
        // The free service rate-limits bursts (HTTP 429): wait longer each time.
        if (attempt >= 8) throw e;
        console.log(`  ${scene.id}: ${String(e.message ?? e).slice(0, 80)}; retrying in ${attempt * 10} s`);
        await new Promise((r) => setTimeout(r, attempt * 10000));
      }
    }
    await rm(tmp, { recursive: true, force: true });
    durations[key] = { seconds: await probeDuration(file), fingerprint };
    await writeFile(durationsFile, JSON.stringify(durations, null, 2));
    await new Promise((r) => setTimeout(r, 2500));
    console.log(`${scene.id}  ${durations[key].seconds.toFixed(1)} s`);
  }
}
await writeFile(durationsFile, JSON.stringify(durations, null, 2));
console.log('Voice done.');
process.exit(0);
