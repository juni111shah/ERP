import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, catchError, expand, of, reduce } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { CopyOutline, DownOutline, DownloadOutline, ExportOutline, HistoryOutline, UpOutline } from '@ant-design/icons-angular/icons';
import { NzIconModule, provideNzIconsPatch } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSegmentedModule } from 'ng-zorro-antd/segmented';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { AuditApi, AuditEvent, AuditEventDetail, AuditFacets, AuditFieldDiff } from '@core/api/admin.api';
import { FilterCriterion } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { auditFieldLabel, auditIsCode, auditIsId, auditIsPrimary, auditRecordType, auditSummary, auditTone, auditValue, initials, relativeTime } from '@core/format/audit-format';
import { displayDate, displayLabel } from '@core/format/display';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { CsvExportService } from '@core/services/csv-export.service';
import { ErpDatePipe, ErpLabelPipe } from '@shared/pipes/display.pipes';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { recordLink } from '@shared/utils/record-links';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';

type RangeKey = 'all' | 'today' | '7' | '30';
const EXPORT_PAGE = 200;
const EXPORT_LIMIT = 5000;
const MANY_FIELDS = 12;
const SAME_SAVE_SHOWN = 5;

@Component({
  selector: 'erp-audit',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, ErpLabelPipe, FormsModule, NzButtonModule, NzDrawerModule, NzIconModule, NzInputModule, NzSegmentedModule, NzSkeletonModule, NzTabsModule, NzTagModule, NzToolTipModule,
    PagedTableComponent, RouterLink, StatCardComponent, StatePanelComponent, TranslatePipe],
  providers: [provideNzIconsPatch([CopyOutline, DownOutline, DownloadOutline, ExportOutline, HistoryOutline, UpOutline])],
  styleUrl: './audit.page.scss',
  templateUrl: './audit.page.html',
})
export class AuditPage implements OnInit {
  private readonly api = inject(AuditApi);
  private readonly csv = inject(CsvExportService);
  private readonly i18n = inject(I18nService);
  private readonly message = inject(NzMessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly manyFields = MANY_FIELDS;
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-occurredAt' });
  readonly facets = signal<AuditFacets | null>(null);
  readonly range = signal<RangeKey>('all');
  readonly recordScope = signal<{ recordType: string; recordId: string; typeLabel: string; label: string } | null>(null);
  readonly exporting = signal(false);

  readonly selected = signal<AuditEvent | null>(null);
  readonly detail = signal<AuditEventDetail | null>(null);
  readonly detailLoading = signal(false);
  readonly detailFailed = signal(false);
  readonly showUnchanged = signal(false);
  readonly fieldSearch = signal('');
  readonly sameSaveOpen = signal(false);
  readonly sameSaveShown = SAME_SAVE_SHOWN;

  readonly ranges = computed(() => {
    const t = (key: string, fallback: string) => this.i18n.t(key, fallback);
    return [
      { value: 'all', label: t('ui.audit.rangeAll', 'All time') },
      { value: 'today', label: t('ui.audit.rangeToday', 'Today') },
      { value: '7', label: t('ui.audit.range7', '7 days') },
      { value: '30', label: t('ui.audit.range30', '30 days') },
    ];
  });

  readonly columns = computed((): TableColumn<AuditEvent>[] => {
    const f = this.facets();
    return [
      { key: 'occurredAt', label: 'When', sortField: 'occurredAt', width: '170px', value: (e) => displayDate(e.occurredAt), filter: 'date', filterField: 'occurredAt', filterValue: (e) => e.occurredAt },
      { key: 'userId', label: 'By', width: '200px', value: (e) => this.whoOf(e), filter: 'select', filterField: 'userId', filterValue: (e) => e.userId, filterOptions: f?.users.map((u) => ({ value: u.id, label: u.name })) },
      { key: 'action', label: 'Action', width: '110px', filter: 'select', filterField: 'action', filterOptions: f?.actions.map((a) => ({ value: a, label: displayLabel(a) })) },
      { key: 'record', label: 'Record', sortField: 'recordType', value: (e) => `${this.typeOf(e)} ${e.recordLabel ?? e.recordId}`, filter: 'select', filterField: 'recordType', filterValue: (e) => e.recordType, filterOptions: f?.recordTypes.map((t) => ({ value: t, label: auditRecordType(t) })) },
      { key: 'changedFields', label: 'What changed', value: (e) => this.changeOf(e), filter: false },
      { key: 'ipAddress', label: 'IP address', width: '140px', hidden: true, filter: 'text', filterField: 'ipAddress' },
      { key: 'actions', label: '', width: '96px', filter: false, value: () => '' },
    ];
  });

  ngOnInit(): void {
    this.api.facets().pipe(catchError(() => of(null))).subscribe((f) => this.facets.set(f));
    // `?recordType=&recordId=` opens the log scoped to one record, for links from other screens.
    const q = this.route.snapshot.queryParamMap;
    const recordType = q.get('recordType'), recordId = q.get('recordId');
    if (recordType && recordId) this.recordScope.set({ recordType, recordId, typeLabel: auditRecordType(recordType), label: q.get('label') ?? this.shortId(recordId) });
    if (recordType && recordId) this.applyScope();
  }

  // ---------- scope ----------
  setRange(key: RangeKey): void {
    this.range.set(key);
    this.applyScope();
  }

  scopeToRecord(e: AuditEvent): void {
    this.recordScope.set({ recordType: e.recordType, recordId: e.recordId, typeLabel: this.typeOf(e), label: e.recordLabel ?? this.shortId(e.recordId) });
    this.range.set('all');
    this.applyScope();
  }

  clearRecordScope(): void {
    this.recordScope.set(null);
    void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
    this.applyScope();
  }

  private scopeFilters(): FilterCriterion[] {
    const out: FilterCriterion[] = [];
    const scope = this.recordScope();
    if (scope) out.push({ field: 'recordType', op: 'eq', value: scope.recordType }, { field: 'recordId', op: 'eq', value: scope.recordId });
    const from = this.rangeStart(this.range());
    if (from) out.push({ field: 'occurredAt', op: 'gte', value: from });
    return out;
  }

  private applyScope(): void { this.list.setFilters(this.scopeFilters()); }

  private rangeStart(key: RangeKey): string | null {
    if (key === 'all') return null;
    const start = new Date();
    start.setDate(start.getDate() - (key === 'today' ? 0 : Number(key) - 1));
    start.setHours(0, 0, 0, 0);
    return start.toISOString();
  }

  // ---------- row words ----------
  whoOf(e: AuditEvent): string { return e.userId ? e.userName ?? this.i18n.t('ui.audit.unknownUser', 'Unknown user') : this.i18n.t('history.system', 'System'); }
  initialsOf(e: AuditEvent): string { return e.userId ? initials(e.userName) : 'S'; }
  tone = auditTone;
  typeOf(e: AuditEvent): string { return auditRecordType(e.recordType, e.recordKind); }
  fieldLabel = auditFieldLabel;
  relative = relativeTime;
  isId = auditIsId;
  isCode = auditIsCode;
  /** A stored value in words, with a long id shortened (the tooltip has it in full). */
  valueText(value: string | null | undefined): string { return auditIsId(value) ? this.shortId(value!) : auditValue(value); }
  shortId(id: string): string { return id.length > 12 ? `${id.slice(0, 8)}…` : id; }
  linkOf(e: AuditEvent): unknown[] | null { return recordLink(e.recordType, e.recordId, e.recordKind); }
  changeOf(e: AuditEvent): string { return auditSummary(e, (key, fallback) => this.i18n.t(key, fallback)); }

  // ---------- drawer ----------
  open(row: AuditEvent): void {
    this.selected.set(row);
    this.showUnchanged.set(false);
    this.sameSaveOpen.set(false);
    this.fieldSearch.set('');
    this.load(row);
  }

  close(): void { this.selected.set(null); this.detail.set(null); }

  load(row: AuditEvent): void {
    this.detail.set(null);
    this.detailFailed.set(false);
    this.detailLoading.set(true);
    this.api.detail(row.id).subscribe({
      next: (d) => { if (this.selected()?.id === row.id) { this.detail.set(d); this.detailLoading.set(false); } },
      error: () => { if (this.selected()?.id === row.id) { this.detailFailed.set(true); this.detailLoading.set(false); } },
    });
  }

  private indexOfSelected(): number { return this.list.items().findIndex((e) => e.id === this.selected()?.id); }
  hasPrevious(): boolean { return this.indexOfSelected() > 0; }
  hasNext(): boolean { const i = this.indexOfSelected(); return i >= 0 && i < this.list.items().length - 1; }
  step(by: number): void {
    const next = this.list.items()[this.indexOfSelected() + by];
    if (next) this.open(next);
  }

  /** Up and down walk the events while the drawer is open, unless the person is typing. */
  @HostListener('document:keydown', ['$event'])
  onKey(e: KeyboardEvent): void {
    if (!this.selected() || (e.key !== 'ArrowUp' && e.key !== 'ArrowDown')) return;
    if ((e.target as HTMLElement).closest('input, textarea, pre, [contenteditable]')) return;
    this.step(e.key === 'ArrowUp' ? -1 : 1);
    e.preventDefault();
  }

  hasFooter(): boolean {
    const e = this.selected();
    return !!e && (!!this.linkOf(e) || (this.detail()?.recordEventCount ?? 0) > 1);
  }

  position(): string | null {
    const i = this.indexOfSelected();
    return i < 0 ? null : `${i + 1} / ${this.list.items().length}`;
  }

  /** What the event is about: the fields it changed, or for a new or removed record the values it held. */
  primaryFields(d: AuditEventDetail): AuditFieldDiff[] { return d.fields.filter((f) => auditIsPrimary(d.event.action, f)); }
  /** Everything else, folded away: fields left alone, or fields with no value. */
  restFields(d: AuditEventDetail): AuditFieldDiff[] { return d.fields.filter((f) => !auditIsPrimary(d.event.action, f)); }

  visibleSameSave(d: AuditEventDetail): AuditEvent[] { return this.sameSaveOpen() ? d.sameSave : d.sameSave.slice(0, SAME_SAVE_SHOWN); }

  matchingRest(d: AuditEventDetail): AuditFieldDiff[] {
    const term = this.fieldSearch().trim().toLowerCase();
    return this.restFields(d).filter((f) => !term || this.fieldLabel(f.field).toLowerCase().includes(term));
  }

  changesTab(d: AuditEventDetail): string {
    return `${d.event.action === 'Updated' ? this.i18n.t('ui.audit.changesTab', 'Changes') : this.i18n.t('ui.audit.valuesTab', 'Values')} (${this.primaryFields(d).length})`;
  }

  noFieldsText(e: AuditEvent): string {
    return e.action === 'Updated' ? this.i18n.t('ui.audit.onlyBookkeeping', 'Only bookkeeping values such as the last-updated time changed.') : this.i18n.t('ui.audit.noSnapshot', 'No field values were kept for this event.');
  }

  pretty(json?: string | null): string {
    if (!json) return '—';
    try { return JSON.stringify(JSON.parse(json), null, 2); } catch { return json; }
  }

  copy(text: string): void {
    void navigator.clipboard?.writeText(text).then(() => this.message.success(`${this.i18n.t('ui.audit.copied', 'Copied')} ${this.shortId(text)}`));
  }

  // ---------- export ----------
  /** Every event matching the filters rather than just this page, up to a limit a browser can handle. */
  exportAll(): void {
    const base = { ...this.list.query(), pageSize: EXPORT_PAGE };
    this.exporting.set(true);
    this.api.list({ ...base, page: 1 }).pipe(
      expand((r) => (r.page * EXPORT_PAGE < Math.min(r.total, EXPORT_LIMIT) ? this.api.list({ ...base, page: r.page + 1 }) : EMPTY)),
      reduce((rows: AuditEvent[], r) => [...rows, ...r.items], []),
    ).subscribe({
      next: (rows) => {
        this.exporting.set(false);
        this.csv.exportToCsv(rows.map((e) => ({
          occurredAt: e.occurredAt, by: this.whoOf(e), action: e.action, recordType: e.recordType, record: e.recordLabel ?? '', recordId: e.recordId, changedFields: e.changedFields.join(', '), ipAddress: e.ipAddress ?? '',
        })), 'audit-log', [
          { field: 'occurredAt', header: 'When (UTC)' }, { field: 'by', header: 'By' }, { field: 'action', header: 'Action' }, { field: 'recordType', header: 'Record type' },
          { field: 'record', header: 'Record' }, { field: 'recordId', header: 'Record ID' }, { field: 'changedFields', header: 'Changed fields' }, { field: 'ipAddress', header: 'IP address' },
        ]);
        this.message.success(`audit-log.csv: ${rows.length} ${this.i18n.t('table.rowsExported', 'rows exported')}`);
      },
      error: () => { this.exporting.set(false); this.message.error(this.i18n.t('ui.audit.exportFailed', 'Could not export the audit log. Try again.')); },
    });
  }
}
