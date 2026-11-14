import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ActivityDto, CommentDto, ProjectsExtraApi } from '@core/api/projects-extra.api';
import { SessionStore } from '@core/session/session.store';
import { AttachmentsComponent } from '@shared/components/attachments/attachments.component';
import { ErpLabelPipe } from '@shared/pipes/display.pipes';
import { loadOrFallback } from '@shared/utils/load-or-fallback';
import { errorText, PRJ_STYLES, PRJ_SUBTABS } from '../../projects-ui';
import { fieldWords } from '../../projects-view';

/** Discussion, files and what happened on the project. */
@Component({
  selector: 'erp-project-notes',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, DatePipe, FormsModule, NzButtonModule, NzIconModule, NzInputModule, AttachmentsComponent],
  templateUrl: './project-notes.component.html',
  styles: [PRJ_STYLES + PRJ_SUBTABS + `
    .comment { border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); padding:8px 0; } .comment p { margin:4px 0 0; white-space:pre-wrap; } .row { display:flex; gap:8px; margin-top:12px; align-items:flex-start; }
    .event { display:flex; gap:10px; padding:7px 0; font-size:13px; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); } .dot { width:9px; height:9px; border-radius:50%; background:#60a5fa; margin-top:5px; flex:none; } .dot.created { background:#10955d; } .dot.deleted { background:#ef4444; }
  `],
})
export class ProjectNotesComponent {
  readonly projectId = input.required<string>();
  private readonly api = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  /** Files needs read access to attachments; the tab is left out without it. */
  readonly views = computed(() => [{ key: 'discussion', label: 'Discussion' }, ...(this.session.hasPermission('core.attachments.read') ? [{ key: 'files', label: 'Files' }] : []), { key: 'activity', label: 'Activity' }]);
  readonly view = signal('discussion');
  readonly comments = signal<CommentDto[]>([]);
  readonly activity = signal<ActivityDto[]>([]);
  newComment = '';

  constructor() { effect(() => { this.projectId(); this.load(); }); }

  load(): void {
    loadOrFallback(this.comments, this.api.comments('project', this.projectId()), []);
    loadOrFallback(this.activity, this.api.activity(this.projectId()), []);
  }

  canSeeFiles(): boolean { return this.session.hasPermission('core.attachments.read'); }
  /** Authors delete their own comments; project managers delete any. */
  canDelete(c: CommentDto): boolean { return c.userId === this.session.me()?.user?.id || this.session.hasPermission('projects.projects.manage'); }

  addComment(): void {
    this.api.addComment('project', this.projectId(), this.newComment.trim()).subscribe({
      next: () => { this.newComment = ''; this.load(); },
      error: (e) => this.msg.error(errorText(e, 'Could not post.')),
    });
  }

  removeComment(c: CommentDto): void {
    this.api.deleteComment(c.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) });
  }

  verb(a: ActivityDto): string { return a.action === 'Created' ? 'added' : a.action === 'Deleted' ? 'removed' : 'changed'; }
  fieldList(a: ActivityDto): string { return a.fields.map(fieldWords).join(', '); }
}
