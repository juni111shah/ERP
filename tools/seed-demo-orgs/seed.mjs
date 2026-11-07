#!/usr/bin/env node
// Seeds the labelled DEMO organizations through the public API. See README.md here. Safe to re-run: finished steps are skipped.
// Usage: node tools/seed-demo-orgs/seed.mjs [org-key ...]     Environment: API (base URL, default http://localhost:5390/api/v1)
import { ORGS } from './orgs.mjs';
import { counters, API } from './lib.mjs';
import { buildFoundation, stockLevel } from './foundation.mjs';
import { months, purchaseCycle, salesCycle, monthlyExpenses, expenseClaims, budget, fixedAssets, peopleAndPayroll, crmPipeline } from './flows.mjs';
import { SCENARIOS } from './scenarios.mjs';

const only = process.argv.slice(2);
const chosen = ORGS.filter((o) => only.length === 0 || only.includes(o.key));

async function seedOrg(org) {
  const log = (m) => console.log(m);
  console.log(`\n${org.name} (${org.code})`);
  const ctx = await buildFoundation(org, log);
  ctx.buyable = () => ctx.stockItems;
  ctx.sellable = () => ctx.items.filter((i) => i.price > 0);
  ctx.stockFactor = (it) => (it.sku === 'MLK-RAW' ? 80 : stockLevel(it) / 30);

  const scenario = SCENARIOS[org.scenario];
  if (scenario) await scenario(ctx);

  const volume = org.volume ?? 3;
  const purchaseEnd = org.trading ? ['paid', 'paid', 'paid', 'part-paid', 'billed', 'received', 'returned', 'partial', 'approved', 'cancelled', 'draft'] : ['paid', 'paid', 'paid', 'part-paid', 'billed', 'cancelled'];
  const salesEnd = ['paid', 'paid', 'paid', 'paid', 'part-paid', 'overdue', 'returned', 'partial', 'open', 'cancelled', 'voided', 'quote-lost', 'quote-open', 'draft'];
  const buys = (org.trading || org.boms || org.scenario === 'construction' || org.scenario === 'healthcare') && ctx.modules.has('purchasing') && ctx.stockItems.length > 0;
  for (const first of months(5)) {
    // A month in business order, so stock is there when it is needed.
    ctx.band = [1, 5];
    if (buys) {
      await ctx.run(`purchases-${first.slice(0, 7)}`, async () => {
        for (let n = 1; n <= volume + 2; n++) await purchaseCycle(ctx, first, n, purchaseEnd[(n + months(5).indexOf(first)) % purchaseEnd.length]);
      });
    }
    ctx.band = [12, 18];
    if (ctx.monthly) await ctx.monthly(first);
    ctx.band = [22, 26];
    if (org.trading) {
      await ctx.run(`sales-${first.slice(0, 7)}`, async () => {
        for (let n = 1; n <= (org.salesVolume ?? volume * 2); n++) await salesCycle(ctx, first, n, salesEnd[(n + months(5).indexOf(first)) % salesEnd.length]);
      });
    }
    ctx.band = null;
  }

  for (const first of months(5)) await ctx.run(`expenses-${first.slice(0, 7)}`, async () => { await monthlyExpenses(ctx, first, org.salaryBase); for (let n = 0; n < 3; n++) await expenseClaims(ctx, first, n + months(5).indexOf(first)); });
  await peopleAndPayroll(ctx);
  await ctx.run('budget', () => budget(ctx, org.salaryBase * org.employees.length * 3));
  await ctx.run('crm', () => crmPipeline(ctx));
  await ctx.run('fixed-assets', () => fixedAssets(ctx, [['Delivery vehicle', org.salaryBase * 12, org.employees[1]], ['Office equipment', org.salaryBase * 4, org.employees[2]], ['IT equipment', org.salaryBase * 3, org.employees[3]]]));
  await ctx.run('contracts', async () => {
    if (!ctx.modules.has('contracts')) return;
    const start = months(5)[0];
    for (const [title, party, direction, amount, kind] of [['Annual service agreement', ctx.customers[0], 'Customer', org.salaryBase * 20, 'Service'], ['Supply framework', ctx.suppliers[0], 'Supplier', org.salaryBase * 30, 'Framework']]) {
      ctx.on(start);
      const c = await ctx.owner.post('/m/contracts/records/agreements', { legalEntityId: ctx.entity, title, recordDate: start, partyId: party.id, amount, dueDate: new Date(Date.parse(start) + 365 * 86400000).toISOString().slice(0, 10), data: { direction, kind, startsOn: start, noticeDays: 60, autoRenew: false } });
      await ctx.owner.post(`/m/contracts/records/agreements/${c.id}/transition`, { to: 'submitted' });
      await ctx.people.admin.post(`/m/contracts/records/agreements/${c.id}/transition`, { to: 'active' });
    }
  });
  ctx.on(null);
}

const started = Date.now();
console.log(`Seeding ${chosen.length} demo organization(s) through ${API}`);
const failures = [];
for (const org of chosen) {
  try { await seedOrg(org); }
  catch (e) {
    console.error(`  FAILED ${org.code}: ${e.message}`);
    failures.push(org.code);
    if (process.env.STOP_ON_ERROR === '1') { console.error(e.stack); process.exit(1); }
  }
}
console.log(`\nDone in ${((Date.now() - started) / 60000).toFixed(1)} min, ${counters.calls} API calls.${failures.length ? ` Failed: ${failures.join(', ')}. Re-run to continue them.` : ''}`);
process.exit(failures.length ? 1 : 0);
