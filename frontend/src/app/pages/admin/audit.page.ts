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
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { AuditApi, AuditEvent, AuditEventDetail, AuditFacets, AuditFieldDiff } from '../../core/api/admin.api';
import { FilterCriterion } from '../../core/api/api.models';
import { createPagedList } from '../../core/api/paged-list';
import { auditFieldLabel, auditSummary, auditTone, auditValue, initials, relativeTime } from '../../core/format/audit-format';
import { displayDate, displayLabel } from '../../core/format/display';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { CsvExportService } from '../../core/services/csv-export.service';
import { ErpDatePipe, ErpLabelPipe } from '../../shared/display.pipes';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { recordLink } from '../../shared/record-links';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';

type RangeKey = 'all' | 'today' | '7' | '30';
const EXPORT_PAGE = 200;
const EXPORT_LIMIT = 5000;
const MANY_FIELDS = 12;

@Component({
  selector: 'erp-audit',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, ErpLabelPipe, FormsModule, NzButtonModule, NzDrawerModule, NzIconModule, NzInputModule, NzSegmentedModule, NzSkeletonModule, NzSwitchModule, NzTabsModule, NzTagModule, NzToolTipModule,
    PagedTableComponent, RouterLink, StatCardComponent, StatePanelComponent, TranslatePipe],
  providers: [provideNzIconsPatch([CopyOutline, DownOutline, DownloadOutline, ExportOutline, HistoryOutline, UpOutline])],
  styles: [`
    .scope { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 12px; margin-block-end: 12px; }
    .scope nz-segmented { max-width: 100%; }
    .chip { display: inline-flex; align-items: center; gap: 4px; }
    .who { display: inline-flex; align-items: center; gap: 8px; max-width: 100%; min-width: 0; }
    .avatar { flex: none; display: inline-flex; align-items: center; justify-content: center; width: 24px; height: 24px; border-radius: 50%; font-size: 11px; font-weight: 600; background: var(--erp-color-neutral-bg); color: var(--erp-color-neutral-text); }
    .avatar.system { background: var(--erp-color-bg-sunken); color: var(--erp-color-text-muted); }
    .record { min-width: 0; }
    .record-type { color: var(--erp-color-text-muted); }
    .mono { font-family: var(--erp-font-family-mono); font-size: 12px; }
    .muted { color: var(--erp-color-text-muted); }
    .head-title { margin: 0 0 4px; font-size: var(--erp-font-size-title-sm); line-height: var(--erp-line-height-title-sm); font-weight: 600; color: var(--erp-color-text-primary); overflow-wrap: anywhere; }
    .head-sub { margin: 0; color: var(--erp-color-text-muted); }
    .facts { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px 24px; margin: 16px 0; padding: 12px 0; border-block: 1px solid var(--erp-color-border-subtle); }
    .facts dt { font-size: 12px; color: var(--erp-color-text-muted); }
    .facts dd { margin: 0; min-width: 0; overflow-wrap: anywhere; }
    .facts .copy { margin-inline-start: 4px; }
    .tools { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; margin-block-end: 8px; }
    .tools label { display: inline-flex; align-items: center; gap: 8px; color: var(--erp-color-text-secondary); }
    .tools input { max-width: 220px; }
    table.diff { width: 100%; border-collapse: collapse; table-layout: fixed; }
    table.diff th { padding: 6px 8px; text-align: start; font-size: 12px; font-weight: 500; color: var(--erp-color-text-muted); border-block-end: 1px solid var(--erp-color-border-default); }
    table.diff td { padding: 8px; vertical-align: top; border-block-end: 1px solid var(--erp-color-border-subtle); overflow-wrap: anywhere; }
    table.diff .field { width: 30%; color: var(--erp-color-text-secondary); }
    table.diff tr.same td { color: var(--erp-color-text-muted); }
    .val { display: inline-block; max-width: 100%; padding: 1px 6px; border-radius: var(--erp-radius-sm); white-space: pre-wrap; }
    .val.old { background: var(--erp-color-error-bg); color: var(--erp-color-error-text); text-decoration: line-through; text-decoration-thickness: 1px; }
    .val.new { background: var(--erp-color-success-bg); color: var(--erp-color-success-text); }
    .val.empty { padding-inline: 0; color: var(--erp-color-text-muted); }
    pre.raw { margin: 0 0 12px; padding: 12px; max-height: 320px; overflow: auto; border-radius: var(--erp-radius-control); background: var(--erp-color-bg-subtle); border: 1px solid var(--erp-color-border-subtle); color: var(--erp-color-text-primary); font-size: 12px; direction: ltr; text-align: start; }
    h4.sub { margin: 20px 0 8px; font-size: 13px; font-weight: 600; color: var(--erp-color-text-secondary); }
    ul.related { margin: 0; padding: 0; list-style: none; }
    ul.related li { display: flex; align-items: center; gap: 8px; padding: 6px 0; border-block-end: 1px solid var(--erp-color-border-subtle); min-width: 0; }
    ul.related button { padding-inline: 0; text-align: start; }
    .drawer-nav { display: inline-flex; gap: 4px; }
  `],
  template: `
    <h1 class="erp-page-title">{{ 'ui.audit.title' | translate: 'Audit log' }}</h1>

    @if (facets(); as f) {
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="audit-summary">
        <erp-stat-card label="Changes today" [value]="f.today" format="number" [tone]="1" />
        <erp-stat-card label="Changes in 7 days" [value]="f.last7Days" format="number" [tone]="0" />
        <erp-stat-card label="Deletions in 7 days" [value]="f.deletionsLast7Days" format="number" [tone]="f.deletionsLast7Days ? 'danger' : 'neutral'" />
        <erp-stat-card label="People active in 7 days" [value]="f.activeUsersLast7Days" format="number" [tone]="2" />
      </div>
    }

    <div class="scope" data-testid="audit-scope">
      <nz-segmented [nzOptions]="ranges()" [ngModel]="range()" (ngModelChange)="setRange($event)" data-testid="audit-range"></nz-segmented>
      @if (recordScope(); as s) {
        <nz-tag class="erp-tone chip" data-tone="info" nzMode="closeable" (nzOnClose)="clearRecordScope()" data-testid="audit-record-scope">{{ s.recordType | erpLabel }} {{ s.label }}</nz-tag>
      }
    </div>

    <erp-paged-table [list]="list" [columns]="columns()" [exportable]="false" searchPlaceholder="Search record, field or IP address" emptyText="No changes have been recorded yet" (rowClick)="open($event)" tableName="audit">
      <button toolbar nz-button type="button" [nzLoading]="exporting()" [disabled]="!list.total()" (click)="exportAll()" nz-tooltip [nzTooltipTitle]="'ui.audit.exportHint' | translate: 'Every event matching these filters'" data-testid="audit-export">
        <span nz-icon nzType="download"></span> {{ 'table.exportCsv' | translate: 'Export CSV' }}
      </button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('userId') {
            <span class="who"><span class="avatar" [class.system]="!row.userId" aria-hidden="true">{{ initialsOf(row) }}</span><span class="erp-truncate">{{ whoOf(row) }}</span></span>
          }
          @case ('action') { <nz-tag class="erp-tone" [attr.data-tone]="tone(row.action)">{{ row.action | erpLabel }}</nz-tag> }
          @case ('record') {
            <span class="record erp-truncate"><span class="record-type">{{ row.recordType | erpLabel }}</span> {{ row.recordLabel || shortId(row.recordId) }}</span>
          }
          @case ('changedFields') { <span class="erp-truncate">{{ changeOf(row) }}</span> }
          @case ('actions') {
            @if (linkOf(row); as link) {
              <a nz-button nzType="text" nzSize="small" [routerLink]="link" (click)="$event.stopPropagation()" nz-tooltip [nzTooltipTitle]="'ui.audit.openRecord' | translate: 'Open record'" [attr.aria-label]="'ui.audit.openRecord' | translate: 'Open record'" data-testid="audit-row-open"><span nz-icon nzType="export"></span></a>
            }
            <button nz-button nzType="text" nzSize="small" type="button" (click)="scopeToRecord(row)" nz-tooltip [nzTooltipTitle]="'ui.audit.recordHistory' | translate: 'All changes to this record'" [attr.aria-label]="'ui.audit.recordHistory' | translate: 'All changes to this record'" data-testid="audit-row-history"><span nz-icon nzType="history"></span></button>
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

    <nz-drawer [nzVisible]="!!selected()" [nzTitle]="'ui.audit.event' | translate: 'Audit event'" [nzExtra]="navigation" nzWidth="720px" (nzOnClose)="close()" data-testid="audit-drawer">
      <ng-template #navigation>
        <span class="drawer-nav">
          <button nz-button type="button" nzSize="small" [disabled]="!hasPrevious()" (click)="step(-1)" nz-tooltip [nzTooltipTitle]="'ui.audit.newer' | translate: 'Newer event'" [attr.aria-label]="'ui.audit.newer' | translate: 'Newer event'" data-testid="audit-newer"><span nz-icon nzType="up"></span></button>
          <button nz-button type="button" nzSize="small" [disabled]="!hasNext()" (click)="step(1)" nz-tooltip [nzTooltipTitle]="'ui.audit.older' | translate: 'Older event'" [attr.aria-label]="'ui.audit.older' | translate: 'Older event'" data-testid="audit-older"><span nz-icon nzType="down"></span></button>
        </span>
      </ng-template>
      <ng-container *nzDrawerContent>
        @if (selected(); as e) {
          <p class="head-title" data-testid="audit-headline">{{ headline(e) }}</p>
          <p class="head-sub"><span>{{ e.occurredAt | erpDate }}</span> · <span>{{ relative(e.occurredAt) }}</span></p>

          <dl class="facts">
            <div><dt>{{ 'ui.audit.by' | translate: 'By' }}</dt><dd><span class="who"><span class="avatar" [class.system]="!e.userId" aria-hidden="true">{{ initialsOf(e) }}</span>{{ whoOf(e) }}</span></dd></div>
            <div>
              <dt>{{ 'ui.audit.record' | translate: 'Record' }}</dt>
              <dd>
                {{ e.recordType | erpLabel }} @if (e.recordLabel) { {{ e.recordLabel }} }
                @if (linkOf(e); as link) { <div><a [routerLink]="link" data-testid="audit-open-record">{{ 'ui.audit.openRecord' | translate: 'Open record' }}</a></div> }
              </dd>
            </div>
            <div>
              <dt>{{ 'ui.audit.recordId' | translate: 'Record ID' }}</dt>
              <dd><bdi class="mono" dir="ltr">{{ e.recordId }}</bdi><button class="copy" nz-button nzType="text" nzSize="small" type="button" (click)="copy(e.recordId)" nz-tooltip [nzTooltipTitle]="'ui.audit.copy' | translate: 'Copy'" [attr.aria-label]="'ui.audit.copy' | translate: 'Copy'" data-testid="audit-copy-id"><span nz-icon nzType="copy"></span></button></dd>
            </div>
            <div><dt>{{ 'ui.audit.ipAddress' | translate: 'IP address' }}</dt><dd><bdi dir="ltr">{{ e.ipAddress || '—' }}</bdi></dd></div>
          </dl>

          @if (detailLoading()) {
            <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 5 }" data-testid="audit-detail-loading"></nz-skeleton>
          } @else if (detailFailed()) {
            <erp-state-panel kind="error" title="Could not load this event" hint="The list is still up to date." actionLabel="Try again" (action)="load(e)" />
          } @else if (detail(); as d) {
            <nz-tabset nzSize="small">
              <nz-tab [nzTitle]="changesTab(d)">
                <div class="tools">
                  @if (unchangedCount(d)) {
                    <label><nz-switch nzSize="small" [ngModel]="showUnchanged()" (ngModelChange)="showUnchanged.set($event)" data-testid="audit-show-unchanged"></nz-switch>{{ 'ui.audit.showUnchanged' | translate: 'Show unchanged fields' }} ({{ unchangedCount(d) }})</label>
                  } @else { <span></span> }
                  @if (d.fields.length > manyFields) {
                    <input nz-input nzSize="small" [attr.aria-label]="'ui.audit.findField' | translate: 'Find a field'" [placeholder]="'ui.audit.findField' | translate: 'Find a field'" [ngModel]="fieldSearch()" (ngModelChange)="fieldSearch.set($event)" data-testid="audit-find-field" />
                  }
                </div>
                @if (visibleFields(d); as rows) {
                  @if (rows.length) {
                    <table class="diff" data-testid="audit-diff">
                      <thead><tr>
                        <th class="field">{{ 'ui.audit.field' | translate: 'Field' }}</th>
                        @if (e.action !== 'Created') { <th>{{ 'ui.audit.before' | translate: 'Before' }}</th> }
                        @if (e.action !== 'Deleted') { <th>{{ 'ui.audit.after' | translate: 'After' }}</th> }
                      </tr></thead>
                      <tbody>
                        @for (f of rows; track f.field) {
                          <tr [class.same]="!f.changed" [attr.data-testid]="f.changed ? 'audit-diff-changed' : 'audit-diff-same'">
                            <td class="field">{{ fieldLabel(f.field) }}</td>
                            @if (e.action !== 'Created') { <td><span class="val" [class.old]="f.changed && !!f.old" [class.empty]="!f.old" dir="auto">{{ shown(f.old) }}</span></td> }
                            @if (e.action !== 'Deleted') { <td><span class="val" [class.new]="f.changed && !!f.new" [class.empty]="!f.new" dir="auto">{{ shown(f.new) }}</span></td> }
                          </tr>
                        }
                      </tbody>
                    </table>
                  } @else {
                    <p class="muted" data-testid="audit-no-fields">{{ noFieldsText(e) }}</p>
                  }
                }

                @if (d.sameSave.length) {
                  <h4 class="sub">{{ 'ui.audit.sameSave' | translate: 'Saved at the same moment' }} ({{ d.sameSave.length }})</h4>
                  <ul class="related" data-testid="audit-same-save">
                    @for (r of d.sameSave; track r.id) {
                      <li>
                        <nz-tag class="erp-tone" [attr.data-tone]="tone(r.action)">{{ r.action | erpLabel }}</nz-tag>
                        <button nz-button nzType="link" nzSize="small" type="button" (click)="open(r)" data-testid="audit-related-open">{{ r.recordType | erpLabel }} {{ r.recordLabel || shortId(r.recordId) }}</button>
                      </li>
                    }
                  </ul>
                }

                @if (d.recordEventCount > 1) {
                  <h4 class="sub">{{ 'ui.audit.moreOnRecord' | translate: 'More on this record' }}</h4>
                  <button nz-button type="button" (click)="scopeToRecord(e); close()" data-testid="audit-record-history"><span nz-icon nzType="history"></span> {{ 'ui.audit.seeAll' | translate: 'See all' }} {{ d.recordEventCount }} {{ 'ui.audit.changes' | translate: 'changes' }}</button>
                }
              </nz-tab>
              <nz-tab [nzTitle]="'ui.audit.rawData' | translate: 'Raw data'">
                @if (e.action !== 'Created') {
                  <h4 class="sub">{{ 'ui.audit.before' | translate: 'Before' }}</h4>
                  <pre class="raw" data-testid="audit-before" dir="ltr">{{ pretty(d.before) }}</pre>
                }
                @if (e.action !== 'Deleted') {
                  <h4 class="sub">{{ 'ui.audit.after' | translate: 'After' }}</h4>
                  <pre class="raw" data-testid="audit-after" dir="ltr">{{ pretty(d.after) }}</pre>
                }
              </nz-tab>
            </nz-tabset>
          }
        }
      </ng-container>
    </nz-drawer>`,
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
  readonly recordScope = signal<{ recordType: string; recordId: string; label: string } | null>(null);
  readonly exporting = signal(false);

  readonly selected = signal<AuditEvent | null>(null);
  readonly detail = signal<AuditEventDetail | null>(null);
  readonly detailLoading = signal(false);
  readonly detailFailed = signal(false);
  readonly showUnchanged = signal(false);
  readonly fieldSearch = signal('');

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
      { key: 'record', label: 'Record', sortField: 'recordType', value: (e) => `${displayLabel(e.recordType)} ${e.recordLabel ?? e.recordId}`, filter: 'select', filterField: 'recordType', filterValue: (e) => e.recordType, filterOptions: f?.recordTypes.map((t) => ({ value: t, label: displayLabel(t) })) },
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
    if (recordType && recordId) this.recordScope.set({ recordType, recordId, label: q.get('label') ?? this.shortId(recordId) });
    if (recordType && recordId) this.applyScope();
  }

  // ---------- scope ----------
  setRange(key: RangeKey): void {
    this.range.set(key);
    this.applyScope();
  }

  scopeToRecord(e: AuditEvent): void {
    this.recordScope.set({ recordType: e.recordType, recordId: e.recordId, label: e.recordLabel ?? this.shortId(e.recordId) });
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
  fieldLabel = auditFieldLabel;
  relative = relativeTime;
  shown = auditValue;
  shortId(id: string): string { return id.length > 12 ? `${id.slice(0, 8)}…` : id; }
  linkOf(e: AuditEvent): unknown[] | null { return recordLink(e.recordType, e.recordId, e.recordKind); }
  changeOf(e: AuditEvent): string { return auditSummary(e, (key, fallback) => this.i18n.t(key, fallback)); }
  headline(e: AuditEvent): string {
    const record = `${displayLabel(e.recordType)}${e.recordLabel ? ` ${e.recordLabel}` : ''}`;
    return this.i18n.lang() === 'ar' ? `${this.whoOf(e)}: ${displayLabel(e.action)} ${record}` : `${this.whoOf(e)} ${e.action.toLowerCase()} ${record}`;
  }

  // ---------- drawer ----------
  open(row: AuditEvent): void {
    this.selected.set(row);
    this.showUnchanged.set(false);
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

  unchangedCount(d: AuditEventDetail): number { return d.event.action === 'Updated' ? d.fields.filter((f) => !f.changed).length : 0; }
  changesTab(d: AuditEventDetail): string {
    const count = d.fields.filter((f) => f.changed).length;
    return `${d.event.action === 'Updated' ? this.i18n.t('ui.audit.changesTab', 'Changes') : this.i18n.t('ui.audit.valuesTab', 'Values')} (${count})`;
  }

  visibleFields(d: AuditEventDetail): AuditFieldDiff[] {
    const term = this.fieldSearch().trim().toLowerCase();
    const showAll = d.event.action !== 'Updated' || this.showUnchanged();
    return d.fields.filter((f) => (showAll || f.changed) && (!term || this.fieldLabel(f.field).toLowerCase().includes(term)));
  }

  noFieldsText(e: AuditEvent): string {
    if (this.fieldSearch().trim()) return this.i18n.t('ui.audit.noFieldMatch', 'No field matches that.');
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
