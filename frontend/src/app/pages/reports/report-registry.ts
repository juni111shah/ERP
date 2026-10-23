import { currentLang, phraseNow, tNow } from '../../core/i18n/translate-now';

/** Declarative definitions of the server-built reports. A report page needs no code of its own: add an entry here. */
export type ColType = 'text' | 'money' | 'number' | 'percent' | 'date';
export interface ReportColumn { key: string; label: string; type?: ColType; }
export type ReportFilter = 'range' | 'asOf' | 'weeks' | 'months' | 'optionalAsOf' | 'owner';

export interface ReportDef {
  slug: string;
  title: string;
  subtitle: string;
  /** API path under the base url */
  endpoint: string;
  filters: ReportFilter[];
  /** module key the report belongs to (hidden when the module is off); omitted for core */
  module?: string;
  columns: (data: any) => ReportColumn[];
  rows: (data: any) => Record<string, unknown>[];
  /** a closing row shown in bold */
  totals?: (data: any) => Record<string, unknown> | null;
  /** an extra button (for example posting the end-of-service provision); needs a single legal entity in scope */
  action?: { label: string; path: string; permission: string; done: string };
  /** headline numbers above the table */
  summary?: (data: any) => { label: string; value: string }[];
}

const m = (key: string, label: string): ReportColumn => ({ key, label, type: 'money' });
const n = (key: string, label: string): ReportColumn => ({ key, label, type: 'number' });
const p = (key: string, label: string): ReportColumn => ({ key, label, type: 'percent' });
const t = (key: string, label: string): ReportColumn => ({ key, label });
const d = (key: string, label: string): ReportColumn => ({ key, label, type: 'date' });
const fmt = (v: number) => v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export const REPORTS: ReportDef[] = [
  {
    slug: 'tax-summary', title: 'Tax summary', subtitle: 'Output tax on issued invoices and credit notes against recoverable input tax on posted bills.',
    endpoint: 'finance/reports/tax-summary', filters: ['range'],
    columns: () => [t('section', 'Section'), t('code', 'Tax code'), t('name', 'Description'), n('rate', 'Rate %'), m('net', 'Net'), m('tax', 'Tax')],
    rows: (x) => x.rows, totals: undefined,
    summary: (x) => [{ label: 'Output tax', value: fmt(x.outputTax) }, { label: 'Recoverable input tax', value: fmt(x.inputTax) }, { label: 'Net tax payable', value: fmt(x.netPayable) }, { label: 'Tax not recoverable (in costs)', value: fmt(x.nonRecoverableTax) }],
  },
  {
    slug: 'cash-movements', title: 'Cash movements', subtitle: 'Bank and cash ledger activity: opening, inflows, outflows and closing per account.',
    endpoint: 'finance/reports/cash-movements', filters: ['range'],
    columns: () => [t('code', 'Account'), t('name', 'Name'), m('opening', 'Opening'), m('inflows', 'Inflows'), m('outflows', 'Outflows'), m('closing', 'Closing')],
    rows: (x) => x.accounts,
    totals: (x) => ({ code: '', name: 'Total', opening: x.opening, inflows: x.inflows, outflows: x.outflows, closing: x.closing }),
    summary: (x) => (x.bySource as { source: string; inflows: number; outflows: number }[]).map((s) => ({ label: tNow('ui.report.bySource', 'By source: {source}', { source: sourceLabel(s.source) }), value: tNow('ui.report.inOut', 'in {in} / out {out}', { in: fmt(s.inflows), out: fmt(s.outflows) }) })),
  },
  {
    slug: 'cash-forecast', title: 'Cash forecast', subtitle: 'Expected cash from cheques and receivables, less bills and other commitments, week by week. Overdue items fall into the first week.',
    endpoint: 'finance/reports/cash-forecast', filters: ['weeks'],
    columns: (x) => [d('weekOf', 'Week of'), ...((x?.sources ?? []) as { key: string; label: string }[]).map((s) => m(s.key, s.label)), m('net', 'Net'), m('projected', 'Projected balance')],
    rows: (x) => x.weeks.map((w: any) => ({ weekOf: w.weekOf, ...w.amounts, net: w.net, projected: w.projected })),
    summary: (x) => [{ label: 'Cash today', value: fmt(x.cashToday) }, { label: 'Projected at the end', value: fmt(x.endBalance) }, { label: 'Lowest projected balance', value: fmt(x.lowestBalance) }],
  },
  {
    slug: 'owner-statement', title: 'Owner statement', subtitle: 'Rent collected for the owner\'s managed properties, less the management fee and completed maintenance. Cash basis.', module: 'realestate',
    endpoint: 'm/realestate/reports/owner-statement', filters: ['owner', 'range'],
    columns: () => [t('property', 'Property'), p('feePercent', 'Fee %'), m('rentCollected', 'Rent collected'), m('managementFee', 'Management fee'), m('maintenanceCosts', 'Maintenance'), m('netToOwner', 'Net to owner')],
    rows: (x) => x.rows,
    totals: (x) => ({ property: 'Total', feePercent: null, rentCollected: x.rentCollected, managementFee: x.managementFee, maintenanceCosts: x.maintenanceCosts, netToOwner: x.netToOwner }),
    summary: (x) => [{ label: 'Owner', value: x.owner }, { label: 'Net payable to owner', value: fmt(x.netToOwner) }],
  },
  {
    slug: 'rent-roll', title: 'Rent roll', subtitle: 'Active leases with rent per area, held cheques and what each tenant owes.', module: 'realestate',
    endpoint: 'm/realestate/reports/rent-roll', filters: ['asOf'],
    columns: () => [t('property', 'Property'), t('unit', 'Unit'), t('unitType', 'Type'), n('areaSqft', 'Area sqft'), t('tenant', 'Tenant'), t('leaseNumber', 'Lease'), d('start', 'Start'), d('expiry', 'Expiry'),
      n('daysLeft', 'Days left'), m('annualRent', 'Annual rent'), m('rentPerSqft', 'Rent / sqft'), t('vatStatus', 'VAT'), m('pdcsHeld', 'PDCs held'), m('outstanding', 'Outstanding')],
    rows: (x) => x.rows,
    totals: (x) => ({ property: 'Total', annualRent: x.totalAnnualRent, outstanding: x.totalOutstanding }),
  },
  {
    slug: 'occupancy', title: 'Occupancy', subtitle: 'Registered units by status, contracted rent and what vacant units could earn.', module: 'realestate',
    endpoint: 'm/realestate/reports/occupancy', filters: [],
    columns: () => [t('property', 'Property'), n('totalUnits', 'Total units'), n('registered', 'Registered'), n('occupied', 'Occupied'), n('vacant', 'Vacant'), n('other', 'Other'), p('occupancyPercent', 'Occupancy'), m('contractedRent', 'Contracted rent'), m('vacantAskingRent', 'Vacant asking rent')],
    rows: (x) => x.rows,
    summary: (x) => [{ label: 'Occupancy', value: `${x.occupancyPercent}%` }, { label: 'Contracted rent', value: fmt(x.contractedRent) }, { label: 'Vacancy (asking rent)', value: fmt(x.vacancyAskingRent) }, { label: 'Vacant units', value: String(x.vacantUnits) }],
  },
  {
    slug: 'portfolio-performance', title: 'Portfolio performance', subtitle: 'NOI before financing: rent invoiced less maintenance spend; yield annualised from the period against indicative value.', module: 'realestate',
    endpoint: 'm/realestate/reports/portfolio-performance', filters: ['range'],
    columns: () => [t('property', 'Property'), m('value', 'Value'), p('occupancyPercent', 'Occupancy'), m('income', 'Income'), m('operatingExpenses', 'Operating expenses'), m('noi', 'NOI'), p('annualisedYieldPercent', 'Annualised yield'), m('billed', 'Billed'), m('collected', 'Collected'), p('collectionRatePercent', 'Collection rate')],
    rows: (x) => [...x.rows],
    totals: (x) => x.total,
  },
  {
    slug: 'lease-expiries', title: 'Lease expiries', subtitle: 'Active leases by month of expiry. Plan renewals early.', module: 'realestate',
    endpoint: 'm/realestate/reports/lease-expiries', filters: ['months'],
    columns: () => [t('label', 'Month'), n('leases', 'Leases expiring'), m('annualRentAtRisk', 'Annual rent at risk')],
    rows: (x) => x.rows, totals: (x) => ({ label: 'Total', leases: x.totalLeases, annualRentAtRisk: x.totalRentAtRisk }),
  },
  {
    slug: 'maintenance-costs', title: 'Maintenance costs', subtitle: 'Work orders reported in the period, estimated against actual cost.', module: 'realestate',
    endpoint: 'm/realestate/reports/maintenance-costs', filters: ['range'],
    columns: () => [t('property', 'Property'), t('category', 'Category'), n('workOrders', 'Work orders'), n('open', 'Open'), m('estimated', 'Estimated'), m('actual', 'Actual')],
    rows: (x) => x.rows, totals: (x) => ({ property: 'Total', workOrders: x.workOrders, open: x.open, estimated: x.estimated, actual: x.actual }),
  },
  {
    slug: 'commissions', title: 'Brokerage commissions', subtitle: 'Deals by closing agent, excluding lost deals.', module: 'realestate',
    endpoint: 'm/realestate/reports/commissions', filters: ['range'],
    columns: () => [t('agent', 'Agent'), n('deals', 'Deals'), m('dealValue', 'Deal value'), m('grossCommission', 'Gross commission'), m('agentShare', 'Agent share'), m('teamLeadShare', 'Team lead share'), m('companyShare', 'Company share'), m('paidOut', 'Paid out')],
    rows: (x) => x.rows, totals: (x) => x.total,
  },
  {
    slug: 'gratuity', title: 'End-of-service gratuity', subtitle: 'Accrued end-of-service benefit per employee under the employing entity\'s country rule.', module: 'hr',
    endpoint: 'm/hr/reports/gratuity', filters: ['asOf'],
    columns: () => [t('number', 'No.'), t('employee', 'Employee'), d('joined', 'Joined'), m('basicSalary', 'Basic salary'), n('serviceYears', 'Service (yrs)'), t('eligible', 'Eligible'), n('entitlementDays', 'Entitlement (days)'), m('gratuity', 'Gratuity')],
    rows: (x) => x.rows.map((r: any) => ({ ...r, eligible: r.eligible ? 'Yes' : 'No (under the minimum service)' })),
    totals: (x) => ({ employee: 'Total liability', gratuity: x.totalLiability }),
    summary: (x) => (x.rule ? [{ label: 'Rule', value: x.rule }] : [{ label: 'Rule', value: 'No end-of-service rule for this country' }]),
    action: { label: 'Post provision', path: 'm/hr/reports/gratuity/provision', permission: 'hr.payroll.post', done: 'Provision posted' },
  },
];

/** Reports that live on the ledger reports page (tabs) rather than a generic page. */
export const LEDGER_REPORT_LINKS = [
  { title: 'Profit and loss', note: 'Income and expenses for a period', link: '/finance/reports', tab: 2 },
  { title: 'Balance sheet', note: 'Assets, liabilities and equity as of a date', link: '/finance/reports', tab: 3 },
  { title: 'Trial balance', note: 'Opening, movement and closing per account', link: '/finance/reports', tab: 0 },
  { title: 'General ledger', note: 'Running balance of one account', link: '/finance/reports', tab: 1 },
  { title: 'Receivables ageing', note: 'Open invoices by days past due', link: '/finance/reports', tab: 4 },
  { title: 'Payables ageing', note: 'Open bills by days past due', link: '/finance/reports', tab: 5 },
];

/** A cash-movement source code as shown in the summary: the raw code in English (unchanged), its Arabic label otherwise. */
function sourceLabel(source: string): string {
  if (currentLang() !== 'ar') return source;
  const spaced = String(source ?? '').replace(/([a-z])([A-Z])/g, '$1 $2').replace(/[._-]+/g, ' ').trim();
  return phraseNow(source) !== source ? phraseNow(source) : phraseNow(spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase());
}
