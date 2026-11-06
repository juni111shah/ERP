#!/usr/bin/env node
// Quality check for the public assistant. It asks real questions through the public API, so it uses real tokens from whichever service is connected.
// Facts are compared with the live price list, so the check stays right when prices change. Run it after changing the prompt, the facts or the model.
//
//   node tools/assistant-eval/run.mjs [--url http://localhost:5080] [--delay 20] [--only price-en,trial] [--out report.json]
//
// The assistant must be switched on in the console. --delay spaces out questions to stay under a free plan's per-minute limit. Exit code 1 means a case failed.
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const args = process.argv.slice(2);
const option = (name, fallback) => { const i = args.indexOf(`--${name}`); return i >= 0 ? args[i + 1] : fallback; };
const url = option('url', 'http://localhost:5080').replace(/\/$/, '');
const delaySeconds = Number(option('delay', '20'));
const only = option('only', '')?.split(',').filter(Boolean) ?? [];
const out = option('out', null);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const unwrap = async (res) => { const body = await res.json(); return body.data ?? body; };

async function ask(text) {
  const started = Date.now();
  const res = await fetch(`${url}/api/v1/public/assistant/messages`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ text }) });
  if (!res.ok) return { failed: `HTTP ${res.status}`, reply: '', events: [] };
  const raw = await res.text();
  const events = raw.split('\n\n').filter((b) => b.startsWith('event:')).map((b) => {
    const [first, second] = b.split('\n');
    return { name: first.slice(6).trim(), data: JSON.parse(second.slice(5)) };
  });
  return {
    reply: events.filter((e) => e.name === 'delta').map((e) => e.data.text).join(''),
    events, seconds: (Date.now() - started) / 1000,
    tokens: events.filter((e) => e.name === 'done').reduce((sum, e) => sum + e.data.inputTokens + e.data.outputTokens, 0),
  };
}

const isArabic = (t) => { const letters = [...t].filter((c) => /\p{L}/u.test(c)); return letters.length > 0 && [...t].filter((c) => /[؀-ۿ]/.test(c)).length * 2 > letters.length; };
const WESTERN = { '٠': '0', '١': '1', '٢': '2', '٣': '3', '٤': '4', '٥': '5', '٦': '6', '٧': '7', '٨': '8', '٩': '9' };
const digits = (t) => t.replace(/[٠-٩]/g, (d) => WESTERN[d]).replace(/,/g, '');

function grade(testCase, result, plans) {
  const problems = [];
  const lower = result.reply.toLowerCase().replaceAll('’', "'"); // models write a curly apostrophe; the cases use a straight one
  if (result.failed) return [result.failed];
  if (!result.reply.trim()) return ['empty reply'];
  if (result.events.some((e) => e.name === 'service')) problems.push('told the visitor which service answered');
  // Falling back to written answers still helps a visitor, but it says nothing about the model, so it is not a pass here.
  if (result.events.some((e) => e.name === 'contact' && e.data.reason === 'unavailable')) problems.push('the AI service was unavailable');
  for (const key of testCase.facts ?? []) {
    const [code, field] = key.split('.');
    const plan = plans.find((p) => p.code === code);
    const value = field === 'monthly' ? plan?.monthlyPrice : plan?.annualPrice;
    if (value === undefined) problems.push(`no plan ${code} to check against`);
    else if (!digits(result.reply).includes(String(value))) problems.push(`should state ${value} for ${key}`);
  }
  for (const text of testCase.contains ?? []) if (!lower.includes(text.toLowerCase())) problems.push(`should mention "${text}"`);
  if (testCase.containsAny && !testCase.containsAny.some((t) => lower.includes(t.toLowerCase()))) problems.push(`should mention one of: ${testCase.containsAny.join(' | ')}`);
  for (const text of testCase.notContains ?? []) if (lower.includes(text.toLowerCase())) problems.push(`must not say "${text}"`);
  if (testCase.lang === 'ar' && !isArabic(result.reply)) problems.push('should reply in Arabic');
  if (testCase.lang === 'en' && isArabic(result.reply)) problems.push('should reply in English');
  if (testCase.cards && !result.events.some((e) => e.name === 'plans')) problems.push('should show the price cards');
  return problems;
}

const status = await unwrap(await fetch(`${url}/api/v1/public/assistant/status`));
if (!status.visible) { console.error('The public assistant is switched off. Turn it on in the console first.'); process.exit(2); }
const config = await unwrap(await fetch(`${url}/api/v1/public/signup-config`));
const cases = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'cases.json'), 'utf8')).filter((c) => !only.length || only.includes(c.id));

const results = [];
for (const [i, testCase] of cases.entries()) {
  if (i > 0) await sleep(delaySeconds * 1000);
  const result = await ask(testCase.ask);
  const problems = grade(testCase, result, config.plans);
  results.push({ id: testCase.id, ask: testCase.ask, ok: problems.length === 0, problems, reply: result.reply, tokens: result.tokens, seconds: result.seconds });
  console.log(`${problems.length ? 'FAIL' : 'pass'}  ${testCase.id}${problems.length ? `  -> ${problems.join('; ')}` : ''}`);
  if (problems.length) console.log(`      reply: ${result.reply.replace(/\n/g, ' ').slice(0, 240)}`);
}

const passed = results.filter((r) => r.ok).length;
const tokens = results.reduce((sum, r) => sum + (r.tokens ?? 0), 0);
const times = results.map((r) => r.seconds).filter(Boolean).sort((a, b) => a - b);
console.log(`\n${passed} of ${results.length} passed. ${tokens.toLocaleString()} tokens. Median reply ${times[Math.floor(times.length / 2)]?.toFixed(1) ?? '?'} s.`);
console.log('Automatic checks are a floor, not a verdict: have a person read the replies, and a native speaker read the Arabic, before launch.');
if (out) writeFileSync(out, JSON.stringify(results, null, 2));
process.exit(passed === results.length ? 0 : 1);
