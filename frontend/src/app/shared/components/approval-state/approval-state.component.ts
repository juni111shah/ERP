import { ChangeDetectionStrategy, Component, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AdminApi, ApprovalDto } from '@core/api/admin.api';

/**
 * Where a document stands in approval: the request raised for it, the level waiting and who decided. Looks the request up by document id
 * among the approvals the person can see, so it shows nothing when there is none (or none visible). Turn `active` on only while the
 * document can be waiting, to spare a call.
 */
@Component({
  selector: 'erp-approval-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzTagModule, RouterLink],
  template: `
    @if (request(); as r) {
      <nz-tag [nzColor]="r.status === 'pending' ? 'orange' : r.status === 'approved' ? 'green' : 'red'" data-testid="approval-state">
        @if (r.status === 'pending') { Waiting for approval@if (r.currentLevel) { , level {{ r.currentLevel }} } }
        @else if (r.status === 'approved') { Approval granted }
        @else if (r.status === 'rejected') { Approval rejected }
        @else { Approval withdrawn }
      </nz-tag>
      <a routerLink="/approvals" style="font-size:12px">Open approvals</a>
    }`,
})
export class ApprovalStateComponent {
  readonly documentType = input.required<string>();
  readonly documentId = input.required<string>();
  readonly active = input(true);
  private readonly api = inject(AdminApi);
  readonly request = signal<ApprovalDto | null>(null);

  constructor() {
    effect(() => {
      const id = this.documentId();
      const type = this.documentType();
      if (!this.active()) { untracked(() => this.request.set(null)); return; }
      untracked(() => {
        // The inbox holds what this person can decide; "mine" holds what they asked for. A request can be in either.
        this.api.approvals().subscribe({
          next: (inbox) => {
            const hit = inbox.find((a) => a.documentId === id && a.documentType === type);
            if (hit) { this.request.set(hit); return; }
            this.api.approvals('mine').subscribe({ next: (mine) => this.request.set(mine.find((a) => a.documentId === id && a.documentType === type) ?? null), error: () => this.request.set(null) });
          },
          error: () => this.request.set(null),
        });
      });
    });
  }
}
