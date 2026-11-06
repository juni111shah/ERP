import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { doneText } from '../../core/format/done-text';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { createPagedList } from '../../core/api/paged-list';
import { problemMessage } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { CompetencyDto, EmployeeSummary, EmployeesApi, RATINGS, ReviewCycleDto, ReviewCyclesApi, ReviewDto, ReviewRow, ReviewsApi } from './hr.api';
import { RevealDirective } from '../../shared/reveal.directive';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

const DEFAULT_COMPETENCIES = ['Job knowledge', 'Quality of work', 'Communication', 'Teamwork', 'Initiative'];

/** Performance reviews: overall rating, goals and competency scores. Draft -> Submitted -> Acknowledged by the employee. */
@Component({
  selector: 'erp-reviews',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, FormsModule, PagedTableComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzDrawerModule, NzIconModule, NzInputModule, NzModalModule, NzSelectModule],
  styles: [`.fld { display: flex; flex-direction: column; gap: 4px; min-width: 0; } .fld > label { font-size: 13px; color: var(--erp-color-text-secondary); } .fld nz-select { width: 100%; } .comp-head { display: grid; grid-template-columns: 1.3fr 90px 1.5fr 32px; gap: 8px; margin-bottom: 4px; font-size: 12px; color: var(--erp-color-text-secondary); }`],
  template: `
    <h1 class="erp-page-title">Performance reviews</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search reviewer" emptyText="No reviews yet" (rowClick)="open($event.id)">
      <button toolbar *erpHasPermission="'hr.reviews.manage'" nz-button nzType="primary" (click)="create()" data-testid="review-new"><span nz-icon nzType="plus"></span> New review</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'employeeId') {
          <span class="name-cell">{{ value }}</span>
        } @else if (column.key === 'overallRating') {
          <strong>{{ value }}</strong>
        } @else if (column.key === 'status') {
          <erp-status [value]="row.status" />
        } @else {
          {{ value }}
        }
      </ng-template>
    </erp-paged-table>
    <nz-drawer [nzVisible]="drawer()" [nzTitle]="id() ? 'Review: ' + employeeName() : 'New review'" [nzWidth]="680" (nzOnClose)="drawer.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
          <div class="fld"><label for="review-employee">Employee</label>
            <nz-select nzId="review-employee" nzShowSearch nzPlaceHolder="Choose an employee" [(ngModel)]="employeeId" [nzDisabled]="!!id()" data-testid="review-employee">@for (e of employees(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.number + ' · ' + e.fullName"></nz-option> }</nz-select></div>
          <div class="fld"><label for="review-cycle">Cycle</label>
            <nz-select nzId="review-cycle" nzPlaceHolder="Choose a cycle" [(ngModel)]="cycleId" [nzDisabled]="!!id()" data-testid="review-cycle">@for (c of id() ? cycles() : openCycles(); track c.id) { <nz-option [nzValue]="c.id" [nzLabel]="c.name"></nz-option> }</nz-select></div>
          <div class="fld"><label for="review-reviewer">Reviewer</label><input id="review-reviewer" nz-input [(ngModel)]="reviewer" [disabled]="locked()" data-testid="review-reviewer" /></div>
          <div class="fld"><label for="review-rating">Overall rating</label>
            <nz-select nzId="review-rating" nzAllowClear nzPlaceHolder="Not rated yet" [(ngModel)]="rating" [nzDisabled]="locked()" data-testid="review-rating">@for (r of ratings; track r.value) { <nz-option [nzValue]="r.value" [nzLabel]="r.label"></nz-option> }</nz-select></div>
        </div>
        <h4 style="margin-top:16px"><label for="review-goals">Goals</label></h4>
        <textarea id="review-goals" nz-input rows="3" [(ngModel)]="goals" [disabled]="locked()" data-testid="review-goals"></textarea>
        <h4 style="margin-top:16px">Competencies</h4>
        <div class="comp-head" aria-hidden="true"><span>Competency</span><span>Score (1 to 5)</span><span>Notes</span><span></span></div>
        @for (c of competencies(); track $index; let i = $index) {
          <div style="display:grid;grid-template-columns:1.3fr 90px 1.5fr 32px;gap:8px;margin-bottom:8px;align-items:center">
            <input [attr.aria-label]="'Competency ' + (i + 1)" nz-input placeholder="Competency" [ngModel]="c.name" (ngModelChange)="patch(i, { name: $event })" [disabled]="locked()" />
            <nz-select [nzId]="'review-score-' + i" [attr.aria-label]="'Score ' + (i + 1)" [ngModel]="c.score" (ngModelChange)="patch(i, { score: $event })" [nzDisabled]="locked()">@for (n of [1, 2, 3, 4, 5]; track n) { <nz-option [nzValue]="n" [nzLabel]="'' + n"></nz-option> }</nz-select>
            <input [attr.aria-label]="'Notes ' + (i + 1)" nz-input placeholder="Notes" [ngModel]="c.notes" (ngModelChange)="patch(i, { notes: $event })" [disabled]="locked()" />
            @if (!locked()) { <button [attr.aria-label]="'Remove competency ' + (i + 1)" nz-button nzType="text" nzSize="small" (click)="remove(i)"><span nz-icon nzType="delete"></span></button> }
          </div>
        }
        @if (!locked()) { <button nz-button nzType="dashed" nzSize="small" (click)="add()" data-testid="review-add-competency"><span nz-icon nzType="plus"></span> Add competency</button> }
        <h4 style="margin-top:16px"><label for="review-comments">Comments</label></h4>
        <textarea id="review-comments" nz-input rows="3" [(ngModel)]="comments" [disabled]="locked()"></textarea>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <div style="display:flex;gap:8px;align-items:center">
        <span style="flex:1">@if (id()) { <erp-status [value]="status()" /> }</span>
        @if (!locked()) {
          @if (id()) { <button *erpHasPermission="'hr.reviews.manage'" nz-button nzDanger (click)="deleteDraft()" [disabled]="busy()" data-testid="review-delete">Delete draft</button> }
          <button *erpHasPermission="'hr.reviews.manage'" nz-button (click)="save()" [nzLoading]="busy()" data-testid="review-save">Save draft</button>
          <button *erpHasPermission="'hr.reviews.manage'" nz-button nzType="primary" (click)="submit()" [nzLoading]="busy()" data-testid="review-submit">Submit</button>
        }
        @if (status() === 'submitted') { <button *erpHasPermission="'hr.reviews.manage'" nz-button nzType="primary" (click)="acknowledge()" [nzLoading]="busy()" data-testid="review-acknowledge">Employee acknowledged</button> }
      </div>
    </ng-template>`,
})
export class ReviewsPage {
  private readonly modal = inject(NzModalService);
  private readonly api = inject(ReviewsApi);
  private readonly employeesApi = inject(EmployeesApi);
  private readonly cyclesApi = inject(ReviewCyclesApi);
  private readonly message = inject(NzMessageService);
  readonly ratings = RATINGS;
  readonly employees = signal<EmployeeSummary[]>([]);
  readonly cycles = signal<ReviewCycleDto[]>([]);
  readonly openCycles = computed(() => this.cycles().filter((c) => c.status === 'open'));
  private readonly employeeNames = computed(() => new Map(this.employees().map((e) => [e.id, e.fullName])));
  private readonly cycleNames = computed(() => new Map(this.cycles().map((c) => [c.id, c.name])));

  readonly list = createPagedList((q) => this.api.list(q), { sort: '-createdAt' });
  readonly columns: TableColumn<ReviewRow>[] = [
    { key: 'employeeId', label: 'Employee', value: (r) => this.employeeNames().get(r.employeeId) ?? '' },
    { key: 'cycleId', label: 'Cycle', value: (r) => this.cycleNames().get(r.cycleId) ?? '' },
    { key: 'reviewerName', label: 'Reviewer' },
    { key: 'overallRating', label: 'Rating', sortField: 'overallRating', value: (r) => (r.overallRating ? `${r.overallRating} / 5` : ''), width: '100px' },
    { key: 'status', label: 'Status', width: '130px' },
  ];

  readonly drawer = signal(false);
  readonly id = signal<string | null>(null);
  readonly status = signal('draft');
  readonly busy = signal(false);
  readonly employeeName = signal('');
  readonly competencies = signal<CompetencyDto[]>([]);
  readonly locked = computed(() => this.status() !== 'draft');

  employeeId: string | null = null;
  cycleId: string | null = null;
  reviewer = '';
  rating: number | null = null;
  goals = '';
  comments = '';

  constructor() {
    this.employeesApi.list({ page: 1, pageSize: 200 }).subscribe((r) => this.employees.set(r.items));
    this.cyclesApi.list({ page: 1, pageSize: 100 }).subscribe((r) => this.cycles.set(r.items));
  }

  create(): void {
    this.id.set(null); this.status.set('draft'); this.employeeId = null; this.cycleId = this.openCycles()[0]?.id ?? null; this.reviewer = ''; this.rating = null; this.goals = ''; this.comments = '';
    this.competencies.set(DEFAULT_COMPETENCIES.map((name) => ({ name, score: 3 })));
    this.drawer.set(true);
  }

  open(id: string): void {
    this.api.detail(id).subscribe((r: ReviewDto) => {
      this.id.set(r.id); this.status.set(r.status); this.employeeId = r.employeeId; this.cycleId = r.cycleId; this.employeeName.set(r.employeeName);
      this.reviewer = r.reviewerName ?? ''; this.rating = r.overallRating ?? null; this.goals = r.goals ?? ''; this.comments = r.comments ?? '';
      this.competencies.set(r.competencies.map((c) => ({ ...c })));
      this.drawer.set(true);
    });
  }

  add(): void { this.competencies.update((l) => [...l, { name: '', score: 3 }]); }
  remove(i: number): void { this.competencies.update((l) => l.filter((_, idx) => idx !== i)); }
  patch(i: number, change: Partial<CompetencyDto>): void { this.competencies.update((l) => l.map((c, idx) => (idx === i ? { ...c, ...change } : c))); }

  private body(): Record<string, unknown> | null {
    if (!this.employeeId || !this.cycleId) { this.message.warning('Choose an employee and a cycle'); return null; }
    return {
      employeeId: this.employeeId, cycleId: this.cycleId, reviewerName: this.reviewer || null, overallRating: this.rating, goals: this.goals || null, comments: this.comments || null,
      competencies: this.competencies().filter((c) => c.name.trim()).map((c) => ({ name: c.name, score: c.score, notes: c.notes || null })),
    };
  }

  private fail(e: unknown, fallback: string): void { this.busy.set(false); this.message.error(problemMessage(e, fallback)); }

  private persist(then: (id: string) => void): void {
    const body = this.body();
    if (!body) return;
    this.busy.set(true);
    this.api.save(this.id(), body).subscribe({
      next: (r) => { this.busy.set(false); this.id.set(r.id); this.employeeName.set(r.employeeName); then(r.id); },
      error: (e) => this.fail(e, 'Could not save the review'),
    });
  }

  save(): void { this.persist(() => { this.message.success(doneText('saved', 'Review', this.employeeName())); this.list.reload(); }); }

  deleteDraft(): void {
    const id = this.id();
    if (!id) return;
    const deletedFor = this.employeeName();
    this.modal.confirm({
      nzTitle: 'Delete this draft review?', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => this.api.delete(id).subscribe({
        next: () => { this.message.success(doneText('deleted', 'Review', deletedFor)); this.drawer.set(false); this.list.reload(); resolve(); },
        error: (e) => { this.fail(e, 'Could not delete the review'); resolve(); },
      })),
    });
  }

  submit(): void {
    this.persist((id) => {
      this.busy.set(true);
      this.api.submit(id).subscribe({
        next: (r) => { this.busy.set(false); this.status.set(r.status); this.message.success(doneText('submitted', 'Review', this.employeeName())); this.list.reload(); },
        error: (e) => this.fail(e, 'Could not submit the review'),
      });
    });
  }

  acknowledge(): void {
    const id = this.id();
    if (!id) return;
    this.busy.set(true);
    this.api.acknowledge(id).subscribe({
      next: (r) => { this.busy.set(false); this.status.set(r.status); this.message.success(`Review acknowledged for ${this.employeeName()}`); this.list.reload(); },
      error: (e) => this.fail(e, 'Could not acknowledge the review'),
    });
  }
}
