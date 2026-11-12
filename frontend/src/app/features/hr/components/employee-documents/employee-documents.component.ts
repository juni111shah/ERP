import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ProblemDetails } from '@core/api/api.models';
import { DOCUMENT_TYPES, EmployeeDocumentDto, EmployeeDocumentRow, EmployeeDocumentsApi } from '@core/api/hr-people.api';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { dayText } from '../../hr-format';
import { optionLabel } from '../../hr-options';

const DAY_MS = 86_400_000;

/** A drawer date as yyyy-MM-dd, or null when blank. */
const optionalDay = (v: unknown): string | null => (v ? dayText(v) : null);

/** The identity and work documents of one employee (passport, visa, labour card ...) with expiry badges. */
@Component({
  selector: 'erp-employee-documents',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzDrawerModule, NzIconModule, NzModalModule, NzTableModule, NzTagModule],
  templateUrl: './employee-documents.component.html',
})
export class EmployeeDocumentsComponent {
  private readonly documentsApi = inject(EmployeeDocumentsApi);
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

  typeLabel(t: string): string { return optionLabel(DOCUMENT_TYPES, t); }
  /** Whole days from today to the expiry date (negative once expired). */
  daysLeft(d: EmployeeDocumentRow): number { return Math.floor((new Date(d.expiryDate!).getTime() - new Date(new Date().toDateString()).getTime()) / DAY_MS); }

  startAdd(): void { this.editing.set(null); this.errors.set(null); this.form.set(true); }
  startEdit(d: EmployeeDocumentRow): void { this.documentsApi.detail(d.id).subscribe((full) => { this.editing.set(full); this.errors.set(null); this.form.set(true); }); }

  save(v: Record<string, unknown>): void {
    this.saving.set(true); this.errors.set(null);
    const body = { ...v, employeeId: this.employeeId(), issueDate: optionalDay(v['issueDate']), expiryDate: optionalDay(v['expiryDate']) };
    const id = this.editing()?.id;
    (id ? this.documentsApi.update(id, body) : this.documentsApi.create(body)).subscribe({
      next: () => { this.saving.set(false); this.form.set(false); this.message.success(doneText('saved', 'Document', (v['number'] as string) || this.typeLabel(String(v['type'])))); this.load(); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not save the document'); },
    });
  }

  remove(d: EmployeeDocumentRow): void {
    this.modal.confirm({
      nzTitle: 'Delete this document?', nzOkDanger: true, nzOkText: 'Delete',
      nzOnOk: () => new Promise<void>((resolve) => this.documentsApi.delete(d.id).subscribe({ next: () => { this.load(); resolve(); }, error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not delete'); resolve(); } })),
    });
  }

  private load(): void { this.documentsApi.list({ page: 1, pageSize: 100, sort: 'expiryDate', filters: [{ field: 'employeeId', op: 'eq', value: this.employeeId() }] }).subscribe((p) => this.rows.set(p.items)); }
}
