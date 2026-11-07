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
import { toIsoDate } from '../core/custom-fields/custom-field-form';
import { SelectOption } from './record-drawer.component';

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
  styles: [`
    .head { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-bottom: 12px; }
    .field { display: flex; flex-direction: column; gap: 4px; font-size: 12px; color: var(--erp-color-text-muted); }
    table { width: 100%; border-collapse: collapse; font-size: 13px; margin-top: 12px; }
    th { background: var(--erp-color-bg-subtle); padding: 8px; border: 1px solid var(--erp-color-border-subtle); text-align: start; }
    td { padding: 6px; border: 1px solid var(--erp-color-border-subtle); vertical-align: middle; }
    .num { text-align: end; font-variant-numeric: tabular-nums; }
    .hint { font-size: 12px; color: var(--erp-color-text-muted); margin-top: 6px; }
  `],
  template: `
    <nz-drawer [nzVisible]="open()" [nzTitle]="title()" [nzWidth]="720" (nzOnClose)="closed.emit()" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        @if (error()) { <nz-alert nzType="error" nzShowIcon [nzMessage]="error()" style="margin-bottom:12px" data-testid="return-error"></nz-alert> }
        <div class="head">
          <div class="field">Return date <nz-date-picker [(ngModel)]="date" [nzAllowClear]="false" data-testid="return-date" /></div>
          @if (warehouses().length) {
            <div class="field">Warehouse the goods leave
              <nz-select [(ngModel)]="warehouseId" nzAllowClear nzPlaceHolder="Where the order was received" data-testid="return-warehouse">
                @for (w of warehouses(); track w.value) { <nz-option [nzValue]="w.value" [nzLabel]="w.label" /> }
              </nz-select>
            </div>
          }
        </div>
        <div class="field">Reason <input nz-input [(ngModel)]="reason" maxlength="500" data-testid="return-reason" /></div>
        <div class="field" style="margin-top:12px">Notes <textarea nz-input rows="2" [(ngModel)]="notes" maxlength="1000"></textarea></div>
        <table data-testid="return-lines">
          <thead><tr><th>Item</th><th class="num">Can come back</th><th>Quantity</th>@if (showRestock()) { <th>Back on the shelf</th> }</tr></thead>
          <tbody>
            @for (l of lines(); track l.key; let i = $index) {
              <tr>
                <td>{{ l.label }}</td>
                <td class="num">{{ l.max | number: '1.0-4' }} {{ l.unit }}</td>
                <td><nz-input-number [ngModel]="quantities()[i]" (ngModelChange)="setQuantity(i, $event)" [nzMin]="0" [nzMax]="l.max" [nzPrecision]="2" style="width:110px" [attr.aria-label]="'Quantity to return, ' + l.label" /></td>
                @if (showRestock()) { <td><nz-switch [ngModel]="restocks()[i]" (ngModelChange)="setRestock(i, $event)" [attr.aria-label]="'Restock ' + l.label" /></td> }
              </tr>
            }
          </tbody>
        </table>
        @if (showRestock()) { <p class="hint">Goods that are not restocked (damaged, for example) stay at cost in the books and are not added back to the shelf.</p> }
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button type="button" (click)="closed.emit()">Cancel</button>
      <button nz-button nzType="primary" type="button" [nzLoading]="saving()" [disabled]="!valid()" (click)="submit()" style="margin-inline-start:8px" data-testid="return-save">Create return</button>
    </ng-template>`,
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
