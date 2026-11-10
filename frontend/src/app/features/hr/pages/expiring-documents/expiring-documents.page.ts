import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { DOCUMENT_TYPES, EmployeeDocumentsApi, ExpiringDocumentDto } from '@core/api/hr-people.api';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** Passports, visas, residence IDs, labour cards and other documents that are expired or about to expire. */
@Component({
  selector: 'erp-expiring-documents',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, FormsModule, NzSelectModule, NzTableModule, StatePanelComponent, StatusTagComponent],
  template: `
    <h1 class="erp-page-title">Document expiry</h1>
    <div style="display:flex;gap:12px;align-items:center;margin:12px 0">
      <label for="expiry-days">Expiring within</label>
      <nz-select nzId="expiry-days" style="width:140px" [ngModel]="days()" (ngModelChange)="setDays($event)" data-testid="expiry-days">
        @for (d of dayOptions; track d) { <nz-option [nzValue]="d" [nzLabel]="d + ' days'"></nz-option> }
      </nz-select>
      <span style="color:var(--erp-color-text-secondary)">{{ rows().length }} document(s)</span>
    </div>
    @if (failed()) {
      <erp-state-panel kind="error" title="Documents could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else {
    <nz-table [nzData]="rows()" [nzLoading]="loading()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" nzNoResult="Nothing expires in this period" data-testid="expiry-table">
      <thead><tr><th>Employee</th><th>Document</th><th>Number</th><th>Expires</th><th>Status</th></tr></thead>
      <tbody>
        @for (r of rows(); track r.id) {
          <tr>
            <td>{{ r.employeeName }}<div style="font-size:12px;color:var(--erp-color-text-secondary)">{{ r.employeeNumber }}</div></td>
            <td>{{ typeLabel(r.type) }}</td>
            <td>{{ r.number }}</td>
            <td>{{ r.expiryDate | erpDate }}</td>
            <td>
              @if (r.isExpired) { <erp-status value="expired" [label]="'Expired ' + -r.daysLeft + ' days ago'" /> }
              @else if (r.daysLeft <= 30) { <erp-status value="expiringSoon" [label]="r.daysLeft + ' days left'" /> }
              @else { <erp-status value="open" [label]="r.daysLeft + ' days left'" /> }
            </td>
          </tr>
        }
      </tbody>
    </nz-table>
    }`,
})
export class ExpiringDocumentsPage {
  private readonly api = inject(EmployeeDocumentsApi);
  readonly dayOptions = [30, 60, 90, 180, 365];
  readonly days = signal(60);
  readonly rows = signal<ExpiringDocumentDto[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);
  constructor() { this.load(); }
  setDays(d: number): void { this.days.set(d); this.load(); }
  typeLabel(t: string): string { return DOCUMENT_TYPES.find((x) => x.value === t)?.label ?? t; }
  load(): void {
    this.loading.set(true); this.failed.set(false);
    this.api.expiring(this.days()).subscribe({ next: (r) => { this.rows.set(r); this.loading.set(false); }, error: () => { this.loading.set(false); this.failed.set(true); } });
  }
}
