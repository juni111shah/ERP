// CI only (Development API): signs in as the seeded platform founder and sets sign-up to "open", so a registration in the demo
// creates its workspace at once instead of waiting for the founder's approval.
const API = process.argv[2] ?? 'http://localhost:5080/api/v1';
const unwrap = (b) => (b && typeof b === 'object' && 'success' in b ? b.data : b);
let token = '';
async function call(method, path, body) {
  const res = await fetch(API + path, { method, headers: { 'content-type': 'application/json', 'x-erp-client': 'web', ...(token ? { authorization: 'Bearer ' + token } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await res.text();
  if (!res.ok) throw new Error(`${method} ${path} -> ${res.status} ${text.slice(0, 300)}`);
  return unwrap(text ? JSON.parse(text) : null);
}
const login = await call('POST', '/auth/login', { email: 'founder@platform.local', password: 'DevOnly#2026' });
if (login.accessToken) token = login.accessToken;
else if (login.selectionToken) {
  const ws = login.workspaces[0];
  token = (await call('POST', '/auth/select-workspace', { selectionToken: login.selectionToken, workspaceId: ws.id })).accessToken;
}
if (!token) throw new Error('Founder sign-in gave no token: ' + JSON.stringify(login).slice(0, 300));
const s = await call('GET', '/platform-admin/settings');
console.log('Sign-up mode was', s.signupMode);
const input = { ...s, signupMode: 'open', requireEmailVerification: false };
await call('PUT', '/platform-admin/settings', input);
console.log('Sign-up mode is now', (await call('GET', '/platform-admin/settings')).signupMode);
