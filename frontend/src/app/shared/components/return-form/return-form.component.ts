import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { toIsoDate } from '@core/format/dates';
import { SelectOption } from '../record-drawer/record-drawer.component';

/** One line of the document being returned against: how much of it can still come back. */
export interface ReturnFormLine { key: string; label: string; max: number; unit?: string; }
export interface ReturnFormValue { returnDate: string; reason: string; notes: string | null; warehouseId: string | null; lines: { key: string; quantity: number; restock: boolean }[]; }

/**
 * The form of a sales or purchase return: when, why, and how much of each line comes back. Sales returns also choose per line
 * whether the goods go back on the shelf; purchase returns may choose the warehouse the goods leave. The page does the API call.
 */
@Component({
  selector: 'erp-return-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzAlertModule, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzInputModule, NzInputNumberModule, NzSelectModule, NzSwitchModule],
  styleUrl: './return-form.component.scss',
  templateUrl: './return-form.component.html',
})
export class ReturnFormComponent {
  readonly open = input.required<boolean>();
  readonly title = input('New return');
  readonly lines = input<ReturnFormLine[]>([]);
  readonly showRestock = input(false);
  readonly warehouses = input<SelectOption[]>([]);
  readonly saving = input(false);
  readonly error = input<string | null>(null);
  readonly submitted = output<ReturnFormValue>();
  readonly closed = output<void>();

  date: Date = new Date();
  reason = '';
  notes = '';
  warehouseId: string | null = null;
  readonly quantities = signal<number[]>([]);
  readonly restocks = signal<boolean[]>([]);
  readonly valid = computed(() => this.reason.trim().length > 0 && this.quantities().some((q) => q > 0) && !this.saving());

  constructor() {
    // A fresh form each time it opens for a document.
    effect(() => {
      const isOpen = this.open();
      const lines = this.lines();
      untracked(() => {
        if (!isOpen) return;
        this.date = new Date();
        this.reason = '';
        this.notes = '';
        this.warehouseId = null;
        this.quantities.set(lines.map(() => 0));
        this.restocks.set(lines.map(() => true));
      });
    });
  }

  setQuantity(i: number, v: number | null): void { this.quantities.update((q) => q.map((x, n) => (n === i ? v ?? 0 : x))); }
  setRestock(i: number, v: boolean): void { this.restocks.update((r) => r.map((x, n) => (n === i ? v : x))); }

  submit(): void {
    if (!this.valid()) return;
    this.submitted.emit({
      returnDate: toIsoDate(this.date), reason: this.reason.trim(), notes: this.notes.trim() || null, warehouseId: this.warehouseId,
      lines: this.lines().map((l, i) => ({ key: l.key, quantity: this.quantities()[i] ?? 0, restock: this.restocks()[i] ?? true })).filter((l) => l.quantity > 0),
    });
  }
}
