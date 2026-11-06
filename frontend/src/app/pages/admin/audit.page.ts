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
import { AuditApi, AuditEvent, AuditEventDetail, AuditFacets, AuditFieldDiff } from '../../core/api/admin.api';
import { FilterCriterion } from '../../core/api/api.models';
import { createPagedList } from '../../core/api/paged-list';
import { auditFieldLabel, auditIsCode, auditIsId, auditIsPrimary, auditRecordType, auditSummary, auditTone, auditValue, initials, relativeTime } from '../../core/format/audit-format';
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
const SAME_SAVE_SHOWN = 5;

@Component({
  selector: 'erp-audit',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, ErpLabelPipe, FormsModule, NzButtonModule, NzDrawerModule, NzIconModule, NzInputModule, NzSegmentedModule, NzSkeletonModule, NzTabsModule, NzTagModule, NzToolTipModule,
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
    .title-row { display: flex; align-items: center; gap: 8px; min-width: 0; }
    .title-row nz-tag { flex: none; margin: 0; }
    .title-text { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; color: var(--erp-color-text-primary); }
    .title-name { font-weight: 400; color: var(--erp-color-text-secondary); }
    .position { margin-inline-end: 4px; font-size: 12px; font-variant-numeric: tabular-nums; color: var(--erp-color-text-muted); white-space: nowrap; }
    .summary { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 12px; padding-block-end: 16px; margin-block-end: 8px; border-block-end: 1px solid var(--erp-color-border-subtle); }
    .avatar.large { width: 36px; height: 36px; font-size: 13px; }
    .summary-main { flex: 1 1 240px; min-width: 0; }
    .summary-who { font-weight: 600; color: var(--erp-color-text-primary); overflow-wrap: anywhere; }
    .summary-when { font-size: var(--erp-font-size-body-sm); line-height: var(--erp-line-height-body-sm); color: var(--erp-color-text-muted); }
    .summary-id { display: inline-flex; align-items: center; gap: 6px; flex: none; }
    .summary-id-label { font-size: 12px; color: var(--erp-color-text-muted); }
    table.diff { width: 100%; border-collapse: collapse; table-layout: fixed; font-variant-numeric: tabular-nums; }
    table.diff th { padding: 8px; text-align: start; font-size: 12px; font-weight: 500; color: var(--erp-color-text-muted); border-block-end: 1px solid var(--erp-color-border-default); }
    table.diff td { padding: 10px 8px; vertical-align: top; border-block-end: 1px solid var(--erp-color-border-subtle); overflow-wrap: anywhere; }
    table.diff .field { width: 34%; color: var(--erp-color-text-secondary); }
    table.diff.rest { margin-block-start: 4px; }
    table.diff.rest td { padding-block: 8px; color: var(--erp-color-text-muted); }
    table.diff.rest .field { color: var(--erp-color-text-muted); }
    .val { display: inline-block; max-width: 100%; max-height: 240px; overflow: auto; padding: 1px 6px; border-radius: var(--erp-radius-sm); white-space: pre-wrap; }
    .val.old { background: var(--erp-color-error-bg); color: var(--erp-color-error-text); text-decoration: line-through; text-decoration-thickness: 1px; }
    .val.new { background: var(--erp-color-success-bg); color: var(--erp-color-success-text); }
    .val.empty { padding-inline: 0; color: var(--erp-color-text-muted); }
    .more-toggle { display: inline-flex; align-items: center; gap: 6px; margin-block-start: 12px; padding: 4px 0; border: 0; background: none; font: inherit; color: var(--erp-color-primary-text); cursor: pointer; }
    .more-toggle:hover { text-decoration: underline; }
    .more-toggle .count { color: var(--erp-color-text-muted); }
    .find { display: block; max-width: 240px; margin-block: 8px 4px; }
    @container erp-drawer (max-width: 480px) {
      table.diff, table.diff tbody, table.diff tr, table.diff td { display: block; width: auto; }
      table.diff thead { display: none; }
      table.diff tr { padding-block: 8px; border-block-end: 1px solid var(--erp-color-border-subtle); }
      table.diff td { padding: 2px 0; border: 0; }
      table.diff .field { width: auto; font-weight: 500; }
      table.diff td[data-label]::before { content: attr(data-label); display: inline-block; min-width: 56px; font-size: 12px; color: var(--erp-color-text-muted); }
      .summary-id { flex-basis: 100%; }
    }
    pre.raw { margin: 0 0 12px; padding: 12px; max-height: 320px; overflow: auto; border-radius: var(--erp-radius-control); background: var(--erp-color-bg-subtle); border: 1px solid var(--erp-color-border-subtle); color: var(--erp-color-text-primary); font-size: 12px; direction: ltr; text-align: start; }
    h4.sub { margin: 20px 0 8px; font-size: 13px; font-weight: 600; color: var(--erp-color-text-secondary); }
    ul.related { margin: 0; padding: 0; list-style: none; }
    ul.related li { display: flex; align-items: center; gap: 8px; padding: 6px 0; border-block-end: 1px solid var(--erp-color-border-subtle); min-width: 0; }
    ul.related button { padding-inline: 0; text-align: start; }
    .drawer-nav { display: inline-flex; align-items: center; gap: 4px; }
    @media (max-width: 480px) { .position { display: none; } }
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
        <nz-tag class="erp-tone chip" data-tone="info" nzMode="closeable" (nzOnClose)="clearRecordScope()" data-testid="audit-record-scope">{{ s.typeLabel }} {{ s.label }}</nz-tag>
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
            <span class="record erp-truncate"><span class="record-type">{{ typeOf(row) }}</span> {{ row.recordLabel || shortId(row.recordId) }}</span>
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

    <nz-drawer [nzVisible]="!!selected()" [nzTitle]="drawerTitle" [nzExtra]="navigation" [nzFooter]="hasFooter() ? drawerFooter : undefined" nzWidth="720px" (nzOnClose)="close()" data-testid="audit-drawer">
      <ng-template #drawerTitle>
        @if (selected(); as e) {
          <span class="title-row" data-testid="audit-headline">
            <nz-tag class="erp-tone" [attr.data-tone]="tone(e.action)">{{ e.action | erpLabel }}</nz-tag>
            <span class="title-text"><span>{{ typeOf(e) }}</span>&ngsp;@if (e.recordLabel) { <span class="title-name">{{ e.recordLabel }}</span> }</span>
          </span>
        }
      </ng-template>
      <ng-template #navigation>
        <span class="drawer-nav">
          @if (position(); as p) { <span class="position" data-testid="audit-position"><bdi dir="ltr">{{ p }}</bdi></span> }
          <button nz-button type="button" nzSize="small" [disabled]="!hasPrevious()" (click)="step(-1)" nz-tooltip [nzTooltipTitle]="'ui.audit.newer' | translate: 'Newer event'" [attr.aria-label]="'ui.audit.newer' | translate: 'Newer event'" data-testid="audit-newer"><span nz-icon nzType="up"></span></button>
          <button nz-button type="button" nzSize="small" [disabled]="!hasNext()" (click)="step(1)" nz-tooltip [nzTooltipTitle]="'ui.audit.older' | translate: 'Older event'" [attr.aria-label]="'ui.audit.older' | translate: 'Older event'" data-testid="audit-older"><span nz-icon nzType="down"></span></button>
        </span>
      </ng-template>
      <ng-template #drawerFooter>
        @if (selected(); as e) {
          @if ((detail()?.recordEventCount ?? 0) > 1) {
            <button nz-button type="button" (click)="scopeToRecord(e); close()" data-testid="audit-record-history"><span nz-icon nzType="history"></span> {{ 'ui.audit.seeAll' | translate: 'See all' }} {{ detail()?.recordEventCount }} {{ 'ui.audit.changes' | translate: 'changes' }}</button>
          }
          @if (linkOf(e); as link) {
            <a nz-button nzType="primary" [routerLink]="link" data-testid="audit-open-record"><span nz-icon nzType="export"></span> {{ 'ui.audit.openRecord' | translate: 'Open record' }}</a>
          }
        }
      </ng-template>
      <ng-container *nzDrawerContent>
        @if (selected(); as e) {
          <div class="summary" data-testid="audit-summary-block">
            <span class="avatar large" [class.system]="!e.userId" aria-hidden="true">{{ initialsOf(e) }}</span>
            <div class="summary-main">
              <div class="summary-who">{{ whoOf(e) }}</div>
              <div class="summary-when">{{ e.occurredAt | erpDate }} · {{ relative(e.occurredAt) }}@if (e.ipAddress) { · <bdi dir="ltr">IP {{ e.ipAddress }}</bdi> }</div>
            </div>
            <div class="summary-id">
              <span class="summary-id-label">{{ 'ui.audit.recordId' | translate: 'Record ID' }}</span>
              <bdi class="mono" dir="ltr" nz-tooltip [nzTooltipTitle]="e.recordId" data-testid="audit-record-id">{{ shortId(e.recordId) }}</bdi>
              <button nz-button nzType="text" nzSize="small" type="button" (click)="copy(e.recordId)" nz-tooltip [nzTooltipTitle]="'ui.audit.copy' | translate: 'Copy'" [attr.aria-label]="'ui.audit.copy' | translate: 'Copy'" data-testid="audit-copy-id"><span nz-icon nzType="copy"></span></button>
            </div>
          </div>

          @if (detailLoading()) {
            <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 5 }" data-testid="audit-detail-loading"></nz-skeleton>
          } @else if (detailFailed()) {
            <erp-state-panel kind="error" title="Could not load this event" hint="The list is still up to date." actionLabel="Try again" (action)="load(e)" />
          } @else if (detail(); as d) {
            <nz-tabset nzSize="small">
              <nz-tab [nzTitle]="changesTab(d)">
                @if (primaryFields(d); as rows) {
                  @if (rows.length) {
                    <table class="diff" data-testid="audit-diff">
                      <thead><tr>
                        <th class="field">{{ 'ui.audit.field' | translate: 'Field' }}</th>
                        @if (e.action !== 'Created') { <th>{{ (e.action === 'Updated' ? 'ui.audit.before' : 'ui.audit.value') | translate: (e.action === 'Updated' ? 'Before' : 'Value') }}</th> }
                        @if (e.action !== 'Deleted') { <th>{{ (e.action === 'Updated' ? 'ui.audit.after' : 'ui.audit.value') | translate: (e.action === 'Updated' ? 'After' : 'Value') }}</th> }
                      </tr></thead>
                      <tbody>
                        @for (f of rows; track f.field) {
                          <tr data-testid="audit-diff-changed">
                            <td class="field">{{ fieldLabel(f.field) }}</td>
                            @if (e.action !== 'Created') {
                              <td [attr.data-label]="e.action === 'Updated' ? ('ui.audit.before' | translate: 'Before') : null"><span class="val" [class.old]="e.action === 'Updated' && !!f.old" [class.empty]="!f.old" [class.mono]="isCode(f.old)" nz-tooltip [nzTooltipTitle]="isId(f.old) ? f.old : null" dir="auto">{{ valueText(f.old) }}</span></td>
                            }
                            @if (e.action !== 'Deleted') {
                              <td [attr.data-label]="e.action === 'Updated' ? ('ui.audit.after' | translate: 'After') : null"><span class="val" [class.new]="e.action === 'Updated' && !!f.new" [class.empty]="!f.new" [class.mono]="isCode(f.new)" nz-tooltip [nzTooltipTitle]="isId(f.new) ? f.new : null" dir="auto">{{ valueText(f.new) }}</span></td>
                            }
                          </tr>
                        }
                      </tbody>
                    </table>
                  } @else {
                    <p class="muted" data-testid="audit-no-fields">{{ noFieldsText(e) }}</p>
                  }
                }

                @if (restFields(d).length; as restCount) {
                  <button class="more-toggle" type="button" [attr.aria-expanded]="showUnchanged()" (click)="showUnchanged.set(!showUnchanged())" data-testid="audit-show-unchanged">
                    <span nz-icon [nzType]="showUnchanged() ? 'up' : 'down'"></span>
                    @if (e.action === 'Updated') { {{ 'ui.audit.unchangedFields' | translate: 'Unchanged fields' }} } @else { {{ 'ui.audit.emptyFields' | translate: 'Empty fields' }} }
                    <span class="count">({{ restCount }})</span>
                  </button>
                  @if (showUnchanged()) {
                    @if (restCount > manyFields) {
                      <input class="find" nz-input nzSize="small" [attr.aria-label]="'ui.audit.findField' | translate: 'Find a field'" [placeholder]="'ui.audit.findField' | translate: 'Find a field'" [ngModel]="fieldSearch()" (ngModelChange)="fieldSearch.set($event)" data-testid="audit-find-field" />
                    }
                    <table class="diff rest" data-testid="audit-diff-rest">
                      <tbody>
                        @for (f of matchingRest(d); track f.field) {
                          <tr data-testid="audit-diff-same">
                            <td class="field">{{ fieldLabel(f.field) }}</td>
                            <td><span class="val" [class.empty]="!(f.new ?? f.old)" [class.mono]="isCode(f.new ?? f.old)" nz-tooltip [nzTooltipTitle]="isId(f.new ?? f.old) ? (f.new ?? f.old) : null" dir="auto">{{ valueText(f.new ?? f.old) }}</span></td>
                          </tr>
                        } @empty {
                          <tr><td class="muted" data-testid="audit-no-field-match">{{ 'ui.audit.noFieldMatch' | translate: 'No field matches that.' }}</td></tr>
                        }
                      </tbody>
                    </table>
                  }
                }

                @if (d.sameSave.length) {
                  <h4 class="sub">{{ 'ui.audit.sameSave' | translate: 'Saved at the same moment' }} ({{ d.sameSave.length }})</h4>
                  <ul class="related" data-testid="audit-same-save">
                    @for (r of visibleSameSave(d); track r.id) {
                      <li>
                        <nz-tag class="erp-tone" [attr.data-tone]="tone(r.action)">{{ r.action | erpLabel }}</nz-tag>
                        <button nz-button nzType="link" nzSize="small" type="button" (click)="open(r)" data-testid="audit-related-open">{{ typeOf(r) }} {{ r.recordLabel || shortId(r.recordId) }}</button>
                      </li>
                    }
                  </ul>
                  @if (!sameSaveOpen() && d.sameSave.length > sameSaveShown) {
                    <button class="more-toggle" type="button" (click)="sameSaveOpen.set(true)" data-testid="audit-same-save-more"><span nz-icon nzType="down"></span>{{ 'ui.audit.seeAll' | translate: 'See all' }} {{ d.sameSave.length }}</button>
                  }
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
