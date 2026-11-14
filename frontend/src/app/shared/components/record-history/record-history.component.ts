import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { HistoryEntry, RecordHistoryApi } from '@core/api/record-history.api';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';

/** Entries fetched per "more" click. */
const PAGE_SIZE = 20;
const ACTION_LABELS: Record<string, string> = { Created: 'Created', Updated: 'Updated', Deleted: 'Deleted' };

/**
 * Who changed a record and when, with each field from old to new (from the audit trail, newest first).
 *   <erp-record-history recordType="Invoice" [recordId]="id" />
 * Needs `core.audit.read`; hosts hide it without that permission (see DetailPageComponent).
 */
@Component({
  selector: 'erp-record-history',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, TranslatePipe, NzButtonModule, NzSkeletonModule, NzTagModule],
  styleUrl: './record-history.component.scss',
  templateUrl: './record-history.component.html',
})
export class RecordHistoryComponent {
  /** The record class name used by the audit trail, for example "Invoice", "Bill", "Party", "Lease", "Property". */
  readonly recordType = input.required<string>();
  readonly recordId = input.required<string>();
  private readonly historyApi = inject(RecordHistoryApi);
  private readonly session = inject(SessionStore);
  /** GET /audit/record needs the audit permission; without it nothing is asked. */
  readonly canRead = computed(() => this.session.hasPermission('core.audit.read'));
  readonly entries = signal<HistoryEntry[]>([]);
  readonly loading = signal(false);
  readonly hasMore = signal(false);
  private page = 1;

  constructor() {
    effect(() => {
      this.recordType(); this.recordId();
      const allowed = this.canRead();
      untracked(() => { this.page = 1; this.entries.set([]); this.hasMore.set(false); if (allowed) this.load(); });
    });
  }

  more(): void { this.page++; this.load(); }

  label(action: string): string { return ACTION_LABELS[action] ?? action; }

  /** "legalEntityId" becomes "legal Entity": the field name split into words, without a trailing "Id". */
  humanize(field: string): string { return field.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/Id$/, '').trim(); }

  private load(): void {
    const id = this.recordId();
    if (!id) return;
    this.loading.set(true);
    this.historyApi.list(this.recordType(), id, this.page, PAGE_SIZE).subscribe({
      next: (r) => { this.entries.update((all) => [...all, ...r.items]); this.hasMore.set(this.page < Math.ceil(r.total / PAGE_SIZE)); this.loading.set(false); },
      error: () => { this.loading.set(false); this.hasMore.set(false); },
    });
  }
}
