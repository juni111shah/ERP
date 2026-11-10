import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { Observable } from 'rxjs';
import { createPagedList } from '@core/api/paged-list';
import { CONDITIONS, INSPECTION_KINDS, InspectionDetail, InspectionInput, InspectionItem, InspectionRow, InspectionsApi, RealEstateLookups, humanize } from '@core/api/realestate.api';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { toIsoDate } from '@core/format/dates';
import { doneText } from '@core/format/done-text';
import { fromIsoDate } from '@core/format/from-iso-date';
import { asProblem } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';

/** A blank checklist line. */
const newItem = (): InspectionItem => ({ area: '', condition: 'good', repairCost: 0 });

/** Move-in / move-out / routine inspections with an area checklist; completing can raise repair work orders. */
@Component({
  selector: 'erp-inspections',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzToolTipModule, TranslatePipe, FormsModule, PagedTableComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzCheckboxModule, NzDatePickerModule, NzDrawerModule, NzIconModule, NzInputModule, NzInputNumberModule, NzSelectModule],
  templateUrl: './inspections.page.html',
})
export class InspectionsPage {
  private readonly i18n = inject(I18nService);
  private readonly inspectionsApi = inject(InspectionsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly kinds = INSPECTION_KINDS;
  readonly conditions = CONDITIONS;
  readonly list = createPagedList((q) => this.inspectionsApi.list(q), { sort: '-inspectionDate' });
  readonly columns: TableColumn<InspectionRow>[] = [
    { key: 'inspectionDate', label: 'Date', sortField: 'inspectionDate', width: '120px' },
    { key: 'kind', label: 'Kind', value: (r) => humanize(r.kind), width: '110px' },
    { key: 'inspector', label: 'Inspector' },
    { key: 'damageCost', label: 'Damage cost', align: 'right', value: (r) => r.damageCost.toLocaleString(), width: '130px' },
    { key: 'status', label: 'Status', width: '110px' },
  ];
  readonly drawer = signal(false);
  readonly id = signal<string | null>(null);
  readonly locked = signal(false);
  readonly saving = signal(false);
  readonly items = signal<InspectionItem[]>([]);
  readonly propertyOptions = signal<LookupOption[]>([]);
  readonly unitOptions = signal<LookupOption[]>([]);
  propertyId: string | null = null;
  unitId: string | null = null;
  kind = 'routine';
  date: Date | null = new Date();
  inspector = '';
  raise = true;

  /** The repair cost of the areas found poor or damaged. */
  damage(): number { return this.items().filter((i) => i.condition === 'poor' || i.condition === 'damaged').reduce((a, i) => a + (i.repairCost ?? 0), 0); }

  searchProperty(term: string): void { this.lookups.property(term).subscribe((o) => this.propertyOptions.set(o)); }
  searchUnit(term: string): void { this.lookups.unit(term).subscribe((o) => this.unitOptions.set(o)); }

  create(): void {
    this.id.set(null); this.locked.set(false); this.propertyId = null; this.unitId = null; this.kind = 'routine'; this.date = new Date(); this.inspector = '';
    this.items.set([newItem()]);
    this.searchProperty(''); this.searchUnit('');
    this.drawer.set(true);
  }

  open(id: string): void {
    this.inspectionsApi.detail(id).subscribe((d: InspectionDetail) => {
      this.id.set(d.id); this.locked.set(d.status === 'completed'); this.propertyId = d.propertyId; this.unitId = d.unitId ?? null; this.kind = d.kind; this.inspector = d.inspector ?? '';
      this.date = fromIsoDate(d.inspectionDate);
      this.propertyOptions.set([{ value: d.propertyId, label: d.propertyName }]);
      this.unitOptions.set(d.unitId ? [{ value: d.unitId, label: `${this.i18n.t('ui.re.unit', 'Unit')} ${d.unitNumber}` }] : []);
      this.items.set(d.items.map((i) => ({ ...i })));
      this.drawer.set(true);
    });
  }

  addItem(): void { this.items.update((l) => [...l, newItem()]); }
  removeItem(i: number): void { this.items.update((l) => l.filter((_, idx) => idx !== i)); }
  patch(i: number, change: Partial<InspectionItem>): void { this.items.update((l) => l.map((it, idx) => (idx === i ? { ...it, ...change } : it))); }

  save(): void { this.send(() => { this.message.success(doneText('saved', 'Inspection', this.propertyLabel())); this.list.reload(); }); }

  complete(): void {
    this.send((id) => this.inspectionsApi.complete(id, this.raise).subscribe({
      next: () => { this.message.success(doneText('completed', 'Inspection', this.propertyLabel())); this.drawer.set(false); this.list.reload(); },
      error: (e) => { const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not complete'); },
    }));
  }

  private propertyLabel(): string | undefined { return this.propertyOptions().find((o) => o.value === this.propertyId)?.label; }

  /** The inspection as saved, or null (with a warning) while the property or date is missing. Blank areas are left out. */
  private body(): InspectionInput | null {
    if (!this.propertyId || !this.date) { this.message.warning('Choose a property and a date'); return null; }
    const items = this.items().filter((i) => i.area.trim()).map((i) => ({ area: i.area, condition: i.condition, notes: i.notes, repairCost: i.repairCost }));
    return { propertyId: this.propertyId, unitId: this.unitId, kind: this.kind, inspectionDate: toIsoDate(this.date), inspector: this.inspector || null, items };
  }

  /** Saves the inspection (create or update), then runs `then` with its id. */
  private send(then: (id: string) => void): void {
    const body = this.body();
    if (!body) return;
    this.saving.set(true);
    const id = this.id();
    const call: Observable<InspectionRow> = id ? this.inspectionsApi.update(id, body) : this.inspectionsApi.create(body);
    call.subscribe({
      next: (r) => { this.saving.set(false); this.id.set(r.id); then(r.id); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not save'); },
    });
  }
}
