import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { InvoiceDocument, PlatformApi } from '../../core/api/platform.api';
import { problemMessage } from '../../core/http/problem-details';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { SubscriptionInvoiceComponent } from '../../shared/subscription-invoice.component';

/** A client's own invoice, ready to print. It stays reachable when the workspace is blocked. */
@Component({
  selector: 'erp-billing-invoice',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzSpinModule, StatePanelComponent, SubscriptionInvoiceComponent],
  template: `
    @if (doc(); as d) {
      <erp-subscription-invoice [doc]="d" [backLink]="['/admin/billing']" backLabel="Subscription and billing" />
    } @else if (failed()) {
      <div style="padding:24px"><erp-state-panel kind="error" title="Could not load this invoice" [hint]="failed()" actionLabel="Try again" (action)="load()" /></div>
    } @else {
      <div style="padding:64px;text-align:center"><nz-spin nzSimple></nz-spin></div>
    }`,
})
export class BillingInvoicePage {
  private readonly api = inject(PlatformApi);
  private readonly title = inject(Title);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly doc = signal<InvoiceDocument | null>(null);
  readonly failed = signal<string | null>(null);

  constructor() { this.load(); }

  load(): void {
    this.failed.set(null);
    this.api.myInvoice(this.id).subscribe({
      next: (d) => { this.doc.set(d); this.title.setTitle(`Invoice ${d.number}`); },
      error: (e) => this.failed.set(problemMessage(e, 'The server did not answer.')),
    });
  }
}
