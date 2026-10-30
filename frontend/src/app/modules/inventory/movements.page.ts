import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzInputModule } from 'ng-zorro-antd/input';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { MovementRow, MovementsApi } from './inventory.api';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';

@Component({
  selector: 'erp-movements',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NzInputModule, DatePipe, DecimalPipe, RouterModule, NzButtonModule, NzIconModule, NzTagModule, PagedTableComponent, TranslatePipe],
  styles: [`
    .qty-badge {
      display: inline-flex;
      align-items: center;
      gap: 3px;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 6px;
      font-size: 13px;
    }
    .qty-in {
      background: #ecfdf5;
      color: #059669;
    }
    .qty-out {
      background: #fff1f2;
      color: #e11d48;
    }
    .qty-neutral {
      background: #f1f5f9;
      color: #475569;
    }
    .ref-pill {
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
      font-size: 12px;
      font-weight: 600;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      padding: 2px 7px;
      border-radius: 4px;
      color: #334155;
    }
  `],
  template: `
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
      <div>
        <h1 class="erp-page-title" style="margin:0;">{{ 'menu.inventory.movements' | translate: 'Stock movements' }}</h1>
        <p style="color:#64748b; margin:4px 0 0 0; font-size:13px;">Complete audit trail of all warehouse receipts, issues, transfers, and stock adjustments.</p>
      </div>
      <div style="display:flex; gap:8px;">
        <a routerLink="/inventory/stock" nz-button nzType="default">
          <span nz-icon nzType="appstore"></span> {{ 'menu.inventory.stock' | translate: 'Stock on hand' }}
        </a>
        <a routerLink="/inventory/warehouses" nz-button nzType="default">
          <span nz-icon nzType="home"></span> {{ 'menu.inventory.warehouses' | translate: 'Warehouses' }}
        </a>
      </div>
    </div>

    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search reference, memo, or warehouse..." emptyText="No stock movements yet" [clickable]="false">
      <input aria-label="Exact reference (e.g. CNT-00001)" toolbar nz-input style="width:200px" placeholder="Exact reference (e.g. CNT-00001)" [ngModel]="reference" (ngModelChange)="filterReference($event)" data-testid="movement-reference" />
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('date') {
            <span>{{ row.date | date:'dd MMM yyyy' }}</span>
          }
          @case ('kind') {
            <nz-tag [nzColor]="kindColor(row.kind)">
              {{ kindLabel(row.kind) }}
            </nz-tag>
          }
          @case ('quantity') {
            <span class="qty-badge" [ngClass]="row.quantity > 0 ? 'qty-in' : row.quantity < 0 ? 'qty-out' : 'qty-neutral'">
              <span nz-icon [nzType]="row.quantity > 0 ? 'arrow-down' : row.quantity < 0 ? 'arrow-up' : 'minus'"></span>
              {{ row.quantity > 0 ? '+' : '' }}{{ row.quantity | number:'1.0-4' }}
            </span>
          }
          @case ('unitCost') {
            <span style="font-weight:600; color:#0f172a;">{{ currency() }} {{ row.unitCost | number:'1.2-2' }}</span>
          }
          @case ('reference') {
            @if (row.reference) {
              <span class="ref-pill">{{ row.reference }}</span>
            } @else {
              <span style="color:#94a3b8;">—</span>
            }
          }
          @case ('journal') {
            @if (row.journalEntryId) {
              <a [routerLink]="['/finance/journals', row.journalEntryId]" nz-button nzType="link" nzSize="small">
                <span nz-icon nzType="book"></span> Journal
              </a>
            } @else {
              <span style="color:#94a3b8;">—</span>
            }
          }
          @default {
            {{ value || '—' }}
          }
        }
      </ng-template>
    </erp-paged-table>
  `,
})
export class MovementsPage {
  reference = '';
  filterReference(value: string): void {
    this.reference = value;
    this.list.setFilters(value.trim() ? [{ field: 'reference', op: 'eq', value: value.trim() }] : []);
  }

  private readonly api = inject(MovementsApi);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-date' });

  readonly columns: TableColumn<MovementRow>[] = [
    { key: 'date', label: 'Date', sortField: 'date', width: '130px' },
    { key: 'kind', label: 'Movement Kind', sortField: 'kind', width: '150px' },
    { key: 'quantity', label: 'Quantity', sortField: 'quantity', width: '130px' },
    { key: 'unitCost', label: 'Unit Cost', width: '120px' },
    { key: 'reference', label: 'Reference', sortField: 'reference', width: '160px' },
    { key: 'memo', label: 'Notes / Memo' },
    { key: 'journal', label: 'Ledger Entry', width: '110px' },
  ];

  kindColor(kind: string): string {
    const k = (kind || '').toLowerCase();
    if (k.includes('receipt') || k.includes('in') || k.includes('purchase')) return 'green';
    if (k.includes('issue') || k.includes('out') || k.includes('sale')) return 'orange';
    if (k.includes('transfer')) return 'purple';
    if (k.includes('adjust')) return 'blue';
    if (k.includes('scrap')) return 'red';
    return 'default';
  }

  kindLabel(kind: string): string {
    return (kind || '').replace(/([A-Z])/g, ' $1').trim();
  }
}
