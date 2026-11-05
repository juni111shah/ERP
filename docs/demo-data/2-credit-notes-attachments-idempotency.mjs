const BASE = 'http://localhost:5080/api/v1';
let token = '';
async function call(method, path, body, headers = {}) {
  const isForm = body instanceof FormData;
  const res = await fetch(BASE + path, { method, headers: { ...(isForm ? {} : { 'Content-Type': 'application/json' }), ...(token ? { Authorization: `Bearer ${token}` } : {}), ...headers }, body: body === undefined ? undefined : isForm ? body : JSON.stringify(body) });
  const t = await res.text(); let j; try { j = JSON.parse(t); } catch { j = t; }
  return { status: res.status, ok: res.ok, d: j?.data ?? j, m: j?.message, raw: j };
}
const items = (r) => (Array.isArray(r.d) ? r.d : r.d?.items ?? []);
const l = (await call('POST', '/auth/login', { email: 'admin@demo.local', password: 'DevOnly#2026' })).d;

for (const code of ['demo', 'demo-re']) {
  console.log('==', code);
  const ws = l.workspaces.find((w) => w.code === code);
  const sel = await call('POST', '/auth/select-workspace', { selectionToken: l.selectionToken, workspaceId: ws.id });
  token = sel.d.accessToken;

  const invs = items(await call('GET', '/finance/invoices?page=1&pageSize=100'));
  const open = invs.filter((i) => !i.isCreditNote && i.status !== 'void' && i.status !== 'draft' && i.total - i.paid > 100);
  console.log('  invoices', invs.length, 'open', open.length, 'statuses', [...new Set(invs.map((i) => i.status))].join(','));
  const inv = open[0];
  if (inv) {
    const cn = await call('POST', `/finance/invoices/${inv.id}/credit-note`, { notes: 'Goods returned - demo credit' });
    console.log('  credit note', cn.status, cn.m ?? '', cn.d?.id ?? '', cn.d?.status ?? '', cn.d?.total ?? '');
    if (cn.ok) {
      const issued = await call('POST', `/finance/invoices/${cn.d.id}/issue`);
      console.log('  issue', issued.status, issued.m ?? '', issued.d?.status ?? '');
      const amount = Math.min(50, cn.d.total ?? 50);
      const ap = await call('POST', `/finance/invoices/${cn.d.id}/apply-credit`, { invoiceId: inv.id, amount });
      console.log('  apply', ap.status, ap.m ?? '', JSON.stringify(ap.raw).slice(0, 200));
    }
    const fd = new FormData();
    fd.append('ownerType', 'invoice'); fd.append('ownerId', inv.id); fd.append('category', 'Supporting'); fd.append('description', 'Signed delivery note');
    fd.append('file', new Blob(['Delivery note DN-1001\nReceived in good condition.\n'], { type: 'text/plain' }), 'delivery-note.txt');
    const up = await call('POST', '/attachments', fd);
    console.log('  attachment', up.status, up.m ?? '', up.ok ? '' : JSON.stringify(up.raw).slice(0, 200));
  }

  const idem = { 'Idempotency-Key': `demo-seed-${code}-1` };
  const a = await call('POST', '/approvals', { subject: 'Quarterly team lunch', justification: 'Team building', amount: 600 }, idem);
  const b = await call('POST', '/approvals', { subject: 'Quarterly team lunch', justification: 'Team building', amount: 600 }, idem);
  console.log('  idempotent post', a.status, b.status);
}
