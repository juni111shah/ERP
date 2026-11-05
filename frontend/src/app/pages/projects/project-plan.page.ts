import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { ProjectDto, ProjectsApi } from '../../core/api/projects.api';
import { ChangeOrderDto, CommentDto, MemberDto, ProjectsExtraApi, RiskDto, ScheduleDto, UserOption } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { AttachmentsComponent } from '../../shared/attachments.component';
import { HasPermissionDirective } from '../../shared/has-permission.directive';

const HEALTH: Record<string, { label: string; color: string }> = { onTrack: { label: 'On track', color: 'green' }, atRisk: { label: 'At risk', color: 'orange' }, offTrack: { label: 'Off track', color: 'red' }, closed: { label: 'Closed', color: 'default' } };

/** The plan of one project: Gantt chart with the critical path, task dependencies, team, discussion, change orders, risks, and clone / archive. */
@Component({
  selector: 'erp-project-plan',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzInputModule, NzModalModule, NzSelectModule, NzTableModule, NzTabsModule, NzTagModule, HasPermissionDirective, AttachmentsComponent],
  template: `
    @if (project(); as p) {
      <div class="head">
        <div>
          <a [routerLink]="['/projects', p.id]" class="back"><span nz-icon nzType="arrow-left"></span> {{ p.number }}</a>
          <h1 class="erp-page-title" style="margin:0">{{ p.name }}</h1>
          <span class="sub">
            <nz-tag [nzColor]="health(p.health).color" data-testid="plan-health">{{ health(p.health).label }}</nz-tag>
            {{ p.percentComplete | number: '1.0-0' }}% done · budget {{ currency() }} {{ p.effectiveBudget | number: '1.0-0' }}
            @if (p.approvedChanges !== 0) { (includes {{ p.approvedChanges | number: '1.0-0' }} from change orders) }
            · cost {{ currency() }} {{ p.totalTimesheetCost | number: '1.0-0' }}
            @if (p.isTemplate) { <nz-tag>Template</nz-tag> } @if (p.isArchived) { <nz-tag>Archived</nz-tag> }
          </span>
        </div>
        <div class="actions" *erpHasPermission="'projects.projects.manage'">
          <button nz-button *erpHasPermission="'projects.billing.manage'" (click)="postCosts(p)" data-testid="plan-post-costs" title="Move the cost of approved time and expenses into work in progress"><span nz-icon nzType="book"></span> Post costs to ledger</button>
          <button nz-button *erpHasPermission="'projects.milestones.manage'" (click)="askSignOff(p)" data-testid="plan-signoff" title="Ask the customer to sign off the project in their portal"><span nz-icon nzType="edit"></span> Ask for sign-off</button>
          <button nz-button (click)="cloneOpen.set(true)" data-testid="plan-clone"><span nz-icon nzType="copy"></span> Clone</button>
          <button nz-button (click)="toggleArchive(p)" data-testid="plan-archive">{{ p.isArchived ? 'Restore' : 'Archive' }}</button>
        </div>
      </div>

      <nz-tabs>
        <nz-tab nzTitle="Schedule">
          @if (schedule(); as s) {
            <div class="gantt" data-testid="gantt">
              <div class="scale"><span>{{ s.start }}</span><span>{{ s.finish }}</span></div>
              @for (t of s.tasks; track t.id) {
                <div class="grow" [class.sub]="!!t.parentTaskId">
                  <div class="glabel" [title]="t.title">{{ t.number }} {{ t.title }}</div>
                  <div class="gtrack">
                    <div class="bar" [class.critical]="t.critical" [class.done]="t.status === 'Done'" [style.left.%]="left(s, t.start)" [style.width.%]="width(s, t)" [attr.data-testid]="'bar-' + t.number"
                         [title]="t.start + ' to ' + t.end + (t.critical ? ' · critical path' : ' · slack ' + t.slackDays + ' d')"></div>
                  </div>
                </div>
              } @empty { <p>No tasks yet. Add tasks to see the plan.</p> }
            </div>
            <p class="hint"><span class="swatch critical"></span> critical path (delaying these delays the project) <span class="swatch"></span> other tasks with slack</p>
            <h3>Dependencies</h3>
            <nz-table [nzData]="s.tasks" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="deps">
              <thead><tr><th>Task</th><th>Waits for</th></tr></thead>
              <tbody>
                @for (t of s.tasks; track t.id) {
                  <tr><td>{{ t.number }} {{ t.title }}</td>
                    <td><nz-select nzMode="multiple" nzSize="small" style="min-width:300px" [ngModel]="t.dependsOn" (ngModelChange)="setDeps(t.id, $event)" nzPlaceHolder="Nothing" [attr.aria-label]="'Dependencies of ' + t.number">
                      @for (o of s.tasks; track o.id) { @if (o.id !== t.id) { <nz-option [nzValue]="o.id" [nzLabel]="o.number + ' ' + o.title"></nz-option> } }
                    </nz-select></td></tr>
                }
              </tbody>
            </nz-table>
          }
        </nz-tab>

        <nz-tab nzTitle="Team">
          <nz-table [nzData]="members()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="team">
            <thead><tr><th>Person</th><th>Role</th><th></th></tr></thead>
            <tbody>
              @for (m of members(); track m.id) { <tr><td>{{ m.name }}</td><td>{{ m.role }}</td><td *erpHasPermission="'projects.team.manage'"><button nz-button nzSize="small" nzDanger (click)="removeMember(m)">Remove</button></td></tr> }
              @empty { <tr><td colspan="3">No team members yet.</td></tr> }
            </tbody>
          </nz-table>
          <div class="row" *erpHasPermission="'projects.team.manage'">
            <nz-select [(ngModel)]="newMember" nzShowSearch nzPlaceHolder="Add a person" style="width:260px" aria-label="Person to add" data-testid="member-user">
              @for (u of users(); track u.id) { <nz-option [nzValue]="u.id" [nzLabel]="u.fullName"></nz-option> }
            </nz-select>
            <input nz-input [(ngModel)]="newRole" placeholder="Role" maxlength="100" style="width:200px" aria-label="Role" />
            <button nz-button nzType="primary" [disabled]="!newMember" (click)="addMember()" data-testid="member-add">Add</button>
          </div>
        </nz-tab>

        <nz-tab nzTitle="Discussion">
          <div class="comments" data-testid="comments">
            @for (c of comments(); track c.id) {
              <div class="comment"><b>{{ c.authorName }}</b> <span class="when">{{ c.createdAt | date: 'medium' }}</span><p>{{ c.text }}</p><button nz-button nzSize="small" nzType="link" (click)="deleteComment(c)">Delete</button></div>
            } @empty { <p>No comments yet.</p> }
          </div>
          <div class="row">
            <textarea nz-input rows="2" [(ngModel)]="newComment" maxlength="2000" placeholder="Write a comment" aria-label="Comment" data-testid="comment-text" style="flex:1"></textarea>
            <button nz-button nzType="primary" [disabled]="!newComment.trim()" (click)="addComment()" data-testid="comment-add">Post</button>
          </div>
        </nz-tab>

        <nz-tab nzTitle="Files">
          <erp-attachments ownerType="project" [ownerId]="p.id" />
        </nz-tab>

        <nz-tab nzTitle="Change orders">
          <nz-table [nzData]="changes()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="plan-changes">
            <thead><tr><th>Number</th><th>Title</th><th style="text-align:right">Amount</th><th>Status</th></tr></thead>
            <tbody>@for (c of changes(); track c.id) { <tr><td>{{ c.number }}</td><td>{{ c.title }}</td><td style="text-align:right">{{ c.amountChange | number: '1.0-0' }}</td><td>{{ c.status }}</td></tr> } @empty { <tr><td colspan="4">None. <a routerLink="/projects/change-orders">Manage change orders</a></td></tr> }</tbody>
          </nz-table>
        </nz-tab>

        <nz-tab nzTitle="Risks and issues">
          <nz-table [nzData]="risks()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="plan-risks">
            <thead><tr><th>Kind</th><th>Title</th><th>Severity</th><th>Status</th></tr></thead>
            <tbody>@for (r of risks(); track r.id) { <tr><td>{{ r.kind }}</td><td>{{ r.title }}</td><td>{{ r.severity }}</td><td>{{ r.status }}</td></tr> } @empty { <tr><td colspan="4">None. <a routerLink="/projects/risks">Log a risk or issue</a></td></tr> }</tbody>
          </nz-table>
        </nz-tab>
      </nz-tabs>

      <nz-modal [nzVisible]="cloneOpen()" nzTitle="Clone project" nzOkText="Create copy" (nzOnOk)="clone(p)" (nzOnCancel)="cloneOpen.set(false)">
        <ng-container *nzModalContent>
          <label>Name<input nz-input [(ngModel)]="cloneName" maxlength="200" aria-label="Name of the copy" data-testid="clone-name" /></label>
          <label style="display:block;margin-top:8px">Start date<input nz-input type="date" [(ngModel)]="cloneStart" aria-label="Start date of the copy" /></label>
          <label style="display:block;margin-top:8px"><input type="checkbox" [(ngModel)]="cloneTeam" /> Keep the team and task assignees</label>
          <label style="display:block"><input type="checkbox" [(ngModel)]="cloneTemplate" /> Save the copy as a template</label>
          <p class="hint">Tasks, milestones, dependencies and project rate cards are copied; dates move with the start date. Time, expenses and billing are not copied.</p>
        </ng-container>
      </nz-modal>
    }
  `,
  styles: [`
    .head { display:flex; justify-content:space-between; align-items:flex-start; margin-bottom:12px; gap:12px }
    .back { font-size:12.5px; color:#64748b } .sub { color:#475569; font-size:13px } .actions { display:flex; gap:8px }
    .gantt { border:1px solid #e2e8f0; border-radius:10px; padding:8px 12px; background:#fff }
    .scale { display:flex; justify-content:space-between; font-size:12px; color:#64748b; margin-left:230px; margin-bottom:4px }
    .grow { display:flex; align-items:center; height:28px } .grow.sub .glabel { padding-left:16px; color:#475569 }
    .glabel { width:230px; font-size:12.5px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; padding-right:8px }
    .gtrack { position:relative; flex:1; height:16px; background:#f1f5f9; border-radius:4px }
    .bar { position:absolute; top:0; height:16px; min-width:4px; background:#60a5fa; border-radius:4px } .bar.critical { background:#ef4444 } .bar.done { opacity:.45 }
    .swatch { display:inline-block; width:12px; height:12px; background:#60a5fa; border-radius:3px; margin:0 4px 0 12px; vertical-align:middle } .swatch.critical { background:#ef4444; margin-left:0 }
    .hint { color:#64748b; font-size:12.5px; margin-top:8px } .row { display:flex; gap:8px; margin-top:12px }
    .comment { border-bottom:1px solid #e2e8f0; padding:8px 0 } .comment p { margin:4px 0; white-space:pre-wrap } .when { color:#94a3b8; font-size:12px }
  `],
})
export class ProjectPlanPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly api = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  private readonly id = this.route.snapshot.paramMap.get('id')!;
  readonly project = signal<ProjectDto | null>(null);
  readonly schedule = signal<ScheduleDto | null>(null);
  readonly members = signal<MemberDto[]>([]);
  readonly comments = signal<CommentDto[]>([]);
  readonly changes = signal<ChangeOrderDto[]>([]);
  readonly risks = signal<RiskDto[]>([]);
  readonly users = toSignal(this.api.users().pipe(catchError(() => of([] as UserOption[]))), { initialValue: [] as UserOption[] });
  readonly cloneOpen = signal(false);
  readonly span = computed(() => { const s = this.schedule(); return s ? Math.max(1, this.days(s.start, s.finish) + 1) : 1; });
  newMember: string | null = null; newRole = ''; newComment = '';
  cloneName = ''; cloneStart = ''; cloneTeam = false; cloneTemplate = false;

  constructor() { this.load(); }

  health(h: string): { label: string; color: string } { return HEALTH[h] ?? HEALTH['closed']; }
  private days(a: string, b: string): number { return Math.round((new Date(b).getTime() - new Date(a).getTime()) / 864e5); }
  left(s: ScheduleDto, start: string): number { return (this.days(s.start, start) / this.span()) * 100; }
  width(s: ScheduleDto, t: { start: string; durationDays: number }): number { return Math.max(0.8, (t.durationDays / this.span()) * 100); }

  load(): void {
    this.projectsApi.get(this.id).subscribe((p) => { this.project.set(p); if (!this.cloneName) this.cloneName = `${p.name} (copy)`; });
    this.api.schedule(this.id).subscribe((s) => this.schedule.set(s));
    this.api.members(this.id).subscribe((m) => this.members.set(m));
    this.api.comments('project', this.id).subscribe((c) => this.comments.set(c));
    this.api.changeOrders(this.id).subscribe((c) => this.changes.set(c));
    this.api.risks(this.id).subscribe((r) => this.risks.set(r));
  }

  setDeps(taskId: string, deps: string[]): void {
    this.api.setDependencies(taskId, deps).subscribe({ next: () => this.api.schedule(this.id).subscribe((s) => this.schedule.set(s)), error: (e) => { this.msg.error(e?.error?.detail ?? 'Those dependencies are not allowed.'); this.api.schedule(this.id).subscribe((s) => this.schedule.set(s)); } });
  }
  addMember(): void { if (!this.newMember) return; this.api.addMember(this.id, this.newMember, this.newRole || null).subscribe({ next: () => { this.newMember = null; this.newRole = ''; this.load(); }, error: (e) => this.msg.error(e?.error?.detail ?? 'Could not add.') }); }
  removeMember(m: MemberDto): void { this.api.removeMember(m.id).subscribe(() => this.load()); }
  addComment(): void { this.api.addComment('project', this.id, this.newComment.trim()).subscribe({ next: () => { this.newComment = ''; this.load(); }, error: (e) => this.msg.error(e?.error?.detail ?? 'Could not post.') }); }
  deleteComment(c: CommentDto): void { this.api.deleteComment(c.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(e?.error?.detail ?? 'Could not delete.') }); }
  askSignOff(p: ProjectDto): void {
    this.api.requestAcceptance({ projectId: p.id, title: `Sign-off of ${p.name}` }).subscribe({
      next: () => this.msg.success('The customer can sign it in the portal'),
      error: (e) => this.msg.error(e?.error?.detail ?? e?.error?.message ?? 'Could not ask for the sign-off.'),
    });
  }
  postCosts(p: ProjectDto): void {
    this.api.postCosts(p.id).subscribe({
      next: (r) => this.msg.success(r.entries ? `Posted ${r.entries} entries (${r.amount.toFixed(2)}) to work in progress in journal ${r.journalNumber}` : 'Nothing new to post'),
      error: (e) => this.msg.error(e?.error?.detail ?? e?.error?.message ?? 'Could not post the costs.'),
    });
  }
  toggleArchive(p: ProjectDto): void { this.api.archive(p.id, !p.isArchived).subscribe({ next: (r) => { this.project.set(r); this.msg.success(r.isArchived ? 'Archived' : 'Restored'); }, error: (e) => this.msg.error(e?.error?.detail ?? 'Could not change.') }); }
  clone(p: ProjectDto): void {
    this.api.clone(p.id, { name: this.cloneName.trim() || `${p.name} (copy)`, startDate: this.cloneStart || null, includeTeam: this.cloneTeam, asTemplate: this.cloneTemplate }).subscribe({
      next: (c) => { this.cloneOpen.set(false); this.msg.success(`Created ${c.number}`); void this.router.navigate(['/projects', c.id]); },
      error: (e) => this.msg.error(e?.error?.detail ?? 'Could not clone.'),
    });
  }
}
