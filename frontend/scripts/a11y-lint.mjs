// Fails when a template has an icon-only <button> without an accessible name.
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

const walk = (d) => readdirSync(d).flatMap((n) => { const p = join(d, n); return statSync(p).isDirectory() ? walk(p) : p.endsWith('.ts') && !p.endsWith('.spec.ts') ? [p] : []; });
const problems = [];
for (const file of walk('src/app')) {
  const s = readFileSync(file, 'utf8');
  const line = (i) => s.slice(0, i).split('\n').length;
  for (const m of s.matchAll(/<button\b([^>]*)>([\s\S]*?)<\/button>/g)) {
    const text = m[2].replace(/<[^>]+>/g, '').replace(/\{\{[\s\S]*?\}\}/g, 'x').trim();
    if (!text && m[2].includes('nz-icon') && !/aria-label|\[attr\.aria-label\]|\btitle=|\[title\]/.test(m[1])) problems.push(`${file}:${line(m.index)} icon-only button has no aria-label`);
  }
}
if (problems.length) { console.error(problems.join('\n')); process.exit(1); }
console.log('a11y lint: ok');
