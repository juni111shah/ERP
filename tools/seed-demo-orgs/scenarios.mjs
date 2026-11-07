// What makes each demo organization its own business: the records and flows of its industry.
import { addDays, clampToToday, monthStart, round2, today } from './lib.mjs';
import { months, dayIn, purchaseCycle, salesCycle, record, move } from './flows.mjs';

const taxId = (ctx) => ctx.tax?.id ?? null;

/** Pays part of what a record invoiced, so receivables show a mix of paid, part-paid and open. */
async function settle(ctx, invoiceId, date, share = 1) {
  const finance = ctx.people.finance ?? ctx.owner;
  const invoice = await finance.get(`/finance/invoices/${invoiceId}`);
  const amount = round2((invoice.total - invoice.paid) * share);
  if (amount <= 0) return;
  finance.at(date);
  await finance.post('/finance/receipts', { legalEntityId: ctx.entity, partyId: invoice.partyId, date, method: 'BankTransfer', bankAccountId: ctx.bank, amount, reference: `RCV-${invoice.number}`, allocations: [{ invoiceId, amount }] });
}

async function issueIfDraft(ctx, invoiceId) {
  const finance = ctx.people.finance ?? ctx.owner;
  const row = await finance.get(`/finance/invoices/${invoiceId}`);
  return row.status === 'draft' ? finance.post(`/finance/invoices/${invoiceId}/issue`, {}) : row;
}

async function paySome(ctx, billId, date) {
  const finance = ctx.people.finance ?? ctx.owner;
  const bill = await finance.get(`/finance/bills/${billId}`);
  const amount = round2(bill.total - bill.paid);
  if (amount <= 0 || bill.status !== 'issued') return;
  finance.at(date);
  await finance.post('/finance/payments', { legalEntityId: ctx.entity, partyId: bill.partyId, date, method: 'BankTransfer', bankAccountId: ctx.bank, amount, reference: `PMT-${bill.number}`, allocations: [{ billId, amount }] });
}

/** Most months' billing ends up paid; the recent ones stay open or overdue. */
const settleByAge = async (ctx, invoiceId, date, month) => {
  const old = month < monthStart(1);
  const roll = ctx.random.next();
  if (old && roll < 0.8) await settle(ctx, invoiceId, clampToToday(addDays(date, ctx.random.int(5, 25))));
  else if (old && roll < 0.9) await settle(ctx, invoiceId, clampToToday(addDays(date, ctx.random.int(5, 25))), 0.5);
  else if (!old && roll < 0.35) await settle(ctx, invoiceId, clampToToday(addDays(date, ctx.random.int(1, 6))));
};

// ---- Manufacturing --------------------------------------------------------------------------------------------------------------------------

async function createBoms(ctx) {
  const ops = ctx.people.ops ?? ctx.owner;
  for (const b of ctx.org.boms) {
    const produced = ctx.item(b.sku);
    await ops.post('/m/manufacturing/boms', {
      legalEntityId: ctx.entity, code: b.code, name: `${produced.name} recipe`, producedItemId: produced.id, producedItemCode: produced.sku, producedItemName: produced.name, outputQuantity: b.output, uom: produced.uom, version: '1',
      laborCostPerUnit: b.labour, overheadCostPerUnit: b.overhead, isActive: true,
      components: b.components.map(([sku, quantity]) => { const c = ctx.item(sku); return { componentItemId: c.id, componentItemCode: c.sku, componentItemName: c.name, quantityRequired: quantity, uom: c.uom, scrapPercentage: 2 }; }),
    });
  }
}

async function productionRun(ctx, month, n, outcome = 'completed') {
  const ops = ctx.people.ops ?? ctx.owner;
  const boms = await ops.list('/m/manufacturing/boms');
  const bom = ctx.random.pick(boms);
  const start = dayIn(ctx, month);
  ctx.on(start);
  const factor = bom.outputQuantity > 50 ? ctx.random.int(2, 5) : ctx.random.int(1, 3);
  const planned = bom.outputQuantity * factor;
  const order = await ops.post('/m/manufacturing/workorders', {
    legalEntityId: ctx.entity, bomId: bom.id, plannedQuantity: planned, warehouseId: ctx.warehouse, startDate: start, dueDate: addDays(start, 5), notes: `Batch ${month.slice(2, 7)}-${n}`,
    operations: [{ sequenceOrder: 1, operationName: 'Preparation', workCenterName: 'Line 1', plannedHours: 4, hourlyRate: ctx.org.salaryBase / 200 }, { sequenceOrder: 2, operationName: 'Production', workCenterName: 'Line 2', plannedHours: 6, hourlyRate: ctx.org.salaryBase / 180 }],
  });
  if (outcome === 'planned') return order;
  if (outcome === 'cancelled') { await ops.post(`/m/manufacturing/workorders/${order.id}/cancel`, { reason: 'Customer order withdrawn' }); return order; }
  try {
    await ops.post(`/m/manufacturing/workorders/${order.id}/release`, { issueMaterials: false });
    await ops.post(`/m/manufacturing/workorders/${order.id}/issue`, { date: start, notes: 'Issued to the line' });
  } catch (e) {
    if (e.status !== 409) throw e;
    // Materials short: the order stays open and shows up as a shortage, as it would on a real shop floor.
    return order;
  }
  if (outcome === 'inprogress') return order;
  const done = clampToToday(addDays(start, ctx.random.int(2, 5)));
  ctx.on(done);
  const scrapped = Math.round(planned * ctx.random.pick([0, 0.01, 0.02, 0.03]));
  const result = await ops.post(`/m/manufacturing/workorders/${order.id}/complete`, { completedQuantity: planned - scrapped, scrappedQuantity: scrapped, actualTotalCost: 0, completionDate: done, notes: 'Batch finished' });
  if (ctx.modules.has('quality')) {
    const fail = ctx.random.chance(0.12);
    const inspection = await record(ctx, 'quality', 'inspections', { title: `Final check ${(order.workOrderNumber ?? order.number)}`, recordDate: done, data: { stage: 'Final', sourceRef: (order.workOrderNumber ?? order.number), product: bom.producedItemName, sampleSize: 20, defects: fail ? 4 : ctx.random.int(0, 1), inspector: ctx.org.employees[3] } });
    await move(ctx, 'quality', 'inspections', inspection.id, fail ? ['failed', 'Defect rate above the limit'] : 'passed');
    if (fail) await record(ctx, 'quality', 'nonconformances', { title: `Defects in ${(order.workOrderNumber ?? order.number)}`, recordDate: done, data: { severity: 'Major', sourceRef: (order.workOrderNumber ?? order.number), rootCause: 'Machine setting drift', correctiveAction: 'Recalibrate and re-inspect' } }, ['investigating']);
  }
  return result;
}

export async function manufacturing(ctx) {
  await ctx.run('boms', () => createBoms(ctx));
  const finished = new Set(ctx.org.finishedGoods ?? []);
  // Raw materials are bought in; finished goods are made, then sold.
  ctx.buyable = () => ctx.stockItems.filter((i) => !finished.has(i.sku));
  ctx.sellable = () => ctx.items.filter((i) => finished.has(i.sku));
  await ctx.run('production-plans', async () => {
    for (const first of months(5)) {
      const date = clampToToday(addDays(first, 1));
      ctx.on(date);
      const lines = ctx.org.boms.map((b) => ({ description: ctx.item(b.sku).name, code: b.sku, itemId: ctx.item(b.sku).id, unitOfMeasure: ctx.item(b.sku).uom, quantity: b.output * ctx.random.int(4, 8), unitPrice: 0, completedQuantity: 0 }));
      await record(ctx, 'quality', 'plans', { title: `Plan ${first.slice(0, 7)}`, recordDate: date, data: { period: first.slice(0, 7), plant: ctx.org.branches[0] }, lines }, first < monthStart(0) ? ['released', 'completed'] : ['released']);
    }
  });
  ctx.monthly = async (first) => {
    await ctx.run(`production-${first.slice(0, 7)}`, async () => {
      for (let n = 1; n <= 7; n++) await productionRun(ctx, first, n, n === 6 && first === monthStart(1) ? 'cancelled' : (first === monthStart(0) && n > 3 ? (n % 2 ? 'inprogress' : 'planned') : 'completed'));
    });
  };
}

// ---- Retail and restaurants: tills -----------------------------------------------------------------------------------------------------------

async function posDays(ctx, first, perDay, sellable) {
  const ops = ctx.people.ops ?? ctx.owner;
  const registers = await ops.list('/m/pos/registers');
  const days = [3, 9, 15, 22].map((d) => addDays(first, d)).filter((d) => d <= today);
  let returnsDone = 0;
  for (const day of days) {
    ctx.on(day);
    for (const register of registers) {
      const session = await ops.post('/m/pos/sessions/open', { legalEntityId: ctx.entity, registerId: register.id, openingCash: ctx.org.salaryBase / 10, notes: 'Morning float' });
      let cash = ctx.org.salaryBase / 10;
      const made = [];
      for (let n = 0; n < perDay; n++) {
        const lines = Array.from({ length: ctx.random.int(1, 4) }, () => ctx.random.pick(sellable)).filter((it, i, all) => all.indexOf(it) === i).map((it) => ({ itemId: it.id, quantity: it.uom === 'kg' ? ctx.random.int(1, 3) : ctx.random.int(1, 4), discountPercent: ctx.random.chance(0.12) ? 5 : 0 }));
        const quote = await ops.post('/m/pos/orders/quote', { legalEntityId: ctx.entity, lines });
        const byCard = ctx.random.chance(0.45);
        const order = await ops.post('/m/pos/orders', {
          legalEntityId: ctx.entity, registerId: register.id, sessionId: session.id, customerName: 'Walk-in customer', lines,
          payments: [{ method: byCard ? 'Card' : 'Cash', amount: quote.totalAmount, tenderedAmount: quote.totalAmount, changeAmount: 0, reference: byCard ? `CARD-${day}-${n}` : undefined }],
        });
        if (!byCard) cash += quote.totalAmount;
        made.push({ order, byCard, total: quote.totalAmount });
      }
      const candidate = made.find((m) => m.total > 0);
      if (candidate && returnsDone < 2 && ctx.random.chance(0.4)) {
        const detail = await ops.get(`/m/pos/orders/${candidate.order.id}`);
        const line = detail.lines[0];
        const preview = round2((line.lineTotal ?? line.total ?? 0) / line.quantity);
        if (preview > 0) {
          await ctx.owner.at(day).post('/m/pos/returns', { orderId: candidate.order.id, sessionId: session.id, reason: ctx.random.pick(['Customer changed mind', 'Damaged packaging', 'Wrong item scanned']), lines: [{ orderLineId: line.id, quantity: 1, restock: true }], refunds: [{ method: candidate.byCard ? 'Card' : 'Cash', amount: preview, reference: `REF-${day}` }] })
            .then(() => { returnsDone++; if (!candidate.byCard) cash -= preview; })
            .catch((e) => ctx.log(`      return skipped: ${e.message.slice(0, 120)}`));
        }
      }
      const variance = ctx.random.chance(0.2) ? ctx.random.pick([-2, -1, 1]) : 0;
      await ops.post(`/m/pos/sessions/${session.id}/close`, { actualCash: round2(cash + variance), notes: variance ? 'Small till difference' : 'Balanced' });
    }
  }
}

async function posSetup(ctx) {
  const ops = ctx.people.ops ?? ctx.owner;
  let n = 0;
  for (const wh of ctx.warehouses.slice(0, ctx.org.key === 'restaurant' ? 2 : 3)) {
    await ctx.owner.post('/m/pos/registers', { legalEntityId: ctx.entity, code: `TILL-${++n}`, name: `${wh.name.replace(' store', '')} till`, warehouseId: wh.id, isActive: true, receiptFooter: 'Thank you. Demonstration data.', defaultCashAccountId: ctx.cash });
  }
}

export async function retail(ctx) {
  const sellable = ctx.items.filter((i) => i.price > 0);
  ctx.buyable = () => ctx.stockItems;
  ctx.sellable = () => sellable;
  await ctx.run('retail-setup', async () => {
    await posSetup(ctx);
    let n = 0;
    for (const wh of ctx.warehouses) await ctx.owner.post('/m/retail/stores', { legalEntityId: ctx.entity, code: `ST-${++n}`, name: wh.name.replace(' store', ''), warehouseId: wh.id, storeManagerName: ctx.org.employees[n], isActive: true });
    await ctx.owner.post('/m/retail/promotions', { legalEntityId: ctx.entity, code: 'SAVE10', name: 'Ten percent weekend', type: 'percentageDiscount', discountValue: 10, startDate: monthStart(5), endDate: addDays(today, 60), usageLimit: 500, isActive: true });
    await ctx.owner.post('/m/retail/promotions', { legalEntityId: ctx.entity, code: 'SUMMER5', name: 'Summer fixed saving', type: 'fixedAmountDiscount', discountValue: 5, minimumOrderAmount: 50, startDate: monthStart(5), endDate: monthStart(2), isActive: false });
    await ctx.owner.post('/m/retail/pricelists', {
      legalEntityId: ctx.entity, code: 'PROMO-DAIRY', name: 'Dairy offer', type: 'promotional', currency: ctx.org.currency, validFrom: monthStart(1), validTo: addDays(today, 30), isDefault: false, isActive: true,
      items: ctx.items.filter((i) => i.category === 'Dairy').map((i) => ({ itemId: i.id, itemCode: i.sku, itemName: i.name, basePrice: i.price, price: round2(i.price * 0.9), minQuantity: 1 })),
    });
  });
  // Stock moves between branches, then the tills sell from each one.
  await ctx.run('store-transfers', async () => {
    const inv = ctx.people.inventory ?? ctx.owner;
    inv.at(monthStart(6));
    for (const wh of ctx.warehouses.slice(1)) {
      for (const it of ctx.stockItems) {
        const onHand = (await inv.get(`/m/inventory/stock?warehouseId=${ctx.warehouse}&itemId=${it.id}`)).rows?.[0]?.quantity ?? 0;
        const quantity = Math.floor(onHand * 0.3);
        if (quantity > 0) await inv.post('/m/inventory/stock/transfer', { fromWarehouseId: ctx.warehouse, toWarehouseId: wh.id, itemId: it.id, quantity, date: monthStart(6), reference: `TR-${wh.code}-${it.sku}`, memo: 'Initial store allocation' });
      }
    }
    inv.at(null);
  });
  ctx.monthly = async (first) => {
    await ctx.run(`restock-${first.slice(0, 7)}`, async () => {
      const inv = ctx.people.inventory ?? ctx.owner;
      const date = clampToToday(addDays(first, 8));
      inv.at(date);
      for (const wh of ctx.warehouses.slice(1)) {
        for (const it of ctx.stockItems) {
          const onHand = (await inv.get(`/m/inventory/stock?warehouseId=${ctx.warehouse}&itemId=${it.id}`)).rows?.[0]?.quantity ?? 0;
          const quantity = Math.floor(onHand * 0.08);
          if (quantity > 0) await inv.post('/m/inventory/stock/transfer', { fromWarehouseId: ctx.warehouse, toWarehouseId: wh.id, itemId: it.id, quantity, date, reference: `TR-${first.slice(2, 7)}-${wh.code}-${it.sku}`, memo: 'Monthly store replenishment' });
        }
      }
      inv.at(null);
    });
    await ctx.run(`pos-${first.slice(0, 7)}`, () => posDays(ctx, first, 6, sellable));
  };
}

export async function restaurant(ctx) {
  await ctx.run('boms', () => createBoms(ctx));
  const menu = new Set(ctx.org.finishedGoods);
  ctx.buyable = () => ctx.stockItems.filter((i) => !menu.has(i.sku));
  ctx.sellable = () => ctx.items.filter((i) => menu.has(i.sku));
  const sellable = ctx.sellable();
  await ctx.run('restaurant-setup', async () => {
    await posSetup(ctx);
    for (const [title, kind, rate] of [['Table 1', 'Table', 0], ['Table 2', 'Table', 0], ['Terrace', 'Hall', 900], ['Private dining room', 'Hall', 1500]])
      await record(ctx, 'hospitality', 'rooms', { title, recordDate: monthStart(6), data: { kind, floor: 'Ground', rate, capacity: kind === 'Table' ? 4 : 24 } });
  });
  ctx.monthly = async (first) => {
    await ctx.run(`kitchen-${first.slice(0, 7)}`, async () => {
      for (let n = 1; n <= 5; n++) await productionRun(ctx, first, n, 'completed');
      const inv = ctx.people.inventory ?? ctx.owner;
      const date = dayIn(ctx, first);
      ctx.on(date);
      await inv.post('/m/inventory/stock/receive', { warehouseId: ctx.warehouse, itemId: ctx.item('BEV-TEA').id, quantity: 300, unitCost: ctx.item('BEV-TEA').cost, date, offsetAccountId: ctx.account('purchases.expense'), reference: `BAR-${first.slice(2, 7)}-T`, memo: 'Bar preparation' });
      await inv.post('/m/inventory/stock/receive', { warehouseId: ctx.warehouse, itemId: ctx.item('BEV-LSI').id, quantity: 150, unitCost: ctx.item('BEV-LSI').cost, date, offsetAccountId: ctx.account('purchases.expense'), reference: `BAR-${first.slice(2, 7)}-L`, memo: 'Bar preparation' });
    });
    await ctx.run(`pos-${first.slice(0, 7)}`, async () => posDays(ctx, first, 8, sellable));
    await ctx.run(`waste-${first.slice(0, 7)}`, async () => {
      const date = dayIn(ctx, first);
      ctx.on(date);
      const veg = ctx.item('ING-VEG');
      const waste = await record(ctx, 'hospitality', 'waste', { title: `Kitchen waste ${first.slice(0, 7)}`, recordDate: date, data: { reason: ctx.random.pick(['Spoiled', 'Overproduction', 'Preparation']), recordedBy: ctx.org.employees[1] }, lines: [{ description: veg.name, itemId: veg.id, quantity: ctx.random.int(2, 6), unitPrice: veg.cost, unitOfMeasure: veg.uom }] });
      await (ctx.people.ops ?? ctx.owner).post(`/m/hospitality/records/waste/${waste.id}/consume`, { date, warehouseId: ctx.warehouse }).catch((e) => ctx.log(`      waste skipped: ${e.message.slice(0, 100)}`));
      await move(ctx, 'hospitality', 'waste', waste.id, 'posted');
      const client = ctx.customers[ctx.random.int(1, ctx.customers.length - 1)];
      const event = await record(ctx, 'hospitality', 'events', { title: `Corporate lunch ${first.slice(0, 7)}`, recordDate: date, partyId: client.id, data: { eventDate: date, covers: ctx.random.int(20, 60), venue: 'Terrace' }, lines: [{ description: 'Buffet per head', quantity: ctx.random.int(20, 60), unitPrice: 85 }] }, ['confirmed', 'delivered']);
      const done = await (ctx.people.ops ?? ctx.owner).post(`/m/hospitality/records/events/${event.id}/invoice`, { date, taxCodeId: taxId(ctx) });
      await settleByAge(ctx, done.invoiceId, date, first);
    });
  };
}

// ---- Real estate ---------------------------------------------------------------------------------------------------------------------------------

export async function realEstate(ctx) {
  const ops = ctx.people.ops ?? ctx.owner;
  const owner = ctx.owner;
  await ctx.run('portfolio', async () => {
    owner.at(monthStart(6));
    const residential = await owner.post('/m/realestate/unit-types', { code: 'APT2', name: 'Two bedroom apartment', defaultUsage: 1 });
    const office = await owner.post('/m/realestate/unit-types', { code: 'OFF', name: 'Office suite', defaultUsage: 2 });
    const landlord = await owner.post('/parties', { name: 'Al Jazi Family Investments', kind: 'Company', roles: 'Vendor', email: `landlord@${ctx.org.code}.example.test`, paymentTermsDays: 30 });
    const towers = [
      ['Horizon Marina Tower', 'residential', 'owned', residential.id, 2000, 8500, 5, 4, null, 180000000],
      ['Corniche Business Centre', 'commercial', 'owned', office.id, 1400, 11000, 3, 4, null, 120000000],
      ['Al Jazi Residences', 'residential', 'managedForOwner', residential.id, 1800, 7200, 3, 3, landlord.id, 60000000],
    ];
    for (const [name, propertyType, ownershipType, unitTypeId, areaSqft, askingRent, floors, perFloor, ownerPartyId, value] of towers) {
      const p = await owner.post('/m/realestate/properties', { legalEntityId: ctx.entity, name, propertyType, ownershipType, ownerPartyId, totalUnits: floors * perFloor, indicativeValue: value, managementFeePercent: ownerPartyId ? 8 : undefined });
      await owner.post('/m/realestate/units/bulk', { propertyId: p.id, unitTypeId, fromFloor: 1, toFloor: floors, unitsPerFloor: perFloor, areaSqft, askingRent });
    }
    owner.at(null);
  });
  const units = await owner.list('/m/realestate/units');
  const tenants = ctx.customers;
  await ctx.run('leases', async () => {
    let i = 0;
    for (const unit of units.slice(0, Math.floor(units.length * 0.8))) {
      const start = addDays(monthStart(5), ctx.random.int(0, 40));
      owner.at(start);
      const rent = (unit.askingRent ?? 8000) * 12;
      try {
        const annualRent = Math.round(rent / 100) * 100;
        const lease = await owner.post('/m/realestate/leases', { unitId: unit.id, lesseePartyId: tenants[i++ % tenants.length].id, startDate: start, annualRent, paymentFrequency: ctx.random.pick([4, 12, 12]), securityDepositAmount: Math.round(annualRent / 12), graceDays: 5, activateNow: true });
        await owner.at(start).post(`/m/realestate/leases/${lease.id}/deposit`, { bankAccountId: ctx.bank, date: start, amount: Math.round(annualRent / 12) }).catch((e) => ctx.log(`      deposit skipped: ${e.message.slice(0, 100)}`));
      } catch (e) { ctx.log(`      lease skipped: ${e.message.slice(0, 120)}`); }
    }
    owner.at(null);
  });
  ctx.monthly = async (first) => {
    await ctx.run(`rent-${first.slice(0, 7)}`, async () => {
      const asOf = addDays(first, 5) > today ? today : addDays(first, 5);
      ctx.on(asOf);
      await owner.post('/m/realestate/billing/run', { asOf, legalEntityId: ctx.entity });
      const invoices = (await owner.list(`/finance/invoices?filter=invoiceDate:ge:${first}&filter=invoiceDate:le:${asOf}`)).filter((i) => i.status === 'issued');
      for (const inv of invoices) await settleByAge(ctx, inv.id, asOf, first);
    });
    await ctx.run(`maintenance-${first.slice(0, 7)}`, async () => {
      const properties = await owner.list('/m/realestate/properties');
      for (let n = 0; n < 3; n++) {
        const date = dayIn(ctx, first);
        ctx.on(date);
        const vendor = ctx.random.pick(ctx.suppliers);
        const issue = ctx.random.pick(['Air conditioning not cooling', 'Lift service visit', 'Water leak in bathroom', 'Lobby light replacement', 'Fire alarm test']);
        const wo = await ops.post('/m/realestate/work-orders', { propertyId: ctx.random.pick(properties).id, issue, priority: ctx.random.int(1, 3), vendorPartyId: vendor.id, vendorId: vendor.id });
        if (n < 2 || first < monthStart(1)) {
          await ops.post(`/m/realestate/work-orders/${wo.id}/status`, { status: 'assigned' }).catch(() => {});
          await ops.post(`/m/realestate/work-orders/${wo.id}/status`, { status: 'inProgress' }).catch(() => {});
          await ops.post(`/m/realestate/work-orders/${wo.id}/status`, { status: 'completed', actualCost: ctx.random.money(400, 3500, 50), resolutionNotes: 'Resolved on site' }).catch((e) => ctx.log(`      work order: ${e.message.slice(0, 100)}`));
        }
      }
    });
  };
  await ctx.run('commissions', async () => {
    const client = ctx.customers[4];
    for (let n = 0; n < 4; n++) {
      const date = dayIn(ctx, monthStart(4 - n));
      owner.at(date);
      const deal = await ops.post('/m/realestate/deals', { legalEntityId: ctx.entity, dealType: n % 2 ? 'sale' : 'lease', clientName: client.name, clientPartyId: client.id, dealDate: date, dealValue: ctx.random.money(120000, 900000, 5000), commissionRatePercent: 2, agentName: ctx.org.employees[3], agentCommissionPercent: 50 });
      await ops.post(`/m/realestate/deals/${deal.id}/stage`, { stage: 'negotiation' }).catch(() => {});
      await ops.post(`/m/realestate/deals/${deal.id}/stage`, { stage: 'agreed' }).catch((e) => ctx.log(`      deal stage: ${e.message.slice(0, 100)}`));
      await (ctx.people.finance ?? owner).at(date).post(`/m/realestate/deals/${deal.id}/invoice-commission`, {}).catch((e) => ctx.log(`      commission: ${e.message.slice(0, 120)}`));
    }
    owner.at(null);
  });
  await ctx.run('property-sales', async () => {
    const plans = [['Unit A-1201 sale', 'A-1201', 1450000, 0], ['Unit B-904 sale', 'B-904', 980000, 1], ['Unit C-310 sale', 'C-310', 760000, 2], ['Unit A-701 sale (withdrawn)', 'A-701', 1100000, 3]];
    for (const [title, unitRef, price, age] of plans) {
      const date = addDays(monthStart(5 - age), 3);
      ctx.on(date);
      const buyer = ctx.customers[ctx.random.int(0, ctx.customers.length - 1)];
      const lines = [{ description: 'Down payment 20%', quantity: 1, unitPrice: price * 0.2, dueDate: addDays(date, 5) }];
      for (let m = 1; m <= 8; m++) lines.push({ description: `Instalment ${m} of 8`, quantity: 1, unitPrice: price * 0.1, dueDate: addDays(date, m * 30) });
      const contract = await record(ctx, 'propertysales', 'salecontracts', { title, recordDate: date, partyId: buyer.id, data: { unitRef, projectName: 'Horizon Marina Tower', listPrice: price, agent: ctx.org.employees[3] }, lines }, ['submitted']);
      if (age === 3) { await move(ctx, 'propertysales', 'salecontracts', contract.id, ['rejected', 'Buyer finance was not approved'], ctx.owner); continue; }
      const active = await move(ctx, 'propertysales', 'salecontracts', contract.id, 'active', ctx.owner);
      for (const line of active.lines ?? contract.lines) {
        if (!line.dueDate || line.dueDate > today) break;
        ctx.on(line.dueDate);
        const result = await (ctx.people.ops ?? ctx.owner).post(`/m/propertysales/records/salecontracts/${contract.id}/invoice`, { date: line.dueDate, lineIds: [line.id] });
        const invoiceId = result.lines.find((l) => l.id === line.id).invoiceId;
        if (ctx.random.chance(0.75)) await settle(ctx, invoiceId, clampToToday(addDays(line.dueDate, ctx.random.int(1, 10))));
      }
    }
    for (const [title, propertyRef, valuer, marketValue] of [['Annual valuation 2026', 'Horizon Marina Tower', 'Peninsula Valuers', 182000000], ['Annual valuation 2026', 'Corniche Business Centre', 'Peninsula Valuers', 118500000]])
      await record(ctx, 'propertysales', 'valuations', { title: `${title}: ${propertyRef}`, recordDate: monthStart(3), data: { propertyRef, valuer, basis: 'Market value', marketValue } }, [], ctx.owner).then((v) => move(ctx, 'propertysales', 'valuations', v.id, 'final', ctx.people.admin));
  });
}

// ---- Construction -----------------------------------------------------------------------------------------------------------------------------

export async function construction(ctx) {
  ctx.buyable = () => ctx.stockItems;
  const projects = [
    ['Najd Business Park Phase 1', 'Riyadh', 0, 18500000, 15200000, 62],
    ['Red Sea Staff Village', 'Jeddah', 1, 9400000, 7900000, 38],
    ['Al Faisal School Extension', 'Riyadh', 3, 4200000, 3550000, 81],
  ];
  const manager = ctx.people.ops ?? ctx.owner;
  await ctx.run('sites-and-boqs', async () => {
    const start = monthStart(5);
    ctx.on(start);
    for (const [title, location, client, contractValue, costBudget, progress] of projects) {
      const site = await record(ctx, 'construction', 'sites', { title, recordDate: start, partyId: ctx.customers[client].id, dueDate: addDays(start, 420), data: { location, contractValue, costBudget, projectManager: ctx.org.employees[2], progressPercent: progress } }, ['active']);
      const boq = await record(ctx, 'construction', 'boqs', {
        title: `${title} main BOQ`, recordDate: start, parentId: site.id, data: { revision: 'Rev 1', discipline: 'Civil' },
        lines: [
          { code: '01.01', description: 'Excavation and backfill', unitOfMeasure: 'cu m', quantity: Math.round(contractValue / 2200), unitPrice: 85 },
          { code: '02.01', description: 'Reinforced concrete', unitOfMeasure: 'cu m', quantity: Math.round(contractValue / 1800), unitPrice: 620 },
          { code: '03.01', description: 'Blockwork', unitOfMeasure: 'sq m', quantity: Math.round(contractValue / 1200), unitPrice: 110 },
          { code: '04.01', description: 'Finishes and tiling', unitOfMeasure: 'sq m', quantity: Math.round(contractValue / 1000), unitPrice: 190 },
          { code: '05.01', description: 'Mechanical and electrical works', unitOfMeasure: 'lump sum', quantity: 1, unitPrice: Math.round(contractValue * 0.18) },
        ],
      }, ['submitted', 'approved']);
      await (manager).post(`/m/construction/records/boqs/${boq.id}/progress`, { lines: boq.lines.slice(0, 2).map((l) => ({ lineId: l.id, completedQuantity: Math.round(l.quantity * progress / 100) })) });
      const sub = ctx.party(ctx.org.subcontractors[client % 2]);
      await record(ctx, 'construction', 'subcontracts', { title: `${title} MEP works`, recordDate: addDays(start, 10), partyId: sub.id, parentId: site.id, amount: Math.round(contractValue * 0.14), data: { scope: 'Mechanical and electrical installation', retentionPercent: 10 } }, ['submitted', 'active']);
    }
    const draft = await record(ctx, 'construction', 'sites', { title: 'Tabuk Warehouse (tender)', recordDate: start, partyId: ctx.customers[2].id, data: { location: 'Tabuk', contractValue: 6200000, costBudget: 5200000, projectManager: ctx.org.employees[2], progressPercent: 0 } });
    await move(ctx, 'construction', 'sites', draft.id, ['cancelled', 'Tender lost to a competitor'], ctx.owner);
  });
  const sites = (await ctx.owner.list('/m/construction/records/sites')).filter((s) => s.status === 'active');
  ctx.monthly = async (first) => {
    await ctx.run(`site-work-${first.slice(0, 7)}`, async () => {
      for (const site of sites) {
        const date = dayIn(ctx, first);
        ctx.on(date);
        const pickMats = ctx.stockItems.slice(0, 4);
        const issue = await record(ctx, 'construction', 'materialissues', {
          title: `Materials ${first.slice(0, 7)}`, recordDate: date, parentId: site.id, data: { issuedTo: ctx.org.employees[3], workArea: 'Structure' },
          lines: pickMats.map((m) => ({ description: m.name, itemId: m.id, unitOfMeasure: m.uom, quantity: m.cost > 1000 ? ctx.random.int(1, 3) : ctx.random.int(10, 40), unitPrice: m.cost })),
        });
        try {
          await (ctx.people.ops ?? ctx.owner).post(`/m/construction/records/materialissues/${issue.id}/consume`, { date, warehouseId: ctx.warehouse });
          await move(ctx, 'construction', 'materialissues', issue.id, 'issued');
        } catch (e) { if (e.status !== 409) throw e; ctx.log(`      materials short: ${e.message.slice(0, 80)}`); }
        const eq = await record(ctx, 'construction', 'equipment', { title: `Crane hire ${first.slice(0, 7)}`, recordDate: date, parentId: site.id, partyId: ctx.party('Heavy Lift Equipment Rental').id, amount: ctx.random.money(18000, 42000, 500), data: { equipmentName: '40 tonne mobile crane', hours: ctx.random.int(60, 160), ownership: 'Hired' } }, ['approved']);
        const billed = await (ctx.people.ops ?? ctx.owner).post(`/m/construction/records/equipment/${eq.id}/bill`, { date, taxCodeId: taxId(ctx) });
        if (first < monthStart(1)) await paySome(ctx, billed.billId, clampToToday(addDays(date, 15)));
        await record(ctx, 'construction', 'sitereports', { title: `Site report ${date}`, recordDate: date, parentId: site.id, data: { workers: ctx.random.int(35, 120), weather: ctx.random.pick(['Clear', 'Hot', 'Windy']), workDone: 'Structure works on schedule.', issues: ctx.random.chance(0.2) ? 'Late delivery of rebar' : undefined } });

        // Subcontractor certificate, then the valuation to the client.
        const subName = ctx.org.subcontractors[ctx.random.int(0, 1)];
        const cert = await record(ctx, 'construction', 'subcontractorpayments', { title: `Certificate ${first.slice(0, 7)}`, recordDate: date, parentId: site.id, partyId: ctx.party(subName).id, data: { subcontractRef: `${site.title} MEP works`, periodNumber: months(5).indexOf(first) + 1 }, lines: [{ description: 'MEP first fix this period', quantity: 1, unitPrice: ctx.random.money(45000, 120000, 500) }] }, ['submitted']);
        const approved = await move(ctx, 'construction', 'subcontractorpayments', cert.id, 'approved', ctx.owner);
        const certBilled = await ctx.owner.post(`/m/construction/records/subcontractorpayments/${approved.id}/bill`, { date, taxCodeId: taxId(ctx) });
        if (first < monthStart(1)) await paySome(ctx, certBilled.billId, clampToToday(addDays(date, 20)));

        const valuation = await record(ctx, 'construction', 'valuations', { title: `Valuation ${first.slice(0, 7)}`, recordDate: date, partyId: site.partyId, parentId: site.id, dueDate: addDays(date, 30), data: { periodNumber: months(5).indexOf(first) + 1, retentionPercent: 10, certifiedPercent: 10 + months(5).indexOf(first) * 10, certifiedBy: 'Client engineer' }, lines: [{ description: 'Structure works this period', quantity: 1, unitPrice: ctx.random.money(220000, 620000, 1000) }, { description: 'Finishes this period', quantity: 1, unitPrice: ctx.random.money(40000, 160000, 1000) }] }, ['submitted']);
        if (first === monthStart(0)) continue;
        const certified = await move(ctx, 'construction', 'valuations', valuation.id, 'certified', ctx.owner);
        const invoiced = await (ctx.people.ops ?? ctx.owner).post(`/m/construction/records/valuations/${certified.id}/invoice`, { date: clampToToday(addDays(date, 3)), taxCodeId: taxId(ctx) });
        await settleByAge(ctx, invoiced.invoiceId, clampToToday(addDays(date, 3)), first);
      }
    });
  };
}

// ---- Healthcare --------------------------------------------------------------------------------------------------------------------------------

export async function healthcare(ctx) {
  const firstNames = ['Amal', 'Bassam', 'Carla', 'Dina', 'Elias', 'Farida', 'Ghassan', 'Huda', 'Issa', 'Jana', 'Karim', 'Lubna', 'Majed', 'Nour', 'Osama', 'Rana', 'Samir', 'Tala', 'Wael', 'Yara'];
  const lastNames = ['Test-One', 'Test-Two', 'Test-Three', 'Test-Four', 'Test-Five'];
  ctx.buyable = () => ctx.stockItems;
  const selfPay = ctx.customers[0];
  const insurers = ctx.customers.slice(1);
  const staff = ctx.people.ops ?? ctx.owner;
  await ctx.run('patients', async () => {
    ctx.on(monthStart(5));
    for (let i = 0; i < firstNames.length; i++) {
      const insured = ctx.random.chance(0.55);
      await record(ctx, 'healthcare', 'patients', {
        title: `${firstNames[i]} ${lastNames[i % lastNames.length]}`, recordDate: addDays(monthStart(5), i), partyId: (insured ? ctx.random.pick(insurers) : selfPay).id,
        data: { dateOfBirth: `${1955 + (i * 3) % 60}-0${1 + i % 9}-15`, gender: i % 2 ? 'Male' : 'Female', phone: `+000 555 3${String(i).padStart(3, '0')}`, bloodGroup: ['A+', 'O+', 'B+', 'AB-'][i % 4], allergies: i % 5 === 0 ? 'Penicillin' : 'None known', insurer: insured ? 'Yes' : undefined, policyNumber: insured ? `TEST-${1000 + i}` : undefined },
      }, [], ctx.owner);
    }
  });
  const patients = await ctx.owner.list('/m/healthcare/records/patients');
  const departments = ctx.org.departments.slice(0, 4);
  const doctors = ctx.org.employees.slice(0, 3);
  ctx.monthly = async (first) => {
    await ctx.run(`clinic-${first.slice(0, 7)}`, async () => {
      for (let n = 0; n < 14; n++) {
        const date = dayIn(ctx, first);
        ctx.on(date);
        const patient = ctx.random.pick(patients);
        const appointment = await record(ctx, 'healthcare', 'appointments', { title: ctx.random.pick(['Consultation', 'Follow-up review', 'Annual check']), recordDate: date, parentId: patient.id, data: { department: ctx.random.pick(departments), clinician: ctx.random.pick(doctors), time: `${ctx.random.int(9, 16)}:00`, visitType: ctx.random.pick(['New', 'Follow-up']) } });
        const roll = ctx.random.next();
        if (roll < 0.08) { await move(ctx, 'healthcare', 'appointments', appointment.id, 'noshow'); continue; }
        if (roll < 0.14) { await move(ctx, 'healthcare', 'appointments', appointment.id, ['cancelled', 'Patient asked to rebook']); continue; }
        if (date >= addDays(today, -1)) continue;
        await move(ctx, 'healthcare', 'appointments', appointment.id, 'checkedin');
        await move(ctx, 'healthcare', 'appointments', appointment.id, 'completed');
        const consult = ctx.item(ctx.random.chance(0.5) ? 'SVC-CONS' : 'SVC-GP');
        const lines = [{ description: consult.name, itemId: consult.id, quantity: 1, unitPrice: consult.price }];
        if (ctx.random.chance(0.45)) { const t = ctx.item(ctx.random.pick(['SVC-CBC', 'SVC-ECG', 'SVC-XRY'])); lines.push({ description: t.name, itemId: t.id, quantity: 1, unitPrice: t.price }); }
        const bill = await record(ctx, 'healthcare', 'medicalbills', { title: `Visit ${date}`, recordDate: date, parentId: patient.id, partyId: patient.partyId, dueDate: addDays(date, 30), data: { department: ctx.random.pick(departments), payer: patient.partyId === selfPay.id ? 'Self' : 'Insurer' }, lines }, ['finalized']);
        if (ctx.random.chance(0.12) && first === monthStart(0)) continue;
        const invoiced = await staff.post(`/m/healthcare/records/medicalbills/${bill.id}/invoice`, { date });
        await settleByAge(ctx, invoiced.invoiceId, date, first);
        if (ctx.random.chance(0.4)) {
          const med = ctx.item(ctx.random.pick(['MED-PCM', 'MED-AMX', 'MED-IBU']));
          const dispensing = await record(ctx, 'healthcare', 'dispensings', { title: `Prescription ${date}`, recordDate: date, parentId: patient.id, partyId: patient.partyId, data: { prescriber: ctx.random.pick(doctors), prescriptionRef: `RX-${date}-${n}` }, lines: [{ description: med.name, itemId: med.id, unitOfMeasure: med.uom, quantity: ctx.random.int(1, 3), unitPrice: med.price }] });
          try {
            await staff.post(`/m/healthcare/records/dispensings/${dispensing.id}/consume`, { date, warehouseId: ctx.warehouse });
            await move(ctx, 'healthcare', 'dispensings', dispensing.id, 'dispensed');
            const billed = await staff.post(`/m/healthcare/records/dispensings/${dispensing.id}/invoice`, { date });
            await settleByAge(ctx, billed.invoiceId, date, first);
          } catch (e) { if (e.status !== 409) throw e; ctx.log(`      pharmacy short: ${e.message.slice(0, 80)}`); }
        }
      }
      // Insurance claims: most approved, some rejected or waiting.
      for (let n = 0; n < 3; n++) {
        const date = dayIn(ctx, first);
        ctx.on(date);
        const insurer = ctx.random.pick(insurers);
        const patient = ctx.random.pick(patients);
        const claim = await record(ctx, 'healthcare', 'claims', { title: `Treatment claim ${date}`, recordDate: date, parentId: patient.id, partyId: insurer.id, dueDate: addDays(date, 45), amount: ctx.random.money(400, 6500, 25), data: { authorizationCode: `AUTH-${date.replace(/-/g, '')}-${n}` } }, ['submitted']);
        const roll = ctx.random.next();
        if (roll < 0.15) { await move(ctx, 'healthcare', 'claims', claim.id, ['rejected', 'Treatment not covered by the policy'], ctx.owner); continue; }
        if (roll < 0.3 || first === monthStart(0)) continue;
        const approved = await move(ctx, 'healthcare', 'claims', claim.id, 'approved', ctx.owner);
        const invoiced = await staff.post(`/m/healthcare/records/claims/${approved.id}/invoice`, { date });
        await settleByAge(ctx, invoiced.invoiceId, clampToToday(addDays(date, 10)), first);
      }
    });
  };
}

// ---- Logistics ------------------------------------------------------------------------------------------------------------------------------------

export async function logistics(ctx) {
  const staff = ctx.people.ops ?? ctx.owner;
  await ctx.run('fleet', async () => {
    ctx.on(monthStart(5));
    for (const [title, plate, kind, capacityKg, km] of [['Truck 1', 'QA-10231', 'Heavy truck', 18000, 142000], ['Truck 2', 'QA-10877', 'Heavy truck', 18000, 98000], ['Truck 3', 'QA-11452', 'Light truck', 6000, 61000], ['Van 1', 'QA-20114', 'Van', 1500, 87000], ['Van 2', 'QA-20998', 'Van', 1500, 45000], ['Trailer 1', 'QA-30512', 'Trailer', 24000, 204000]])
      await record(ctx, 'logistics', 'vehicles', { title, recordDate: monthStart(5), data: { plate, kind, capacityKg, odometerKm: km, registrationExpiry: addDays(today, ctx.random.int(30, 330)) } });
    for (const name of ctx.org.employees.slice(1, 6))
      await record(ctx, 'logistics', 'drivers', { title: name, recordDate: monthStart(5), data: { licenseNumber: `TEST-DL-${ctx.random.int(1000, 9999)}`, licenseExpiry: addDays(today, ctx.random.int(60, 500)), phone: `+000 555 4${ctx.random.int(100, 999)}` } }, [], ctx.owner);
  });
  const vehicles = await staff.list('/m/logistics/records/vehicles');
  const drivers = await staff.list('/m/logistics/records/drivers');
  const cities = ['Doha', 'Mesaieed', 'Al Khor', 'Al Wakrah', 'Dukhan', 'Lusail', 'Hamad Port', 'Riyadh', 'Dubai'];
  const fuelVendor = ctx.party('Woqod Fuel Accounts');
  const workshop = ctx.party('Al Attiya Motors Workshop');
  const tyres = ctx.party('Tyre Plus Qatar');
  const tolls = ctx.party('Gulf Toll Services');
  const port = ctx.party('Hamad Port Handling');
  ctx.monthly = async (first) => {
    await ctx.run(`shipments-${first.slice(0, 7)}`, async () => {
      for (let n = 0; n < 16; n++) {
        const date = dayIn(ctx, first);
        ctx.on(date);
        const customer = ctx.random.pick(ctx.customers);
        const origin = ctx.random.pick(cities);
        let destination = ctx.random.pick(cities);
        if (destination === origin) destination = origin === 'Doha' ? 'Dukhan' : 'Doha';
        const vehicle = ctx.random.pick(vehicles);
        const weight = ctx.random.int(200, 16000);
        const ftl = weight > 5000;
        const service = ctx.item(ftl ? 'FRT-FTL' : 'FRT-LTL');
        const lines = [{ description: service.name, itemId: service.id, quantity: ftl ? 1 : weight, unitPrice: ftl ? ctx.random.money(1400, 2600, 50) : service.price }];
        if (ctx.random.chance(0.4)) lines.push({ description: 'Loading and handling', itemId: ctx.item('FRT-HDL').id, quantity: 1, unitPrice: 220 });
        const shipment = await record(ctx, 'logistics', 'shipments', {
          title: `${customer.name.split(' ')[0]} consignment ${first.slice(5, 7)}-${n}`, recordDate: date, partyId: customer.id, dueDate: addDays(date, ctx.random.int(1, 4)), businessUnitId: ctx.random.pick(ctx.units).id,
          data: { origin, destination, mode: 'Road', weightKg: weight, pieces: ctx.random.int(1, 40), vehiclePlate: vehicle.data.plate, driver: ctx.random.pick(drivers).title }, lines,
        });
        const roll = ctx.random.next();
        if (roll < 0.06) { await move(ctx, 'logistics', 'shipments', shipment.id, ['cancelled', 'Customer postponed the load']); continue; }
        if (first === monthStart(0) && roll < 0.45) { if (roll > 0.2) { await move(ctx, 'logistics', 'shipments', shipment.id, 'picked'); await move(ctx, 'logistics', 'shipments', shipment.id, 'intransit'); } continue; }
        await move(ctx, 'logistics', 'shipments', shipment.id, 'picked');
        await move(ctx, 'logistics', 'shipments', shipment.id, 'intransit');
        if (roll < 0.1) { await move(ctx, 'logistics', 'shipments', shipment.id, ['returned', 'Consignee refused delivery']); continue; }
        await move(ctx, 'logistics', 'shipments', shipment.id, 'delivered');
        const invoiceDate = clampToToday(addDays(date, ctx.random.int(1, 3)));
        if (roll < 0.2 && first >= monthStart(1)) continue;
        const billed = await staff.post(`/m/logistics/records/shipments/${shipment.id}/invoice`, { date: invoiceDate, taxCodeId: taxId(ctx) });
        await settleByAge(ctx, billed.invoiceId, invoiceDate, first);
      }
    });
    await ctx.run(`fleet-costs-${first.slice(0, 7)}`, async () => {
      for (const vehicle of vehicles) {
        for (let n = 0; n < 2; n++) {
          const date = dayIn(ctx, first);
          ctx.on(date);
          const litres = ctx.random.int(80, 340);
          const log = await record(ctx, 'logistics', 'fuel', { title: `Fill ${vehicle.data.plate}`, recordDate: date, parentId: vehicle.id, partyId: fuelVendor.id, amount: round2(litres * 2.05), data: { litres, odometerKm: (vehicle.data.odometerKm ?? 0) + n * 800, station: ctx.random.pick(['Doha Sadd', 'Salwa Road', 'Mesaieed']) } });
          if (n === 1 && ctx.random.chance(0.1)) { await move(ctx, 'logistics', 'fuel', log.id, ['rejected', 'Odometer reading does not match']); continue; }
          await move(ctx, 'logistics', 'fuel', log.id, 'approved');
          const done = await staff.post(`/m/logistics/records/fuel/${log.id}/bill`, { date });
          if (first < monthStart(1)) await paySome(ctx, done.billId, clampToToday(addDays(date, 12)));
        }
      }
      const date = dayIn(ctx, first);
      ctx.on(date);
      const v = ctx.random.pick(vehicles);
      const job = await record(ctx, 'logistics', 'maintenance', { title: `${ctx.random.pick(['Service', 'Brake repair', 'New tyres'])} ${v.data.plate}`, recordDate: date, parentId: v.id, partyId: (ctx.random.chance(0.7) ? workshop : tyres).id, data: { kind: 'Scheduled service', odometerKm: v.data.odometerKm, workshop: 'Al Attiya Motors' }, lines: [{ description: 'Labour', quantity: ctx.random.int(2, 8), unitPrice: 120 }, { description: 'Parts', quantity: 1, unitPrice: ctx.random.money(250, 2400, 50) }] });
      if (first === monthStart(0)) return;
      await move(ctx, 'logistics', 'maintenance', job.id, 'completed');
      const billedJob = await staff.post(`/m/logistics/records/maintenance/${job.id}/bill`, { date, taxCodeId: taxId(ctx) });
      await paySome(ctx, billedJob.billId, clampToToday(addDays(date, 20)));
      for (const [kind, vendor, amount] of [['Tolls', tolls, ctx.random.money(300, 900, 10)], ['Port charges', port, ctx.random.money(600, 2500, 50)], ['Driver allowance', tolls, ctx.random.money(400, 900, 10)]]) {
        const e = await record(ctx, 'logistics', 'deliveryexpenses', { title: `${kind} ${first.slice(0, 7)}`, recordDate: date, partyId: vendor.id, amount, data: { kind } }, ['approved']);
        await staff.post(`/m/logistics/records/deliveryexpenses/${e.id}/bill`, { date, vendorReference: `DEX-${e.number}` });
      }
    });
  };
}

// ---- Software and IT services -----------------------------------------------------------------------------------------------------------------------

export async function software(ctx) {
  const pm = ctx.people.ops ?? ctx.owner;
  const owner = ctx.owner;
  await ctx.run('crm', async () => {
    if (!ctx.modules.has('crm')) return;
    let n = 0;
    for (const [fullName, companyName, status] of [['Hannah Cole', 'Cobalt Fintech', 'qualified'], ['Lewis Boyd', 'Oakline Logistics', 'contacted'], ['Zara Sheikh', 'Bright Retail', 'new'], ['Peter Vance', 'Summit Education', 'unqualified'], ['Lara Quinn', 'Harbour Retail Group', 'qualified']]) {
      ctx.on(addDays(monthStart(5), ++n * 12));
      const lead = await owner.post('/m/crm/leads', { legalEntityId: ctx.entity, fullName, companyName, email: `lead${n}@${ctx.org.code}.example.test`, source: 'website', estimatedValue: ctx.random.money(15000, 90000, 1000), assignedUserName: 'Project manager' });
      await owner.put(`/m/crm/leads/${lead.id}`, { legalEntityId: ctx.entity, fullName, companyName, source: 'website', estimatedValue: lead.estimatedValue, status }).catch(() => {});
    }
  });
  const portfolio = [
    ['Harbour customer portal', 0, 'timeAndMaterials', 180000], ['Northwind claims platform', 1, 'fixedFee', 240000], ['Greenfield analytics', 2, 'timeAndMaterials', 95000],
    ['Cobalt mobile app', 3, 'fixedFee', 130000], ['Internal tooling', null, 'nonBillable', 40000],
  ];
  await ctx.run('projects', async () => {
    const start = monthStart(5);
    ctx.on(start);
    for (const [name, c, billingType, budgetAmount] of portfolio) {
      const customer = c === null ? null : ctx.customers[c];
      const project = await pm.post('/m/projects/projects', { legalEntityId: ctx.entity, name, customerId: customer?.id, customerName: customer?.name, managerName: ctx.org.employees[0], status: 'active', billingType, budgetAmount, contractValue: billingType === 'fixedFee' ? budgetAmount : undefined, startDate: start, endDate: addDays(start, 300), budgetHours: Math.round(budgetAmount / 80), taxCodeId: taxId(ctx) });
      for (const [title, est] of [['Discovery', 60], ['Build', 240], ['Testing', 90], ['Release', 30]])
        await pm.post('/m/projects/tasks', { projectId: project.id, title, status: title === 'Discovery' ? 'done' : title === 'Build' ? 'inProgress' : 'todo', priority: 'medium', assignedTo: ctx.org.employees[ctx.random.int(1, 4)], estimatedHours: est, actualHours: 0, dueDate: addDays(start, est * 2) }).catch((e) => ctx.log(`      task: ${e.message.slice(0, 120)}`));
      if (billingType === 'fixedFee') {
        let i = 0;
        for (const [title, share] of [['Kick-off', 0.2], ['Design approved', 0.3], ['Beta release', 0.3], ['Go-live', 0.2]]) {
          const due = addDays(start, 30 + i++ * 45);
          await pm.post('/m/projects/milestones', { projectId: project.id, title, dueDate: due, amount: round2(budgetAmount * share) });
        }
      }
    }
    await pm.post('/m/projects/rates', { role: 'Developer', billRate: 95, costRate: 48 }).catch(() => {});
  });
  const projects = await pm.list('/m/projects/projects');
  const people = ctx.org.employees.slice(1, 7);
  ctx.monthly = async (first) => {
    await ctx.run(`timesheets-${first.slice(0, 7)}`, async () => {
      for (const project of projects) {
        for (let n = 0; n < 12; n++) {
          const date = dayIn(ctx, first);
          ctx.on(date);
          await pm.post('/m/projects/timesheets', { projectId: project.id, date, workerName: ctx.random.pick(people), hours: ctx.random.int(3, 8), hourlyRate: 95, costRate: 48, description: ctx.random.pick(['Feature work', 'Code review', 'Client workshop', 'Bug fixing', 'Release support']), isBillable: project.billingType !== 'nonBillable' });
        }
        if (ctx.random.chance(0.5)) {
          const date = dayIn(ctx, first);
          ctx.on(date);
          await pm.post('/m/projects/expenses', { projectId: project.id, date, category: 'Travel', description: 'Client site visit', amount: ctx.random.money(120, 900, 10), isBillable: project.billingType === 'timeAndMaterials', markupPercent: 0 });
        }
      }
      if (first === monthStart(0)) return;
      const end = addDays(first, 27);
      ctx.on(end);
      for (const project of projects) {
        await pm.post('/m/projects/billing/post-costs', { projectId: project.id });
        if (project.billingType === 'timeAndMaterials') {
          const invoice = await pm.post('/m/projects/billing/bill', { projectId: project.id, grouping: 'person', discountPercent: 0, dueDate: addDays(end, 30) });
          const issued = await issueIfDraft(ctx, invoice.invoiceId);
          await settleByAge(ctx, issued.id, end, first);
        }
        if (project.billingType === 'fixedFee') {
          const milestones = (await pm.list(`/m/projects/milestones?filter=projectId:eq:${project.id}`)).filter((m) => m.dueDate <= end && m.status !== 'achieved' && m.status !== 'invoiced' && m.status !== 'billed');
          for (const m of milestones) {
            await pm.post(`/m/projects/milestones/${m.id}/achieve`, { achievedDate: m.dueDate });
            const invoice = await pm.post('/m/projects/billing/bill', { projectId: project.id, milestoneIds: [m.id], grouping: 'detailed', discountPercent: 0, dueDate: addDays(end, 30) });
            const issued = await issueIfDraft(ctx, invoice.invoiceId);
            await settleByAge(ctx, issued.id, end, first);
          }
        }
      }
    });
  };
  await ctx.run('retainers', async () => {
    const start = monthStart(5);
    ctx.on(start);
    const customer = ctx.customers[5];
    const engagement = await owner.post('/m/services/engagements', { legalEntityId: ctx.entity, title: 'Maple Health support', clientId: customer.id, clientName: customer.name, type: 3, contractValue: 0, budgetedHours: 240, startDate: start, leadConsultantName: ctx.org.employees[0] });
    const retainer = await owner.post('/m/services/retainers', { legalEntityId: ctx.entity, engagementId: engagement.id, clientId: customer.id, clientName: customer.name, title: 'Monthly support retainer', cycle: 1, recurringFee: 3200, includedHours: 30, overageHourlyRate: 110, startDate: start, terms: 'Billed monthly in advance' });
    for (const first of months(5)) {
      const date = clampToToday(addDays(first, 2));
      ctx.on(date);
      const invoice = await owner.post(`/m/services/retainers/${retainer.id}/invoice`, { periodDate: date, invoiceDate: date, taxCodeId: taxId(ctx) });
      const invoiceId = invoice.invoiceId ?? invoice.invoice?.id ?? invoice.id;
      const finance = ctx.people.finance ?? owner;
      const row = await finance.get(`/finance/invoices/${invoiceId}`);
      if (row.status === 'draft') await finance.post(`/finance/invoices/${invoiceId}/issue`, {});
      await settleByAge(ctx, invoiceId, date, first);
    }
  });
  ctx.on(null);
}

// ---- Education ---------------------------------------------------------------------------------------------------------------------------------------

export async function education(ctx) {
  const staff = ctx.people.ops ?? ctx.owner;
  const start = monthStart(5);
  const pupils = ['Ali Ahmad', 'Sara Siddiqui', 'Hamza Rehman', 'Maira Chaudhry', 'Taha Mirza', 'Iqra Baig', 'Zain Sheikh', 'Aiman Abbasi', 'Rayan Lodhi', 'Noor Kazmi', 'Bilal Ahmad', 'Hina Siddiqui', 'Saad Rehman', 'Eman Chaudhry', 'Omer Mirza', 'Laiba Baig', 'Fahad Sheikh', 'Anaya Abbasi', 'Musa Lodhi', 'Dua Kazmi'];
  await ctx.run('academic-setup', async () => {
    ctx.on(start);
    const session = await record(ctx, 'education', 'sessions', { title: `Academic year ${start.slice(0, 4)}`, recordDate: start, data: { startsOn: start, endsOn: addDays(start, 300) } }, ['open']);
    for (const [title, grade, section, teacher] of [['Grade 5 A', 'Grade 5', 'A', ctx.org.employees[0]], ['Grade 6 A', 'Grade 6', 'A', ctx.org.employees[1]], ['Grade 7 A', 'Grade 7', 'A', ctx.org.employees[2]], ['Grade 8 A', 'Grade 8', 'A', ctx.org.employees[3]]])
      await record(ctx, 'education', 'classes', { title, recordDate: start, parentId: session.id, data: { grade, section, classTeacher: teacher, capacity: 30 } });
  });
  const session = (await staff.list('/m/education/records/sessions'))[0];
  const classes = await staff.list('/m/education/records/classes');
  await ctx.run('admissions-and-students', async () => {
    let i = 0;
    for (const name of pupils) {
      const date = addDays(start, i % 25);
      ctx.on(date);
      const family = ctx.customers[i % ctx.customers.length];
      const cls = classes[i % classes.length];
      const admission = await record(ctx, 'education', 'admissions', { title: name, recordDate: date, parentId: session.id, data: { programme: cls.data.grade, dateOfBirth: `${2011 + i % 5}-0${1 + i % 9}-10`, guardianName: family.name.replace('Fee Payer ', ''), guardianPhone: `+000 555 5${String(i).padStart(3, '0')}`, previousSchool: 'Previous Demo School' } }, [], ctx.owner);
      await move(ctx, 'education', 'admissions', admission.id, 'shortlisted', ctx.people.admin);
      await move(ctx, 'education', 'admissions', admission.id, 'approved', ctx.people.admin);
      await move(ctx, 'education', 'admissions', admission.id, 'enrolled', ctx.people.admin);
      await record(ctx, 'education', 'students', { title: name, recordDate: date, partyId: family.id, parentId: cls.id, data: { grade: cls.data.grade, dateOfBirth: `${2011 + i % 5}-0${1 + i % 9}-10`, guardianName: family.name.replace('Fee Payer ', ''), guardianPhone: `+000 555 5${String(i).padStart(3, '0')}` } }, [], ctx.owner);
      i++;
    }
    for (const [name, outcome, reason] of [['Applicant Waiting One', null], ['Applicant Waiting Two', 'shortlisted'], ['Applicant Rejected', 'rejected', 'Grade 6 is full for this session'], ['Applicant Withdrawn', 'withdrawn', 'Family moved to another city']]) {
      ctx.on(addDays(today, -20));
      const a = await record(ctx, 'education', 'admissions', { title: name, recordDate: addDays(today, -20), parentId: session.id, data: { programme: 'Grade 6', guardianName: 'Guardian (demo)' } }, [], ctx.owner);
      if (outcome) await move(ctx, 'education', 'admissions', a.id, reason ? [outcome, reason] : outcome, ctx.people.admin);
    }
  });
  const students = await staff.list('/m/education/records/students');
  await ctx.run('fee-plans', async () => {
    const tuition = ctx.item('FEE-TUI');
    let i = 0;
    for (const student of students) {
      ctx.on(addDays(start, 1));
      const lines = [{ description: 'Admission fee', quantity: 1, unitPrice: ctx.item('FEE-ADM').price, itemId: ctx.item('FEE-ADM').id, dueDate: addDays(start, 5) }];
      for (let t = 0; t < 3; t++) lines.push({ description: `Tuition instalment ${t + 1} of 3`, quantity: 1, unitPrice: tuition.price / 2, itemId: tuition.id, dueDate: addDays(start, 5 + t * 75) });
      if (i % 4 === 0) lines.push({ description: 'Transport term 1', quantity: 1, unitPrice: ctx.item('FEE-TRN').price, itemId: ctx.item('FEE-TRN').id, dueDate: addDays(start, 10) });
      const plan = await record(ctx, 'education', 'fees', { title: `${student.title} fees`, recordDate: addDays(start, 1), partyId: student.partyId, parentId: student.id, data: { term: 'Term 1 to 3', scholarship: i % 7 === 0 ? 'Sibling discount applied' : undefined }, lines }, ['active']);
      for (const line of plan.lines) {
        if (!line.dueDate || line.dueDate > today) continue;
        ctx.on(line.dueDate);
        const billed = await staff.post(`/m/education/records/fees/${plan.id}/invoice`, { date: line.dueDate, lineIds: [line.id] });
        const invoiceId = billed.lines.find((l) => l.id === line.id).invoiceId;
        const month = line.dueDate.slice(0, 7) + '-01';
        await settleByAge(ctx, invoiceId, clampToToday(addDays(line.dueDate, ctx.random.int(0, 10))), month);
      }
      i++;
    }
  });
  await ctx.run('examinations', async () => {
    for (const [term, back] of [['Midterm', 3], ['Monthly test', 1]]) {
      const date = addDays(monthStart(back), 10);
      for (const cls of classes) {
        ctx.on(date);
        for (const subject of ['Mathematics', 'English', 'Science']) {
          const exam = await record(ctx, 'education', 'exams', { title: `${term} ${subject}`, recordDate: date, parentId: cls.id, data: { subject, maxMarks: 100, examDate: date } }, ['held']);
          await move(ctx, 'education', 'exams', exam.id, 'published', ctx.owner);
          for (const student of students.filter((s) => s.parentId === cls.id)) {
            const marks = ctx.random.int(38, 98);
            await record(ctx, 'education', 'results', { title: `${term} ${subject}`, recordDate: date, parentId: student.id, data: { subject, marks, grade: marks >= 85 ? 'A' : marks >= 70 ? 'B' : marks >= 50 ? 'C' : 'D' } }, ['final']);
          }
        }
      }
    }
  });
}

// ---- Dairy ---------------------------------------------------------------------------------------------------------------------------------------------

export async function dairy(ctx) {
  const stockIds = new Set(ctx.org.sellOnly);
  ctx.buyable = () => ctx.stockItems.filter((i) => !stockIds.has(i.sku));
  ctx.sellable = () => ctx.items.filter((i) => stockIds.has(i.sku));
  await ctx.run('herds', async () => {
    ctx.on(monthStart(5));
    for (const [title, species, headCount, location] of [['Holstein herd A', 'Dairy cattle', 64, 'Shed 1'], ['Holstein herd B', 'Dairy cattle', 48, 'Shed 2'], ['Murrah buffalo group', 'Buffalo', 36, 'Shed 3'], ['Heifer group', 'Dairy cattle', 22, 'Paddock 2']])
      await record(ctx, 'agriculture', 'herds', { title, recordDate: monthStart(5), data: { species, headCount, location } });
    for (const [title, crop, areaHectares, field, status] of [['Maize for silage', 'Maize', 18, 'North field', ['planted', 'harvested']], ['Lucerne fodder', 'Lucerne', 9, 'East field', ['planted']], ['Oats winter fodder', 'Oats', 12, 'South field', []]])
      await record(ctx, 'agriculture', 'crops', { title, recordDate: monthStart(4), data: { crop, areaHectares, field, plantedOn: monthStart(4), expectedHarvest: addDays(monthStart(4), 110) } }, status);
  });
  const herds = await ctx.owner.list('/m/agriculture/records/herds');
  const staff = ctx.people.ops ?? ctx.owner;
  const conc = ctx.item('FED-CON'); const silage = ctx.item('FED-SIL'); const minerals = ctx.item('FED-MIN');
  const vet = ctx.party('Anand Vet Services');
  const inv = ctx.people.inventory ?? ctx.owner;
  ctx.monthly = async (first) => {
    await ctx.run(`farm-${first.slice(0, 7)}`, async () => {
      for (const herd of herds.slice(0, 3)) {
        for (let week = 0; week < 4; week++) {
          const date = addDays(first, week * 7 + 1);
          if (date > today) continue;
          ctx.on(date);
          const milk = Math.round(herd.data.headCount * ctx.random.int(9, 13) * 7);
          await record(ctx, 'agriculture', 'production', { title: `Week ${week + 1} milk`, recordDate: date, parentId: herd.id, data: { product: 'Milk (litres)', quantity: milk, fatPercent: ctx.random.int(35, 48) / 10, mortality: ctx.random.chance(0.05) ? 1 : 0 } });
          // The week's milk enters stock at its production cost, with feed and labour already expensed in the ledger.
          await inv.post('/m/inventory/stock/receive', { warehouseId: ctx.warehouse, itemId: ctx.item('MLK-RAW').id, quantity: milk, unitCost: ctx.item('MLK-RAW').cost, date, offsetAccountId: ctx.account('purchases.expense'), reference: `MILK-${herd.number}-${date}`, memo: 'Milk produced' });
          const feed = await record(ctx, 'agriculture', 'feed', { title: `Feed week ${week + 1}`, recordDate: date, parentId: herd.id, data: { ration: 'Lactation ration', issuedBy: ctx.org.employees[2] }, lines: [{ description: conc.name, itemId: conc.id, unitOfMeasure: conc.uom, quantity: Math.ceil(herd.data.headCount / 6), unitPrice: conc.cost }, { description: silage.name, itemId: silage.id, unitOfMeasure: silage.uom, quantity: herd.data.headCount * 30, unitPrice: silage.cost }, { description: minerals.name, itemId: minerals.id, unitOfMeasure: minerals.uom, quantity: Math.ceil(herd.data.headCount / 10), unitPrice: minerals.cost }] });
          try {
            await staff.post(`/m/agriculture/records/feed/${feed.id}/consume`, { date, warehouseId: ctx.warehouse });
            await move(ctx, 'agriculture', 'feed', feed.id, 'issued');
          } catch (e) {
            if (e.status !== 409) throw e;
            await purchaseCycle(ctx, first, 900 + week, 'paid', [{ it: conc, quantity: 60 }, { it: silage, quantity: 9000 }, { it: minerals, quantity: 60 }]);
            await move(ctx, 'agriculture', 'feed', feed.id, ['cancelled', 'Feed short on the day, issued from the next delivery']);
          }
        }
      }
      const date = dayIn(ctx, first);
      ctx.on(date);
      const visit = await record(ctx, 'agriculture', 'vetcare', { title: `${ctx.random.pick(['FMD vaccination', 'Pregnancy checks', 'Mastitis treatment'])} ${first.slice(0, 7)}`, recordDate: date, parentId: ctx.random.pick(herds).id, partyId: vet.id, amount: ctx.random.money(6000, 28000, 500), data: { kind: 'Vaccination', animalsTreated: ctx.random.int(15, 60), veterinarian: 'Dr. Demo Vet' } }, ['done']);
      const billed = await staff.post(`/m/agriculture/records/vetcare/${visit.id}/bill`, { date, taxCodeId: taxId(ctx) });
      if (first < monthStart(1)) await paySome(ctx, billed.billId, clampToToday(addDays(date, 18)));
    });
  };
  const crops = (await ctx.owner.list('/m/agriculture/records/crops')).filter((c) => c.status === 'harvested');
  await ctx.run('harvests', async () => {
    for (const c of crops) await record(ctx, 'agriculture', 'harvests', { title: `${c.title} lot 1`, recordDate: monthStart(1), parentId: c.id, data: { quantityKg: ctx.random.int(40000, 90000), grade: 'A' } });
  });
}

// ---- Import and export ---------------------------------------------------------------------------------------------------------------------------------

export async function trade(ctx) {
  const staff = ctx.people.ops ?? ctx.owner;
  const brokers = ctx.org.brokers.map((n) => ctx.party(n));
  ctx.monthly = async (first) => {
    await ctx.run(`trade-${first.slice(0, 7)}`, async () => {
      const date = dayIn(ctx, first);
      ctx.on(date);
      const supplier = ctx.party(ctx.org.suppliers[ctx.random.int(0, 3)]);
      const lc = await record(ctx, 'trade', 'lettersofcredit', { title: `Import LC ${first.slice(0, 7)}`, recordDate: date, partyId: supplier.id, amount: ctx.random.money(80000, 420000, 1000), dueDate: addDays(date, 120), data: { issuingBank: 'Demo Trade Bank', lcReference: `LC-${date.replace(/-/g, '')}`, kind: 'Sight' } }, ['submitted']);
      await move(ctx, 'trade', 'lettersofcredit', lc.id, ctx.random.chance(0.1) ? ['rejected', 'Credit line not available'] : 'opened', ctx.owner);
      const consignment = await record(ctx, 'trade', 'imports', {
        title: `Consignment ${first.slice(0, 7)}`, recordDate: date, partyId: brokers[0].id, data: { billOfLading: `BL-${date.replace(/-/g, '')}`, vessel: ctx.random.pick(['MV Demo Star', 'MV Test Pearl']), portOfEntry: 'Jebel Ali', eta: addDays(date, 21), customsEntry: `CE-${ctx.random.int(10000, 99999)}`, purchaseOrderRef: `PO ref ${first.slice(5, 7)}` },
        lines: [{ description: 'Ocean freight', quantity: 1, unitPrice: ctx.random.money(6000, 18000, 100) }, { description: 'Customs duty', quantity: 1, unitPrice: ctx.random.money(3000, 12000, 100) }, { description: 'Clearing and port handling', quantity: 1, unitPrice: ctx.random.money(1200, 4800, 50) }],
      });
      if (first === monthStart(0)) { await move(ctx, 'trade', 'imports', consignment.id, 'onwater'); return; }
      for (const step of ['onwater', 'atport', 'cleared']) await move(ctx, 'trade', 'imports', consignment.id, step);
      const billed = await staff.post(`/m/trade/records/imports/${consignment.id}/bill`, { date: clampToToday(addDays(date, 22)), taxCodeId: taxId(ctx), vendorReference: `IMP-${consignment.number}` });
      if (first < monthStart(1)) await paySome(ctx, billed.billId, clampToToday(addDays(date, 40)));

      const buyer = ctx.random.pick(ctx.customers);
      const goods = ctx.items.filter((i) => i.category === 'Export goods');
      const lines = goods.map((g) => ({ description: g.name, itemId: g.id, unitOfMeasure: g.uom, quantity: ctx.random.int(20, 120), unitPrice: g.price }));
      const exportOrder = await record(ctx, 'trade', 'exports', { title: `Export to ${buyer.name}`, recordDate: date, partyId: buyer.id, data: { destinationCountry: buyer.name.split(' ').pop(), incoterm: ctx.random.pick(['FOB', 'CIF', 'CFR']), portOfLoading: 'Jebel Ali', billOfLading: `EX-${date.replace(/-/g, '')}`, salesOrderRef: `SO ref ${first.slice(5, 7)}` }, lines }, ['confirmed']);
      const roll = ctx.random.next();
      if (roll < 0.1) { await move(ctx, 'trade', 'exports', exportOrder.id, ['cancelled', 'Buyer cancelled before shipment']); return; }
      await move(ctx, 'trade', 'exports', exportOrder.id, 'shipped');
      const invoiced = await staff.post(`/m/trade/records/exports/${exportOrder.id}/invoice`, { date: clampToToday(addDays(date, 2)), taxCodeId: ctx.taxes.find((t) => t.rate === 0)?.id ?? taxId(ctx) });
      await settleByAge(ctx, invoiced.invoiceId, clampToToday(addDays(date, 2)), first);
    });
  };
}

export const SCENARIOS = { realestate: realEstate, manufacturing, retail, restaurant, construction, healthcare, logistics, software, education, dairy, trade, distribution: null };
