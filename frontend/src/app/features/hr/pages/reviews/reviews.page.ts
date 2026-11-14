import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { CompetencyDto, EmployeeSummary, EmployeesApi, RATINGS, ReviewCycleDto, ReviewCyclesApi, ReviewDto, ReviewInput, ReviewRow, ReviewsApi } from '@core/api/hr.api';
import { createPagedList } from '@core/api/paged-list';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';

/** The competencies a new review starts with, each scored 3 (meets expectations). */
const DEFAULT_COMPETENCIES = ['Job knowledge', 'Quality of work', 'Communication', 'Teamwork', 'Initiative'];

/** Performance reviews: overall rating, goals and competency scores. Draft -> Submitted -> Acknowledged by the employee. */
@Component({
  selector: 'erp-reviews',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzToolTipModule, FormsModule, PagedTableComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzDrawerModule, NzIconModule, NzInputModule, NzModalModule, NzSelectModule],
  styleUrl: './reviews.page.scss',
  templateUrl: './reviews.page.html',
})
export class ReviewsPage {
  private readonly modal = inject(NzModalService);
  private readonly reviewsApi = inject(ReviewsApi);
  private readonly employeesApi = inject(EmployeesApi);
  private readonly cyclesApi = inject(ReviewCyclesApi);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  readonly ratings = RATINGS;
  readonly employees = signal<EmployeeSummary[]>([]);
  readonly cycles = signal<ReviewCycleDto[]>([]);
  readonly openCycles = computed(() => this.cycles().filter((c) => c.status === 'open'));
  private readonly employeeNames = computed(() => new Map(this.employees().map((e) => [e.id, e.fullName])));
  private readonly cycleNames = computed(() => new Map(this.cycles().map((c) => [c.id, c.name])));

  readonly list = createPagedList((q) => this.reviewsApi.list(q), { sort: '-createdAt' });
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
  /** Only a draft can be changed, and only by someone who may manage reviews (hr.reviews.manage). */
  readonly locked = computed(() => this.status() !== 'draft' || !this.session.hasPermission('hr.reviews.manage'));

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
    this.reviewsApi.detail(id).subscribe((r: ReviewDto) => {
      this.id.set(r.id); this.status.set(r.status); this.employeeId = r.employeeId; this.cycleId = r.cycleId; this.employeeName.set(r.employeeName);
      this.reviewer = r.reviewerName ?? ''; this.rating = r.overallRating ?? null; this.goals = r.goals ?? ''; this.comments = r.comments ?? '';
      this.competencies.set(r.competencies.map((c) => ({ ...c })));
      this.drawer.set(true);
    });
  }

  add(): void { this.competencies.update((l) => [...l, { name: '', score: 3 }]); }
  remove(i: number): void { this.competencies.update((l) => l.filter((_, idx) => idx !== i)); }
  patch(i: number, change: Partial<CompetencyDto>): void { this.competencies.update((l) => l.map((c, idx) => (idx === i ? { ...c, ...change } : c))); }

  save(): void { this.persist(() => { this.message.success(doneText('saved', 'Review', this.employeeName())); this.list.reload(); }); }

  deleteDraft(): void {
    const id = this.id();
    if (!id) return;
    const deletedFor = this.employeeName();
    this.modal.confirm({
      nzTitle: 'Delete this draft review?', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => this.reviewsApi.delete(id).subscribe({
        next: () => { this.message.success(doneText('deleted', 'Review', deletedFor)); this.drawer.set(false); this.list.reload(); resolve(); },
        error: (e) => { this.fail(e, 'Could not delete the review'); resolve(); },
      })),
    });
  }

  submit(): void {
    this.persist((id) => {
      this.busy.set(true);
      this.reviewsApi.submit(id).subscribe({
        next: (r) => { this.busy.set(false); this.status.set(r.status); this.message.success(doneText('submitted', 'Review', this.employeeName())); this.list.reload(); },
        error: (e) => this.fail(e, 'Could not submit the review'),
      });
    });
  }

  acknowledge(): void {
    const id = this.id();
    if (!id) return;
    this.busy.set(true);
    this.reviewsApi.acknowledge(id).subscribe({
      next: (r) => { this.busy.set(false); this.status.set(r.status); this.message.success(`Review acknowledged for ${this.employeeName()}`); this.list.reload(); },
      error: (e) => this.fail(e, 'Could not acknowledge the review'),
    });
  }

  /** The review as saved, or null (with a warning) while the employee or cycle is missing. Blank competencies are left out. */
  private body(): ReviewInput | null {
    if (!this.employeeId || !this.cycleId) { this.message.warning('Choose an employee and a cycle'); return null; }
    return {
      employeeId: this.employeeId, cycleId: this.cycleId, reviewerName: this.reviewer || null, overallRating: this.rating, goals: this.goals || null, comments: this.comments || null,
      competencies: this.competencies().filter((c) => c.name.trim()).map((c) => ({ name: c.name, score: c.score, notes: c.notes || null })),
    };
  }

  /** Saves the review (create or update), then runs `then` with its id. */
  private persist(then: (id: string) => void): void {
    const body = this.body();
    if (!body) return;
    this.busy.set(true);
    this.reviewsApi.save(this.id(), body).subscribe({
      next: (r) => { this.busy.set(false); this.id.set(r.id); this.employeeName.set(r.employeeName); then(r.id); },
      error: (e) => this.fail(e, 'Could not save the review'),
    });
  }

  private fail(e: unknown, fallback: string): void { this.busy.set(false); this.message.error(problemMessage(e, fallback)); }
}
