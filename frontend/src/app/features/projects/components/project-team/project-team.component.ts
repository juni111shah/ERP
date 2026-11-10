import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { AllocationDto, MemberDto, ProjectsExtraApi, UserOption } from '@core/api/projects-extra.api';
import { RowActionsComponent } from '@shared/components/row-actions/row-actions.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { loadOrFallback } from '@shared/utils/load-or-fallback';
import { isoToday } from '../../projects-format';
import { errorText, PRJ_STYLES, PRJ_SUBTABS } from '../../projects-ui';

/** Who is on the project and how much of their time is planned for it. */
@Component({
  selector: 'erp-project-team',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RowActionsComponent, DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzInputModule, NzSelectModule, HasPermissionDirective],
  templateUrl: './project-team.component.html',
  styles: [PRJ_STYLES + PRJ_SUBTABS + '.add { margin:12px 0 0; }'],
})
export class ProjectTeamComponent {
  readonly projectId = input.required<string>();
  private readonly api = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly members = signal<MemberDto[]>([]);
  readonly allocations = signal<AllocationDto[]>([]);
  readonly users = signal<UserOption[]>([]);
  readonly available = computed(() => this.users().filter((u) => !this.members().some((m) => m.userId === u.id)));
  newMember: string | null = null; newRole = '';

  constructor() {
    loadOrFallback(this.users, this.api.users(), []);
    effect(() => { this.projectId(); this.load(); });
  }

  load(): void {
    this.api.members(this.projectId()).subscribe({ next: (m) => this.members.set(m), error: (e) => this.msg.error(errorText(e, 'Could not load the team.')) });
    loadOrFallback(this.allocations, this.api.allocations(this.projectId()), []);
  }

  /** The hours a week planned for this person today, over all their allocations on the project. */
  plannedFor(userId: string): number {
    const now = isoToday();
    return this.allocations().filter((a) => a.userId === userId && a.fromDate <= now && a.toDate >= now).reduce((s, a) => s + a.hoursPerWeek, 0);
  }

  add(): void {
    if (!this.newMember) return;
    this.api.addMember(this.projectId(), this.newMember, this.newRole || null).subscribe({
      next: () => { this.newMember = null; this.newRole = ''; this.load(); },
      error: (e) => this.msg.error(errorText(e, 'Could not add.')),
    });
  }

  remove(m: MemberDto): void {
    this.modal.confirm({
      nzTitle: `Remove ${m.name} from the team?`, nzOkText: 'Remove', nzOkDanger: true,
      nzOnOk: () => this.api.removeMember(m.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not remove.')) }),
    });
  }
}
