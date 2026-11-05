import crypto from 'node:crypto';
const BASE = 'http://localhost:5080/api/v1';
let token = '';
async function call(method, path, body, headers = {}) {
  const res = await fetch(BASE + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...headers }, body: body === undefined ? undefined : JSON.stringify(body) });
  const t = await res.text(); let j; try { j = JSON.parse(t); } catch { j = t; }
  return { status: res.status, ok: res.ok, d: j?.data ?? j, m: j?.message, raw: j };
}
const items = (r) => (Array.isArray(r.d) ? r.d : r.d?.items ?? []);

function base32Decode(s) {
  const a = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'; let bits = ''; for (const c of s.replace(/=+$/, '').toUpperCase()) bits += a.indexOf(c).toString(2).padStart(5, '0');
  const out = []; for (let i = 0; i + 8 <= bits.length; i += 8) out.push(parseInt(bits.slice(i, i + 8), 2)); return Buffer.from(out);
}
function totp(secret) {
  const counter = Buffer.alloc(8); counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30000)));
  const h = crypto.createHmac('sha1', base32Decode(secret)).update(counter).digest(); const o = h[h.length - 1] & 15;
  return String(((h.readUInt32BE(o) & 0x7fffffff) % 1_000_000)).padStart(6, '0');
}

const l = (await call('POST', '/auth/login', { email: 'admin@demo.local', password: 'DevOnly#2026' })).d;
for (const code of (process.argv[2] ? [process.argv[2]] : ['demo', 'demo-re'])) {
  console.log('==', code);
  const ws = l.workspaces.find((w) => w.code === code);
  token = (await call('POST', '/auth/select-workspace', { selectionToken: l.selectionToken, workspaceId: ws.id })).d.accessToken;
  const me = (await call('GET', '/me')).d;
  const entity = me.legalEntities?.[0]?.id;
  const roles = items(await call('GET', '/roles?page=1&pageSize=50'));
  console.log('  roles', roles.map((r) => r.name).join(', '));
  const pick = (name) => roles.find((r) => r.name.toLowerCase().includes(name))?.id ?? roles[0].id;
  const email = `${code === 'demo' ? 'accountant' : 'agent'}@demo.local`;
  const inv = await call('POST', '/users', { email, fullName: code === 'demo' ? 'Demo Accountant' : 'Demo Leasing Agent', temporaryPassword: 'DemoUser#2026', roleIds: [pick(code === 'demo' ? 'account' : 'lease')], legalEntityIds: entity ? [entity] : [] });
  console.log('  invite', inv.status, inv.m ?? '', inv.ok ? '' : JSON.stringify(inv.raw).slice(0, 200));

  const fp = await call('POST', '/auth/forgot-password', { email });
  console.log('  forgot-password', fp.status);

  // two-factor on the new user: sign in as them, set it up, confirm with a real code
  const ul = await call('POST', '/auth/login', { email, password: 'DemoUser#2026' });
  let ut = ul.d?.accessToken;
  if (!ut && ul.d?.selectionToken) {
    const w2 = ul.d.workspaces.find((w) => w.code === code);
    ut = (await call('POST', '/auth/select-workspace', { selectionToken: ul.d.selectionToken, workspaceId: w2.id })).d?.accessToken;
  }
  console.log('  user login', ul.status, ut ? 'token' : JSON.stringify(ul.raw).slice(0, 200));
  if (ut) {
    const keep = token; token = ut;
    const su = await call('POST', '/auth/2fa/setup');
    const secret = su.d?.secret?.replace(/[^A-Za-z2-7]/g, ''); console.log('  secret len', su.d?.secret?.length, String(su.d?.secret).slice(0,6), 'uri', String(su.d?.otpAuthUri).slice(0,120));
    if (secret) { const en = await call('POST', '/auth/2fa/enable', { code: totp(secret) }); console.log('  2fa enable', en.status, JSON.stringify(en.raw).slice(0, 300)); }
    else console.log('  2fa setup', su.status, JSON.stringify(su.raw).slice(0, 160));
    token = keep;
  }

  // portal sign-in code for a customer with an email
  const parties = items(await call('GET', '/parties?page=1&pageSize=50'));
  const withMail = parties.find((p) => p.email);
  if (withMail) {
    const pc = await call('POST', '/portal/auth/request-code', { workspace: code, email: withMail.email });
    console.log('  portal code for', withMail.email, pc.status);
  } else console.log('  no party with an email; parties:', parties.length);
}
