import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { DOCUMENT_TYPES, EmployeeDocumentsApi, ExpiringDocumentDto } from './hr-people.api';

/** Passports, visas, residence IDs, labour cards and other documents that are expired or about to expire. */
@Component({
  selector: 'erp-expiring-documents',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzSelectModule, NzTableModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">Document expiry</h1>
    <div style="display:flex;gap:12px;align-items:center;margin:12px 0">
      <span>Expiring within</span>
      <nz-select style="width:140px" [ngModel]="days()" (ngModelChange)="setDays($event)" data-testid="expiry-days">
        @for (d of dayOptions; track d) { <nz-option [nzValue]="d" [nzLabel]="d + ' days'"></nz-option> }
      </nz-select>
      <span style="color:var(--erp-color-text-secondary)">{{ rows().length }} document(s)</span>
    </div>
    <nz-table [nzData]="rows()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="expiry-table">
      <thead><tr><th>Employee</th><th>Document</th><th>Number</th><th>Expires</th><th>Status</th></tr></thead>
      <tbody>
        @for (r of rows(); track r.id) {
          <tr>
            <td>{{ r.employeeName }}<div style="font-size:12px;color:var(--erp-color-text-secondary)">{{ r.employeeNumber }}</div></td>
            <td>{{ typeLabel(r.type) }}</td>
            <td>{{ r.number }}</td>
            <td>{{ r.expiryDate }}</td>
            <td>
              @if (r.isExpired) { <nz-tag nzColor="red">Expired {{ -r.daysLeft }} days ago</nz-tag> }
              @else if (r.daysLeft <= 30) { <nz-tag nzColor="orange">{{ r.daysLeft }} days left</nz-tag> }
              @else { <nz-tag nzColor="blue">{{ r.daysLeft }} days left</nz-tag> }
            </td>
          </tr>
        } @empty { <tr><td colspan="5" style="text-align:center;color:var(--erp-color-text-secondary)">Nothing expires in this period</td></tr> }
      </tbody>
    </nz-table>`,
})
export class ExpiringDocumentsPage {
  private readonly api = inject(EmployeeDocumentsApi);
  readonly dayOptions = [30, 60, 90, 180, 365];
  readonly days = signal(60);
  readonly rows = signal<ExpiringDocumentDto[]>([]);
  constructor() { this.load(); }
  setDays(d: number): void { this.days.set(d); this.load(); }
  typeLabel(t: string): string { return DOCUMENT_TYPES.find((x) => x.value === t)?.label ?? t; }
  private load(): void { this.api.expiring(this.days()).subscribe((r) => this.rows.set(r)); }
}
