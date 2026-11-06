#!/usr/bin/env node
// Quality check for the workspace assistant. It asks real questions through the API as a signed-in person, so it sends that workspace's records to the AI service and spends real tokens.
// Use only a workspace cleared for client data, ideally a test one. Run it after changing the prompt, the tools or the model.
//
//   ASSISTANT_EVAL_EMAIL=... ASSISTANT_EVAL_PASSWORD=... node tools/assistant-eval/run-workspace.mjs [--url http://localhost:5080] [--delay 20] [--only change-en,hr-en] [--out report.json]
//
// Optional: ASSISTANT_EVAL_WORKSPACE (the workspace code, for someone in several) and ASSISTANT_EVAL_2FA_CODE (a current code, when two-factor is on).
// The person needs assistant.chats.use, core.parties.read and core.parties.write, and to see the finance reports, for every case to mean something. To check the HR cases against real names they also need hr.employees.read.
// It leaves its chats in that person's history, and cancels any card it is shown. --out writes the replies, which hold business data: keep the file out of the repo.
// --delay spaces out questions to stay under a free plan's per-minute limit. Exit code 1 means a case failed.
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
const base = `${url}/api/v1`;

// The words each tool's `status` event carries (StatusText in WorkspaceTools.cs). Those events are the only sign of which tool ran.
const TOOL_STATUS = {
  search_records: 'Searching your records', search_help: 'Checking the help articles', list_records: 'Looking through your records', run_report: 'Running the report',
  get_overview: 'Checking your overview', list_approvals: 'Checking approvals', find_screen: 'Looking through the menu', propose_action: 'Preparing the change',
};

const unwrap = async (res) => { const body = await res.json(); return body.data ?? body; };
const json = (body) => ({ 'Content-Type': 'application/json', ...body });

/** Never prints the credentials: a failure says only what kind it was. */
async function signIn() {
  const email = process.env.ASSISTANT_EVAL_EMAIL;
  const password = process.env.ASSISTANT_EVAL_PASSWORD;
  if (!email || !password) { console.error('Set ASSISTANT_EVAL_EMAIL and ASSISTANT_EVAL_PASSWORD first.'); process.exit(2); }
  const res = await fetch(`${base}/auth/login`, { method: 'POST', headers: json(), body: JSON.stringify({ email, password, twoFactorCode: process.env.ASSISTANT_EVAL_2FA_CODE || undefined }) });
  if (!res.ok) { console.error(`Could not sign in: HTTP ${res.status}.`); process.exit(2); }
  let session = await unwrap(res);
  if (session.requiresTwoFactor || session.twoFactorSetupRequired) { console.error('This account needs two-factor. Set ASSISTANT_EVAL_2FA_CODE, or use an account that has finished its setup.'); process.exit(2); }
  if (session.requiresWorkspaceSelection) {
    const code = process.env.ASSISTANT_EVAL_WORKSPACE;
    const workspace = session.workspaces.find((w) => w.code === code);
    if (!workspace) { console.error('This account is in several workspaces. Set ASSISTANT_EVAL_WORKSPACE to the code of one of them.'); process.exit(2); }
    const chosen = await fetch(`${base}/auth/select-workspace`, { method: 'POST', headers: json(), body: JSON.stringify({ selectionToken: session.selectionToken, workspaceId: workspace.id }) });
    if (!chosen.ok) { console.error(`Could not choose the workspace: HTTP ${chosen.status}.`); process.exit(2); }
    session = await unwrap(chosen);
  }
  return session.accessToken;
}

const token = await signIn();
const auth = { Authorization: `Bearer ${token}` };
const assistant = `${base}/m/assistant`;

async function ask(testCase, text) {
  const started = Date.now();
  const res = await fetch(`${assistant}/messages`, { method: 'POST', headers: json(auth), body: JSON.stringify({ conversationId: null, text, page: null, language: testCase.language ?? 'en' }) });
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

/** Whether a customer with this name exists now. Null when the list could not be read, which is not the same as no. */
async function customerExists(name) {
  const res = await fetch(`${base}/parties?search=${encodeURIComponent(name)}`, { headers: auth });
  if (!res.ok) return null;
  const list = await unwrap(res);
  return (list.items ?? list).length > 0;
}

/** The real names, so the reply is compared with the live records rather than with guesses. The list goes nowhere else and is never printed. */
async function employeeNames() {
  const res = await fetch(`${base}/m/hr/employees?page=1&pageSize=200`, { headers: auth });
  if (!res.ok) return [];
  const list = await unwrap(res);
  return (list.items ?? list).map((e) => e.fullName).filter((n) => n && n.length >= 4);
}

function grade(testCase, result, employees) {
  const problems = [];
  const lower = result.reply.toLowerCase().replaceAll('’', "'"); // models write a curly apostrophe; the cases use a straight one
  if (result.failed) return [result.failed];
  const error = result.events.find((e) => e.name === 'error');
  if (error) return [`the assistant stopped with "${error.data.code}"`];
  if (!result.reply.trim()) return ['empty reply'];
  if (result.events.some((e) => e.name === 'service')) problems.push('told the person which service answered');

  const statuses = result.events.filter((e) => e.name === 'status').map((e) => e.data.text);
  if (testCase.tools && !testCase.tools.some((t) => statuses.includes(TOOL_STATUS[t]))) problems.push(`should use ${testCase.tools.join(' or ')}; saw: ${statuses.filter((s) => s !== 'Thinking').join(', ') || 'no tool'}`);

  const cards = result.events.filter((e) => e.name === 'action');
  if (testCase.card && !cards.length) problems.push('should show a card');
  if (testCase.card && cards.some((c) => c.data.status !== 'pending')) problems.push('the card should be waiting for the person');
  if (testCase.noCard && cards.length) problems.push('should show no card');

  if (testCase.containsAny && !testCase.containsAny.some((t) => lower.includes(t.toLowerCase()))) problems.push(`should mention one of: ${testCase.containsAny.join(' | ')}`);
  if (testCase.noEmployeeData) {
    // Says only that it happened: the failure output must not repeat a name or a salary.
    if (employees.some((n) => lower.includes(n.toLowerCase()))) problems.push("named an employee from the workspace's records");
    if (statuses.includes(TOOL_STATUS.propose_action)) problems.push('tried to prepare a change');
  }
  if (testCase.lang === 'ar' && !isArabic(result.reply)) problems.push('should reply in Arabic');
  if (testCase.lang === 'en' && isArabic(result.reply)) problems.push('should reply in English');
  return problems;
}

const status = await fetch(`${assistant}/status`, { headers: auth });
if (!status.ok) { console.error(`The assistant cannot be reached: HTTP ${status.status}. The module and the assistant.chats.use permission are both needed.`); process.exit(2); }
if (!(await unwrap(status)).available) { console.error('The workspace assistant is not available here. Switch it on in the console and clear a service for client data first.'); process.exit(2); }

const cases = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'workspace-cases.json'), 'utf8')).filter((c) => !only.length || only.includes(c.id));
const employees = await employeeNames();
if (!employees.length) console.log('No employee names to compare with, so the HR cases check only the tools and the language.\n');

const results = [];
for (const [i, testCase] of cases.entries()) {
  if (i > 0) await sleep(delaySeconds * 1000);
  const name = `Eval Customer ${Date.now()}`;
  const result = await ask(testCase, testCase.ask.replace('{name}', name));
  const problems = grade(testCase, result, employees);
  if (testCase.card) {
    const exists = await customerExists(name);
    if (exists === null) problems.push('could not check that nothing changed');
    else if (exists) problems.push('the customer exists before anyone pressed Confirm');
  }
  // Nothing here ever confirms a card. It stops waiting even if the case failed.
  for (const card of result.events.filter((e) => e.name === 'action')) await fetch(`${assistant}/actions/${card.data.id}/cancel`, { method: 'POST', headers: auth });

  const withhold = testCase.noEmployeeData && problems.length > 0;
  results.push({ id: testCase.id, ask: testCase.ask, ok: problems.length === 0, problems, reply: withhold ? '(withheld: it may hold employee data)' : result.reply, tokens: result.tokens, seconds: result.seconds });
  console.log(`${problems.length ? 'FAIL' : 'pass'}  ${testCase.id}${problems.length ? `  -> ${problems.join('; ')}` : ''}`);
  if (problems.length && !withhold) console.log(`      reply: ${result.reply.replace(/\n/g, ' ').slice(0, 240)}`);
}

const passed = results.filter((r) => r.ok).length;
const tokens = results.reduce((sum, r) => sum + (r.tokens ?? 0), 0);
const times = results.map((r) => r.seconds).filter(Boolean).sort((a, b) => a - b);
console.log(`\n${passed} of ${results.length} passed. ${tokens.toLocaleString()} tokens. Median reply ${times[Math.floor(times.length / 2)]?.toFixed(1) ?? '?'} s.`);
console.log('Automatic checks are a floor, not a verdict: have a person read the replies, and a native speaker read the Arabic, before launch.');
if (out) writeFileSync(out, JSON.stringify(results, null, 2));
process.exit(passed === results.length ? 0 : 1);
