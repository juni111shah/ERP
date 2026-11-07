// Creates one demo organization and its people, structure and master data.
import { Client, Checkpoints, ApiError, PASSWORD, addDays, monthStart, round2, rng, today } from './lib.mjs';
import { emailOf } from './orgs.mjs';

const FOUNDER_EMAIL = process.env.FOUNDER_EMAIL ?? 'founder@platform.local';
const FOUNDER_PASSWORD = process.env.FOUNDER_PASSWORD ?? 'DevOnly#2026';

let founder;
async function founderClient() {
  if (!founder) founder = await new Client(FOUNDER_EMAIL, FOUNDER_PASSWORD).signIn();
  return founder;
}

const ROLE_RULES = {
  sales: {
    label: 'Sales management', description: 'Customers, quotations, orders and delivery',
    match: (k) => /^(sales|crm|distribution|retail)\./.test(k) || /^pos\.(orders|analytics|terminal)/.test(k) || /^core\.(parties|items)\./.test(k) || /^finance\.invoices\.(read|write)$/.test(k) || k === 'core.attachments.read',
  },
  purchase: {
    label: 'Purchase management', description: 'Suppliers, requisitions, orders and receipts',
    match: (k) => /^purchasing\./.test(k) || /^core\.(parties|items)\./.test(k) || /^finance\.bills\.(read|write)$/.test(k) || k === 'core.attachments.read',
  },
  inventory: {
    label: 'Inventory management', description: 'Warehouses, stock movements and counts',
    match: (k) => /^inventory\./.test(k) || /^purchasing\.receipts\./.test(k) || /^purchasing\.orders\.read$/.test(k) || /^sales\.orders\./.test(k) || /^core\.items\./.test(k),
  },
  ops: {
    label: 'Operations billing', description: 'Raises invoices and bills from operational records',
    match: (k) => /^finance\.(invoices|bills)\.(read|write|issue|post|submit)$/.test(k) || /^core\.(parties|items)\.read$/.test(k) || /^inventory\.(stock|warehouses)\.read$/.test(k) || /^(projects|manufacturing|pos|retail|realestate|services)\./.test(k),
  },
  hr: {
    label: 'HR management', description: 'Employees, attendance, leave and payroll',
    match: (k) => /^hr\./.test(k),
  },
};

const PEOPLE = [
  ['owner', 'Owner', 'Org admin'],
  ['admin', 'Administrator', 'Org admin'],
  ['finance', 'Finance Manager', 'Finance manager'],
  ['accountant', 'Accountant', 'Accountant'],
  ['sales', 'Sales Manager', 'Sales management'],
  ['purchase', 'Purchase Manager', 'Purchase management'],
  ['inventory', 'Inventory Manager', 'Inventory management'],
  ['hr', 'HR Manager', 'HR management'],
  ['ops', 'Operations', null],
];

/** What this person can do once module roles are known. */
function rolesFor(key, org, allRoles) {
  const name = PEOPLE.find((p) => p[0] === key)[2];
  if (name) return allRoles.filter((r) => r.name === name);
  // Operational staff: the manager role of each industry module, plus their module's own operating roles.
  return allRoles.filter((r) => r.name === 'Operations billing' || (r.isBuiltIn && r.moduleKey && / manager$/i.test(r.name)));
}

async function ensureWorkspace(org, log) {
  const f = await founderClient();
  const adminEmail = emailOf(org, 'owner');
  const clients = await f.get(`/platform-admin/clients?search=${encodeURIComponent(org.code)}&pageSize=5`);
  const row = (Array.isArray(clients) ? clients : clients?.items ?? []).find((c) => c.code === org.code);
  let orgId = row?.orgId;
  if (!orgId) {
    const plans = await f.get('/platform-admin/plans');
    // The widest active plan: no module limit and the most users. Demo workspaces are complimentary, so it is never billed.
    const plan = (Array.isArray(plans) ? plans : plans.items).filter((p) => p.isActive && !p.modules?.length).sort((a, b) => (b.maxUsers ?? 1e9) - (a.maxUsers ?? 1e9) || b.sortOrder - a.sortOrder)[0];
    const created = await f.post('/platform-admin/clients', {
      workspaceName: org.name, signInKey: org.code, industry: org.industry, countryCode: org.country, baseCurrency: org.currency, timeZone: org.timeZone,
      adminName: `${org.name.replace('DEMO - ', '')} Owner`, adminEmail, phone: '+000 555 0100', planId: plan.id, cycle: 'monthly', startMode: 'complimentary',
      notes: 'Synthetic demonstration workspace. All records are invented.',
    });
    orgId = created.orgId;
    log(`    created ${org.code}`);
    const admin = new Client(adminEmail, created.temporaryPassword);
    await admin.signIn(org.code);
    await admin.post('/auth/change-password', { currentPassword: created.temporaryPassword, newPassword: PASSWORD });
  }
  await f.put(`/platform-admin/clients/${orgId}/demo`, { isDemo: true });
  return orgId;
}

export async function buildFoundation(org, log) {
  const orgId = await ensureWorkspace(org, log);
  const owner = await new Client(emailOf(org, 'owner')).signIn(org.code);
  const checkpoints = new Checkpoints(owner);
  await checkpoints.load();
  const random = rng(org.code);
  const me = await owner.get('/me');
  const entity = me.legalEntities[0].id;
  const ctx = {
    org, orgId, entity, random, owner, checkpoints, people: { owner }, log,
    run: (name, fn) => checkpoints.run(name, fn, log),
    /** Pins every signed-in person's clock to a date. */
    on(date) { for (const c of Object.values(ctx.people)) c.at(date); return ctx; },
  };

  await ctx.run('structure', async () => {
    let n = 0;
    for (const name of org.branches) await owner.post('/business-units', { legalEntityId: entity, code: `BR-${++n}`, name, kind: 'branch', address: `${name}, demo address` });
    n = 0;
    for (const name of org.departments) await owner.post('/business-units', { legalEntityId: entity, code: `DP-${++n}`, name, kind: 'department' });
    await owner.put(`/legal-entities/${entity}`, {
      name: org.name.replace('DEMO - ', ''), legalName: `${org.name.replace('DEMO - ', '')} (demonstration only)`, countryCode: org.country, baseCurrency: org.currency, address: 'Demonstration address, not a real location',
      email: `accounts@${org.code}.example.test`, phone: '+000 555 0100', isActive: true,
    }).catch((e) => log(`    legal entity details skipped: ${e.message}`));
  });

  await ctx.run('users', async () => {
    const permissions = (await owner.get('/permissions')).map((p) => p.key);
    const enabled = new Set(me.enabledModules.map((m) => m.key));
    const roles = await owner.list('/roles');
    for (const rule of Object.values(ROLE_RULES)) {
      if (roles.some((r) => r.name === rule.label)) continue;
      const keys = permissions.filter(rule.match);
      if (keys.length === 0 && !/^(HR)/.test(rule.label)) continue;
      if (keys.length > 0) await owner.post('/roles', { name: rule.label, description: rule.description, permissions: keys });
    }
    const all = await owner.list('/roles');
    for (const [key, title] of PEOPLE) {
      if (key === 'owner') continue;
      const assigned = rolesFor(key, org, all);
      if (assigned.length === 0) { log(`    no role for ${key}, skipping`); continue; }
      const email = emailOf(org, key);
      const fullName = `${key === 'ops' ? org.opsRole : title} (${org.name.replace('DEMO - ', '')})`;
      await owner.post('/users', { email, fullName, temporaryPassword: PASSWORD, roleIds: assigned.map((r) => r.id) });
    }
  });

  for (const [key] of PEOPLE) {
    if (key === 'owner') continue;
    try { ctx.people[key] = await new Client(emailOf(org, key)).signIn(org.code); }
    catch (e) { if (!(e instanceof ApiError)) throw e; }
  }

  const accounts = await owner.list('/finance/accounts');
  ctx.accounts = accounts;
  ctx.account = (role) => accounts.find((a) => a.systemRole === role)?.id;
  ctx.accountCode = (code) => accounts.find((a) => a.code === code)?.id;
  ctx.bank = ctx.account('bank');
  ctx.cash = ctx.account('cash');
  const taxes = await owner.list('/finance/tax-codes');
  ctx.taxes = taxes;
  ctx.tax = taxes.find((t) => t.isDefault) ?? taxes[0];
  ctx.taxRate = ctx.tax?.rate ?? 0;
  ctx.units = (await owner.list('/business-units')).filter((u) => (u.kind ?? 'branch') === 'branch');
  ctx.modules = new Set((await owner.get('/me')).enabledModules.map((m) => m.key));

  await ctx.run('parties', async () => {
    let n = 0;
    for (const name of org.customers) {
      const individual = !/ (Ltd|Limited|W\.L\.L\.|Company|Co|Group|Trading|Traders|Store|Mart|Bazaar|House|Grocers|Hospital|Clinic|Cafe|Consulting|Oasis|Foods|Hotel|Sweets|Bakers|Caterers|Parlour|Booth|Canteen|Nursery|Insurance|Health|Medical|Holding|Works|Trust|Family|Guests)\b/i.test(name) && name.split(' ').length <= 3;
      await owner.post('/parties', { name, kind: individual ? 'Individual' : 'Company', roles: 'Customer', email: `customer${++n}@${org.code}.example.test`, phone: `+000 555 1${String(n).padStart(3, '0')}`, paymentTermsDays: random.pick([15, 30, 30, 45]), address: 'Demonstration address' });
    }
    n = 0;
    for (const name of org.suppliers) await owner.post('/parties', { name, kind: 'Company', roles: 'Vendor', email: `vendor${++n}@${org.code}.example.test`, phone: `+000 555 2${String(n).padStart(3, '0')}`, paymentTermsDays: random.pick([30, 30, 45, 60]), address: 'Demonstration address' });
  });
  const parties = await owner.list('/parties');
  ctx.customers = org.customers.map((name) => parties.find((p) => p.name === name)).filter(Boolean);
  ctx.suppliers = org.suppliers.map((name) => parties.find((p) => p.name === name)).filter(Boolean);
  ctx.party = (name) => parties.find((p) => p.name === name);

  await ctx.run('items', async () => {
    let barcode = 6290000000000 + (org.code.length * 1000);
    for (const it of org.items) {
      await owner.post('/items', {
        sku: it.sku, name: it.name, type: it.service ? 'Service' : 'Product', uom: it.uom, category: it.category,
        salesPrice: it.price || null, purchasePrice: it.cost || null, taxCodeId: ctx.tax?.id ?? null,
        barcode: org.barcodes && !it.service ? String(barcode++) : undefined,
      });
    }
  });
  const items = await owner.list('/items');
  ctx.items = org.items.map((it) => ({ ...it, id: items.find((x) => x.sku === it.sku)?.id }));
  ctx.item = (sku) => ctx.items.find((i) => i.sku === sku);
  ctx.stockItems = ctx.items.filter((i) => !i.service);

  if (ctx.modules.has('inventory')) {
    await ctx.run('warehouses', async () => {
      let n = 0;
      for (const name of org.branches) await owner.post('/m/inventory/warehouses', { legalEntityId: entity, code: `WH-${++n}`, name: `${name} store` });
      for (const it of ctx.stockItems) await owner.put('/m/inventory/stock/policy', { itemId: it.id, reorderLevel: stockLevel(it) * (org.scale ?? 1), reorderQuantity: stockLevel(it) * 2 * (org.scale ?? 1) });
    });
    ctx.warehouses = await owner.list('/m/inventory/warehouses');
    ctx.warehouse = ctx.warehouses[0]?.id;
  }

  await ctx.run('opening-balances', async () => {
    const opening = monthStart(6);
    const size = org.salaryBase * 40;
    const lines = [{ accountId: ctx.bank, debit: round2(size * 6), credit: 0, description: 'Opening bank balance' }, { accountId: ctx.cash, debit: round2(size * 0.1), credit: 0, description: 'Opening cash balance' }];
    const ar = ctx.account('ar.control'); const ap = ctx.account('ap.control');
    for (const c of ctx.customers.slice(0, 3)) lines.push({ accountId: ar, debit: random.money(size * 0.05, size * 0.15, 100), credit: 0, partyId: c.id, description: 'Opening receivable', reference: `OB-${c.name.slice(0, 8)}`, dueDate: addDays(opening, 30) });
    for (const s of ctx.suppliers.slice(0, 2)) lines.push({ accountId: ap, debit: 0, credit: random.money(size * 0.03, size * 0.1, 100), partyId: s.id, description: 'Opening payable', reference: `OB-${s.name.slice(0, 8)}`, dueDate: addDays(opening, 45) });
    owner.at(opening);
    await owner.post('/finance/opening-balances', { legalEntityId: entity, date: opening, memo: 'Opening balances for the demonstration period', lines });
    owner.at(null);
  });

  await ctx.run('approval-rules', async () => {
    // Larger vendor bills wait for a decision, so inboxes and histories have something real in them.
    await owner.post('/approval-policies', { documentType: 'bill', minAmount: org.salaryBase * 4, isActive: true });
  });

  if (ctx.modules.has('inventory') && ctx.stockItems.length > 0) {
    await ctx.run('opening-stock', async () => {
      const opening = monthStart(6);
      const equity = ctx.account('equity.opening');
      owner.at(opening);
      for (const it of ctx.stockItems) {
        const quantity = stockLevel(it) * random.int(6, 10) * (org.scale ?? 1);
        await owner.post('/m/inventory/stock/receive', { warehouseId: ctx.warehouse, itemId: it.id, quantity, unitCost: it.cost, date: opening, offsetAccountId: equity, reference: `OPEN-${it.sku}`, memo: 'Opening stock' });
      }
      owner.at(null);
    });
  }
  return ctx;
}

/** A sensible level for an item by price, so expensive goods are held in smaller numbers. */
export function stockLevel(item) {
  const c = item.cost || 1;
  if (c > 2000) return 15; if (c > 500) return 30; if (c > 100) return 60; if (c > 20) return 120; return 250;
}

export { today };
