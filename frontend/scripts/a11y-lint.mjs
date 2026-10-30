// Fails when a template has an icon-only <button> without an accessible name, a click handler on a non-interactive element without keyboard support,
// or an input/textarea whose only label is a placeholder.
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
  for (const m of s.matchAll(/<(div|span|tr|li|td)\b([^>]*\(click\)[^>]*)>/g)) {
    if (m[1] === 'li' && /nz-menu-item/.test(m[2])) continue;
    if (/\(click\)="\$event\.stopPropagation\(\)"/.test(m[2])) continue; // only stops bubbling, not an action
    if (!/erpKeyboardClick|\(keydown|\(keyup|tabindex|role="button"/.test(m[2])) problems.push(`${file}:${line(m.index)} <${m[1]}> with (click) is not keyboard accessible (add erpKeyboardClick)`);
  }
  for (const m of s.matchAll(/<(input|textarea)(\s[^>]*)>/g)) {
    if (/aria-label|type="(hidden|checkbox|radio|file)"/.test(m[2]) || !/(^|[\s\]])placeholder="/.test(m[2] + ' ')) continue;
    const before = s.slice(Math.max(0, m.index - 500), m.index);
    const form = before.lastIndexOf('<nz-form-item');
    if (form >= 0 && !before.slice(form).includes('</nz-form-item>')) continue; // inside a labelled form item
    problems.push(`${file}:${line(m.index)} <${m[1]}> has only a placeholder, add aria-label`);
  }
}
if (problems.length) { console.error(problems.join('\n')); process.exit(1); }
console.log('a11y lint: ok');
