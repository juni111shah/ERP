// Fails when a template has an icon-only <button> without an accessible name, a click handler on a non-interactive element without keyboard support,
// an input/textarea whose only visible label is a placeholder, a code typed as a number, or a screen that skips the shared form and loading rules.
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

const walk = (d) => readdirSync(d).flatMap((n) => { const p = join(d, n); return statSync(p).isDirectory() ? walk(p) : p.endsWith('.ts') && !p.endsWith('.spec.ts') ? [p] : []; });
const problems = [];
// Waiting on an action with nothing to lay out, not on content.
const SPIN_ALLOWED = ['pages/register-verify.page.ts'];
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
    if (/type="(hidden|checkbox|radio|file)"/.test(m[2]) || !/(^|[\s\]])placeholder="/.test(m[2] + ' ')) continue;
    const before = s.slice(Math.max(0, m.index - 500), m.index);
    const form = before.lastIndexOf('<nz-form-label');
    if (form >= 0 && !before.slice(form).includes('</nz-form-control>')) continue; // inside a labelled form item
    if (/search|scan|find|quick|\btoolbar\b/i.test(m[2])) continue; // a search or filter box above a list is named by its aria-label
    if (before.includes('line-head')) continue; // a line of a document form is labelled by the head row above the lines
    if (/<td\b[^>]*>\s*$/.test(before)) continue; // a table cell is labelled by its column header
    const label = before.lastIndexOf('<label');
    if (label >= 0 && !before.slice(label).includes('</label>')) continue; // wrapped in a label
    const id = m[2].match(/\bid="([^"]+)"/)?.[1];
    if (id && s.includes(`for="${id}"`)) continue; // has its own label
    problems.push(`${file}:${line(m.index)} <${m[1]}> uses a placeholder as its label, add a visible label`);
  }
  for (const m of s.matchAll(/<input\b[^>]*type="number"[^>]*>/g)) {
    if (/(code|zip|postal|otp|pin|phone|iban)/i.test(m[0])) problems.push(`${file}:${line(m.index)} a code is not a quantity: use inputmode="numeric" text, not type="number"`);
  }
  const path = file.split('\\').join('/');
  if (!path.includes('core/forms/') && /from 'ng-zorro-antd\/form'/.test(s)) problems.push(`${file} imports ng-zorro's form module directly, use ErpFormModule so the form rules apply`);
  if (/<nz-table\b[^>]*nzLoading/.test(s) && !s.includes('ErpTableModule')) problems.push(`${file} has a loading table without ErpTableModule, so it shows a spinner instead of rows`);
  if (s.includes('<nz-spin') && !SPIN_ALLOWED.some((a) => path.endsWith(a))) problems.push(`${file} uses a spinner for content, use <erp-skeleton> (see shared/skeleton.component.ts)`);
}
if (problems.length) { console.error(problems.join('\n')); process.exit(1); }
console.log('a11y lint: ok');
