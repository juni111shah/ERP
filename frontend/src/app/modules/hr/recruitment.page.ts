import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { catchError, of } from 'rxjs';
import { LegalEntitiesApi, LegalEntityDto } from '../../core/api/admin.api';
import { ProblemDetails } from '../../core/api/api.models';
import { createPagedList } from '../../core/api/paged-list';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { CANDIDATE_STAGES, CandidateDto, CandidateRow, CandidatesApi, ChecklistRow, ChecklistsApi, OPENING_STATUSES, OpeningDto, OpeningRow, OpeningsApi } from './hr-people.api';

const NEXT: Record<string, string> = { applied: 'screening', screening: 'interview', interview: 'offer' };

/** Vacancies and the candidates for them: move people along the pipeline, then hire (which creates the employee and starts their onboarding). */
@Component({
  selector: 'erp-recruitment',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzModalModule, NzTabsModule],
  template: `
    <h1 class="erp-page-title">Recruitment</h1>
    <nz-tabs>
      <nz-tab nzTitle="Vacancies">
        <erp-paged-table [list]="openings" [columns]="openingColumns" searchPlaceholder="Search vacancies" emptyText="No vacancies yet" (rowClick)="openOpening($event)">
          <button toolbar *erpHasPermission="'hr.recruitment.manage'" nz-button nzType="primary" (click)="openingState.create()" data-testid="opening-new"><span nz-icon nzType="plus"></span>New vacancy</button>
          <ng-template #cell let-row let-column="column" let-value="value">
            @if (column.key === 'status') { <erp-status [value]="row.status" [label]="value" /> } @else { {{ value }} }
          </ng-template>
        </erp-paged-table>
      </nz-tab>
      <nz-tab nzTitle="Candidates">
        <erp-paged-table [list]="candidates" [columns]="candidateColumns" searchPlaceholder="Search candidates" emptyText="No candidates yet" (rowClick)="openCandidate($event)">
          <button toolbar *erpHasPermission="'hr.recruitment.manage'" nz-button nzType="primary" (click)="candidateState.create()" data-testid="candidate-new"><span nz-icon nzType="plus"></span>New candidate</button>
          <ng-template #cell let-row let-column="column" let-value="value">
            @switch (column.key) {
              @case ('stage') { <erp-status [value]="row.stage" [label]="stageLabel(row.stage)" /> }
              @case ('actions') {
                <ng-container *erpHasPermission="'hr.recruitment.manage'">
                  @if (next(row.stage); as n) { <a (click)="$event.stopPropagation(); move(row, n)" style="margin-inline-end:10px" data-testid="candidate-next">Move to {{ stageLabel(n) }}</a> }
                  @if (row.stage === 'offer') { <a (click)="$event.stopPropagation(); startHire(row)" style="margin-inline-end:10px" data-testid="candidate-hire">Hire</a> }
                  @if (row.stage !== 'hired' && row.stage !== 'rejected') { <a (click)="$event.stopPropagation(); move(row, 'rejected')" data-testid="candidate-reject">Reject</a> }
                </ng-container>
              }
              @default { {{ value }} }
            }
          </ng-template>
        </erp-paged-table>
      </nz-tab>
    </nz-tabs>
    <erp-record-drawer [title]="openingState.editing() ? 'Edit vacancy' : 'New vacancy'" [open]="openingState.open()" [fields]="openingFields" [value]="$any(openingState.editing())"
                       [saving]="openingState.saving()" [errors]="openingState.errors()" testId="opening-form" (submitted)="openingState.submit($event)" (closed)="openingState.close()">
      @if (openingState.editing(); as o) { <div drawerActions style="margin-bottom:12px"><button *erpHasPermission="'hr.recruitment.manage'" nz-button nzDanger type="button" (click)="removeOpening(o.id)" data-testid="opening-delete">Delete</button></div> }
    </erp-record-drawer>
    <erp-record-drawer [title]="candidateState.editing() ? 'Edit candidate' : 'New candidate'" [open]="candidateState.open()" [fields]="candidateFields()" [value]="$any(candidateState.editing())"
                       [saving]="candidateState.saving()" [errors]="candidateState.errors()" testId="candidate-form" (submitted)="candidateState.submit($event)" (closed)="candidateState.close()" />
    <erp-record-drawer title="Hire" [open]="hiring() !== null" [fields]="hireFields()" [saving]="busy()" [errors]="errors()" [width]="460" testId="hire-form" submitLabel="Hire"
                       (submitted)="hire($event)" (closed)="hiring.set(null)" />`,
})
export class RecruitmentPage {
  private readonly openingsApi = inject(OpeningsApi);
  private readonly candidatesApi = inject(CandidatesApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly router = inject(Router);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly templatePage = toSignal(inject(ChecklistsApi).list({ page: 1, pageSize: 100, filters: [{ field: 'kind', op: 'eq', value: 'onboarding' }] }).pipe(catchError(() => of({ items: [] as ChecklistRow[] }))), { initialValue: { items: [] as ChecklistRow[] } });

  readonly openings = createPagedList((q) => this.openingsApi.list(q), { sort: '-createdAt' });
  readonly candidates = createPagedList((q) => this.candidatesApi.list(q), { sort: '-createdAt' });
  readonly hiring = signal<CandidateRow | null>(null);
  readonly busy = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);

  readonly openingState = new MasterDataState<OpeningDto>({
    save: (id, body) => (id ? this.openingsApi.update(id, body) : this.openingsApi.create(body)), reload: () => this.openings.reload(), message: this.message, entityName: 'Vacancy',
  });
  readonly candidateState = new MasterDataState<CandidateDto>({
    save: (id, body) => (id ? this.candidatesApi.update(id, body) : this.candidatesApi.create(body)), reload: () => this.candidates.reload(), message: this.message, entityName: 'Candidate',
  });

  readonly openingColumns: TableColumn<OpeningRow>[] = [
    { key: 'title', label: 'Vacancy', sortField: 'title' },
    { key: 'headcount', label: 'Positions', width: '100px' },
    { key: 'status', label: 'Status', value: (o) => OPENING_STATUSES.find((s) => s.value === o.status)?.label ?? o.status, width: '120px', filterField: 'status', filterOptions: OPENING_STATUSES },
    { key: 'closingDate', label: 'Closes', width: '120px' },
  ];
  readonly candidateColumns: TableColumn<CandidateRow>[] = [
    { key: 'fullName', label: 'Candidate', sortField: 'fullName' },
    { key: 'vacancy', label: 'Vacancy', value: (c) => this.openings.items().find((o) => o.id === c.openingId)?.title ?? '' },
    { key: 'email', label: 'Email' },
    { key: 'stage', label: 'Stage', sortField: 'stage', width: '120px', filterField: 'stage', filterOptions: CANDIDATE_STAGES },
    { key: 'actions', label: '', width: '300px' },
  ];

  readonly openingFields: FieldSpec[] = [
    { key: 'title', label: 'Title', type: 'text', required: true, maxLength: 150 },
    { key: 'headcount', label: 'Positions to fill', type: 'number', min: 1, max: 500, default: 1, precision: 0 },
    { key: 'status', label: 'Status', type: 'select', options: OPENING_STATUSES, default: 'open' },
    { key: 'closingDate', label: 'Closing date', type: 'date' },
    { key: 'description', label: 'Description', type: 'textarea', maxLength: 4000 },
  ];
  readonly candidateFields = computed<FieldSpec[]>(() => [
    { key: 'openingId', label: 'Vacancy', type: 'select', required: true, readonlyOnEdit: true, options: this.openings.items().filter((o) => o.status !== 'closed').map((o) => ({ value: o.id, label: o.title })) },
    { key: 'fullName', label: 'Full name', type: 'text', required: true, maxLength: 200 },
    { key: 'email', label: 'Email', type: 'text', maxLength: 200 },
    { key: 'phone', label: 'Phone', type: 'text', maxLength: 50 },
    { key: 'source', label: 'Source', type: 'text', maxLength: 100 },
    { key: 'expectedSalary', label: 'Expected salary', type: 'number', min: 0 },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 2000 },
  ]);
  readonly hireFields = computed<FieldSpec[]>(() => [
    { key: 'legalEntityId', label: 'Employer (legal entity)', type: 'select', required: true, options: this.entityPage().items.map((e) => ({ value: e.id, label: e.name })) },
    { key: 'joinDate', label: 'Join date', type: 'date', required: true, default: new Date() },
    { key: 'designation', label: 'Designation', type: 'text', required: true, maxLength: 150 },
    { key: 'basicSalary', label: 'Basic salary (monthly)', type: 'number', required: true, min: 0.01 },
    { key: 'housingAllowance', label: 'Housing allowance', type: 'number', min: 0, default: 0 },
    { key: 'transportAllowance', label: 'Transport allowance', type: 'number', min: 0, default: 0 },
    { key: 'nationality', label: 'Nationality', type: 'text', maxLength: 100 },
    { key: 'onboardingTemplateId', label: 'Start the onboarding checklist', type: 'select', options: this.templatePage().items.filter((t) => t.isActive).map((t) => ({ value: t.id, label: t.name })) },
  ]);

  stageLabel(s: string): string { return CANDIDATE_STAGES.find((x) => x.value === s)?.label ?? s; }
  next(stage: string): string | undefined { return NEXT[stage]; }

  openOpening(row: OpeningRow): void { this.openingsApi.detail(row.id).subscribe((o) => this.openingState.edit(o)); }
  removeOpening(id: string): void {
    this.modal.confirm({
      nzTitle: 'Delete this vacancy?', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => this.openingsApi.delete(id).subscribe({
        next: () => { this.message.success('Vacancy deleted'); this.openingState.close(); this.openings.reload(); resolve(); },
        error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not delete the vacancy'); resolve(); },
      })),
    });
  }

  openCandidate(row: CandidateRow): void { this.candidatesApi.detail(row.id).subscribe((c) => this.candidateState.edit(c)); }

  move(c: CandidateRow, stage: string): void {
    const run = () => new Promise<void>((resolve) => this.candidatesApi.move(c.id, stage).subscribe({
      next: () => { this.candidates.reload(); resolve(); }, error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not move the candidate'); resolve(); },
    }));
    if (stage === 'rejected') this.modal.confirm({ nzTitle: `Reject ${c.fullName}?`, nzOkDanger: true, nzOkText: 'Reject', nzOnOk: run });
    else void run();
  }

  startHire(c: CandidateRow): void { this.errors.set(null); this.hiring.set(c); }

  hire(v: Record<string, unknown>): void {
    const c = this.hiring();
    if (!c) return;
    this.busy.set(true); this.errors.set(null);
    this.candidatesApi.hire(c.id, { ...v, joinDate: String(v['joinDate']).slice(0, 10), onboardingTemplateId: v['onboardingTemplateId'] ?? null }).subscribe({
      next: (r) => {
        this.busy.set(false); this.hiring.set(null); this.candidates.reload(); this.openings.reload();
        this.message.success(`${c.fullName} is now employee ${r.employeeNumber}${r.tasksCreated ? ` with ${r.tasksCreated} onboarding tasks` : ''}`);
        void this.router.navigate(['/hr/employees']);
      },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not hire'); },
    });
  }
}
