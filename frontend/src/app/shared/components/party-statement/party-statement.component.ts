import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTableModule } from 'ng-zorro-antd/table';
import { PartyLedger, PartyLedgerApi } from '@core/api/party-ledger.api';
import { toIsoDate } from '@core/format/dates';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';
import { HasPermissionDirective } from '../../directives/has-permission.directive';
import { RevealDirective } from '../../directives/reveal.directive';
import { EmailDocumentDrawerComponent } from '../email-document-drawer/email-document-drawer.component';
import { statementPrintHtml } from './statement-print';

export type { PartyLedger, PartyLedgerLine } from '@core/api/party-ledger.api';

const KIND_ROUTES: Record<string, string> = { invoice: '/finance/invoices', creditNote: '/finance/invoices', receipt: '/finance/receipts', bill: '/finance/bills', payment: '/finance/payments' };
const KIND_LABELS: Record<string, string> = { invoice: 'Invoice', creditNote: 'Credit note', receipt: 'Receipt', bill: 'Bill', payment: 'Payment' };

/** The server's own explanation of a failed load (detail, else title), if it sent one. */
function serverMessage(e: { error?: { detail?: string; title?: string } } | null | undefined): string | undefined {
  return e?.error?.detail || e?.error?.title;
}

/**
 * Statement of account for one party and side (customer: invoices, credit notes, receipts; vendor: bills, payments), with a running
 * balance from GET /finance/reports/{customer|vendor}-ledger. Defaults to the start of this year up to today; prints in its own window.
 */
@Component({
  selector: 'erp-party-statement',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, FormsModule, RouterLink, NzButtonModule, NzDatePickerModule, NzIconModule, NzTableModule, TranslatePipe, RevealDirective, EmailDocumentDrawerComponent, HasPermissionDirective],
  styleUrl: './party-statement.component.scss',
  templateUrl: './party-statement.component.html',
})
export class PartyStatementComponent {
  private readonly ledgerApi = inject(PartyLedgerApi);
  private readonly i18n = inject(I18nService);
  private readonly session = inject(SessionStore);

  readonly partyId = input.required<string>();
  readonly side = input.required<'customer' | 'vendor'>();
  readonly partyName = input<string>('');

  readonly emailOpen = signal(false);
  readonly range = signal<Date[]>([new Date(new Date().getFullYear(), 0, 1), new Date()]);
  readonly emailParams = computed(() => ({ asOf: toIsoDate(this.range()[1]) }));
  readonly data = signal<PartyLedger | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly hint = computed(() => this.side() === 'customer'
    ? this.i18n.t('statement.hintCustomer', 'Positive balance: the customer owes you. Draft and voided documents are not included.')
    : this.i18n.t('statement.hintVendor', 'Positive balance: you owe the vendor. Draft and voided documents are not included.'));

  private readonly autoLoad = effect(() => { this.partyId(); this.side(); untracked(() => this.load()); });

  kindLabel(kind: string): string { return this.i18n.t(`statement.kind.${kind}`, KIND_LABELS[kind] ?? kind); }
  route(kind: string): string | null { return KIND_ROUTES[kind] ?? null; }

  setRange(r: Date[] | null): void {
    if (!r || r.length !== 2 || !r[0] || !r[1]) return;
    this.range.set(r);
    this.load();
  }

  load(): void {
    const [from, to] = this.range();
    this.loading.set(true);
    this.error.set(null);
    this.ledgerApi.ledger(this.side(), this.partyId(), toIsoDate(from), toIsoDate(to)).subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: (e) => { this.data.set(null); this.loading.set(false); this.error.set(serverMessage(e) || this.i18n.t('statement.failed', 'Could not load the statement')); },
    });
  }

  /** Prints the statement on its own page (a new window), with brand color and clean document layout. */
  print(): void {
    const d = this.data();
    if (!d) return;
    const ws = this.session.me()?.workspace;
    const entity = this.session.me()?.legalEntities.find((e) => e.id === this.session.currentEntityId());
    const html = statementPrintHtml({
      ledger: d,
      side: this.side(),
      partyName: this.partyName(),
      orgName: ws?.name || 'HigherSquare 360 ERP',
      currency: entity?.baseCurrency || ws?.baseCurrency || '',
      dir: document.documentElement.dir || 'ltr',
      t: (k, f) => this.i18n.t(k, f),
      kindLabel: (kind) => this.kindLabel(kind),
    });
    const w = window.open('', '_blank');
    if (!w) { this.error.set(this.i18n.t('statement.popupBlocked', 'Allow pop-ups for this site to print the statement')); return; }
    w.document.open(); w.document.write(html); w.document.close();
    w.focus();
    w.print();
  }
}
