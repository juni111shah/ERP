import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { displayLabel } from '@core/format/display';
import { PosShiftReportDto } from '@core/api/pos.api';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { TENDER_LABELS } from '../../pos-cart';

/** A shift on paper: an X report while it is open, the Z report once closed. Used on screen and on the print page. */
@Component({
  selector: 'erp-pos-shift-report',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, ErpDatePipe],
  templateUrl: './pos-shift-report.component.html',
  styleUrl: './pos-shift-report.component.scss',
})
export class PosShiftReportComponent {
  readonly report = input.required<PosShiftReportDto>();
  readonly currency = input('');

  readonly moves = computed(() => this.report().session.drawerTransactions.filter((t) => t.reason !== 'Opening cash float'));

  money(n: number): string {
    const text = (n ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    return this.currency() ? `${this.currency()} ${text}` : text;
  }

  tenderLabel(method: string): string {
    return TENDER_LABELS[method as keyof typeof TENDER_LABELS] ?? displayLabel(method);
  }

  moveLabel(type: string): string {
    return { floatIn: 'Float in', payIn: 'Pay-in', payOut: 'Pay-out', dropOut: 'Safe drop' }[type] ?? displayLabel(type);
  }
}
