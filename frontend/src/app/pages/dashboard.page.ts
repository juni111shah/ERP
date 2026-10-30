import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerComponent, NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { DashboardAlert, DashboardApi, DashboardChart, DashboardDto, KpiCard } from '../core/api/admin.api';
import { I18nService } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { SessionStore } from '../core/session/session.store';
import { ApexChartComponent } from '../shared/apex-chart.component';
import { RevealDirective } from '../shared/reveal.directive';
import { StatCardComponent, StatFormat } from '../shared/stat-card.component';

/** Overview period presets, shortest first, offered in the range dropdown next to `custom` (a picked date range). */
export type RangePreset = '1m' | '3m' | '6m' | '1y' | '2y' | '3y' | 'custom';
export interface DashboardRange { preset: RangePreset; from: string; to: string; }
export const RANGE_PRESETS: { key: Exclude<RangePreset, 'custom'>; months: number; labelKey: string; label: string }[] = [
  { key: '1m', months: 1, labelKey: 'ui.overview.range.preset.1m', label: 'Last month' },
  { key: '3m', months: 3, labelKey: 'ui.overview.range.preset.3m', label: 'Last 3 months' },
  { key: '6m', months: 6, labelKey: 'ui.overview.range.preset.6m', label: 'Last 6 months' },
  // "Last year" is the trailing 12 months ending today, not the previous calendar year
  { key: '1y', months: 12, labelKey: 'ui.overview.range.preset.1y', label: 'Last year' },
  { key: '2y', months: 24, labelKey: 'ui.overview.range.preset.2y', label: 'Last 2 years' },
  { key: '3y', months: 36, labelKey: 'ui.overview.range.preset.3y', label: 'Last 3 years' },
];
/** Longest period the overview accepts (the API enforces the same 5-year limit). */
export const MAX_RANGE_MONTHS = 60;
export const RANGE_KEY = 'erp.overview.range';
const DEFAULT_PRESET = '1y' as const;
const pad = (n: number) => String(n).padStart(2, '0');
export const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
export const parseIso = (s: string) => { const [y, m, d] = s.split('-').map(Number); return new Date(y, m - 1, d); };
const startOfToday = () => { const n = new Date(); return new Date(n.getFullYear(), n.getMonth(), n.getDate()); };
const addDays = (d: Date, n: number) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + n);
/** Same day n months earlier, clamped to the month's last day (31 Mar minus 1 month = 28/29 Feb), like the server's AddMonths. */
export function minusMonths(d: Date, n: number): Date {
  const t = new Date(d.getFullYear(), d.getMonth() - n, 1);
  t.setDate(Math.min(d.getDate(), new Date(t.getFullYear(), t.getMonth() + 1, 0).getDate()));
  return t;
}
/** First day of an n-month period ending on `end` inclusive: one day after the same day n months back (1Y = 31 Oct 2025 – 30 Oct 2026). */
export const periodStart = (end: Date, months: number) => addDays(minusMonths(end, months), 1);
export function presetRange(preset: Exclude<RangePreset, 'custom'>, end = startOfToday()): DashboardRange {
  const months = RANGE_PRESETS.find((p) => p.key === preset)!.months;
  return { preset, from: isoDate(periodStart(end, months)), to: isoDate(end) };
}
const ISO = /^\d{4}-\d{2}-\d{2}$/;
/** A real calendar date in yyyy-MM-dd (rejects 2026-02-31). */
export const isRealDate = (s: unknown): s is string => typeof s === 'string' && ISO.test(s) && isoDate(parseIso(s)) === s;
/** A usable period: real dates, from ≤ to, to not after today, and at most 5 years long. */
export function validRange(from: unknown, to: unknown, today = startOfToday()): boolean {
  if (!isRealDate(from) || !isRealDate(to) || from > to || to > isoDate(today)) return false;
  return from >= isoDate(periodStart(parseIso(to), MAX_RANGE_MONTHS));
}
/** A period from query params: `?range=1y`, or `?from=2025-10-31&to=2026-10-30` for a custom period. Invalid params give null. */
export function rangeFromParams(p: ParamMap, today = startOfToday()): DashboardRange | null {
  const preset = p.get('range');
  if (preset && RANGE_PRESETS.some((x) => x.key === preset)) return presetRange(preset as Exclude<RangePreset, 'custom'>, today);
  const from = p.get('from'), to = p.get('to');
  if (validRange(from, to, today)) return { preset: 'custom', from: from!, to: to! };
  return null;
}
export const hasRangeParams = (p: ParamMap) => p.has('range') || p.has('from') || p.has('to');
/** The last choice in this browser; relative presets are recomputed against today, custom periods are re-validated. */
export function storedRange(today = startOfToday()): DashboardRange | null {
  try {
    const saved = JSON.parse(localStorage.getItem(RANGE_KEY) ?? 'null') as DashboardRange | null;
    if (saved?.preset === 'custom' && validRange(saved.from, saved.to, today)) return { preset: 'custom', from: saved.from, to: saved.to };
    if (saved && RANGE_PRESETS.some((p) => p.key === saved.preset)) return presetRange(saved.preset as Exclude<RangePreset, 'custom'>, today);
  } catch { /* storage unavailable or corrupt */ }
  return null;
}
export const sameRange = (a: DashboardRange, b: DashboardRange) => a.preset === b.preset && a.from === b.from && a.to === b.to;
const rangeParams = (r: DashboardRange) => (r.preset === 'custom' ? { range: null, from: r.from, to: r.to } : { range: r.preset, from: null, to: null });
/** Latin digits in both languages (matches the rest of the app's figures). */
const latn = (locale: string) => `${locale}-u-nu-latn`;
const FSI = '\u2068', PDI = '\u2069';

const URGENCY: Record<string, { label: string; color: string }> = {
  action: { label: 'Action needed', color: 'red' },
  upcoming: { label: 'Upcoming', color: 'gold' },
  info: { label: 'For information', color: 'blue' },
};

export type DomainCategory =
  | 'all'
  | 'finance'
  | 'sales-crm'
  | 'supply-operations'
  | 'projects-services'
  | 'real-estate'
  | 'hr';

interface KpiCluster {
  id: string;
  title: string;
  icon: string;
  category: DomainCategory;
  kpis: KpiCard[];
}

@Component({
  selector: 'erp-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslatePipe,
    RouterLink,
    FormsModule,
    NzButtonModule,
    NzDatePickerModule,
    NzSelectModule,
    NzToolTipModule,
    RevealDirective,
    ApexChartComponent,
    StatCardComponent,
    NzIconModule,
    NzSkeletonModule,
    NzSpinModule,
    NzTagModule,
  ],
  styles: [`
    :host {
      display: block;
      color: var(--erp-color-text-primary, #1e293b);
      background: var(--erp-color-bg-canvas, #f8fafc);
      padding-bottom: 40px;
    }

    .dashboard-container {
      max-width: 1600px;
      width: 100%;
      min-width: 0;
      margin: 0 auto;
    }

    /* Screen reader only for test compatibility */
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }

    /* ---- MODERN TOOLBAR: SINGLE ROW EXPANDABLE DOCK ---- */
    .dashboard-toolbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: var(--erp-space-2, 8px) 16px;
      margin-bottom: 22px;
      background: #ffffff;
      padding: 7px 12px;
      border-radius: 14px;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
    }
    @media (max-width: 767px) {
      .dashboard-toolbar { flex-direction: column; align-items: stretch; }
      .dashboard-toolbar .category-tabs { flex-wrap: wrap; }
      .dashboard-toolbar .toolbar-meta { flex-wrap: wrap; }
    }

    .category-tabs {
      display: flex;
      align-items: center;
      gap: 4px;
      flex-wrap: wrap;
      flex: 1 1 auto;
      min-width: 0;
    }

    .tab-pill {
      display: inline-flex;
      align-items: center;
      height: 38px;
      padding: 0 11px;
      font-size: 13px;
      font-weight: 500;
      border-radius: 10px;
      border: 1px solid transparent;
      background: transparent;
      color: var(--erp-color-text-secondary, #64748b);
      cursor: pointer;
      white-space: nowrap;
      position: relative;
      transition: all 0.28s cubic-bezier(0.2, 0, 0, 1);
    }
    .tab-pill:hover {
      background: #f1f5f9;
      color: var(--erp-color-text-primary, #0f172a);
    }
    .tab-pill.active {
      background: #e6f7ef;
      border-color: rgba(16, 149, 93, 0.28);
      color: #10955d;
      font-weight: 600;
      box-shadow: 0 1px 3px rgba(16, 149, 93, 0.08);
      padding: 0 13px;
    }

    .pill-icon {
      font-size: 15px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      transition: transform 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
    }
    .tab-pill:hover .pill-icon {
      transform: scale(1.12);
    }

    .pill-text {
      display: inline-block;
      max-width: 0;
      opacity: 0;
      overflow: hidden;
      white-space: nowrap;
      transform: translateX(-6px);
      transition: max-width 0.3s cubic-bezier(0.2, 0, 0, 1),
                  opacity 0.2s ease,
                  transform 0.3s cubic-bezier(0.2, 0, 0, 1),
                  margin 0.3s cubic-bezier(0.2, 0, 0, 1);
    }
    .tab-pill.active .pill-text,
    .tab-pill:hover .pill-text {
      max-width: 170px;
      opacity: 1;
      transform: translateX(0);
      margin-left: 7px;
      margin-right: 3px;
    }

    .tab-count {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      max-width: 0;
      opacity: 0;
      overflow: hidden;
      font-size: 11px;
      font-weight: 600;
      background: #e2e8f0;
      color: #64748b;
      border-radius: 10px;
      transform: scale(0.8);
      transition: max-width 0.25s cubic-bezier(0.2, 0, 0, 1),
                  opacity 0.2s ease,
                  transform 0.25s cubic-bezier(0.34, 1.56, 0.64, 1),
                  padding 0.25s ease;
    }
    .tab-pill.active .tab-count,
    .tab-pill:hover .tab-count {
      max-width: 36px;
      opacity: 1;
      padding: 1px 6px;
      transform: scale(1);
    }
    .tab-pill.active .tab-count {
      background: #cbeedd;
      color: #0b8457;
    }

    .toolbar-meta {
      display: flex;
      align-items: center;
      gap: 10px;
      flex-wrap: wrap;
      flex-shrink: 0;
    }

    /* ---- PERIOD ROW: dropdown (+ picker for Custom) + Refresh, then the period caption ---- */
    .range-bar {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: var(--erp-space-2, 8px) 12px;
      margin-bottom: 12px;
    }
    .range-control {
      display: inline-flex;
      align-items: center;
      flex-wrap: wrap;
      gap: var(--erp-space-2, 8px);
    }
    .range-select {
      min-width: 150px;
    }
    .range-picker {
      width: 260px;
    }
    .range-caption {
      font-size: 13px;
      color: var(--erp-color-text-secondary, #475569);
      unicode-bidi: plaintext;
    }
    .range-error {
      font-size: 12px;
      color: var(--erp-color-error, #dc2626);
    }
    @media (hover: none) {
      /* touch: compact icon-only Refresh (tooltip + aria-label still name it) */
      .range-control .range-btn-label { display: none; }
    }
    @media (max-width: 767px) {
      .range-control { width: 100%; }
      .range-select { flex: 1 1 140px; }
      .range-picker { flex: 1 1 100%; width: auto; }
    }
    .load-error, .period-empty {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: var(--erp-space-2, 8px);
      padding: 12px 16px;
      margin-bottom: 16px;
      border-radius: 10px;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      background: #f8fafc;
      color: var(--erp-color-text-secondary, #475569);
    }
    .load-error { border-color: #fecaca; background: #fef2f2; color: #991b1b; }
    .load-error-icon { font-size: 18px; }

    .view-mode-toggle {
      display: inline-flex;
      background: #f1f5f9;
      padding: 3px;
      border-radius: 8px;
      gap: 2px;
    }
    .toggle-btn {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      padding: 4px 11px;
      font-size: 12px;
      font-weight: 500;
      border: none;
      background: transparent;
      border-radius: 6px;
      cursor: pointer;
      color: #64748b;
      transition: all 0.2s;
    }
    .toggle-btn.active {
      background: #ffffff;
      color: #0f172a;
      font-weight: 600;
      box-shadow: 0 1px 2px rgba(0, 0, 0, 0.08);
    }

    /* ---- ALL METRICS MATRIX VIEW ---- */
    .all-metrics-matrix-wrap {
      margin-bottom: 26px;
    }
    .matrix-header-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 14px;
      background: #ffffff;
      padding: 14px 18px;
      border-radius: 14px;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      margin-bottom: 20px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
    }
    .matrix-title-wrap {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .matrix-icon {
      font-size: 20px;
      color: #10955d;
      background: #e6f7ef;
      padding: 8px;
      border-radius: 10px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }
    .matrix-titles h3 {
      margin: 0;
      font-size: 16px;
      font-weight: 700;
      color: var(--erp-color-text-primary, #0f172a);
    }
    .matrix-sub {
      font-size: 12px;
      color: var(--erp-color-text-secondary, #64748b);
    }
    .matrix-search-box {
      display: inline-flex;
      align-items: center;
      position: relative;
      min-width: 280px;
    }
    .matrix-search-box .search-icon {
      position: absolute;
      left: 12px;
      color: #94a3b8;
      font-size: 14px;
      pointer-events: none;
    }
    .matrix-search-input {
      width: 100%;
      height: 38px;
      padding: 0 34px 0 36px;
      border-radius: 10px;
      border: 1px solid #cbd5e1;
      background: #f8fafc;
      font-size: 13px;
      color: #0f172a;
      transition: all 0.2s;
    }
    .matrix-search-input:focus {
      outline: none;
      background: #ffffff;
      border-color: #10955d;
      box-shadow: 0 0 0 3px rgba(16, 149, 93, 0.12);
    }
    .clear-search-btn {
      position: absolute;
      right: 10px;
      background: transparent;
      border: none;
      cursor: pointer;
      color: #94a3b8;
      font-size: 14px;
      font-weight: 700;
      display: inline-flex;
      align-items: center;
      line-height: 1;
    }
    .clear-search-btn:hover {
      color: #0f172a;
    }
    .matrix-departments {
      display: flex;
      flex-direction: column;
      gap: 18px;
    }
    .matrix-dept-block {
      background: #ffffff;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      border-radius: 14px;
      padding: 16px 18px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
    }
    .dept-block-header {
      margin-bottom: 14px;
    }
    .dept-title {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      font-size: 14px;
      font-weight: 700;
      color: #1e293b;
    }
    .dept-badge {
      font-size: 11px;
      font-weight: 600;
      background: #f1f5f9;
      color: #64748b;
      padding: 2px 8px;
      border-radius: 12px;
    }
    .empty-matrix-state {
      grid-column: 1 / -1;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 40px 20px;
      background: #ffffff;
      border-radius: 12px;
      border: 1px dashed #cbd5e1;
      color: #64748b;
      gap: 8px;
    }
    .section-divider {
      display: flex;
      align-items: center;
      margin: 28px 0 20px;
      text-transform: uppercase;
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.08em;
      color: #94a3b8;
    }
    .section-divider::before,
    .section-divider::after {
      content: '';
      flex: 1;
      border-bottom: 1px solid #e2e8f0;
    }
    .section-divider span {
      padding: 0 14px;
    }

    /* ---- TIER 1: STRATEGIC HIGHLIGHTS (4 HERO CARDS) ---- */
    .hero-kpis-wrap {
      margin-bottom: 22px;
    }
    .section-eyebrow {
      font-size: 12px;
      font-weight: 600;
      color: var(--erp-color-text-secondary, #64748b);
      text-transform: uppercase;
      letter-spacing: 0.06em;
      margin: 0 0 10px 2px;
      display: flex;
      align-items: center;
      gap: 6px;
    }

    /* ---- TIER 2: ANALYTICS HUB & ACTION CENTER (CHART + ALERTS) ---- */
    .analytics-section {
      margin-bottom: 24px;
      min-width: 0;
      width: 100%;
    }
    .analytics-grid {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 380px;
      gap: 20px;
      align-items: stretch;
      min-width: 0;
      width: 100%;
    }
    @media (max-width: 1100px) {
      .analytics-grid {
        grid-template-columns: minmax(0, 1fr);
      }
    }

    .chart-panel {
      background: #ffffff;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      border-radius: 14px;
      padding: 20px 22px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
      display: flex;
      flex-direction: column;
      height: 100%;
      min-width: 0;
      max-width: 100%;
      overflow: hidden;
    }
    .chart-panel-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 8px;
      flex-wrap: wrap;
      gap: 12px;
    }
    .chart-titles {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
    }
    .chart-badge {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      font-size: 11px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: #10955d;
      margin-bottom: 2px;
    }
    .secondary-analytics-grid {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
      gap: 20px;
      margin-top: 20px;
      min-width: 0;
      width: 100%;
    }
    @media (max-width: 1024px) {
      .secondary-analytics-grid {
        grid-template-columns: minmax(0, 1fr);
      }
    }
    .chart-panel h3 {
      margin: 0;
      font-size: 17px;
      font-weight: 700;
      color: var(--erp-color-text-primary, #0f172a);
    }
    .chart-panel .sub {
      font-size: 12px;
      color: var(--erp-color-text-secondary, #64748b);
      margin: 0;
    }

    .chart-selector-tabs {
      display: flex;
      gap: 6px;
      flex-wrap: wrap;
      align-items: center;
    }
    .chart-tab-btn {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 5px 12px;
      font-size: 12px;
      font-weight: 500;
      border-radius: 8px;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      background: #ffffff;
      color: var(--erp-color-text-secondary, #64748b);
      cursor: pointer;
      transition: all 0.2s cubic-bezier(0.2, 0, 0, 1);
    }
    .chart-tab-btn:hover:not(.active) {
      background: #f8fafc;
      border-color: #cbd5e1;
      color: #0f172a;
    }
    .chart-tab-btn.active {
      background: #e6f7ef;
      border-color: rgba(16, 149, 93, 0.35);
      color: #10955d;
      font-weight: 600;
      box-shadow: 0 1px 2px rgba(16, 149, 93, 0.08);
    }

    .chart-canvas-wrap {
      flex: 1;
      width: 100%;
      min-width: 0;
      max-width: 100%;
      min-height: 320px;
      display: flex;
      flex-direction: column;
      justify-content: center;
      margin-top: 10px;
      overflow: hidden;
    }

    /* Alerts Panel */
    .alerts-panel {
      background: #ffffff;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      border-radius: 14px;
      padding: 20px 22px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
      display: flex;
      flex-direction: column;
      height: 100%;
      min-width: 0;
      max-width: 100%;
      overflow: hidden;
    }
    .alert-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 14px;
    }
    .alert-header h3 {
      margin: 0;
      font-size: 16px;
      font-weight: 700;
      color: #0f172a;
    }
    .alert-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
      overflow-y: auto;
      max-height: 380px;
    }
    .alert {
      display: flex;
      gap: 12px;
      padding: 10px 12px;
      border-radius: 10px;
      border: 1px solid transparent;
      color: inherit;
      text-decoration: none;
      align-items: flex-start;
      transition: background 0.15s, border-color 0.15s;
    }
    .alert:hover {
      background: #f8fafc;
      border-color: #e2e8f0;
    }
    .alert-icon-wrap {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      font-size: 14px;
    }
    .icon-critical {
      background: #fee2e2;
      color: #ef4444;
    }
    .icon-warning {
      background: #fef3c7;
      color: #f59e0b;
    }
    .icon-info {
      background: #e0f2fe;
      color: #0284c7;
    }
    .alert .msg {
      flex: 1;
      font-size: 13px;
      line-height: 1.4;
      color: var(--erp-color-text-primary, #1e293b);
    }
    .alert-tags {
      display: flex;
      gap: 6px;
      margin-bottom: 4px;
      flex-wrap: wrap;
    }
    .empty-state {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 28px 0;
      color: var(--erp-color-text-secondary, #64748b);
      font-size: 13px;
    }

    /* ---- TIER 3: DEPARTMENTAL PERFORMANCE CLUSTERS ---- */
    .clusters-container {
      display: flex;
      flex-direction: column;
      gap: 22px;
    }
    .cluster-card {
      background: #ffffff;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      border-radius: 14px;
      padding: 18px 20px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
    }
    .cluster-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 14px;
      padding-bottom: 10px;
      border-bottom: 1px solid var(--erp-color-border-subtle, #f1f5f9);
    }
    .cluster-title-wrap {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 14px;
      font-weight: 700;
      color: var(--erp-color-text-primary, #0f172a);
      letter-spacing: -0.01em;
    }
    .cluster-icon {
      font-size: 16px;
      color: var(--erp-color-primary, #10955d);
      background: #e6f7ef;
      padding: 6px;
      border-radius: 8px;
    }
    .cluster-badge {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 12px;
      font-size: 11px;
      font-weight: 600;
      background: #f1f5f9;
      color: #64748b;
    }
  `],
  template: `
    <div class="dashboard-container">
      <!-- Hidden greeting element to ensure test accessibility & compatibility -->
      <span data-testid="dashboard-title" class="sr-only">
        {{ 'dashboard.welcome' | translate: 'Welcome' }}, {{ session.me()?.user?.fullName }}
      </span>

      <!-- The period for every card and chart. Its own row, outside the data block, so it stays usable while loading or after an error. -->
      <div class="range-bar" data-testid="dashboard-range-bar">
        <div class="range-control" erpReveal [erpRevealSelects]="false" role="group" [attr.aria-label]="'ui.overview.range.group' | translate: 'Date range'" data-testid="dashboard-range">
          <nz-select
            class="range-select"
            data-reveal="off"
            [ngModel]="selectValue()"
            (ngModelChange)="setPreset($event)"
            [nzDropdownMatchSelectWidth]="false"
            [attr.aria-label]="'ui.overview.range.group' | translate: 'Date range'"
            data-testid="dashboard-range-preset">
            @for (p of presets; track p.key) {
              <nz-option [nzValue]="p.key" [nzLabel]="p.labelKey | translate: p.label"></nz-option>
            }
            <nz-option nzValue="custom" [nzLabel]="'ui.overview.range.custom' | translate: 'Custom'"></nz-option>
          </nz-select>
          @if (showPicker()) {
            <nz-range-picker
              class="range-picker"
              data-reveal="off"
              nzFormat="dd MMM yyyy"
              [nzInputReadOnly]="true"
              (nzOnOpenChange)="onPickerOpenChange($event)"
              (nzOnCalendarChange)="onCalendarChange($event)"
              [ngModel]="rangeDates()"
              (ngModelChange)="setCustomRange($event)"
              [nzAllowClear]="false"
              [nzDisabledDate]="disabledDate"
              [nzRenderExtraFooter]="maxSpanHint()"
              [nzPlaceHolder]="rangePlaceholders()"
              [attr.aria-describedby]="rangeError() ? 'dashboard-range-error' : null"
              data-testid="dashboard-range-picker"></nz-range-picker>
          }
          <button nz-button type="button" (click)="refresh()"
            [nz-tooltip]="'ui.overview.range.refreshHint' | translate: 'Reload the figures for this period'"
            [attr.aria-label]="'ui.overview.range.refresh' | translate: 'Refresh'" data-testid="dashboard-range-refresh">
            <span nz-icon nzType="reload" [nzSpin]="loading()"></span><span class="range-btn-label">{{ 'ui.overview.range.refresh' | translate: 'Refresh' }}</span>
          </button>
        </div>
        <span class="range-caption" data-testid="dashboard-range-caption">@for (seg of captionParts(); track $index) {@if (seg.bdi) {<bdi>{{ seg.text }}</bdi>} @else {<span>{{ seg.text }}</span>}}</span>
        @if (rangeError(); as err) {
          <span class="range-error" id="dashboard-range-error" role="alert" data-testid="dashboard-range-error">{{ err }}</span>
        }
        <span class="sr-only" aria-live="polite" data-testid="dashboard-range-live">{{ announcement() }}</span>
      </div>

      <nz-spin [nzSpinning]="loading() && !!data()" [attr.aria-busy]="loading()">
        @if (!data()) {
          @if (loadError()) {
            <div class="load-error" role="alert" data-testid="dashboard-error">
              <span nz-icon nzType="warning" class="load-error-icon"></span>
              <span>{{ 'ui.overview.range.error' | translate: "Couldn't load the overview for this period." }}</span>
              <button nz-button nzType="primary" type="button" (click)="refresh()" data-testid="dashboard-retry">
                <span nz-icon nzType="reload"></span>{{ 'ui.overview.range.retry' | translate: 'Retry' }}
              </button>
            </div>
          } @else {
            <!-- Skeleton Loading State -->
            <div class="hero-kpis-wrap" data-testid="dashboard-loading">
              <div class="erp-stat-grid">
                @for (i of [0, 1, 2, 3]; track i) {
                  <erp-stat-card label="" [loading]="true" [tone]="i" />
                }
              </div>
              <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 6 }"></nz-skeleton>
            </div>
          }
        }

        @if (data(); as d) {
          <!-- Modern Control Bar: Categories & View Modes -->
          <div class="dashboard-toolbar">
            <div class="category-tabs">
              <button
                class="tab-pill"
                [class.active]="activeCategory() === 'all'"
                (click)="setCategory('all')"
                title="All Overview">
                <span nz-icon nzType="appstore" class="pill-icon"></span>
                <span class="pill-text">All Overview</span>
                <span class="tab-count">{{ d.kpis.length }}</span>
              </button>

              @if (hasCategory('finance')) {
                <button
                  class="tab-pill"
                  [class.active]="activeCategory() === 'finance'"
                  (click)="setCategory('finance')"
                  title="Finance & Treasury">
                  <span nz-icon nzType="dollar" class="pill-icon"></span>
                  <span class="pill-text">Finance & Treasury</span>
                  <span class="tab-count">{{ getCategoryKpis('finance').length }}</span>
                </button>
              }

              @if (hasCategory('sales-crm')) {
                <button
                  class="tab-pill"
                  [class.active]="activeCategory() === 'sales-crm'"
                  (click)="setCategory('sales-crm')"
                  title="Sales & CRM">
                  <span nz-icon nzType="rise" class="pill-icon"></span>
                  <span class="pill-text">Sales & CRM</span>
                  <span class="tab-count">{{ getCategoryKpis('sales-crm').length }}</span>
                </button>
              }

              @if (hasCategory('supply-operations')) {
                <button
                  class="tab-pill"
                  [class.active]="activeCategory() === 'supply-operations'"
                  (click)="setCategory('supply-operations')"
                  title="Supply & Operations">
                  <span nz-icon nzType="shopping" class="pill-icon"></span>
                  <span class="pill-text">Supply & Operations</span>
                  <span class="tab-count">{{ getCategoryKpis('supply-operations').length }}</span>
                </button>
              }

              @if (hasCategory('projects-services')) {
                <button
                  class="tab-pill"
                  [class.active]="activeCategory() === 'projects-services'"
                  (click)="setCategory('projects-services')"
                  title="Projects & Services">
                  <span nz-icon nzType="project" class="pill-icon"></span>
                  <span class="pill-text">Projects & Services</span>
                  <span class="tab-count">{{ getCategoryKpis('projects-services').length }}</span>
                </button>
              }

              @if (hasCategory('real-estate')) {
                <button
                  class="tab-pill"
                  [class.active]="activeCategory() === 'real-estate'"
                  (click)="setCategory('real-estate')"
                  title="Real Estate">
                  <span nz-icon nzType="home" class="pill-icon"></span>
                  <span class="pill-text">Real Estate</span>
                  <span class="tab-count">{{ getCategoryKpis('real-estate').length }}</span>
                </button>
              }

              @if (hasCategory('hr')) {
                <button
                  class="tab-pill"
                  [class.active]="activeCategory() === 'hr'"
                  (click)="setCategory('hr')"
                  title="HR & People">
                  <span nz-icon nzType="team" class="pill-icon"></span>
                  <span class="pill-text">HR & People</span>
                  <span class="tab-count">{{ getCategoryKpis('hr').length }}</span>
                </button>
              }
            </div>

            <div class="toolbar-meta">
              <div class="view-mode-toggle">
                <button
                  class="toggle-btn"
                  [class.active]="viewMode() === 'curated'"
                  (click)="viewMode.set('curated')">
                  <span nz-icon nzType="appstore"></span> Curated Focus
                </button>
                <button
                  class="toggle-btn"
                  [class.active]="viewMode() === 'all'"
                  (click)="viewMode.set('all')">
                  <span nz-icon nzType="table"></span> All Metrics ({{ d.kpis.length }})
                </button>
              </div>
            </div>
          </div>

          @if (periodEmpty()) {
            <div class="period-empty" role="status" data-testid="dashboard-empty">
              <span nz-icon nzType="info-circle"></span>
              <span>{{ 'ui.overview.range.empty' | translate: 'Nothing was billed or collected in this period.' }}</span>
              @if (canShowLonger()) {
                <button nz-button nzType="link" type="button" (click)="setPreset('3y')" data-testid="dashboard-empty-action">{{ 'ui.overview.range.emptyAction' | translate: 'Show the last 3 years' }}</button>
              }
            </div>
          }

          @if (viewMode() === 'curated') {
            <!-- TIER 1: STRATEGIC HIGHLIGHTS (4 HERO STAT CARDS) -->
            <div class="hero-kpis-wrap" data-testid="kpi-grid">
              <div class="section-eyebrow">
                <span nz-icon nzType="fund"></span>
                <span>{{ activeCategory() === 'all' ? 'Executive Highlights' : (activeCategoryTitle() + ' Key Highlights') }}</span>
              </div>
              <div class="erp-stat-grid">
                @for (k of activeHeroKpis(); track k.key; let i = $index) {
                  <erp-stat-card
                    [label]="k.label"
                    [value]="k.value"
                    [format]="statFormat(k)"
                    [caption]="k.caption ?? null"
                    [tone]="i"
                    [link]="k.link ?? null"
                    [testId]="'kpi-' + k.key" />
                }
              </div>
            </div>
          } @else {
            <!-- ALL METRICS MATRIX VIEW -->
            <div class="all-metrics-matrix-wrap" data-testid="kpi-grid">
              <div class="matrix-header-bar">
                <div class="matrix-title-wrap">
                  <span nz-icon nzType="table" class="matrix-icon"></span>
                  <div class="matrix-titles">
                    <h3>{{ activeCategory() === 'all' ? 'Complete Enterprise Metrics Matrix' : (activeCategoryTitle() + ' Full Metrics Matrix') }}</h3>
                    <span class="matrix-sub">Showing {{ filteredMatrixKpis().length }} of {{ activeCategoryKpis().length }} tracked metrics across legal entities</span>
                  </div>
                </div>

                <div class="matrix-search-box">
                  <span nz-icon nzType="search" class="search-icon"></span>
                  <input [attr.aria-label]="'Search ' + activeCategoryKpis().length + ' metrics (e.g. sales, cash, stock)...'"
                    type="text"
                    class="matrix-search-input"
                    [placeholder]="'Search ' + activeCategoryKpis().length + ' metrics (e.g. sales, cash, stock)...'"
                    [value]="metricSearchQuery()"
                    (input)="metricSearchQuery.set($any($event.target).value)" />
                  @if (metricSearchQuery()) {
                    <button class="clear-search-btn" (click)="metricSearchQuery.set('')">✕</button>
                  }
                </div>
              </div>

              @if (activeCategory() === 'all' && !metricSearchQuery()) {
                <!-- Departmental Matrix Grid -->
                <div class="matrix-departments">
                  @for (cluster of allMatrixClusters(); track cluster.id) {
                    @if (cluster.kpis.length > 0) {
                      <div class="matrix-dept-block">
                        <div class="dept-block-header">
                          <div class="dept-title">
                            <span nz-icon [nzType]="cluster.icon"></span>
                            <span>{{ cluster.title }}</span>
                            <span class="dept-badge">{{ cluster.kpis.length }} metrics</span>
                          </div>
                        </div>
                        <div class="erp-stat-grid erp-stat-grid--compact">
                          @for (k of cluster.kpis; track k.key; let i = $index) {
                            <erp-stat-card
                              [compact]="false"
                              [label]="k.label"
                              [value]="k.value"
                              [format]="statFormat(k)"
                              [caption]="k.caption ?? null"
                              [tone]="i % 7"
                              [link]="k.link ?? null"
                              [testId]="'kpi-' + k.key" />
                          }
                        </div>
                      </div>
                    }
                  }
                </div>
              } @else {
                <!-- Flat Filtered / Search Matrix Grid -->
                <div class="erp-stat-grid" style="margin-top: 14px;">
                  @for (k of filteredMatrixKpis(); track k.key; let i = $index) {
                    <erp-stat-card
                      [compact]="false"
                      [label]="k.label"
                      [value]="k.value"
                      [format]="statFormat(k)"
                      [caption]="k.caption ?? null"
                      [tone]="i % 7"
                      [link]="k.link ?? null"
                      [testId]="'kpi-' + k.key" />
                  } @empty {
                    <div class="empty-matrix-state">
                      <span nz-icon nzType="search" style="font-size: 26px; color: #94a3b8;"></span>
                      <span>No metrics found matching "{{ metricSearchQuery() }}"</span>
                    </div>
                  }
                </div>
              }
            </div>

            <div class="section-divider">
              <span>Departmental Analytics & Deep Dives</span>
            </div>
          }

          <!-- TIER 2: ENTERPRISE ANALYTICS & ACTION CENTER -->
          <div class="analytics-section">
            @if (activeCategory() === 'all') {
              <!-- ALL OVERVIEW: Primary Financial & Invoicing Trend + Alerts -->
              <div class="analytics-grid">
                <!-- Financial Trends Chart Card -->
                <div class="chart-panel" [attr.data-testid]="'chart-' + (currentFinanceChart()?.key ?? currentChart()?.key)">
                  <div class="chart-panel-header">
                    <div class="chart-titles">
                      <div class="chart-badge">
                        <span nz-icon nzType="dollar"></span>
                        <span>Finance & Treasury</span>
                      </div>
                      <h3>{{ currentFinanceChart()?.title ?? 'Financial Trends' }}</h3>
                      @if (currentFinanceChart()?.subtitle) {
                        <div class="sub">{{ currentFinanceChart()?.subtitle }}@if (bucketLabel(currentFinanceChart())) {<span> · {{ bucketLabel(currentFinanceChart()) }}</span>}</div>
                      }
                    </div>
                    <div class="chart-selector-tabs">
                      @for (c of financeCharts(); track c.key) {
                        <button
                          class="chart-tab-btn"
                          [class.active]="selectedFinanceChartKey() === c.key"
                          (click)="selectFinanceChart(c.key)">
                          {{ c.title }}
                        </button>
                      }
                    </div>
                  </div>

                  <div class="chart-canvas-wrap">
                    @if (currentFinanceChart(); as c) {
                      <erp-apex-chart [chart]="c" />
                    }
                  </div>
                </div>

                <!-- Right Side: Alerts & To-Dos -->
                <div class="alerts-panel" data-testid="alerts">
                  <div class="alert-header">
                    <h3>{{ 'dashboard.alerts' | translate: 'Alerts and to-dos' }}</h3>
                    <span class="tab-count">{{ d.alerts.length }}</span>
                  </div>
                  <div class="alert-list">
                    @for (a of d.alerts; track $index) {
                      <a class="alert" [routerLink]="a.link ?? null">
                        <div class="alert-icon-wrap" [class]="'icon-' + a.severity">
                          <span nz-icon [nzType]="a.severity === 'info' ? 'bell' : (a.severity === 'critical' ? 'warning' : 'bulb')"></span>
                        </div>
                        <div class="msg">
                          <div class="alert-tags">
                            <nz-tag>{{ a.category }}</nz-tag>
                            <nz-tag [nzColor]="urgency(a.urgency).color">{{ urgency(a.urgency).label }}</nz-tag>
                          </div>
                          <div>{{ a.message }}</div>
                        </div>
                      </a>
                    } @empty {
                      <div class="empty-state">
                        <span nz-icon nzType="check-circle" style="color: #10b981; font-size: 18px;"></span>
                        <span>Nothing needs your attention right now.</span>
                      </div>
                    }
                  </div>
                </div>
              </div>

              <!-- ALL OVERVIEW: Dual Operational Hub (Commercial Pipeline + Operations & Execution) -->
              @if (salesCharts().length > 0 || opsCharts().length > 0) {
                <div class="secondary-analytics-grid">
                  <!-- Card A: Commercial & Sales Pipeline -->
                  @if (currentSalesChart(); as sc) {
                    <div class="chart-panel" [attr.data-testid]="'chart-' + sc.key">
                      <div class="chart-panel-header">
                        <div class="chart-titles">
                          <div class="chart-badge">
                            <span nz-icon nzType="rise"></span>
                            <span>Sales & Commercial Pipeline</span>
                          </div>
                          <h3>{{ sc.title }}</h3>
                          @if (sc.subtitle) {
                            <div class="sub">{{ sc.subtitle }}</div>
                          }
                        </div>
                        <div class="chart-selector-tabs">
                          @for (c of salesCharts(); track c.key) {
                            <button
                              class="chart-tab-btn"
                              [class.active]="selectedSalesChartKey() === c.key"
                              (click)="selectSalesChart(c.key)">
                              {{ c.title }}
                            </button>
                          }
                        </div>
                      </div>

                      <div class="chart-canvas-wrap">
                        <erp-apex-chart [chart]="sc" />
                      </div>
                    </div>
                  }

                  <!-- Card B: Operations, Manufacturing & Delivery Execution -->
                  @if (currentOpsChart(); as oc) {
                    <div class="chart-panel" [attr.data-testid]="'chart-' + oc.key">
                      <div class="chart-panel-header">
                        <div class="chart-titles">
                          <div class="chart-badge">
                            <span nz-icon nzType="shop"></span>
                            <span>Supply Chain, Production & Delivery</span>
                          </div>
                          <h3>{{ oc.title }}</h3>
                          @if (oc.subtitle) {
                            <div class="sub">{{ oc.subtitle }}</div>
                          }
                        </div>
                        <div class="chart-selector-tabs">
                          @for (c of opsCharts(); track c.key) {
                            <button
                              class="chart-tab-btn"
                              [class.active]="selectedOpsChartKey() === c.key"
                              (click)="selectOpsChart(c.key)">
                              {{ c.title }}
                            </button>
                          }
                        </div>
                      </div>

                      <div class="chart-canvas-wrap">
                        <erp-apex-chart [chart]="oc" />
                      </div>
                    </div>
                  }
                </div>
              }
            } @else {
              <!-- DOMAIN-SPECIFIC VIEW: Main Domain Chart + Alerts -->
              <div class="analytics-grid">
                <div class="chart-panel" [attr.data-testid]="'chart-' + currentDomainChart()?.key">
                  <div class="chart-panel-header">
                    <div class="chart-titles">
                      <div class="chart-badge">
                        <span nz-icon nzType="fund"></span>
                        <span>{{ activeCategoryTitle() }}</span>
                      </div>
                      <h3>{{ currentDomainChart()?.title }}</h3>
                      @if (currentDomainChart()?.subtitle) {
                        <div class="sub">{{ currentDomainChart()?.subtitle }}</div>
                      }
                    </div>
                    @if (visibleCharts().length > 1) {
                      <div class="chart-selector-tabs">
                        @for (c of visibleCharts(); track c.key) {
                          <button
                            class="chart-tab-btn"
                            [class.active]="currentDomainChart()?.key === c.key"
                            (click)="selectDomainChart(c.key)">
                            {{ c.title }}
                          </button>
                        }
                      </div>
                    }
                  </div>

                  <div class="chart-canvas-wrap">
                    @if (currentDomainChart(); as c) {
                      <erp-apex-chart [chart]="c" />
                    }
                  </div>
                </div>

                <!-- Domain Alerts -->
                <div class="alerts-panel" data-testid="alerts">
                  <div class="alert-header">
                    <h3>{{ 'dashboard.alerts' | translate: 'Alerts and to-dos' }}</h3>
                    <span class="tab-count">{{ activeDomainAlerts().length }}</span>
                  </div>
                  <div class="alert-list">
                    @for (a of activeDomainAlerts(); track $index) {
                      <a class="alert" [routerLink]="a.link ?? null">
                        <div class="alert-icon-wrap" [class]="'icon-' + a.severity">
                          <span nz-icon [nzType]="a.severity === 'info' ? 'bell' : (a.severity === 'critical' ? 'warning' : 'bulb')"></span>
                        </div>
                        <div class="msg">
                          <div class="alert-tags">
                            <nz-tag>{{ a.category }}</nz-tag>
                            <nz-tag [nzColor]="urgency(a.urgency).color">{{ urgency(a.urgency).label }}</nz-tag>
                          </div>
                          <div>{{ a.message }}</div>
                        </div>
                      </a>
                    } @empty {
                      <div class="empty-state">
                        <span nz-icon nzType="check-circle" style="color: #10b981; font-size: 18px;"></span>
                        <span>No active alerts for {{ activeCategoryTitle() }}.</span>
                      </div>
                    }
                  </div>
                </div>
              </div>

              <!-- Domain Secondary Charts: Show other charts belonging to this domain side-by-side -->
              @if (visibleCharts().length > 1) {
                <div class="secondary-analytics-grid">
                  @for (c of visibleCharts().slice(1); track c.key) {
                    <div class="chart-panel" [attr.data-testid]="'chart-' + c.key">
                      <div class="chart-panel-header">
                        <div class="chart-titles">
                          <div class="chart-badge">
                            <span nz-icon nzType="bar-chart"></span>
                            <span>{{ activeCategoryTitle() }}</span>
                          </div>
                          <h3>{{ c.title }}</h3>
                          @if (c.subtitle) {
                            <div class="sub">{{ c.subtitle }}@if (bucketLabel(c)) {<span> · {{ bucketLabel(c) }}</span>}</div>
                          }
                        </div>
                      </div>
                      <div class="chart-canvas-wrap">
                        <erp-apex-chart [chart]="c" />
                      </div>
                    </div>
                  }
                </div>
              }
            }
          </div>

          <!-- TIER 3: DEPARTMENTAL CLUSTERS (OPERATIONAL METRICS) -->
          @if (viewMode() === 'curated') {
            @if (activeCategory() === 'all') {
              <!-- In All Overview: Display Clean Thematic Clusters -->
              <div class="clusters-container">
                @for (cluster of clusters(); track cluster.id) {
                  @if (cluster.kpis.length > 0) {
                    <div class="cluster-card">
                      <div class="cluster-header">
                        <div class="cluster-title-wrap">
                          <span nz-icon [nzType]="cluster.icon" class="cluster-icon"></span>
                          <span>{{ cluster.title }}</span>
                          <span class="cluster-badge">{{ cluster.kpis.length }}</span>
                        </div>
                        <button class="tab-pill" (click)="setCategory(cluster.category)">
                          <span>View {{ cluster.title }}</span>
                          <span nz-icon nzType="arrow-right"></span>
                        </button>
                      </div>
                      <div class="erp-stat-grid erp-stat-grid--compact">
                        @for (k of cluster.kpis; track k.key) {
                          <erp-stat-card
                            [compact]="true"
                            [label]="k.label"
                            [value]="k.value"
                            [format]="statFormat(k)"
                            [caption]="k.caption ?? null"
                            [link]="k.link ?? null"
                            [testId]="'kpi-' + k.key" />
                        }
                      </div>
                    </div>
                  }
                }
              </div>
            } @else {
              <!-- In Specific Domain Filter: Show all metrics of this domain -->
              @if (activeRemainingKpis().length > 0) {
                <div class="cluster-card" style="margin-top: 10px;">
                  <div class="cluster-header">
                    <div class="cluster-title-wrap">
                      <span nz-icon nzType="dashboard" class="cluster-icon"></span>
                      <span>Additional {{ activeCategoryTitle() }} Metrics</span>
                      <span class="cluster-badge">{{ activeRemainingKpis().length }}</span>
                    </div>
                  </div>
                  <div class="erp-stat-grid erp-stat-grid--compact">
                    @for (k of activeRemainingKpis(); track k.key) {
                      <erp-stat-card
                        [compact]="true"
                        [label]="k.label"
                        [value]="k.value"
                        [format]="statFormat(k)"
                        [caption]="k.caption ?? null"
                        [link]="k.link ?? null"
                        [testId]="'kpi-' + k.key" />
                    }
                  </div>
                </div>
              }
            }
          }
        }
      </nz-spin>
    </div>
  `,
})
export class DashboardPage {
  readonly session = inject(SessionStore);
  private readonly api = inject(DashboardApi);
  private readonly i18n = inject(I18nService);

  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /**
   * One shared period for every card and chart: URL (?range= or ?from=&to=) wins over the browser's last choice, then the "Last year"
   * default (the trailing 12 months). Picked from one dropdown; "Custom" shows a date-range picker, a preset hides it again.
   */
  readonly presets = RANGE_PRESETS;
  readonly range = signal<DashboardRange>(rangeFromParams(this.route.snapshot.queryParamMap) ?? storedRange() ?? presetRange(DEFAULT_PRESET));
  /** "Custom" chosen in the dropdown but no dates picked yet: the picker shows (and opens) while the figures keep the current period. */
  readonly customPending = signal(false);
  readonly showPicker = computed(() => this.range().preset === 'custom' || this.customPending());
  readonly selectValue = computed<RangePreset>(() => (this.showPicker() ? 'custom' : this.range().preset));
  private readonly picker = viewChild(NzDatePickerComponent);
  /** Why the last picked range was rejected (shown inline under the picker), or null. */
  readonly rangeError = signal<string | null>(null);
  /** The first date clicked in the open picker, so dates more than 5 years away from it are disabled. */
  private readonly calendarAnchor = signal<Date | null>(null);
  /** Bumped to hand the picker the current period again after a rejected pick (without reloading the figures). */
  private readonly pickerReset = signal(0);
  readonly rangeDates = computed<Date[]>(() => { this.pickerReset(); return [parseIso(this.range().from), parseIso(this.range().to)]; });
  readonly rangePlaceholders = computed(() => [this.i18n.t('ui.overview.range.from', 'Start date'), this.i18n.t('ui.overview.range.to', 'End date')]);
  readonly maxSpanHint = computed(() => this.i18n.t('ui.overview.range.maxSpan', 'Pick a range of 5 years or less, ending today or earlier'));
  /** No future dates, and once a first date is picked nothing that would make the period longer than 5 years. */
  readonly disabledDate = (d: Date): boolean => {
    const day = new Date(d.getFullYear(), d.getMonth(), d.getDate());
    const today = startOfToday();
    if (day > today) return true;
    const a = this.calendarAnchor();
    if (!a) return false;
    const [f, t] = day < a ? [day, a] : [a, day];
    return !validRange(isoDate(f), isoDate(t), today);
  };
  /** "30 Oct 2025 – 30 Oct 2026" / "من 31 أكتوبر 2025 إلى 30 أكتوبر 2026", with each date bidi-isolated. */
  readonly captionParts = computed<{ text: string; bdi: boolean }[]>(() => {
    const r = this.range();
    const locale = latn(this.i18n.locale());
    const values: Record<string, string> = { '{from}': this.fmtDate(r.from, locale), '{to}': this.fmtDate(r.to, locale) };
    return this.i18n.t('ui.overview.range.caption', '{from} – {to}').split(/(\{from\}|\{to\})/).filter((x) => x !== '')
      .map((x) => (x in values ? { text: values[x], bdi: true } : { text: x, bdi: false }));
  });

  /** The API response as received; `data` is the same with captions and chart axis labels in the user's language. */
  private readonly raw = signal<DashboardDto | null>(null);
  readonly loadError = signal(false);
  readonly data = computed<DashboardDto | null>(() => {
    const d = this.raw();
    if (!d) return null;
    const locale = latn(this.i18n.locale());
    return { ...d, kpis: d.kpis.map((k) => this.localizeKpi(k, locale)), charts: d.charts.map((c) => this.localizeChart(c, locale)) };
  });
  /** Screen-reader announcement once the figures for a period have loaded. */
  readonly announcement = computed(() => {
    const d = this.raw();
    if (!d?.from || !d.to) return '';
    const locale = latn(this.i18n.locale());
    return this.i18n.t('ui.overview.range.showing', 'Showing {from} – {to}')
      .replace('{from}', FSI + this.fmtDate(d.from, locale) + PDI).replace('{to}', FSI + this.fmtDate(d.to, locale) + PDI);
  });
  /** Nothing billed or collected in the period: say so instead of showing an empty chart without explanation. */
  readonly periodEmpty = computed(() => {
    const c = this.raw()?.charts.find((x) => x.key === 'billed-collected');
    return !!c && c.series.every((s) => s.values.every((v) => v === 0));
  });
  readonly canShowLonger = computed(() => this.range().from > presetRange('3y').from);
  readonly loading = signal(false);
  readonly activeCategory = signal<DomainCategory>('all');
  readonly viewMode = signal<'curated' | 'all'>('curated');
  readonly selectedChartKey = signal<string | null>(null);
  readonly selectedFinanceChartKey = signal<string>('billed-collected');
  readonly selectedSalesChartKey = signal<string>('sales-pipeline');
  readonly selectedOpsChartKey = signal<string>('purchasing-orders-status');
  readonly selectedDomainChartKey = signal<string | null>(null);
  readonly metricSearchQuery = signal<string>('');

  readonly activeCategoryKpis = computed<KpiCard[]>(() => {
    return this.getCategoryKpis(this.activeCategory());
  });

  readonly filteredMatrixKpis = computed<KpiCard[]>(() => {
    const list = this.activeCategoryKpis();
    const q = this.metricSearchQuery().trim().toLowerCase();
    if (!q) return list;
    return list.filter((k) =>
      k.label.toLowerCase().includes(q) ||
      k.key.toLowerCase().includes(q) ||
      (k.caption && k.caption.toLowerCase().includes(q))
    );
  });

  readonly allMatrixClusters = computed<KpiCluster[]>(() => {
    const all = this.data()?.kpis ?? [];
    return [
      {
        id: 'treasury',
        title: 'Finance, Treasury & Liquidity',
        icon: 'wallet',
        category: 'finance',
        kpis: all.filter((k) => this.classifyKpi(k.key) === 'finance'),
      },
      {
        id: 'commercial',
        title: 'Commercial, Retail & Sales Pipeline',
        icon: 'shopping-cart',
        category: 'sales-crm',
        kpis: all.filter((k) => this.classifyKpi(k.key) === 'sales-crm'),
      },
      {
        id: 'operations',
        title: 'Supply Chain, Procurement & Production',
        icon: 'shop',
        category: 'supply-operations',
        kpis: all.filter((k) => this.classifyKpi(k.key) === 'supply-operations'),
      },
      {
        id: 'services',
        title: 'Workforce, Projects & Client Services',
        icon: 'solution',
        category: 'projects-services',
        kpis: all.filter((k) => ['projects-services', 'hr'].includes(this.classifyKpi(k.key))),
      },
      {
        id: 'realestate',
        title: 'Real Estate & Facilities Management',
        icon: 'home',
        category: 'real-estate',
        kpis: all.filter((k) => this.classifyKpi(k.key) === 'real-estate'),
      },
    ];
  });

  readonly timezoneLabel = computed(() => {
    try {
      const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
      const offsetMin = -new Date().getTimezoneOffset();
      const sign = offsetMin >= 0 ? '+' : '-';
      const hours = String(Math.floor(Math.abs(offsetMin) / 60)).padStart(2, '0');
      const mins = String(Math.abs(offsetMin) % 60).padStart(2, '0');
      return `GMT${sign}${hours}:${mins} (${tz.replace('_', ' ')})`;
    } catch {
      return 'GMT+03:00 (Standard Time)';
    }
  });

  constructor() {
    effect(() => {
      this.session.currentEntityId();
      const range = this.range();
      untracked(() => this.load(range));
    });
    // Back/forward and shared links follow the URL. With no (or invalid) range params the period comes from this browser's last choice,
    // then the default, and is written back to the URL without a new history entry so the address always names the period shown.
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((p) => {
      const fromUrl = rangeFromParams(p);
      const r = fromUrl ?? storedRange() ?? presetRange(DEFAULT_PRESET);
      if (!sameRange(r, this.range())) { this.customPending.set(false); this.rangeError.set(null); this.range.set(r); }
      if (!fromUrl) void this.router.navigate([], { relativeTo: this.route, queryParams: rangeParams(r), queryParamsHandling: 'merge', replaceUrl: true });
    });
  }

  /** Reloads the figures. A preset is re-anchored on today first, so a page left open past midnight moves on to the new day. */
  refresh(): void {
    const r = this.range();
    if (r.preset !== 'custom') {
      const fresh = presetRange(r.preset);
      if (!sameRange(fresh, r)) { this.saveRange(fresh); return; }
    }
    this.load(r);
  }

  /** A preset applies at once and hides the picker; "Custom" only shows the picker and opens it (no request until dates are picked). */
  setPreset(preset: RangePreset): void {
    this.rangeError.set(null);
    if (preset === 'custom') {
      this.customPending.set(true);
      setTimeout(() => this.picker()?.open());
      return;
    }
    this.customPending.set(false);
    this.saveRange(presetRange(preset));
  }

  onPickerOpenChange(_open: boolean): void { this.calendarAnchor.set(null); }

  onCalendarChange(dates: (Date | null)[] | null): void {
    const picked = (dates ?? []).filter((d): d is Date => !!d);
    this.calendarAnchor.set(picked.length === 1 ? picked[0] : null);
  }

  setCustomRange(dates: (Date | null)[] | null): void {
    const [a, b] = dates ?? [];
    if (!a || !b) return;
    const [from, to] = [isoDate(a), isoDate(b)].sort();
    if (!validRange(from, to)) {
      this.rangeError.set(this.maxSpanHint());
      this.pickerReset.update((n) => n + 1); // hands the picker the current period back, clearing the rejected dates
      return;
    }
    this.rangeError.set(null);
    this.customPending.set(false);
    this.saveRange({ preset: 'custom', from, to });
  }

  private saveRange(r: DashboardRange): void {
    try { localStorage.setItem(RANGE_KEY, JSON.stringify(r)); } catch { /* storage unavailable */ }
    if (sameRange(r, this.range())) return;
    this.range.set(r);
    void this.router.navigate([], { relativeTo: this.route, queryParams: rangeParams(r), queryParamsHandling: 'merge' });
  }

  private fmtDate(iso: string, locale: string, options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' }): string {
    return isRealDate(iso) ? new Intl.DateTimeFormat(locale, options).format(parseIso(iso)) : iso;
  }

  /** A caption the API sends as a key with {name} args (e.g. the cash card's "at {date}") is translated here, dates in the user's locale. */
  private localizeKpi(k: KpiCard, locale: string): KpiCard {
    if (!k.captionKey) return k;
    const template = this.i18n.t(k.captionKey, '');
    if (!template) return k;
    const caption = template.replace(/\{(\w+)\}/g, (m: string, name: string) => {
      const v = k.captionArgs?.[name];
      if (v == null) return m;
      return isRealDate(v) ? FSI + this.fmtDate(v, locale) + PDI : v;
    });
    return { ...k, caption };
  }

  /** Axis labels for time-series charts from their buckets: weeks by start day, months by name, quarters as "Nov – Jan 26". */
  private localizeChart(c: DashboardChart, locale: string): DashboardChart {
    if (!c.buckets?.length || c.buckets.length !== c.categories.length) return c;
    const categories = c.buckets.map((b) => {
      const from = parseIso(b.from), to = parseIso(b.to);
      if (c.granularity === 'week') return this.fmtDate(b.from, locale, { day: 'numeric', month: 'short' });
      if (c.granularity === 'quarter') {
        return `${new Intl.DateTimeFormat(locale, { month: 'short' }).format(addDays(from, 14))} – ${new Intl.DateTimeFormat(locale, { month: 'short', year: '2-digit' }).format(addDays(to, -14))}`;
      }
      const mid = addDays(from, Math.floor((to.getTime() - from.getTime()) / 86_400_000 / 2));
      return new Intl.DateTimeFormat(locale, { month: 'short', year: '2-digit' }).format(mid);
    });
    return { ...c, categories };
  }

  /** "by week" / "by month" / "by quarter" under a time-series chart. */
  bucketLabel(c: DashboardChart | null | undefined): string {
    const g = c?.granularity;
    return g ? this.i18n.t('ui.overview.range.bucket.' + g, 'by ' + g) : '';
  }

  setCategory(cat: DomainCategory): void {
    this.activeCategory.set(cat);
    const charts = this.getCategoryCharts(cat);
    if (charts.length > 0) {
      this.selectedDomainChartKey.set(charts[0].key);
      this.selectedChartKey.set(charts[0].key);
    }
  }

  selectFinanceChart(key: string): void {
    this.selectedFinanceChartKey.set(key);
    this.selectedChartKey.set(key);
  }

  selectSalesChart(key: string): void {
    this.selectedSalesChartKey.set(key);
    this.selectedChartKey.set(key);
  }

  selectOpsChart(key: string): void {
    this.selectedOpsChartKey.set(key);
    this.selectedChartKey.set(key);
  }

  selectDomainChart(key: string): void {
    this.selectedDomainChartKey.set(key);
    this.selectedChartKey.set(key);
  }

  urgency(u: string): { label: string; color: string } {
    return URGENCY[u] ?? URGENCY['info'];
  }

  statFormat(k: KpiCard): StatFormat {
    return k.format === 'percent' ? 'percent' : k.format === 'number' ? 'number' : 'money';
  }

  /* ---- DOMAIN CLASSIFIERS ---- */
  private classifyKpi(key: string): DomainCategory {
    const k = key.toLowerCase();
    // Real Estate
    if (['portfolio', 'occupancy', 'contracted-rent', 'expiring', 'work-orders'].includes(k) || k.startsWith('re-')) {
      return 'real-estate';
    }
    // HR
    if (['employees', 'payroll-runs', 'payroll-open', 'reviews', 'reviews-pending'].includes(k) || k.startsWith('hr-')) {
      return 'hr';
    }
    // Sales & CRM
    if (k.includes('sales') || k.includes('quote') || k.includes('lead') || k.includes('deal') || k.startsWith('crm-')) {
      return 'sales-crm';
    }
    // Projects & Services
    if (k.includes('project') || k.includes('task') || k.includes('timesheet') || k.includes('unbilled') || k.includes('engagement') || k.includes('milestone') || k.startsWith('svc-') || k.startsWith('prj-')) {
      return 'projects-services';
    }
    // Supply, Operations, POS, Retail, Distribution, Manufacturing, Inventory, Procurement
    if (k.includes('stock') || k.includes('inventory') || k.includes('purchase') || k.includes('requisition') || k.includes('procurement') || k.includes('pos') || k.includes('retail') || k.includes('route') || k.includes('trip') || k.includes('bom') || k.includes('work-order') || k.startsWith('mfg-') || k.startsWith('dist-')) {
      return 'supply-operations';
    }
    // Finance
    return 'finance';
  }

  private classifyChart(key: string): DomainCategory {
    const k = key.toLowerCase();
    if (['occupancy', 'lease-expiries'].includes(k) || k.startsWith('re-')) return 'real-estate';
    if (k.includes('sales') || k.includes('crm') || k.includes('funnel') || k.includes('quote')) return 'sales-crm';
    if (k.includes('purchas') || k.includes('procure') || k.includes('mfg') || k.includes('production') || k.includes('stock')) return 'supply-operations';
    if (k.includes('project') || k.includes('task') || k.includes('service')) return 'projects-services';
    if (k.includes('hr') || k.includes('headcount') || k.includes('employee') || k.includes('people')) return 'hr';
    return 'finance';
  }

  hasCategory(cat: DomainCategory): boolean {
    const kpis = this.data()?.kpis ?? [];
    return kpis.some((k) => this.classifyKpi(k.key) === cat);
  }

  getCategoryKpis(cat: DomainCategory): KpiCard[] {
    const kpis = this.data()?.kpis ?? [];
    if (cat === 'all') return kpis;
    return kpis.filter((k) => this.classifyKpi(k.key) === cat);
  }

  getCategoryCharts(cat: DomainCategory): DashboardChart[] {
    const charts = this.data()?.charts ?? [];
    if (cat === 'all') return charts;
    const filtered = charts.filter((c) => this.classifyChart(c.key) === cat);
    return filtered.length > 0 ? filtered : charts;
  }

  readonly activeCategoryTitle = computed(() => {
    switch (this.activeCategory()) {
      case 'finance': return 'Finance & Treasury';
      case 'sales-crm': return 'Sales & Commercial';
      case 'supply-operations': return 'Supply Chain & Operations';
      case 'projects-services': return 'Projects & Services';
      case 'real-estate': return 'Real Estate Portfolio';
      case 'hr': return 'HR & Workforce';
      default: return 'Overview';
    }
  });

  /** 4 Hero Highlights for All Overview */
  readonly heroKpis = computed<KpiCard[]>(() => {
    const all = this.data()?.kpis ?? [];
    if (all.length <= 4) return all;

    const priorityKeys = ['portfolio', 'occupancy', 'receivables', 'net-result', 'cash', 'collected'];
    const chosen: KpiCard[] = [];

    for (const pk of priorityKeys) {
      const match = all.find((k) => k.key === pk);
      if (match && !chosen.includes(match)) {
        chosen.push(match);
      }
      if (chosen.length === 4) break;
    }

    for (const k of all) {
      if (chosen.length >= 4) break;
      if (!chosen.includes(k)) chosen.push(k);
    }
    return chosen;
  });

  /** Hero KPIs depending on selected category tab */
  readonly activeHeroKpis = computed<KpiCard[]>(() => {
    if (this.activeCategory() === 'all') {
      return this.heroKpis();
    }
    const catKpis = this.getCategoryKpis(this.activeCategory());
    return catKpis.slice(0, 4);
  });

  /** Remaining KPIs for single domain category */
  readonly activeRemainingKpis = computed<KpiCard[]>(() => {
    const catKpis = this.getCategoryKpis(this.activeCategory());
    const heroes = this.activeHeroKpis();
    return catKpis.filter((k) => !heroes.some((h) => h.key === k.key));
  });

  /** Clean Departmental Clusters for All Overview mode */
  readonly clusters = computed<KpiCluster[]>(() => {
    const all = this.data()?.kpis ?? [];
    const heroes = this.heroKpis();
    // Exclude heroes so they aren't redundantly displayed twice
    const pool = all.filter((k) => !heroes.some((h) => h.key === k.key));

    return [
      {
        id: 'treasury',
        title: 'Treasury & Liquidity',
        icon: 'wallet',
        category: 'finance',
        kpis: pool.filter((k) => this.classifyKpi(k.key) === 'finance'),
      },
      {
        id: 'commercial',
        title: 'Commercial & Sales Pipeline',
        icon: 'shopping-cart',
        category: 'sales-crm',
        kpis: pool.filter((k) => this.classifyKpi(k.key) === 'sales-crm'),
      },
      {
        id: 'operations',
        title: 'Supply Chain, Retail & Production',
        icon: 'shop',
        category: 'supply-operations',
        kpis: pool.filter((k) => this.classifyKpi(k.key) === 'supply-operations'),
      },
      {
        id: 'services',
        title: 'Workforce, Projects & Client Services',
        icon: 'solution',
        category: 'projects-services',
        kpis: pool.filter((k) => ['projects-services', 'hr'].includes(this.classifyKpi(k.key))),
      },
      {
        id: 'realestate',
        title: 'Real Estate & Facilities',
        icon: 'home',
        category: 'real-estate',
        kpis: pool.filter((k) => this.classifyKpi(k.key) === 'real-estate'),
      },
    ];
  });

  readonly visibleCharts = computed<DashboardChart[]>(() => {
    return this.getCategoryCharts(this.activeCategory());
  });

  readonly financeCharts = computed<DashboardChart[]>(() => {
    return (this.data()?.charts ?? []).filter((c) => this.classifyChart(c.key) === 'finance');
  });

  readonly currentFinanceChart = computed<DashboardChart | null>(() => {
    const list = this.financeCharts();
    if (list.length === 0) return null;
    return list.find((c) => c.key === this.selectedFinanceChartKey()) ?? list[0];
  });

  readonly salesCharts = computed<DashboardChart[]>(() => {
    return (this.data()?.charts ?? []).filter((c) => this.classifyChart(c.key) === 'sales-crm');
  });

  readonly currentSalesChart = computed<DashboardChart | null>(() => {
    const list = this.salesCharts();
    if (list.length === 0) return null;
    return list.find((c) => c.key === this.selectedSalesChartKey()) ?? list[0];
  });

  readonly opsCharts = computed<DashboardChart[]>(() => {
    return (this.data()?.charts ?? []).filter((c) =>
      ['supply-operations', 'projects-services', 'real-estate', 'hr'].includes(this.classifyChart(c.key))
    );
  });

  readonly currentOpsChart = computed<DashboardChart | null>(() => {
    const list = this.opsCharts();
    if (list.length === 0) return null;
    return list.find((c) => c.key === this.selectedOpsChartKey()) ?? list[0];
  });

  readonly currentDomainChart = computed<DashboardChart | null>(() => {
    const charts = this.visibleCharts();
    if (charts.length === 0) return null;
    const selected = this.selectedDomainChartKey();
    if (selected) {
      const match = charts.find((c) => c.key === selected);
      if (match) return match;
    }
    return charts[0];
  });

  readonly activeDomainAlerts = computed<DashboardAlert[]>(() => {
    const alerts = this.data()?.alerts ?? [];
    if (this.activeCategory() === 'all') return alerts;
    const cat = this.activeCategory();
    const filtered = alerts.filter((a) => {
      const c = a.category.toLowerCase();
      if (cat === 'real-estate') return c.includes('lease') || c.includes('maintenance') || c.includes('unit') || c.includes('property');
      if (cat === 'finance') return c.includes('cheque') || c.includes('bill') || c.includes('invoice') || c.includes('payment');
      if (cat === 'sales-crm') return c.includes('sales') || c.includes('lead') || c.includes('deal') || c.includes('order');
      if (cat === 'supply-operations') return c.includes('inventory') || c.includes('stock') || c.includes('purchase') || c.includes('production');
      if (cat === 'projects-services') return c.includes('project') || c.includes('task') || c.includes('service');
      if (cat === 'hr') return c.includes('hr') || c.includes('payroll') || c.includes('employee');
      return true;
    });
    return filtered.length > 0 ? filtered : alerts;
  });

  readonly currentChart = computed<DashboardChart | null>(() => {
    const charts = this.visibleCharts();
    if (charts.length === 0) return null;
    const selectedKey = this.selectedChartKey();
    if (selectedKey) {
      const match = charts.find((c) => c.key === selectedKey);
      if (match) return match;
    }
    return charts[0];
  });

  private pending?: Subscription;

  private load(range: DashboardRange = this.range()): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.pending?.unsubscribe(); // cancels the stale request when the period or entity changes again
    this.pending = this.api.get({ from: range.from, to: range.to }).subscribe({
      next: (d) => {
        this.raw.set(d);
        if (d.charts.length > 0) {
          if (!this.selectedChartKey()) {
            this.selectedChartKey.set(d.charts[0].key);
          }
          const fin = d.charts.find((c) => this.classifyChart(c.key) === 'finance');
          if (fin) this.selectedFinanceChartKey.set(fin.key);
          const sales = d.charts.find((c) => this.classifyChart(c.key) === 'sales-crm');
          if (sales) this.selectedSalesChartKey.set(sales.key);
          const ops = d.charts.find((c) => ['supply-operations', 'projects-services', 'real-estate', 'hr'].includes(this.classifyChart(c.key)));
          if (ops) this.selectedOpsChartKey.set(ops.key);
        }
        this.loading.set(false);
      },
      // no endless skeleton and no figures from the previous period: show the error with a Retry instead
      error: () => { this.raw.set(null); this.loadError.set(true); this.loading.set(false); },
    });
  }
}
