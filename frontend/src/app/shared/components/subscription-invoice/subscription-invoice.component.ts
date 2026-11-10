import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { InvoiceDocument } from '@core/api/platform.api';
import { ErpDatePipe, ErpMoneyPipe } from '../../pipes/display.pipes';

/** A subscription invoice as a clean sheet: the founder's console and a client's billing page print the same document. */
@Component({
  selector: 'erp-subscription-invoice',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzButtonModule, NzIconModule, ErpDatePipe, ErpMoneyPipe],
  styleUrl: './subscription-invoice.component.scss',
  templateUrl: './subscription-invoice.component.html',
})
export class SubscriptionInvoiceComponent {
  readonly doc = input.required<InvoiceDocument>();
  readonly backLink = input.required<string | unknown[]>();
  readonly backLabel = input('Back');
  print(): void { window.print(); }
}
