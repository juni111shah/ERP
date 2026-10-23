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
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { createPagedList } from '../../core/api/paged-list';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { CONDITIONS, INSPECTION_KINDS, InspectionDetail, InspectionItem, InspectionRow, InspectionsApi, RealEstateLookups, humanize } from './realestate.api';
import { StatusTagComponent } from './status-tag.component';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { Observable } from 'rxjs';

/** Move-in / move-out / routine inspections with an area checklist; completing can raise repair work orders. */
@Component({
  selector: 'erp-inspections',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, PagedTableComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzCheckboxModule, NzDatePickerModule, NzDrawerModule, NzIconModule, NzInputModule, NzInputNumberModule, NzSelectModule],
  template: `
    <h1 class="erp-page-title">Inspections</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search inspector" emptyText="No inspections yet" (rowClick)="open($event.id)">
      <button toolbar *erpHasPermission="'realestate.inspections.manage'" nz-button nzType="primary" (click)="create()" data-testid="insp-new"><span nz-icon nzType="plus"></span>New inspection</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <erp-status-tag [status]="row.status" /> } @else { {{ value }} }
      </ng-template>
    </erp-paged-table>
    <nz-drawer [nzVisible]="drawer()" [nzTitle]="id() ? 'Inspection' : 'New inspection'" nzWidth="760" (nzOnClose)="drawer.set(false)">
      <ng-container *nzDrawerContent>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
          <nz-select nzPlaceHolder="Property" nzShowSearch nzServerSearch [(ngModel)]="propertyId" (nzOnSearch)="searchProperty($event)" [nzDisabled]="locked()" data-testid="insp-property">@for (o of propertyOptions(); track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }</nz-select>
          <nz-select nzPlaceHolder="Unit (optional)" nzShowSearch nzServerSearch nzAllowClear [(ngModel)]="unitId" (nzOnSearch)="searchUnit($event)" [nzDisabled]="locked()" data-testid="insp-unit">@for (o of unitOptions(); track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }</nz-select>
          <nz-select [(ngModel)]="kind" [nzDisabled]="locked()" data-testid="insp-kind">@for (k of kinds; track k.value) { <nz-option [nzValue]="k.value" [nzLabel]="k.label"></nz-option> }</nz-select>
          <nz-date-picker [(ngModel)]="date" [nzDisabled]="locked()" data-testid="insp-date"></nz-date-picker>
          <input nz-input placeholder="Inspector" [(ngModel)]="inspector" [disabled]="locked()" data-testid="insp-inspector" />
        </div>
        <h4 style="margin-top:16px">Checklist</h4>
        @for (it of items(); track $index; let i = $index) {
          <div style="display:grid;grid-template-columns:1.4fr 1fr 1.4fr 110px 32px;gap:8px;margin-bottom:8px;align-items:center">
            <input nz-input placeholder="Area" [ngModel]="it.area" (ngModelChange)="patch(i, { area: $event })" [disabled]="locked()" [attr.data-testid]="'insp-area-' + i" />
            <nz-select [ngModel]="it.condition" (ngModelChange)="patch(i, { condition: $event })" [nzDisabled]="locked()">@for (c of conditions; track c.value) { <nz-option [nzValue]="c.value" [nzLabel]="c.label"></nz-option> }</nz-select>
            <input nz-input placeholder="Notes" [ngModel]="it.notes" (ngModelChange)="patch(i, { notes: $event })" [disabled]="locked()" />
            <nz-input-number [ngModel]="it.repairCost" (ngModelChange)="patch(i, { repairCost: $event ?? 0 })" [nzMin]="0" [nzDisabled]="locked()"></nz-input-number>
            @if (!locked()) { <button nz-button nzType="text" nzSize="small" (click)="removeItem(i)"><span nz-icon nzType="delete"></span></button> }
          </div>
        }
        @if (!locked()) { <button nz-button nzSize="small" (click)="addItem()" data-testid="insp-add-item"><span nz-icon nzType="plus"></span>Add area</button> }
        @if (damage() > 0) { <p style="margin-top:12px" data-testid="insp-damage"><b>Estimated damage cost: {{ damage() }}</b></p> }
      </ng-container>
      <div *nzDrawerFooter style="display:flex;gap:8px;align-items:center">
        @if (!locked()) {
          <label nz-checkbox [(ngModel)]="raise">Raise repair work orders on completion</label>
          <span style="flex:1"></span>
          <button *erpHasPermission="'realestate.inspections.manage'" nz-button (click)="save()" [nzLoading]="saving()" data-testid="insp-save">Save</button>
          @if (id()) { <button *erpHasPermission="'realestate.inspections.manage'" nz-button nzType="primary" (click)="complete()" data-testid="insp-complete">Complete</button> }
        }
      </div>
    </nz-drawer>`,
})
export class InspectionsPage {
  private readonly api = inject(InspectionsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly kinds = INSPECTION_KINDS;
  readonly conditions = CONDITIONS;
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-inspectionDate' });
  readonly columns: TableColumn<InspectionRow>[] = [
    { key: 'inspectionDate', label: 'Date', sortField: 'inspectionDate', width: '120px' },
    { key: 'kind', label: 'Kind', value: (r) => humanize(r.kind), width: '110px' },
    { key: 'inspector', label: 'Inspector' },
    { key: 'damageCost', label: 'Damage cost', value: (r) => r.damageCost.toLocaleString(), width: '130px' },
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

  damage(): number { return this.items().filter((i) => i.condition === 'poor' || i.condition === 'damaged').reduce((a, i) => a + (i.repairCost ?? 0), 0); }

  searchProperty(term: string): void { this.lookups.property(term).subscribe((o) => this.propertyOptions.set(o)); }
  searchUnit(term: string): void { this.lookups.unit(term).subscribe((o) => this.unitOptions.set(o)); }

  create(): void {
    this.id.set(null); this.locked.set(false); this.propertyId = null; this.unitId = null; this.kind = 'routine'; this.date = new Date(); this.inspector = '';
    this.items.set([{ area: '', condition: 'good', repairCost: 0 }]);
    this.searchProperty(''); this.searchUnit('');
    this.drawer.set(true);
  }

  open(id: string): void {
    this.api.detail(id).subscribe((d: InspectionDetail) => {
      this.id.set(d.id); this.locked.set(d.status === 'completed'); this.propertyId = d.propertyId; this.unitId = d.unitId ?? null; this.kind = d.kind; this.inspector = d.inspector ?? '';
      const [y, m, day] = d.inspectionDate.split('-').map(Number); this.date = new Date(y, m - 1, day);
      this.propertyOptions.set([{ value: d.propertyId, label: d.propertyName }]);
      this.unitOptions.set(d.unitId ? [{ value: d.unitId, label: `Unit ${d.unitNumber}` }] : []);
      this.items.set(d.items.map((i) => ({ ...i })));
      this.drawer.set(true);
    });
  }

  addItem(): void { this.items.update((l) => [...l, { area: '', condition: 'good', repairCost: 0 }]); }
  removeItem(i: number): void { this.items.update((l) => l.filter((_, idx) => idx !== i)); }
  patch(i: number, change: Partial<InspectionItem>): void { this.items.update((l) => l.map((it, idx) => (idx === i ? { ...it, ...change } : it))); }

  private body(): Record<string, unknown> | null {
    if (!this.propertyId || !this.date) { this.message.warning('Choose a property and a date'); return null; }
    const items = this.items().filter((i) => i.area.trim()).map((i) => ({ area: i.area, condition: i.condition, notes: i.notes, repairCost: i.repairCost }));
    return { propertyId: this.propertyId, unitId: this.unitId, kind: this.kind, inspectionDate: toIsoDate(this.date), inspector: this.inspector || null, items };
  }

  private send(then: (id: string) => void): void {
    const body = this.body();
    if (!body) return;
    this.saving.set(true);
    const id = this.id();
    const call: Observable<unknown> = id ? this.api.update(id, body) : this.api.create(body);
    call.subscribe({
      next: (r) => { this.saving.set(false); const newId = (r as { id: string }).id; this.id.set(newId); then(newId); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not save'); },
    });
  }

  save(): void { this.send(() => { this.message.success('Inspection saved'); this.list.reload(); }); }

  complete(): void {
    this.send((id) => this.api.complete(id, this.raise).subscribe({
      next: () => { this.message.success('Inspection completed'); this.drawer.set(false); this.list.reload(); },
      error: (e) => { const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not complete'); },
    }));
  }
}
