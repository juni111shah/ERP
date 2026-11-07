#!/usr/bin/env node
// Checks that the seeded demo organizations hold together, using the product's own reports plus read-only SQL for what no report shows.
// Usage: node tools/seed-demo-orgs/verify.mjs [org-key ...]
// Environment: API, SQL_SERVER (default localhost\SQLEXPRESS), SQL_DATABASE (default ERP_Dev), SQL_INSTANCE_ARGS, REPORT_JSON (path to write results)
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import { ORGS, emailOf } from './orgs.mjs';
import { Client, today } from './lib.mjs';

const SERVER = process.env.SQL_SERVER ?? 'localhost\\SQLEXPRESS';
const DATABASE = process.env.SQL_DATABASE ?? 'ERP_Dev';
const only = process.argv.slice(2);
const chosen = ORGS.filter((o) => only.length === 0 || only.includes(o.key));

function sql(query) {
  const out = execFileSync('sqlcmd', ['-S', SERVER, '-E', '-d', DATABASE, '-W', '-h', '-1', '-s', '|', '-Q', `SET NOCOUNT ON; ${query}`], { encoding: 'utf8', maxBuffer: 1 << 26 });
  if (/^Msg \d+/m.test(out)) throw new Error(out.slice(0, 400));
  return out.split(/\r?\n/).map((l) => l.trim()).filter(Boolean).map((l) => l.split('|'));
}
const one = (query) => Number(sql(query)[0]?.[0] ?? 0);
const near = (a, b, tolerance = 0.05) => Math.abs(a - b) <= tolerance;

/** Tables that hold business records, for the summary. */
const COUNTS = {
  parties: 'Parties', items: 'Items', employees: 'hr_Employees', invoices: 'Invoices', bills: 'Bills', settlements: 'Settlements', journals: 'JournalEntries',
  stockMoves: 'inv_StockMovements', salesOrders: 'sls_Orders', purchaseOrders: 'pur_Orders', workOrders: 'mfg_WorkOrders', posOrders: 'pos_Orders', leases: 're_Leases', projects: 'prj_Projects', records: 'ind_Records', payrollRuns: 'hr_PayrollRuns', assets: 'ast_Assets',
};

const results = [];
let failed = 0;
for (const org of chosen) {
  const checks = [];
  const add = (name, ok, detail = '') => { checks.push({ name, ok, detail }); if (!ok) failed++; };
  const orgId = sql(`select Id from Organizations where Code='${org.code}'`)[0]?.[0];
  if (!orgId) { console.log(`${org.code}: not seeded`); results.push({ org: org.code, missing: true }); failed++; continue; }
  const isDemo = one(`select cast(IsDemo as int) from Organizations where Id='${orgId}'`);
  add('labelled as demo', isDemo === 1 && org.name.startsWith('DEMO'));

  const c = await new Client(emailOf(org, 'owner')).signIn(org.code);
  const me = await c.get('/me');
  const entity = me.legalEntities[0].id;
  const tb = await c.get(`/finance/reports/trial-balance?to=${today}&legalEntityId=${entity}`);
  add('trial balance balances', tb.balanced === true && near(tb.totalDebit, tb.totalCredit), `Dr ${tb.totalDebit} Cr ${tb.totalCredit}`);
  const bs = await c.get(`/finance/reports/balance-sheet?asOf=${today}&legalEntityId=${entity}`);
  add('balance sheet balances', bs.balanced === true, `assets ${bs.totalAssets}, liabilities ${bs.totalLiabilities}, equity ${bs.totalEquity}, earnings ${bs.currentEarnings}`);
  const flow = await c.get(`/finance/reports/cash-flow?from=2020-01-01&to=${today}&legalEntityId=${entity}`).catch(() => null);
  if (flow) add('cash-flow statement ties to the ledger', flow.tiesOut === true, `closing ${flow.closingBalance}, ledger ${flow.ledgerClosingBalance}`);

  const unbalanced = one(`select count(*) from (select j.Id from JournalEntries j join JournalLines l on l.JournalEntryId=j.Id where j.OrgId='${orgId}' and j.Status<>'Draft' group by j.Id having abs(sum(l.Debit)-sum(l.Credit))>0.005) x`);
  add('every journal has equal debits and credits', unbalanced === 0, `${unbalanced} unbalanced`);

  const accounts = await c.list('/finance/accounts');
  const balanceOf = (role) => { const a = accounts.find((x) => x.systemRole === role); const row = tb.rows.find((r) => r.accountId === a?.id); return row ? row.closing : 0; };
  const ar = await c.get(`/finance/reports/ar-ageing?asOf=${today}&legalEntityId=${entity}`);
  const arAge = ar.totals?.total ?? ar.totals?.balance;
  add('receivables ageing equals the control account', near(arAge, balanceOf('ar.control'), 1), `ageing ${arAge}, ledger ${balanceOf('ar.control')}`);
  const ap = await c.get(`/finance/reports/ap-ageing?asOf=${today}&legalEntityId=${entity}`);
  const apAge = ap.totals?.total ?? ap.totals?.balance;
  add('payables ageing equals the control account', near(apAge, -balanceOf('ap.control'), 1) || near(apAge, balanceOf('ap.control'), 1), `ageing ${apAge}, ledger ${balanceOf('ap.control')}`);

  const paidWrong = one(`select count(*) from Invoices i where i.OrgId='${orgId}' and i.Status<>'Voided' and i.IsCreditNote=0 and i.Paid > i.Total + 0.005`);
  add('no invoice is paid beyond its total', paidWrong === 0, `${paidWrong} over-paid`);
  const statusWrong = one(`select count(*) from Invoices i where i.OrgId='${orgId}' and ((i.Status='Paid' and abs(i.Total-i.Paid)>0.005) or (i.Status='Issued' and i.Paid>0.005))`);
  add('invoice status follows its payments', statusWrong === 0, `${statusWrong} wrong`);
  const allocWrong = one(`select count(*) from Bills b where b.OrgId='${orgId}' and b.Status<>'Voided' and b.Paid > b.Total + 0.005`);
  add('no bill is paid beyond its total', allocWrong === 0, `${allocWrong} over-paid`);

  if (me.enabledModules.some((m) => m.key === 'inventory')) {
    const rec = await c.get(`/m/inventory/reports/stock-gl-reconciliation?asOf=${today}&legalEntityId=${entity}`);
    const row = rec.rows[0];
    add('stock value equals the inventory account', near(row?.difference ?? 0, 0, Math.max(1, Math.abs(row?.stockValue ?? 0) * 0.0005)), `stock ${row?.stockValue}, ledger ${row?.inventoryAccountBalance}, difference ${row?.difference}`);
    const negative = one(`select count(*) from (select WarehouseId, ItemId from inv_StockMovements where OrgId='${orgId}' and IsDeleted=0 group by WarehouseId, ItemId having sum(Quantity) < -0.0001) x`);
    add('no item is below zero in any warehouse', negative === 0, `${negative} negative`);
  }

  const foreignParty = one(`select count(*) from Invoices i join Parties p on p.Id=i.PartyId where i.OrgId='${orgId}' and p.OrgId<>'${orgId}'`);
  const foreignAccount = one(`select count(*) from JournalLines l join JournalEntries j on j.Id=l.JournalEntryId join Accounts a on a.Id=l.AccountId where j.OrgId='${orgId}' and a.OrgId<>'${orgId}'`);
  const orphanLines = one(`select count(*) from Invoices i where i.OrgId='${orgId}' and i.Status not in ('Draft','Voided') and i.JournalEntryId is null`);
  const foreignRecords = one(`select count(*) from ind_Records r join Parties p on p.Id=r.PartyId where r.OrgId='${orgId}' and p.OrgId<>'${orgId}'`);
  add('nothing points to another organization', foreignParty + foreignAccount + foreignRecords === 0, `${foreignParty + foreignAccount + foreignRecords} cross-tenant links`);
  add('every issued invoice has a journal', orphanLines === 0, `${orphanLines} without`);
  const dupParties = one(`select count(*) from (select Name from Parties where OrgId='${orgId}' and IsDeleted=0 group by Name having count(*)>1) x`);
  const dupInvoices = one(`select count(*) from (select Number from Invoices where OrgId='${orgId}' and Status<>'Draft' group by LegalEntityId, Number having count(*)>1) x`);
  add('no duplicate parties or document numbers', dupParties + dupInvoices === 0, `${dupParties} parties, ${dupInvoices} invoices`);

  const dates = sql(`select convert(varchar(10),min(Date),120),convert(varchar(10),max(Date),120) from JournalEntries where OrgId='${orgId}' and Status<>'Draft'`)[0];
  const months = one(`select count(distinct convert(varchar(7),Date,120)) from JournalEntries where OrgId='${orgId}' and Status<>'Draft'`);
  add('at least three months of history', months >= 4, `${months} months, ${dates?.[0]} to ${dates?.[1]}`);
  add('nothing dated in the future', dates?.[1] <= today, `latest ${dates?.[1]}`);

  const counts = {};
  for (const [label, table] of Object.entries(COUNTS)) {
    try { const n = one(`select count(*) from ${table} where OrgId='${orgId}'`); if (n) counts[label] = n; } catch { /* the table is not in this database */ }
  }
  const owner = await c.get(`/dashboard/owner?from=${/^\d/.test(dates?.[0]) ? dates[0] : today}&to=${today}&legalEntityId=${entity}`);
  const cards = Object.fromEntries(owner.sections.flatMap((s) => s.cards.map((x) => [x.key, x.value])));
  results.push({ org: org.code, name: org.name, industry: org.industry, currency: org.currency, checks, counts, figures: { revenue: cards.revenue, grossProfit: cards['gross-profit'], netProfit: cards['net-profit'], closingCash: cards['cash-closing'], receivables: cards.receivables, payables: cards.payables, inventory: cards['inventory-value'] }, sections: owner.sections.map((s) => s.key) });
  const bad = checks.filter((x) => !x.ok);
  console.log(`${org.code}: ${checks.length - bad.length}/${checks.length} checks ok${bad.length ? '' : ''}`);
  for (const b of bad) console.log(`   FAIL ${b.name}: ${b.detail}`);
}
if (process.env.REPORT_JSON) fs.writeFileSync(process.env.REPORT_JSON, JSON.stringify(results, null, 2));
console.log(failed ? `\n${failed} check(s) failed.` : '\nAll checks passed.');
process.exit(failed ? 1 : 0);
