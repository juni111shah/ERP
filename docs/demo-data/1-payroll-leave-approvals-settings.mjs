// Drives the running API as the demo admin to create data through the real workflows.
const BASE = 'http://localhost:5080/api/v1';
const only = process.argv[2]; // optional workspace code
let token = '';

async function call(method, path, body, extra = {}) {
  const res = await fetch(BASE + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...extra },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let json; try { json = JSON.parse(text); } catch { json = text; }
  return { ok: res.ok, status: res.status, data: json?.data ?? json, raw: json };
}
const items = (r) => (Array.isArray(r.data) ? r.data : r.data?.items ?? []);
const say = (...a) => console.log(...a);

async function signIn(code) {
  const l = await call('POST', '/auth/login', { email: 'admin@demo.local', password: 'DevOnly#2026' });
  const ws = l.data.workspaces.find((w) => w.code === code);
  if (!ws) throw new Error('no workspace ' + code);
  token = '';
  const s = await call('POST', '/auth/select-workspace', { selectionToken: l.data.selectionToken, workspaceId: ws.id });
  token = s.data?.accessToken ?? s.raw?.accessToken;
  if (!token) throw new Error('no token: ' + JSON.stringify(s.raw).slice(0, 300));
}

async function step(name, fn) {
  try { const r = await fn(); say(`  ${name}:`, r ?? 'ok'); } catch (e) { say(`  ${name}: FAILED`, String(e.message ?? e).slice(0, 300)); }
}

async function run(code) {
  say(`== ${code}`);
  await signIn(code);
  const me = (await call('GET', '/me')).data;
  const entity = me?.legalEntities?.[0]?.id ?? me?.allLegalEntities?.[0]?.id;

  await step('payroll', async () => {
    if (!entity) return 'no entity';
    const hr = '/m/hr';
    const runs = items(await call('GET', `${hr}/payroll?page=1&pageSize=50`));
    const year = 2026, month = 11;
    let run = runs.find((r) => r.year === year && r.month === month);
    if (!run) {
      const g = await call('POST', `${hr}/payroll`, { legalEntityId: entity, year, month });
      if (!g.ok) return 'generate failed ' + JSON.stringify(g.raw).slice(0, 300);
      run = g.data;
    }
    const detail = await call('GET', `${hr}/payroll/${run.id}`);
    const status = detail.data?.status;
    if (status === 'draft' || status === 0) {
      const p = await call('POST', `${hr}/payroll/${run.id}/post`);
      return `run ${run.id} ${status} -> post ${p.status} ${p.ok ? '' : JSON.stringify(p.raw).slice(0, 200)}`;
    }
    return `run ${run.id} already ${status}`;
  });

  await step('leave approvals', async () => {
    const reqs = items(await call('GET', '/m/hr/leave?page=1&pageSize=100'));
    let n = 0;
    for (const r of reqs.filter((x) => x.status === 'submitted').slice(0, 3)) {
      const a = await call('POST', `/m/hr/leave/${r.id}/approve`, { comment: 'Approved' });
      if (a.ok) n++; else say('    approve failed', a.status, JSON.stringify(a.raw).slice(0, 160));
    }
    return `approved ${n} of ${reqs.filter((x) => x.status === 'submitted').length} submitted`;
  });

  await step('approval requests', async () => {
    const a = await call('POST', '/approvals', { subject: 'Purchase of office laptops', justification: 'Replace 5 devices older than 5 years', amount: 18500, legalEntityId: entity });
    const b = await call('POST', '/approvals', { subject: 'Travel budget for the Dubai trade show', justification: 'Three people, four days', amount: 9200, legalEntityId: entity });
    const c = await call('POST', '/approvals', { subject: 'Annual software licence renewal', justification: 'Accounting package', amount: 4300, legalEntityId: entity });
    let msg = `${a.status}/${b.status}/${c.status}`;
    if (b.ok && b.data?.id) { const d = await call('POST', `/approvals/${b.data.id}/approve`, { comment: 'Approved within budget' }); msg += ` approve ${d.status}`; }
    if (c.ok && c.data?.id) { const d = await call('POST', `/approvals/${c.data.id}/reject`, { comment: 'Postpone to next quarter' }); msg += ` reject ${d.status}`; }
    return msg;
  });

  await step('settings', async () => {
    const r = await call('PUT', '/settings', { 'hr.shiftStart': '08:00', 'hr.lateGraceMinutes': 10, 'hr.hoursPerDay': 8, 'hr.daysBasis': 30, 'hr.overtimeMultiplier': 1.25, 'hr.weekendDays': 'Friday,Saturday' });
    return r.status + (r.ok ? '' : ' ' + JSON.stringify(r.raw).slice(0, 200));
  });
}

for (const code of only ? [only] : ['demo', 'demo-re']) await run(code);
