import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ProblemDetails } from '../../core/api/api.models';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { DOCUMENT_TYPES, EmployeeDocumentDto, EmployeeDocumentRow, EmployeeDocumentsApi } from './hr-people.api';
import { ErpDatePipe } from '../../shared/display.pipes';

/** The identity and work documents of one employee (passport, visa, labour card ...) with expiry badges. */
@Component({
  selector: 'erp-employee-documents',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzDrawerModule, NzIconModule, NzModalModule, NzTableModule, NzTagModule],
  template: `
    <nz-drawer [nzVisible]="open()" [nzWidth]="640" [nzTitle]="'Documents · ' + employeeName()" (nzOnClose)="closed.emit()">
      <ng-container *nzDrawerContent>
        <button *erpHasPermission="'hr.employees.manage'" nz-button nzType="primary" (click)="startAdd()" data-testid="doc-new"><span nz-icon nzType="plus"></span>Add document</button>
        <nz-table [nzData]="rows()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" nzNoResult="No documents recorded" style="margin-top:12px" data-testid="doc-table">
          <thead><tr><th>Type</th><th>Number</th><th>Expires</th><th>Status</th><th></th></tr></thead>
          <tbody>
            @for (d of rows(); track d.id) {
              <tr>
                <td>{{ typeLabel(d.type) }}</td><td>{{ d.number }}</td><td>{{ d.expiryDate | erpDate }}</td>
                <td>@if (d.expiryDate) { @if (daysLeft(d) < 0) { <nz-tag nzColor="red">Expired</nz-tag> } @else if (daysLeft(d) <= 30) { <nz-tag nzColor="orange">{{ daysLeft(d) }} days left</nz-tag> } @else { <nz-tag nzColor="green">Valid</nz-tag> } }</td>
                <td>
                  <a *erpHasPermission="'hr.employees.manage'" (click)="startEdit(d)" style="margin-inline-end:10px">Edit</a>
                  <a *erpHasPermission="'hr.employees.manage'" (click)="remove(d)">Delete</a>
                </td>
              </tr>
            }
          </tbody>
        </nz-table>
      </ng-container>
    </nz-drawer>
    <erp-record-drawer [title]="editing() ? 'Edit document' : 'Add document'" [open]="form()" [fields]="fields" [value]="$any(editing())" [saving]="saving()" [errors]="errors()" [width]="440"
                       testId="doc-form" (submitted)="save($event)" (closed)="form.set(false)" />`,
})
export class EmployeeDocumentsComponent {
  private readonly api = inject(EmployeeDocumentsApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly employeeId = input.required<string>();
  readonly employeeName = input('');
  readonly open = input(false);
  readonly closed = output<void>();

  readonly rows = signal<EmployeeDocumentRow[]>([]);
  readonly form = signal(false);
  readonly editing = signal<EmployeeDocumentDto | null>(null);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly fields: FieldSpec[] = [
    { key: 'type', label: 'Type', type: 'select', required: true, options: DOCUMENT_TYPES, default: 'passport' },
    { key: 'number', label: 'Number', type: 'text', maxLength: 100 },
    { key: 'issueDate', label: 'Issue date', type: 'date' },
    { key: 'expiryDate', label: 'Expiry date', type: 'date' },
    { key: 'issuedBy', label: 'Issued by', type: 'text', maxLength: 150 },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 1000 },
  ];

  constructor() { effect(() => { if (this.open()) this.load(); }); }

  typeLabel(t: string): string { return DOCUMENT_TYPES.find((x) => x.value === t)?.label ?? t; }
  daysLeft(d: EmployeeDocumentRow): number { return Math.floor((new Date(d.expiryDate!).getTime() - new Date(new Date().toDateString()).getTime()) / 86_400_000); }

  startAdd(): void { this.editing.set(null); this.errors.set(null); this.form.set(true); }
  startEdit(d: EmployeeDocumentRow): void { this.api.detail(d.id).subscribe((full) => { this.editing.set(full); this.errors.set(null); this.form.set(true); }); }

  save(v: Record<string, unknown>): void {
    this.saving.set(true); this.errors.set(null);
    const body = { ...v, employeeId: this.employeeId(), issueDate: this.day(v['issueDate']), expiryDate: this.day(v['expiryDate']) };
    const id = this.editing()?.id;
    (id ? this.api.update(id, body) : this.api.create(body)).subscribe({
      next: () => { this.saving.set(false); this.form.set(false); this.message.success('Document saved'); this.load(); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not save the document'); },
    });
  }

  remove(d: EmployeeDocumentRow): void {
    this.modal.confirm({
      nzTitle: 'Delete this document?', nzOkDanger: true, nzOkText: 'Delete',
      nzOnOk: () => new Promise<void>((resolve) => this.api.delete(d.id).subscribe({ next: () => { this.load(); resolve(); }, error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not delete'); resolve(); } })),
    });
  }

  private day(v: unknown): string | null { return v ? String(v).slice(0, 10) : null; }
  private load(): void { this.api.list({ page: 1, pageSize: 100, sort: 'expiryDate', filters: [{ field: 'employeeId', op: 'eq', value: this.employeeId() }] }).subscribe((p) => this.rows.set(p.items)); }
}
