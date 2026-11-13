// Small helpers around the bundled ffmpeg binary.
import ffmpegPath from 'ffmpeg-static';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

export const ffmpeg = ffmpegPath;
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
/** A path inside tools/demo-video. */
export const here = (...p) => path.join(root, ...p);

/** Runs ffmpeg with the given arguments; rejects with the tail of stderr on failure. */
export function run(args, { quiet = true } = {}) {
  return new Promise((resolve, reject) => {
    const p = spawn(ffmpeg, ['-hide_banner', '-y', ...args], { stdio: ['ignore', 'ignore', 'pipe'] });
    let err = '';
    p.stderr.on('data', (d) => { err += d; if (!quiet) process.stderr.write(d); if (err.length > 200000) err = err.slice(-100000); });
    p.on('close', (code) => (code === 0 ? resolve(err) : reject(new Error(`ffmpeg failed (${code}):\n${err.slice(-3000)}`))));
  });
}

/** Duration in seconds of any media file. */
export async function probeDuration(file) {
  const out = await new Promise((resolve) => {
    const p = spawn(ffmpeg, ['-hide_banner', '-i', file], { stdio: ['ignore', 'ignore', 'pipe'] });
    let err = '';
    p.stderr.on('data', (d) => (err += d));
    p.on('close', () => resolve(err));
  });
  const m = out.match(/Duration: (\d+):(\d+):([\d.]+)/);
  if (!m) throw new Error(`No duration for ${file}`);
  return +m[1] * 3600 + +m[2] * 60 + +m[3];
}
