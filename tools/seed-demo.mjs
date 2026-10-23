#!/usr/bin/env node
// Seeds realistic demo data through the public API: parties, items, finance, real estate (properties, units, leases, billing,
// receipts, cheques, work orders, preventive maintenance, deals, an acquisition), a budget and HR (employees, attendance, payroll).
// Usage: node tools/seed-demo.mjs [apiBase]   (default http://localhost:5080/api/v1). Safe to re-run: it skips when already seeded.
const API = process.argv[2] ?? process.env.API ?? 'http://localhost:5080/api/v1';
const PASSWORD = process.env.SEED_PASSWORD ?? 'DevOnly#2026';
const EMAIL = process.env.SEED_OWNER ?? 'owner@demo.local';

let token = '';
const unwrap = (b) => (b && typeof b === 'object' && 'success' in b ? b.data : b);

async function call(method, path, body) {
  const res = await fetch(API + path, {
    method,
    headers: { 'content-type': 'application/json', 'x-erp-client': 'web', ...(token ? { authorization: 'Bearer ' + token } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  const json = text ? JSON.parse(text) : null;
  if (!res.ok) throw new Error(`${method} ${path} -> ${res.status} ${(json?.detail ?? json?.title ?? text).toString().slice(0, 200)}`);
  return unwrap(json);
}

async function login() {
  const sel = await call('POST', '/auth/login', { email: EMAIL, password: PASSWORD });
  const ws = sel.workspaces.find((w) => w.code === 'demo-re');
  const s = await call('POST', '/auth/select-workspace', { selectionToken: sel.selectionToken, workspaceId: ws.id });
  token = s.accessToken;
}

const ok = [];
async function step(name, fn) {
  try { const r = await fn(); ok.push(name); console.log('  ✓', name); return r; }
  catch (e) { console.log('  ✗', name, '-', e.message); return undefined; }
}

const iso = (d) => d.toISOString().slice(0, 10);
const today = new Date();
const addDays = (n) => iso(new Date(today.getTime() + n * 86400000));
const addMonths = (n) => { const d = new Date(today); d.setMonth(d.getMonth() + n); return iso(d); };

await login();
console.log('Seeding demo data into workspace demo-re ...');

const existing = await call('GET', '/parties?search=Al%20Noor&pageSize=5');
if ((existing?.length ?? existing?.items?.length ?? 0) > 0) { console.log('Already seeded - nothing to do.'); process.exit(0); }

const me = await call('GET', '/me');
const entity = me.legalEntities[0].id;
const accounts = await call('GET', '/finance/accounts?pageSize=300');
const acct = (code) => accounts.find((a) => a.code === code)?.id;
const bank = accounts.find((a) => a.systemRole === 'bank')?.id;
const expenseAcc = acct('6900') ?? accounts.find((a) => a.type === 'expense')?.id;

// ---- Parties and catalogue ----------------------------------------------------------------------------------------------
const tenantNames = ['Al Noor Trading LLC', 'Sara Ahmed', 'Gulf Tech Solutions', 'Khalid Al Mansoori', 'Bluewave Cafe', 'Priya Nair', 'Omar Hassan', 'Desert Rose Salon'];
const tenants = [];
await step('customers / tenants', async () => {
  for (const [i, name] of tenantNames.entries()) {
    const p = await call('POST', '/parties', { name, kind: i % 2 === 0 ? 'Company' : 'Individual', roles: 'Customer', email: `tenant${i + 1}@example.com`, phone: `+97150000${100 + i}`, paymentTermsDays: 15 });
    tenants.push(p);
  }
});
const vendors = [];
await step('vendors', async () => {
  for (const name of ['CoolAir HVAC Services', 'Prime Plumbing Co', 'BrightSpark Electricals']) vendors.push(await call('POST', '/parties', { name, kind: 'Company', roles: 'Vendor', email: name.split(' ')[0].toLowerCase() + '@vendor.example.com', paymentTermsDays: 30 }));
});
const agents = [];
await step('brokerage agents', async () => {
  for (const name of ['Imran Qureshi', 'Fatima Zahra']) agents.push(await call('POST', '/parties', { name, kind: 'Individual', roles: 'Employee' }));
});
await step('catalogue items', async () => {
  for (const [sku, name, type, price] of [['SRV-MAINT', 'Maintenance call-out', 'service', 250], ['SRV-CLEAN', 'Deep cleaning', 'service', 400], ['PRD-FILTER', 'AC filter set', 'product', 85]])
    await call('POST', '/items', { sku, name, type, uom: type === 'service' ? 'visit' : 'pcs', salesPrice: price, purchasePrice: price * 0.6, isActive: true });
});

// ---- Real estate: portfolio ---------------------------------------------------------------------------------------------
const unitTypes = {};
await step('unit types', async () => {
  for (const [code, name, usage, beds] of [['STU', 'Studio', 'residential', 0], ['1BR', '1 Bedroom', 'residential', 1], ['2BR', '2 Bedroom', 'residential', 2], ['SHOP', 'Retail shop', 'commercial', null], ['OFF', 'Office', 'commercial', null]])
    unitTypes[code] = await call('POST', '/m/realestate/unit-types', { code, name, defaultUsage: usage, defaultBedrooms: beds });
});
const properties = [];
await step('properties', async () => {
  for (const [name, type, community, value] of [['Marina Heights Tower', 'residential', 'Dubai Marina', 42_000_000], ['Palm Business Centre', 'commercial', 'Business Bay', 28_000_000], ['Garden Court Villas', 'villa', 'Arabian Ranches', 18_500_000]])
    properties.push(await call('POST', '/m/realestate/properties', { legalEntityId: entity, name, propertyType: type, ownershipType: 'owned', region: 'Dubai', community, yearBuilt: 2016, totalUnits: 0, indicativeValue: value }));
});
const units = [];
await step('units', async () => {
  const plan = [
    [0, 'STU', 4, 520, 52_000], [0, '1BR', 4, 780, 78_000], [0, '2BR', 3, 1150, 115_000],
    [1, 'SHOP', 3, 650, 140_000], [1, 'OFF', 4, 900, 120_000], [2, '2BR', 3, 1900, 210_000],
  ];
  let n = 100;
  for (const [pi, code, count, area, rent] of plan)
    for (let i = 0; i < count; i++) units.push({ ...(await call('POST', '/m/realestate/units', { propertyId: properties[pi].id, unitNumber: String(++n), unitTypeId: unitTypes[code].id, areaSqft: area, askingRent: rent, floor: String(1 + (i % 6)) })), rent });
});

// ---- Leases, deposits, billing, receipts, cheques -----------------------------------------------------------------------
const leases = [];
await step('leases (active, one ending soon, one expired)', async () => {
  const specs = [
    { unit: 0, tenant: 0, start: -200, end: 165, freq: 4 }, { unit: 4, tenant: 1, start: -100, end: 265, freq: 12 }, { unit: 8, tenant: 2, start: -300, end: 65, freq: 2 },
    { unit: 12, tenant: 3, start: -250, end: 25, freq: 4 }, { unit: 16, tenant: 4, start: -60, end: 305, freq: 1 }, { unit: 20, tenant: 5, start: -330, end: 35, freq: 4 },
    { unit: 5, tenant: 6, start: -150, end: 215, freq: 6 }, { unit: 17, tenant: 7, start: -20, end: 345, freq: 12 },
  ];
  for (const s of specs) {
    const u = units[s.unit];
    leases.push(await call('POST', '/m/realestate/leases', { unitId: u.id, lesseePartyId: tenants[s.tenant].id, startDate: addDays(s.start), endDate: addDays(s.end), annualRent: u.rent, paymentFrequency: s.freq, securityDepositAmount: Math.round(u.rent * 0.05), activateNow: true }));
  }
});
await step('security deposits received', async () => {
  for (const l of leases.slice(0, 5)) await call('POST', `/m/realestate/leases/${l.id}/deposit`, { bankAccountId: bank, date: l.startDate, amount: l.securityDepositAmount });
});
await step('rent billing run', async () => { await call('POST', '/m/realestate/billing/run', { asOf: addDays(60), legalEntityId: entity }); });
await step('receipts (some tenants have paid)', async () => {
  for (const i of [0, 1, 4, 6]) {
    const stmt = await call('GET', `/finance/invoices?filter=${encodeURIComponent('partyId:eq:' + tenants[i].id)}&pageSize=5`);
    const first = (stmt.items ?? stmt)[0];
    if (first) await call('POST', '/finance/receipts', { legalEntityId: entity, partyId: tenants[i].id, date: addDays(-5), method: 'bankTransfer', bankAccountId: bank, amount: first.total, reference: 'TRF-' + (1000 + i), autoAllocate: true });
  }
});
await step('post-dated cheques', async () => {
  for (const [i, d] of [[2, 20], [3, 45], [5, 70]]) await call('POST', '/finance/cheques', { legalEntityId: entity, partyId: tenants[i].id, kind: 'postDatedCheque', instrumentNo: String(700100 + i), bankName: 'Emirates NBD', amount: 25_000 + i * 1000, instrumentDate: addDays(d), receivedOn: addDays(-3) });
});

// ---- Operations ---------------------------------------------------------------------------------------------------------
await step('work orders', async () => {
  for (const [issue, cat, prio] of [['AC not cooling in unit 101', 'HVAC', 'high'], ['Leaking kitchen tap', 'Plumbing', 'normal'], ['Lobby light flickering', 'Electrical', 'low'], ['Lift stuck between floors', 'Elevators', 'critical']])
    await call('POST', '/m/realestate/work-orders', { propertyId: properties[0].id, issue, category: cat, priority: prio, estimatedCost: 400, vendorPartyId: undefined });
});
await step('preventive maintenance schedules', async () => {
  for (const [t, c, m, d] of [['Quarterly AC service', 'HVAC', 3, 5], ['Fire pump test', 'Safety', 1, 10], ['Lift inspection', 'Elevators', 6, 40]])
    await call('POST', '/m/realestate/pm-schedules', { propertyId: properties[0].id, title: t, category: c, frequencyMonths: m, nextDue: addDays(d), leadDays: 7, estimatedCost: 600 });
});
await step('brokerage deals', async () => {
  const sale = await call('POST', '/m/realestate/deals', { legalEntityId: entity, dealType: 'sale', clientName: 'Rashid Al Falasi', dealDate: addDays(-20), dealValue: 3_200_000, commissionRatePercent: 2, propertyId: properties[0].id, agentPartyId: agents[0].id, agentSharePercent: 40 });
  for (const stage of ['viewing', 'negotiation', 'agreed']) await call('POST', `/m/realestate/deals/${sale.id}/stage`, { stage });
  await call('POST', '/m/realestate/deals', { legalEntityId: entity, dealType: 'lease', clientName: 'Nadia Karim', dealDate: addDays(-5), dealValue: 95_000, commissionRatePercent: 5, propertyId: properties[1].id, agentPartyId: agents[1].id, agentSharePercent: 35 });
});
await step('acquisition with bank mortgage', async () => {
  const prop = await call('POST', '/m/realestate/properties', { legalEntityId: entity, name: 'Skyline Plaza (new purchase)', propertyType: 'commercial', ownershipType: 'owned', region: 'Dubai', community: 'DIFC', totalUnits: 0, indicativeValue: 12_000_000 });
  await call('POST', '/m/realestate/acquisitions', { legalEntityId: entity, propertyId: prop.id, sellerName: 'Emaar Developments', purchaseDate: addDays(-10), contractReference: 'SPA-2026-118', purchasePrice: 12_000_000, downPayment: 2_400_000, transferFees: 480_000, otherCosts: 25_000, payFromAccountId: bank, funding: [{ type: 'bankMortgage', lender: 'Mashreq Bank', principal: 7_200_000, annualRatePercent: 5.4, termMonths: 180, firstDueDate: addDays(30) }] });
});

// ---- Finance: bills, payments, journal, budget --------------------------------------------------------------------------
await step('vendor bills and a payment', async () => {
  const b1 = await call('POST', '/finance/bills', { legalEntityId: entity, partyId: vendors[0].id, vendorReference: 'CA-2217', billDate: addDays(-12), dueDate: addDays(18), lines: [{ description: 'AC servicing - Marina Heights', quantity: 1, price: 3200, accountId: expenseAcc }] });
  await call('POST', `/finance/bills/${b1.id}/post`);
  const b2 = await call('POST', '/finance/bills', { legalEntityId: entity, partyId: vendors[1].id, vendorReference: 'PP-0451', billDate: addDays(-40), dueDate: addDays(-10), lines: [{ description: 'Pipe repair', quantity: 2, price: 650, accountId: expenseAcc }] });
  const posted = await call('POST', `/finance/bills/${b2.id}/post`);
  await call('POST', '/finance/payments', { legalEntityId: entity, partyId: vendors[1].id, date: addDays(-2), method: 'bankTransfer', bankAccountId: bank, amount: posted.total, reference: 'PAY-001', allocations: [{ billId: posted.id, amount: posted.total }] });
});
await step('annual budget', async () => {
  const income = accounts.find((a) => a.type === 'income')?.id;
  const year = today.getFullYear();
  const lines = [];
  for (let m = 1; m <= 12; m++) { lines.push({ accountId: expenseAcc, month: m, amount: 9000 }); if (income) lines.push({ accountId: income, month: m, amount: 150000 }); }
  await call('POST', '/finance/budgets', { legalEntityId: entity, name: `FY${year} operating budget`, fiscalYear: year, isActive: true, lines });
});

// ---- HR -----------------------------------------------------------------------------------------------------------------
await step('enable HR module', async () => { await call('POST', '/modules/hr/enable'); await login(); });
const employees = [];
await step('employees', async () => {
  const staff = [['Ahmed Saleem', 'Property Manager', 'Operations', 14000], ['Maria Santos', 'Accountant', 'Finance', 11000], ['Vikram Rao', 'Facilities Technician', 'Maintenance', 6500], ['Layla Haddad', 'Leasing Executive', 'Leasing', 8500], ['Yusuf Khan', 'Receptionist', 'Admin', 4800]];
  for (const [i, [fullName, designation, department, basic]] of staff.entries())
    employees.push(await call('POST', '/m/hr/employees', { legalEntityId: entity, fullName, designation, department, joinDate: addDays(-(400 + i * 120)), email: fullName.split(' ')[0].toLowerCase() + '@demo.local', nationality: 'AE', status: 'active', basicSalary: basic, housingAllowance: Math.round(basic * 0.25), transportAllowance: 500, otherAllowances: 0, recurringDeductions: 0, iban: 'AE07033123456789012345' + i }));
});
await step('attendance and last month payroll', async () => {
  const d = new Date(today.getFullYear(), today.getMonth() - 1, 1);
  const year = d.getFullYear(), month = d.getMonth() + 1;
  for (const [i, e] of employees.entries()) await call('PUT', '/m/hr/attendance', { employeeId: e.id, year, month, workingDays: 22, daysPresent: 22 - (i % 3), unpaidAbsentDays: i % 3 === 2 ? 1 : 0, paidLeaveDays: i % 3 === 1 ? 1 : 0, overtimeHours: i === 2 ? 6 : 0 });
  await call('POST', '/m/hr/payroll', { legalEntityId: entity, year, month });
});


// ---- CRM, inventory, reviews --------------------------------------------------------------------------------------------
await step('CRM listings and leads', async () => {
  const vacant = units.slice(1, 4);
  for (const [i, u] of vacant.entries()) {
    const l = await call('POST', '/m/realestate/listings', { propertyId: u.propertyId ?? properties[0].id, unitId: u.id, kind: 'rent', title: `Unit ${u.unitNumber} for rent`, price: u.rent, channels: 'Website, Bayut' });
    if (i === 0) await call('POST', `/m/realestate/listings/${l.id}/status`, { status: 'published' }).catch(() => {});
  }
  for (const [name, source, days] of [['Hamad Al Suwaidi', 'Website', 2], ['Jessica Moore', 'Walk-in', 5], ['Tariq Mehmood', 'Referral', -1]])
    await call('POST', '/m/realestate/leads', { legalEntityId: entity, name, phone: '+9715011122' + days, email: name.split(' ')[0].toLowerCase() + '@example.com', source, interest: '1 bedroom near the Marina', followUpOn: addDays(days) });
});
await step('enable Inventory and add stock', async () => {
  await call('POST', '/modules/inventory/enable'); await login();
  const wh = await call('POST', '/m/inventory/warehouses', { code: 'MAIN', name: 'Main store', legalEntityId: entity });
  await call('POST', '/m/inventory/warehouses', { code: 'SITE', name: 'Site store', legalEntityId: entity });
  const filter = await call('GET', '/items?search=PRD-FILTER&pageSize=5');
  const item = (filter.items ?? filter)[0];
  const payable = acct('2000');
  await call('POST', '/m/inventory/stock/receive', { warehouseId: wh.id, itemId: item.id, quantity: 40, unitCost: 52, date: addDays(-15), offsetAccountId: payable, reference: 'GRN-001' });
  await call('POST', '/m/inventory/stock/issue', { warehouseId: wh.id, itemId: item.id, quantity: 12, date: addDays(-3), memo: 'Used for AC servicing' });
  await call('PUT', '/m/inventory/stock/policy', { itemId: item.id, reorderLevel: 30, reorderQuantity: 50 });
});
await step('performance review cycle', async () => {
  const cycle = await call('POST', '/m/hr/review-cycles', { name: `H1 ${today.getFullYear()} review`, periodStart: `${today.getFullYear()}-01-01`, periodEnd: `${today.getFullYear()}-06-30`, status: 'open' });
  if (employees[0]) await call('POST', '/m/hr/reviews', { cycleId: cycle.id, employeeId: employees[0].id, overallRating: 4, goals: 'Reduce vacancy days; improve tenant satisfaction', comments: 'Strong half-year.', competencies: [{ name: 'Ownership', score: 4 }, { name: 'Communication', score: 5 }] });
});

console.log(`\nDone: ${ok.length} sections seeded. Sign in as ${EMAIL} (workspace demo-re) and ${'admin@demo.local'} (workspace demo).`);
