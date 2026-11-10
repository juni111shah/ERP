import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { IndustryApi, IndustryRecordDto, PackView, RecordTypeView, statusLabel, toneColor } from '@core/api/industry.api';
import { PagedList } from '@core/api/paged-list';
import { displayDate } from '@core/format/display';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { IndustryRecordFormComponent } from '../../components/industry-record-form/industry-record-form.component';

/** The list of one record type of one module. Every module and type uses this page; the definition decides the columns. */
@Component({
  selector: 'erp-industry-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzAlertModule, NzButtonModule, NzIconModule, NzSelectModule, NzSkeletonModule, NzTagModule, HasPermissionDirective,
    PagedTableComponent, StatePanelComponent, IndustryRecordFormComponent],
  styleUrl: './industry-list.page.scss',
  templateUrl: './industry-list.page.html',
})
export class IndustryListPage {
  private readonly api = inject(IndustryApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly currency = inject(WorkspaceCurrencyService);

  readonly module = signal(this.route.snapshot.paramMap.get('module') ?? '');
  readonly typeKey = signal(this.route.snapshot.paramMap.get('type') ?? '');
  readonly pack = signal<PackView | null>(null);
  readonly failed = signal(false);
  readonly status = signal<string | null>(null);
  readonly creating = signal(false);
  readonly type = computed<RecordTypeView | null>(() => this.pack()?.types.find((t) => t.key === this.typeKey()) ?? null);

  readonly list = new PagedList<IndustryRecordDto>((q) => this.api.list(this.module(), this.typeKey(), q), inject(DestroyRef));

  readonly columns = computed<TableColumn<IndustryRecordDto>[]>(() => {
    const t = this.type();
    if (!t) return [];
    const columns: TableColumn<IndustryRecordDto>[] = [
      { key: 'number', label: 'Number', sortField: 'number', width: '140px' },
      { key: 'title', label: t.titleLabel || 'Name', sortField: 'title' },
    ];
    if (t.partyRole) columns.push({ key: 'partyName', label: t.partyLabel || 'Party' });
    if (t.parentType) columns.push({ key: 'parentTitle', label: t.parentLabel || 'Belongs to' });
    for (const f of t.fields.filter((x) => x.inList)) {
      columns.push({
        key: 'data.' + f.key, label: f.label, filter: false,
        value: (r) => this.dataValue(f.type, r.data?.[f.key]),
      });
    }
    columns.push({ key: 'status', label: 'Status', sortField: 'status', filter: false });
    columns.push({ key: 'recordDate', label: 'Date', sortField: 'recordDate', value: (r) => displayDate(r.recordDate), filter: false });
    if (t.hasAmount || t.hasLines) columns.push({ key: 'amount', label: `Amount (${this.currency.currency()})`, sortField: 'amount', align: 'right', filter: false });
    return columns;
  });

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((p) => {
      const module = p.get('module') ?? '', type = p.get('type') ?? '';
      const changed = module !== this.module() || type !== this.typeKey();
      this.module.set(module);
      this.typeKey.set(type);
      if (changed) { this.status.set(null); this.creating.set(false); this.list.setFilters([]); this.list.setSearch(''); }
      this.load();
    });
  }

  load(): void {
    this.failed.set(false);
    this.api.definitions(this.module()).subscribe({
      next: (pack) => {
        this.pack.set(pack);
        // A type the person may not read is not in the definition.
        if (!pack.types.some((t) => t.key === this.typeKey())) this.failed.set(true);
      },
      error: () => this.failed.set(true),
    });
  }

  tone(status: string): string { return toneColor(this.type()?.statuses.find((s) => s.key === status)?.tone); }
  statusText(status: string): string { return statusLabel(this.type(), status); }

  filterStatus(value: string | null): void {
    this.status.set(value);
    this.list.setFilters(value ? [{ field: 'status', op: 'eq', value }] : []);
  }

  open(row: IndustryRecordDto): void { void this.router.navigate(['/industry', this.module(), this.typeKey(), row.id]); }

  created(record: IndustryRecordDto): void {
    this.creating.set(false);
    void this.router.navigate(['/industry', this.module(), this.typeKey(), record.id]);
  }

  private dataValue(type: string, v: unknown): string {
    if (v === null || v === undefined || v === '') return '';
    if (type === 'bool') return v ? 'Yes' : 'No';
    if (type === 'date') return displayDate(v);
    return String(v);
  }
}
