import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { ProblemDetails } from '@core/api/api.models';
import { InvoiceStatus, PlatformApi } from '@core/api/platform.api';
import { displayMoney } from '@core/format/display';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { askReason } from '@shared/utils/reason-prompt';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';

const PAYMENT_METHODS = ['Bank transfer', 'Cash', 'Cheque', 'Other'];

/** What can be done with one invoice: an open one takes a payment or is voided, a paid one can have its payment taken back. */
@Component({
  selector: 'erp-invoice-actions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzModalModule, RecordDrawerComponent],
  styles: [`:host { display: inline-flex; gap: 6px; flex-wrap: nowrap; }`],
  template: `
    @if (invoice().status === 'open') {
      <button nz-button nzSize="small" nzType="primary" type="button" (click)="$event.stopPropagation(); open.set(true)" [attr.data-testid]="'invoice-pay-' + invoice().number">Record payment</button>
      <button nz-button nzSize="small" nzDanger type="button" (click)="$event.stopPropagation(); voidIt()" [attr.data-testid]="'invoice-void-' + invoice().number">Void</button>
    }
    @if (invoice().status === 'paid') {
      <button nz-button nzSize="small" type="button" (click)="$event.stopPropagation(); reopen()" [attr.data-testid]="'invoice-reopen-' + invoice().number">Take back payment</button>
    }
    <erp-record-drawer [title]="'Record payment: ' + invoice().number" [open]="open()" [fields]="fields" [saving]="saving()" [errors]="errors()" submitLabel="Record payment" testId="payment-form"
                       (submitted)="pay($event)" (closed)="open.set(false)">
      <p drawerActions style="margin:0 0 16px;color:var(--erp-color-text-muted)">{{ amount() }} from {{ clientName() }}. Record what has already been received: this does not take any money.</p>
    </erp-record-drawer>`,
})
export class InvoiceActionsComponent {
  private readonly api = inject(PlatformApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly invoice = input.required<{ id: string; number: string; status: InvoiceStatus; total: number; currency: string }>();
  readonly clientName = input('the client');
  readonly changed = output<void>();

  readonly open = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly amount = computed(() => displayMoney(this.invoice().total, this.invoice().currency));
  readonly fields: FieldSpec[] = [
    { key: 'paidOn', label: 'Paid on', type: 'date', required: true, default: new Date() },
    { key: 'method', label: 'Method', type: 'select', default: PAYMENT_METHODS[0], options: PAYMENT_METHODS.map((m) => ({ value: m, label: m })) },
    { key: 'reference', label: 'Reference', type: 'text', maxLength: 200, hint: 'For example the bank transfer number.' },
  ];

  pay(value: Record<string, unknown>): void {
    this.saving.set(true);
    this.errors.set(null);
    this.api.recordPayment(this.invoice().id, { paidOn: value['paidOn'] as string, method: value['method'] as string | null, reference: value['reference'] as string | null }).subscribe({
      next: () => { this.saving.set(false); this.open.set(false); this.message.success(`Payment recorded on ${this.invoice().number}`); this.changed.emit(); },
      error: (e) => { this.saving.set(false); this.errors.set(asProblem(e)); if (!asProblem(e)?.errors) this.message.error(problemMessage(e, 'Could not record the payment')); },
    });
  }

  async voidIt(): Promise<void> {
    const reason = await askReason(this.modal, `Void invoice ${this.invoice().number}?`, 'Void invoice', 'The client will no longer owe this invoice. This cannot be undone.', false);
    if (reason === null) return;
    this.api.voidInvoice(this.invoice().id, reason || null).subscribe({
      next: () => { this.message.success(`${this.invoice().number} voided`); this.changed.emit(); },
      error: (e) => this.message.error(problemMessage(e, 'Could not void the invoice')),
    });
  }

  reopen(): void {
    this.modal.confirm({
      nzTitle: `Take back the payment on ${this.invoice().number}?`,
      nzContent: 'Use this for a payment recorded by mistake. The invoice becomes open again, and the next billing run works out the client\'s status.',
      nzOkText: 'Take back payment',
      nzOkDanger: true,
      nzCancelText: 'Keep it',
      nzOnOk: () => new Promise<void>((resolve) => {
        this.api.reopenInvoice(this.invoice().id).subscribe({
          next: () => { this.message.success(`${this.invoice().number} is open again`); this.changed.emit(); resolve(); },
          error: (e) => { this.message.error(problemMessage(e, 'Could not take back the payment')); resolve(); },
        });
      }),
    });
  }
}
