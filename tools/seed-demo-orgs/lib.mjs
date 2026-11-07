// Client and helpers for seeding demo organizations through the public API.
// Every document goes through the real endpoints, so permissions, validation and ledger postings are the product's own.

export const PASSWORD = process.env.DEMO_PASSWORD ?? 'DemoOnly#2026';
export const API = process.env.API ?? 'http://localhost:5390/api/v1';

export const iso = (d) => d.toISOString().slice(0, 10);
export const addDays = (date, n) => { const d = new Date(date + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + n); return iso(d); };
export const today = iso(new Date());
export const clampToToday = (date) => (date > today ? today : date);
/** First day of the month `back` months before the current one. */
export const monthStart = (back) => { const n = new Date(); return iso(new Date(Date.UTC(n.getUTCFullYear(), n.getUTCMonth() - back, 1))); };
export const round2 = (n) => Math.round(n * 100) / 100;

/** A small seeded generator, so a re-run builds the same records. */
export function rng(seedText) {
  let h = 2166136261;
  for (const c of seedText) h = Math.imul(h ^ c.charCodeAt(0), 16777619);
  const next = () => { h ^= h << 13; h ^= h >>> 17; h ^= h << 5; return (h >>> 0) / 4294967296; };
  return {
    next,
    int: (min, max) => min + Math.floor(next() * (max - min + 1)),
    pick: (list) => list[Math.floor(next() * list.length)],
    chance: (p) => next() < p,
    money: (min, max, step = 1) => Math.round((min + next() * (max - min)) / step) * step,
  };
}

export class ApiError extends Error {
  constructor(method, path, status, body) {
    super(`${method} ${path} -> ${status}: ${JSON.stringify(body?.errors ?? body?.detail ?? body?.message ?? body).slice(0, 500)}`);
    this.status = status;
    this.body = body;
  }
}

const unwrap = (b) => (b && typeof b === 'object' && 'success' in b ? b.data : b);
export const counters = { calls: 0 };

/** Signed-in API client for one person. `at(date)` pins the server's business clock for the next calls. */
export class Client {
  constructor(email, password = PASSWORD) { this.email = email; this.password = password; this.token = null; this.clock = null; }

  async raw(method, path, body, headers = {}) {
    counters.calls++;
    const res = await fetch(API + path, {
      method,
      headers: {
        'content-type': 'application/json', 'x-erp-client': 'seed',
        ...(this.token ? { authorization: 'Bearer ' + this.token } : {}),
        ...(this.clock ? { 'x-demo-clock': this.clock + 'T09:00:00Z' } : {}),
        ...headers,
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const text = await res.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch { json = { message: text.slice(0, 300) }; }
    return { res, json };
  }

  async call(method, path, body) {
    let { res, json } = await this.raw(method, path, body);
    if (res.status === 401 && this.signedIn) {
      await this.signIn();
      ({ res, json } = await this.raw(method, path, body));
    }
    if (!res.ok) throw new ApiError(method, path, res.status, json);
    return unwrap(json);
  }

  get(path) { return this.call('GET', path); }
  post(path, body = {}) { return this.call('POST', path, body); }
  put(path, body = {}) { return this.call('PUT', path, body); }
  at(date) { this.clock = date; return this; }

  async list(path) {
    const sep = path.includes('?') ? '&' : '?';
    const r = await this.get(`${path}${sep}pageSize=500`);
    return Array.isArray(r) ? r : (r?.items ?? []);
  }

  async signIn(workspaceCode) {
    this.workspaceCode = workspaceCode ?? this.workspaceCode;
    const clock = this.clock; this.clock = null; this.token = null;
    let { res, json } = await this.raw('POST', '/auth/login', { email: this.email, password: this.password });
    if (!res.ok) throw new ApiError('POST', '/auth/login', res.status, json);
    let data = unwrap(json);
    if (data.requiresWorkspaceSelection) {
      const ws = data.workspaces.find((w) => w.code === this.workspaceCode) ?? data.workspaces[0];
      data = await this.call('POST', '/auth/select-workspace', { selectionToken: data.selectionToken, workspaceId: ws.id });
    }
    this.token = data.accessToken;
    this.signedIn = true;
    this.clock = clock;
    return this;
  }
}

/** Runs a step once per organization: finished steps are recorded in the workspace, so a re-run skips them. */
export class Checkpoints {
  constructor(admin) { this.admin = admin; this.done = new Set(); }

  async load() {
    const settings = await this.admin.get('/settings');
    const row = (Array.isArray(settings) ? settings : settings?.items ?? []).find((s) => s.key === 'core.demoSeedCheckpoints');
    const value = row?.value;
    const list = typeof value === 'string' ? JSON.parse(value) : value;
    this.done = new Set(list ?? []);
  }

  async run(name, fn, log = () => {}) {
    if (this.done.has(name)) return false;
    const started = Date.now();
    const clock = this.admin.clock;
    await fn();
    this.admin.clock = clock;
    this.done.add(name);
    await this.admin.at(null).put('/settings', { 'core.demoSeedCheckpoints': [...this.done] });
    log(`    ${name} (${((Date.now() - started) / 1000).toFixed(1)}s)`);
    return true;
  }
}
