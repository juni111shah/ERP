import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { BillsApi, InvoicesApi } from '@core/api/admin.api';
import { PurchaseOrdersApi } from '@core/api/purchasing.api';
import { PurchaseReturnInput, ReturnKind, SalesReturnInput } from '@core/api/returns.api';
import { SalesOrdersApi } from '@core/api/sales.api';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { ReturnFormLine, ReturnFormValue } from '@shared/components/return-form/return-form.component';

/** The document a return is raised against, with the lines that can still come back. */
export interface ReturnSource { type: 'order' | 'invoice' | 'bill'; id: string; label: string; lines: ReturnFormLine[]; }

export type ReturnSourceType = ReturnSource['type'];

/** What is left of a quantity, to four decimals and never below zero. */
const left = (n: number) => Math.max(0, Math.round(n * 10000) / 10000);

/** "SO-0001 · Gulf Traders", or the number alone. */
const docLabel = (n: string, p?: string | null) => (p ? `${n} · ${p}` : n);

/**
 * The documents a return can be raised against: sales orders and invoices for a sales return, purchase orders and bills
 * for a purchase return. Provided by the returns page.
 */
@Injectable()
export class ReturnSources {
  private readonly salesOrders = inject(SalesOrdersApi);
  private readonly purchaseOrders = inject(PurchaseOrdersApi);
  private readonly invoices = inject(InvoicesApi);
  private readonly bills = inject(BillsApi);

  /** Documents of the picked type matching a search (credit notes cannot be returned against). */
  search(kind: ReturnKind, type: ReturnSourceType, term: string): Observable<LookupOption[]> {
    const page = { page: 1, pageSize: 20, search: term };
    if (kind === 'sales') {
      return type === 'invoice'
        ? this.invoices.list(page).pipe(map((r) => r.items.filter((i) => !i.isCreditNote).map((i) => ({ value: i.id, label: docLabel(i.number, i.partyName) }))))
        : this.salesOrders.list(page).pipe(map((r) => r.items.map((o) => ({ value: o.id, label: docLabel(o.number, o.customerName) }))));
    }
    return type === 'bill'
      ? this.bills.list(page).pipe(map((r) => r.items.map((b) => ({ value: b.id, label: docLabel(b.number, b.partyName) }))))
      : this.purchaseOrders.list(page).pipe(map((r) => r.items.map((o) => ({ value: o.id, label: docLabel(o.number, o.vendorName) }))));
  }

  /** Loads the document and works out how much of each line can still come back. */
  load(kind: ReturnKind, type: ReturnSourceType, id: string): Observable<ReturnSource> {
    if (kind === 'sales' && type === 'order') {
      return this.salesOrders.get(id).pipe(map((o) => ({ type, id, label: o.number, lines: o.lines.map((l) => ({ key: l.id, label: l.description, max: left(l.deliveredQuantity - (l.returnedQuantity ?? 0)), unit: l.unitOfMeasure })).filter((l) => l.max > 0) })));
    }
    if (kind === 'sales') {
      return this.invoices.detail(id).pipe(map((i) => ({ type: 'invoice' as const, id, label: i.number, lines: i.lines.filter((l) => !!l.id).map((l) => ({ key: l.id!, label: l.description, max: left(l.quantity) })) })));
    }
    if (type === 'order') {
      return this.purchaseOrders.get(id).pipe(map((o) => ({ type, id, label: o.number, lines: o.lines.map((l) => ({ key: l.id, label: l.description, max: left(l.receivedQuantity - (l.returnedQuantity ?? 0)), unit: l.unitOfMeasure })).filter((l) => l.max > 0) })));
    }
    return this.bills.detail(id).pipe(map((b) => ({ type: 'bill' as const, id, label: b.number, lines: b.lines.filter((l) => !!l.id).map((l) => ({ key: l.id!, label: l.description, max: left(l.quantity) })) })));
  }
}

/** A sales return against an order or an invoice, as the API takes it. */
export function salesReturnBody(s: ReturnSource, v: ReturnFormValue): SalesReturnInput {
  return {
    salesOrderId: s.type === 'order' ? s.id : null, invoiceId: s.type === 'invoice' ? s.id : null, returnDate: v.returnDate, reason: v.reason, notes: v.notes,
    lines: v.lines.map((l) => ({ orderLineId: s.type === 'order' ? l.key : null, invoiceLineId: s.type === 'invoice' ? l.key : null, quantity: l.quantity, restock: l.restock })),
  };
}

/** A purchase return against an order or a bill, as the API takes it. */
export function purchaseReturnBody(s: ReturnSource, v: ReturnFormValue): PurchaseReturnInput {
  return {
    purchaseOrderId: s.type === 'order' ? s.id : null, billId: s.type === 'bill' ? s.id : null, returnDate: v.returnDate, reason: v.reason, notes: v.notes, warehouseId: v.warehouseId,
    lines: v.lines.map((l) => ({ orderLineId: s.type === 'order' ? l.key : null, billLineId: s.type === 'bill' ? l.key : null, quantity: l.quantity })),
  };
}
