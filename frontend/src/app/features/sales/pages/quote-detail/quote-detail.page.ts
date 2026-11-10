import { SuccessToastService } from '@core/services/success-toast.service';
import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '@core/http/problem-details';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { QuoteDto, SalesQuotesApi, SalesQuoteStatus } from '@core/api/sales.api';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { displayLabel } from '@core/format/display';

@Component({
  selector: 'erp-quote-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatePanelComponent, StatusTagComponent, NzSkeletonModule, NzToolTipModule,
    CommonModule,
    DecimalPipe,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTableModule,
    NzDescriptionsModule,
    NzPopconfirmModule,
    HasPermissionDirective,
  ],
  templateUrl: './quote-detail.page.html',
  styleUrl: './quote-detail.page.scss',
})
export class QuoteDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly quotesApi = inject(SalesQuotesApi);
  private readonly message = inject(NzMessageService);
  private readonly toast = inject(SuccessToastService);

  readonly quote = signal<QuoteDto | null>(null);
  readonly failed = signal(false);
  private quoteId: string | null = null;

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) this.loadQuote(id);
    });
  }

  reload(): void {
    if (this.quoteId) this.loadQuote(this.quoteId);
  }

  loadQuote(id: string): void {
    this.quoteId = id;
    this.failed.set(false);
    this.quotesApi.get(id).subscribe({
      next: (q) => this.quote.set(q),
      error: () => this.failed.set(true),
    });
  }

  changeStatus(status: SalesQuoteStatus): void {
    const q = this.quote();
    if (!q) return;
    this.quotesApi.setStatus(q.id, status).subscribe({
      next: (updated) => {
        this.quote.set(updated);
        this.message.success(`${q.number} is now ${displayLabel(status).toLowerCase()}.`);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to update status')),
    });
  }

  convertToOrder(): void {
    const q = this.quote();
    if (!q) return;
    this.quotesApi.convertToOrder(q.id).subscribe({
      next: (order) => {
        this.toast.show(`${q.number} converted into sales order ${order.number}`, { label: 'View order', to: ['/sales/orders', order.id] });
        this.loadQuote(q.id);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to convert quote')),
    });
  }

}
