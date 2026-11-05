import { Pipe, PipeTransform } from '@angular/core';
import { displayDate, displayLabel, displayMoney } from '../core/format/display';

/** {{ row.status | erpLabel }}: an API value in words ("closedWon" becomes "Closed won"). */
@Pipe({ name: 'erpLabel', standalone: true })
export class ErpLabelPipe implements PipeTransform {
  transform(value: unknown): string { return displayLabel(value); }
}

/** {{ row.dueDate | erpDate }}: the one date format across the app ("05 Nov 2026"). */
@Pipe({ name: 'erpDate', standalone: true })
export class ErpDatePipe implements PipeTransform {
  transform(value: unknown): string { return displayDate(value); }
}

/** {{ row.total | erpMoney: row.currency }}: an amount with its currency, two decimals. */
@Pipe({ name: 'erpMoney', standalone: true })
export class ErpMoneyPipe implements PipeTransform {
  transform(value: number | null | undefined, currency?: string | null): string { return displayMoney(value, currency); }
}
