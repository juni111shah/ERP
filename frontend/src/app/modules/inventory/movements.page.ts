import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { MovementRow, MovementsApi } from './inventory.api';

@Component({
  selector: 'erp-movements',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent],
  template: `<h1 class="erp-page-title">Stock movements</h1><erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search reference or memo" emptyText="No stock movements yet" [clickable]="false" />`,
})
export class MovementsPage {
  private readonly api = inject(MovementsApi);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-date' });
  readonly columns: TableColumn<MovementRow>[] = [
    { key: 'date', label: 'Date', sortField: 'date', width: '120px' },
    { key: 'kind', label: 'Kind', value: (m) => m.kind.replace(/([A-Z])/g, ' $1').trim(), width: '120px' },
    { key: 'quantity', label: 'Quantity', value: (m) => (m.quantity > 0 ? '+' : '') + m.quantity, width: '110px' },
    { key: 'unitCost', label: 'Unit cost', value: (m) => m.unitCost.toLocaleString(), width: '110px' },
    { key: 'reference', label: 'Reference' }, { key: 'memo', label: 'Memo' },
  ];
}
