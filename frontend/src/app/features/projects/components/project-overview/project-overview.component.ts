import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AcceptanceDto, CommentDto, MemberDto, ProjectsExtraApi, RiskDto } from '@core/api/projects-extra.api';
import { MilestoneDto, ProjectDto, ProjectMilestonesApi, ProjectTasksApi, TaskDto } from '@core/api/projects.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { loadOrFallback } from '@shared/utils/load-or-fallback';
import { initials, isoToday } from '../../projects-format';
import { PRJ_STYLES } from '../../projects-ui';
import { attentionItems, nextDueTasks, projectSourceLabel } from '../../projects-view';
import { ProjectTagComponent } from '../project-tag/project-tag.component';
import { TaskDrawerComponent } from '../task-drawer/task-drawer.component';

/** What needs attention, what is coming up and who is involved. */
@Component({
  selector: 'erp-project-overview',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, DatePipe, RouterModule, NzIconModule, ProjectTagComponent, TaskDrawerComponent],
  templateUrl: './project-overview.component.html',
  styles: [PRJ_STYLES + `
    .columns { display:grid; grid-template-columns:minmax(0,3fr) minmax(0,2fr); gap:12px; align-items:start; } .col-main, .col-side { display:flex; flex-direction:column; gap:12px; min-width:0; } @media (max-width:1000px) { .columns { grid-template-columns:minmax(0,1fr); } }
    .attention { border-color:#f59e0b; background:var(--erp-color-bg-surface,#fffbeb); } .attention ul { margin:0; padding-inline-start:18px; } .attention a { color:var(--erp-color-text-primary,#92400e); }
    .line { display:flex; align-items:center; gap:10px; padding:6px 0; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); font-size:13px; } .line[role=button] { cursor:pointer; } .line[role=button]:hover { background:var(--erp-color-bg-subtle,#f8fafc); } .grow { flex:1; min-width:0; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; } .when { white-space:nowrap; font-size:12px; }
    .scope { white-space:pre-wrap; color:var(--erp-color-text-primary,#334155); font-size:13.5px; } .facts { display:grid; grid-template-columns:repeat(auto-fill,minmax(180px,1fr)); gap:10px; margin:0; } dt { font-size:12px; color:var(--erp-color-text-secondary,#64748b); } dd { margin:0; font-size:13.5px; font-weight:500; color:var(--erp-color-text-primary,#0f172a); }
    .team { display:flex; flex-direction:column; gap:6px; font-size:13px; } .who { display:inline-flex; width:24px; height:24px; border-radius:50%; align-items:center; justify-content:center; background:var(--erp-color-primary,#10955d); color:#fff; font-size:10.5px; font-weight:600; margin-inline-end:6px; }
    .comment { padding:6px 0; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); font-size:13px; } .comment p { margin:2px 0 0; white-space:pre-wrap; }
  `],
})
export class ProjectOverviewComponent {
  readonly project = input.required<ProjectDto>();
  readonly changed = output<void>();
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly milestonesApi = inject(ProjectMilestonesApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly session = inject(SessionStore);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly drawer = viewChild(TaskDrawerComponent);
  readonly todayIso = isoToday();
  readonly tasks = signal<TaskDto[]>([]);
  readonly milestones = signal<MilestoneDto[]>([]);
  readonly risks = signal<RiskDto[]>([]);
  readonly members = signal<MemberDto[]>([]);
  readonly comments = signal<CommentDto[]>([]);
  readonly acceptances = signal<AcceptanceDto[]>([]);

  readonly nextTasks = computed(() => nextDueTasks(this.tasks()));
  readonly sourceLabel = computed(() => projectSourceLabel(this.project().sourceType));
  readonly attention = computed(() => attentionItems(this.project(), this.milestones(), this.acceptances(), this.currency(), this.todayIso));

  constructor() { effect(() => { this.project().id; this.load(); }); }

  /** Loads the side lists the person may read; a list that cannot be loaded is shown empty. */
  load(): void {
    const id = this.project().id;
    const filters = [{ field: 'projectId', op: 'eq' as const, value: id }];
    if (this.can('projects.tasks.read')) loadOrFallback(this.tasks, this.tasksApi.list({ page: 1, pageSize: 200, sort: 'dueDate', filters }), [], (r) => r.items);
    if (this.can('projects.milestones.read')) {
      loadOrFallback(this.milestones, this.milestonesApi.list({ page: 1, pageSize: 50, sort: 'dueDate', filters }), [], (r) => r.items.filter((m) => m.status !== 'cancelled'));
      loadOrFallback(this.acceptances, this.extra.acceptances(id), []);
    }
    if (this.can('projects.risks.read')) loadOrFallback(this.risks, this.extra.risks(id), [], (r) => r.filter((x) => x.status === 'open').slice(0, 5));
    loadOrFallback(this.members, this.extra.members(id), []);
    loadOrFallback(this.comments, this.extra.comments('project', id), [], (c) => c.slice(-3).reverse());
  }

  late(t: TaskDto): boolean { return !!t.dueDate && t.dueDate < this.todayIso; }
  initials(name: string): string { return initials(name); }

  private can(permission: string): boolean { return this.session.hasPermission(permission); }
}
