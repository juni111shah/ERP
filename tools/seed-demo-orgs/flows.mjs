// Connected business cycles, each through the real endpoints. Documents are dated by pinning the server's clock to the day they happen.
import { addDays, clampToToday, monthStart, round2, today } from './lib.mjs';

const lastDayOf = (first) => { const d = new Date(first + 'T00:00:00Z'); d.setUTCMonth(d.getUTCMonth() + 1); d.setUTCDate(0); return d.toISOString().slice(0, 10); };
export const months = (count = 5) => Array.from({ length: count + 1 }, (_, i) => monthStart(count - i)).filter((m) => m <= today);
export const dayIn = (ctx, first) => clampToToday(addDays(first, ctx.random.int(1, 26)));

const taxOf = (ctx) => ctx.taxRate;
const unit = (ctx) => ctx.units[ctx.random.int(0, ctx.units.length - 1)];

/** Tracks stock in the script so documents never oversell: the product refuses that now. */
export function stockBook(ctx) {
  if (!ctx.onHand) ctx.onHand = {};
  return ctx.onHand;
}

async function refreshStock(ctx) {
  const stock = await ctx.owner.get(`/m/inventory/stock?warehouseId=${ctx.warehouse}`);
  ctx.onHand = {};
  for (const row of stock.rows ?? []) ctx.onHand[row.itemId] = (ctx.onHand[row.itemId] ?? 0) + row.quantity;
}

// ---- Procure to Pay --------------------------------------------------------------------------------------------------------------------

export async function purchaseCycle(ctx, month, n, outcome = 'paid', chosen) {
  const { people, random, entity } = ctx;
  const buyer = people.purchase ?? people.owner;
  const date = dayIn(ctx, month);
  ctx.on(date);
  const supplier = random.pick(ctx.suppliers.slice(0, Math.max(1, ctx.suppliers.length - 1)));
  const goods = chosen ?? Array.from(new Set(Array.from({ length: random.int(1, 3) }, () => random.pick(ctx.buyable())))).map((it) => ({ it, quantity: Math.max(1, Math.round(random.int(4, 12) * (ctx.stockFactor?.(it) ?? 1))) }));
  const requisition = outcome !== 'draft' && random.chance(0.4);
  let requisitionId;
  const lines = goods.map(({ it, quantity }) => ({ itemId: it.id, itemCode: it.sku, description: it.name, quantity, unitOfMeasure: it.uom, unitPrice: it.cost, estimatedUnitPrice: it.cost, discountPercent: 0, taxRate: taxOf(ctx) }));
  if (requisition) {
    const req = await buyer.post('/m/purchasing/requisitions', { legalEntityId: entity, requesterName: 'Purchase Manager', requisitionDate: date, department: ctx.org.departments[0], reason: 'Replenishment', lines });
    await buyer.put(`/m/purchasing/requisitions/${req.id}/status`, { status: 'pendingApproval' });
    await people.owner.put(`/m/purchasing/requisitions/${req.id}/status`, { status: 'approved' });
    requisitionId = req.id;
  }
  const order = await buyer.post('/m/purchasing/orders', {
    legalEntityId: entity, vendorPartyId: supplier.id, vendorName: supplier.name, orderDate: date, currency: ctx.org.currency, expectedDeliveryDate: addDays(date, random.int(5, 14)),
    vendorReference: `SUP-${ctx.org.key.slice(0, 3).toUpperCase()}-${month.slice(2, 7)}-${n}`, paymentTerms: 'Net 30', requisitionId, lines,
  });
  if (outcome === 'draft') return order;
  await buyer.put(`/m/purchasing/orders/${order.id}/status`, { status: 'submitted' });
  if (outcome === 'cancelled') {
    await buyer.put(`/m/purchasing/orders/${order.id}/status`, { status: 'cancelled' });
    return order;
  }
  await people.owner.put(`/m/purchasing/orders/${order.id}/status`, { status: 'approved' });
  if (outcome === 'approved') return order;

  const receiptDate = clampToToday(addDays(date, random.int(2, 6)));
  ctx.on(receiptDate);
  const detail = await buyer.get(`/m/purchasing/orders/${order.id}`);
  const part = outcome === 'partial' ? 0.5 : 1;
  await (people.inventory ?? buyer).post('/m/purchasing/receipts', {
    legalEntityId: entity, purchaseOrderId: order.id, receivedBy: 'Inventory Manager', receiptDate, deliveryNoteNumber: `DN-${month.slice(2, 7)}-${n}`, warehouseId: ctx.warehouse,
    lines: detail.lines.map((l) => ({ purchaseOrderLineId: l.id, description: l.description, quantityReceived: Math.max(1, Math.floor(l.quantity * part)) })),
  });
  if (outcome === 'partial' || outcome === 'received') return order;

  const billDate = clampToToday(addDays(receiptDate, random.int(1, 4)));
  ctx.on(billDate);
  const finance = people.finance ?? people.owner;
  const bill = await finance.post(`/m/purchasing/orders/${order.id}/convert-to-bill`, {});
  const posted = await finance.post(`/finance/bills/${bill.id}/post`, {});
  if (outcome === 'overdue' || outcome === 'billed') return posted;
  const payDate = clampToToday(addDays(billDate, random.int(5, 25)));
  ctx.on(payDate);
  const total = posted.total;
  const amount = outcome === 'part-paid' ? round2(total * 0.5) : total;
  await finance.post('/finance/payments', {
    legalEntityId: entity, partyId: supplier.id, date: payDate, method: random.chance(0.8) ? 'BankTransfer' : 'Cash', bankAccountId: random.chance(0.8) ? ctx.bank : ctx.bank,
    amount, reference: `PMT-${month.slice(2, 7)}-${n}`, allocations: [{ billId: posted.id, amount }],
  });
  return posted;
}

// ---- Order to Cash ---------------------------------------------------------------------------------------------------------------------

function chooseSale(ctx) {
  const { random } = ctx;
  const sellable = ctx.sellable();
  const chosen = new Map();
  const lines = random.int(1, 4);
  for (let i = 0; i < lines; i++) {
    const it = random.pick(sellable);
    const onHand = ctx.onHand?.[it.id] ?? 0;
    const want = Math.max(1, Math.round(random.int(1, 8) * (ctx.stockFactor?.(it) ?? 1)));
    const quantity = it.service ? want : Math.min(want, Math.floor(onHand * 0.4));
    if (quantity > 0 && !chosen.has(it.id)) chosen.set(it.id, { it, quantity });
  }
  return [...chosen.values()];
}

export async function salesCycle(ctx, month, n, outcome = 'paid') {
  const { people, random, entity } = ctx;
  const seller = people.sales ?? people.owner;
  const date = dayIn(ctx, month);
  ctx.on(date);
  await refreshStock(ctx);
  const goods = chooseSale(ctx);
  if (goods.length === 0) return null;
  const customer = random.pick(ctx.customers);
  const lines = goods.map(({ it, quantity }) => ({ itemId: it.id, itemCode: it.sku, description: it.name, quantity, unitOfMeasure: it.uom, unitPrice: it.price, discountPercent: random.pick([0, 0, 0, 2, 5]), taxRate: taxOf(ctx) }));

  let quoteId;
  if (random.chance(0.5) || outcome === 'quote-lost' || outcome === 'quote-open') {
    const quote = await seller.post('/m/sales/quotes', { legalEntityId: entity, customerPartyId: customer.id, customerName: customer.name, quoteDate: date, currency: ctx.org.currency, validUntil: addDays(date, 30), terms: 'Valid 30 days', lines });
    if (outcome === 'quote-open') return quote;
    await seller.put(`/m/sales/quotes/${quote.id}/status`, { status: 'sent' });
    if (outcome === 'quote-lost') { await seller.put(`/m/sales/quotes/${quote.id}/status`, { status: 'declined' }); return quote; }
    await seller.put(`/m/sales/quotes/${quote.id}/status`, { status: 'accepted' });
    quoteId = quote.id;
  }
  const order = quoteId
    ? await seller.post(`/m/sales/quotes/${quoteId}/convert-to-order`, {})
    : await seller.post('/m/sales/orders', { legalEntityId: entity, customerPartyId: customer.id, customerName: customer.name, orderDate: date, currency: ctx.org.currency, expectedDeliveryDate: addDays(date, 5), paymentTerms: 'Net 30', lines });
  if (!quoteId && outcome !== 'draft') await seller.put(`/m/sales/orders/${order.id}/status`, { status: 'confirmed' });
  if (outcome === 'draft' || outcome === 'open') return order;
  if (outcome === 'cancelled') { await seller.put(`/m/sales/orders/${order.id}/status`, { status: 'cancelled' }); return order; }

  const deliveryDate = clampToToday(addDays(date, random.int(1, 4)));
  ctx.on(deliveryDate);
  const detail = await seller.get(`/m/sales/orders/${order.id}`);
  const part = outcome === 'partial';
  await (people.inventory ?? seller).put(`/m/sales/orders/${order.id}/fulfillment`, {
    deliveryDate, warehouseId: ctx.warehouse,
    lines: detail.lines.map((l) => ({ lineId: l.id, deliveredQuantity: part ? Math.max(1, Math.floor(l.quantity / 2)) : l.quantity })),
  });
  if (part) return order;

  const invoiceDate = clampToToday(addDays(deliveryDate, random.int(0, 2)));
  ctx.on(invoiceDate);
  const finance = people.finance ?? people.owner;
  const invoice = await seller.post(`/m/sales/orders/${order.id}/convert-to-invoice`, {});
  const issued = await finance.post(`/finance/invoices/${invoice.id}/issue`, {});
  if (outcome === 'invoiced' || outcome === 'overdue') return issued;
  if (outcome === 'voided') { await finance.post(`/finance/invoices/${invoice.id}/void`, { reason: 'Issued against the wrong customer account' }); return issued; }
  const payDate = clampToToday(addDays(invoiceDate, random.int(4, 28)));
  ctx.on(payDate);
  const amount = outcome === 'part-paid' ? round2(issued.total * 0.5) : issued.total;
  await finance.post('/finance/receipts', { legalEntityId: entity, partyId: customer.id, date: payDate, method: random.pick(['BankTransfer', 'BankTransfer', 'Cash']), bankAccountId: ctx.bank, amount, reference: `RCV-${month.slice(2, 7)}-${n}`, allocations: [{ invoiceId: issued.id, amount }] });
  return { order, issued };
}

// ---- Direct expenses, budget and fixed assets ---------------------------------------------------------------------------------------------

export async function monthlyExpenses(ctx, month, scale) {
  const { people, random, entity } = ctx;
  const finance = people.finance ?? people.owner;
  const utilityVendor = ctx.suppliers[ctx.suppliers.length - 1];
  const date = clampToToday(addDays(month, random.int(2, 6)));
  ctx.on(date);
  const costs = [['Premises rent', ctx.accountCode('6100'), scale * 1.1], ['Electricity and water', ctx.accountCode('6200'), scale * 0.22], ['General running costs', ctx.accountCode('6900'), scale * 0.15]];
  for (const [label, accountId, amount] of costs) {
    const unitId = unit(ctx)?.id;
    const bill = await finance.post('/finance/bills', {
      legalEntityId: entity, partyId: utilityVendor.id, vendorReference: `EXP-${month.slice(2, 7)}-${accountId.slice(0, 4)}`, billDate: date, dueDate: addDays(date, 15), businessUnitId: unitId,
      lines: [{ description: `${label} ${month.slice(0, 7)}`, quantity: 1, price: random.money(amount * 0.9, amount * 1.1, 10), accountId }],
    });
    const posted = await finance.post(`/finance/bills/${bill.id}/post`, {});
    if (month !== monthStart(0)) {
      const payOn = clampToToday(addDays(date, random.int(3, 12)));
      ctx.on(payOn);
      await finance.post('/finance/payments', { legalEntityId: entity, partyId: utilityVendor.id, date: payOn, method: 'BankTransfer', bankAccountId: ctx.bank, amount: posted.total, reference: `UTL-${posted.number}`, allocations: [{ billId: posted.id, amount: posted.total }] });
      ctx.on(date);
    }
  }
}

export async function expenseClaims(ctx, month, n) {
  if (!ctx.modules.has('expenses')) return;
  const { people, random, entity } = ctx;
  const maker = people.ops ?? people.admin;
  const date = dayIn(ctx, month);
  ctx.on(date);
  const payee = ctx.suppliers[0];
  const statuses = ['billed', 'billed', 'approved', 'submitted', 'rejected', 'draft'];
  const outcome = statuses[n % statuses.length];
  const scale = ctx.org.salaryBase / 50;
  const claim = await people.owner.post('/m/expenses/records/claims', {
    legalEntityId: entity, title: random.pick(['Client visit', 'Site travel', 'Supplier meeting', 'Team training', 'Trade fair']), recordDate: date, partyId: payee.id, businessUnitId: unit(ctx)?.id,
    data: { department: random.pick(ctx.org.departments), category: random.pick(['Travel', 'Meals', 'Accommodation', 'Fuel', 'Office']) },
    lines: [{ description: 'Transport', quantity: 1, unitPrice: random.money(scale, scale * 4, 5) }, { description: 'Meals', quantity: random.int(1, 3), unitPrice: random.money(scale * 0.4, scale, 5) }],
  });
  if (outcome === 'draft') return;
  const path = `/m/expenses/records/claims/${claim.id}`;
  await people.owner.post(`${path}/transition`, { to: 'submitted' });
  if (outcome === 'submitted') return;
  const approver = people.finance ?? people.admin;
  if (!approver) return;
  const admin = people.admin;
  if (outcome === 'rejected') { await admin.post(`${path}/transition`, { to: 'rejected', reason: 'Receipts missing for the taxi fares' }); return; }
  await admin.post(`${path}/transition`, { to: 'approved' });
  if (outcome === 'billed') await admin.post(`${path}/bill`, { date, vendorReference: `CLAIM-${claim.number}` });
}

export async function budget(ctx, revenueMonthly) {
  const { owner, entity } = ctx;
  const year = Number(today.slice(0, 4));
  const lines = [];
  const add = (code, amount) => { const accountId = ctx.accountCode(code); if (accountId) for (let m = 1; m <= 12; m++) lines.push({ accountId, month: m, amount: round2(amount) }); };
  const s = ctx.org.salaryBase;
  add('4000', revenueMonthly); add('5000', revenueMonthly * 0.55); add('6000', s * ctx.org.employees.length * 1.1); add('6100', s * 1.1); add('6200', s * 0.22); add('6900', s * 0.25);
  await owner.post('/finance/budgets', { legalEntityId: entity, name: `Operating budget ${year}`, fiscalYear: year, notes: 'Demonstration budget', isActive: true, lines });
}

export async function fixedAssets(ctx, assets) {
  if (!ctx.modules.has('assets')) return;
  const { owner, entity } = ctx;
  const category = await owner.post('/m/assets/categories', {
    code: 'EQP', name: 'Equipment and vehicles', lifeMonths: 60, method: 'straightLine', assetAccountId: ctx.accountCode('1500'),
    accumulatedDepreciationAccountId: ctx.accountCode('1590'), depreciationExpenseAccountId: ctx.accountCode('6400'), isActive: true,
  });
  const acquired = monthStart(5);
  owner.at(acquired);
  for (const [name, cost, custodian] of assets) {
    const asset = await owner.post('/m/assets/register', { legalEntityId: entity, name, categoryId: category.id, acquisitionDate: acquired, cost, salvageValue: round2(cost * 0.1), custodian, businessUnitId: ctx.units[0]?.id });
    await owner.post(`/m/assets/register/${asset.id}/capitalise`, { source: 'bank', date: acquired, accountId: ctx.bank, reference: `FA-${name.slice(0, 10)}` });
  }
  for (const first of months(5).slice(0, -1)) {
    const end = lastDayOf(first);
    owner.at(end);
    await owner.post('/m/assets/depreciation-runs', { legalEntityId: entity, year: Number(end.slice(0, 4)), month: Number(end.slice(5, 7)) });
  }
  owner.at(null);
}

// ---- Hire to Retire ------------------------------------------------------------------------------------------------------------------------

export async function peopleAndPayroll(ctx) {
  if (!ctx.modules.has('hr')) return;
  const { org, random, entity } = ctx;
  const hr = ctx.people.hr ?? ctx.owner;
  const finance = ctx.people.finance ?? ctx.owner;

  await ctx.run('hr-structure', async () => {
    hr.at(monthStart(6));
    let n = 0;
    for (const name of org.departments) await hr.post('/m/hr/departments', { code: `D${++n}`, name, isActive: true });
    const departments = await hr.list('/m/hr/departments');
    const designation = ['General Manager', 'Senior Officer', 'Officer', 'Coordinator', 'Assistant', 'Supervisor'];
    let i = 0;
    for (const fullName of org.employees) {
      const dep = departments[i % departments.length];
      const factor = i === 0 ? 2.4 : i < 3 ? 1.5 : 1;
      await hr.post('/m/hr/employees', {
        legalEntityId: entity, fullName, designation: designation[Math.min(i, designation.length - 1)], departmentId: dep.id, joinDate: addDays(monthStart(6), -random.int(90, 1400)), status: 'active',
        basicSalary: round2(org.salaryBase * factor * 0.7), housingAllowance: round2(org.salaryBase * factor * 0.2), transportAllowance: round2(org.salaryBase * factor * 0.1), otherAllowances: 0, recurringDeductions: 0,
        nationality: org.country, contractType: 'permanent',
      });
      i++;
    }
    hr.at(null);
  });

  const employees = await hr.list('/m/hr/employees');
  const leaveTypes = await hr.list('/m/hr/leave-types');
  await ctx.run('hr-leave-types', async () => {
    if (leaveTypes.length === 0) {
      await hr.post('/m/hr/leave-types', { code: 'ANNUAL', name: 'Annual leave', isPaid: true, annualDays: 24, carryForwardCap: 5 });
      await hr.post('/m/hr/leave-types', { code: 'SICK', name: 'Sick leave', isPaid: true, annualDays: 10, carryForwardCap: 0 });
    }
  });

  const finished = months(5).slice(0, -1);
  for (const first of finished) {
    const [year, month] = [Number(first.slice(0, 4)), Number(first.slice(5, 7))];
    const end = lastDayOf(first);
    await ctx.run(`payroll-${first.slice(0, 7)}`, async () => {
      ctx.on(end);
      for (const e of employees) {
        const absent = random.chance(0.12) ? 1 : 0;
        const leave = random.chance(0.2) ? random.int(1, 3) : 0;
        await hr.put('/m/hr/attendance', { employeeId: e.id, year, month, workingDays: 26, daysPresent: 26 - absent - leave, unpaidAbsentDays: absent, paidLeaveDays: leave, overtimeHours: random.chance(0.3) ? random.int(2, 12) : 0 });
      }
      const run = await hr.post('/m/hr/payroll', { legalEntityId: entity, year, month, plannedPayDate: end });
      const posted = await hr.post(`/m/hr/payroll/${run.id}/post`, {});
      if (first !== finished[finished.length - 1]) await (hr).post(`/m/hr/payroll/${posted.id ?? run.id}/pay`, { bankAccountId: ctx.bank, date: end });
    });
  }

  await ctx.run('hr-reviews-hiring', async () => {
    ctx.on(monthStart(2));
    const cycle = await hr.post('/m/hr/review-cycles', { name: `Mid-year review ${today.slice(0, 4)}`, periodStart: monthStart(5), periodEnd: monthStart(2), status: 1 });
    let n = 0;
    for (const e of employees.slice(0, 5)) {
      const review = await hr.post('/m/hr/reviews', { employeeId: e.id, cycleId: cycle.id, reviewerName: 'HR Manager', overallRating: random.int(2, 5), competencies: [{ name: 'Delivery', score: random.int(2, 5) }, { name: 'Teamwork', score: random.int(3, 5) }] });
      if (n++ < 3) await hr.post(`/m/hr/reviews/${review.id}/submit`, {});
    }
    const opening = await hr.post('/m/hr/openings', { title: `${org.opsRole} vacancy`, headcount: 1, status: 1, departmentId: undefined });
    const a = await hr.post('/m/hr/candidates', { openingId: opening.id, fullName: 'Applicant One (demo)', email: `applicant1@${org.code}.example.test` });
    const b = await hr.post('/m/hr/candidates', { openingId: opening.id, fullName: 'Applicant Two (demo)', email: `applicant2@${org.code}.example.test` });
    await hr.post(`/m/hr/candidates/${a.id}/move`, { stage: 3 });
    await hr.post(`/m/hr/candidates/${b.id}/move`, { stage: 6, reason: 'Experience does not match the role' });
    const types = await hr.list('/m/hr/leave-types');
    if (types[0]) {
      const from = addDays(monthStart(1), 8);
      const req = await hr.post('/m/hr/leave', { employeeId: employees[1].id, leaveTypeId: types[0].id, fromDate: from, toDate: addDays(from, 2), submit: true });
      await ctx.owner.post(`/m/hr/leave/${req.id}/approve`, {});
      const req2 = await hr.post('/m/hr/leave', { employeeId: employees[2].id, leaveTypeId: types[0].id, fromDate: addDays(from, 15), toDate: addDays(from, 16), submit: true });
      await ctx.owner.post(`/m/hr/leave/${req2.id}/reject`, { reason: 'Peak period, please choose other dates' });
    }
  });
  ctx.on(null);
}

// ---- Records ---------------------------------------------------------------------------------------------------------------------------------

/** Creates a record of an industry module and walks it through the named statuses. */
export async function record(ctx, module, type, body, path = [], who) {
  const actor = who ?? ctx.people.ops ?? ctx.owner;
  const route = `/m/${module}/records/${type}`;
  let created = await actor.post(route, { legalEntityId: ctx.entity, ...body });
  for (const step of path) created = await move(ctx, module, type, created.id, step);
  return created;
}

export async function move(ctx, module, type, id, step, who) {
  const [to, reason] = Array.isArray(step) ? step : [step];
  const route = `/m/${module}/records/${type}/${id}/transition`;
  const actor = who ?? ctx.people.ops ?? ctx.owner;
  try { return await actor.post(route, { to, reason }); }
  catch (e) {
    // A step that needs a second person is decided by the owner, as an approval would be.
    if (e.status === 403 || (e.status === 409 && /cannot approve/.test(e.message))) return ctx.owner.post(route, { to, reason });
    throw e;
  }
}
