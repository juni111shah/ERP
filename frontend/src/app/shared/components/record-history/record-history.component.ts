import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { API_BASE_URL } from '@core/api/api-config';
import { PagedResult } from '@core/api/api.models';
import { TranslatePipe } from '@core/i18n/translate.pipe';

interface FieldChange { field: string; old?: string | null; new?: string | null; }
interface HistoryEntry { id: string; occurredAt: string; userId?: string; userName?: string; action: string; changes: FieldChange[]; }

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
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  readonly entries = signal<HistoryEntry[]>([]);
  readonly loading = signal(false);
  readonly hasMore = signal(false);
  private page = 1;

  constructor() {
    effect(() => { this.recordType(); this.recordId(); this.page = 1; this.entries.set([]); this.load(); });
  }

  more(): void { this.page++; this.load(); }

  label(action: string): string {
    const map: Record<string, string> = { Created: 'Created', Updated: 'Updated', Deleted: 'Deleted' };
    return map[action] ?? action;
  }

  humanize(field: string): string { return field.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/Id$/, '').trim(); }

  private load(): void {
    const id = this.recordId();
    if (!id) return;
    this.loading.set(true);
    this.http.get<PagedResult<HistoryEntry>>(`${this.base}/audit/record`, { params: { recordType: this.recordType(), recordId: id, page: this.page, pageSize: 20 } }).subscribe({
      next: (r) => { this.entries.update((all) => [...all, ...r.items]); this.hasMore.set(this.page < Math.ceil(r.total / 20)); this.loading.set(false); },
      error: () => { this.loading.set(false); this.hasMore.set(false); },
    });
  }
}
