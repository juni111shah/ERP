import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { ProblemDetails } from '@core/api/api.models';
import { InvoiceStatus, PlatformApi } from '@core/api/platform.api';
import { displayMoney } from '@core/format/display';
import { problemMessage } from '@core/http/problem-details';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { showDrawerSaveError } from '@shared/forms/save-errors';
import { askReason } from '@shared/utils/reason-prompt';
import { DrawerValue, paymentFields, toPaymentInput } from '../../platform-forms';

/** The invoice the actions are for. */
export interface InvoiceActionTarget { id: string; number: string; status: InvoiceStatus; total: number; currency: string; }

/** What can be done with one invoice: an open one takes a payment or is voided, a paid one can have its payment taken back. */
@Component({
  selector: 'erp-invoice-actions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzModalModule, RecordDrawerComponent],
  styleUrl: './invoice-actions.component.scss',
  templateUrl: './invoice-actions.component.html',
})
export class InvoiceActionsComponent {
  private readonly api = inject(PlatformApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly invoice = input.required<InvoiceActionTarget>();
  readonly clientName = input('the client');
  readonly changed = output<void>();

  readonly open = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly amount = computed(() => displayMoney(this.invoice().total, this.invoice().currency));
  readonly fields: FieldSpec[] = paymentFields();

  pay(value: DrawerValue): void {
    this.saving.set(true);
    this.errors.set(null);
    this.api.recordPayment(this.invoice().id, toPaymentInput(value)).subscribe({
      next: () => { this.saving.set(false); this.open.set(false); this.message.success(`Payment recorded on ${this.invoice().number}`); this.changed.emit(); },
      error: (e) => { this.saving.set(false); showDrawerSaveError(e, this.errors, this.message, 'Could not record the payment'); },
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
