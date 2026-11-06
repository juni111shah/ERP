import { Component, Input, Output, EventEmitter, computed, inject, OnInit, OnChanges } from '@angular/core';
import { PageTitleService } from '../core/services/page-title.service';
import { SessionStore } from '../core/session/session.store';
import { RecordHistoryComponent } from './record-history.component';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { HasPermissionDirective } from './has-permission.directive';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { RevealDirective } from './reveal.directive';
import { StatCardComponent, StatTone } from './stat-card.component';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { displayDate, displayLabel, statusTone, StatusTone as DisplayTone } from '../core/format/display';

const BADGE_COLOR: Record<DisplayTone, string> = { success: 'success', info: 'processing', warning: 'warning', error: 'error', orange: 'orange', neutral: 'default', plain: 'default' };

export interface KpiCard {
  title: string;
  value: number | string;
  suffix?: string;
  /** Legacy colour hint ({ color }): red/green shades map to the stat card's danger/success tones. */
  valueStyle?: Record<string, string>;
  caption?: string;
  tone?: StatTone;
}

/** Maps the legacy `valueStyle.color` of a KpiCard to a stat-card tone (red-ish = danger, green-ish = success). */
export function kpiTone(card: KpiCard, index: number): StatTone {
  if (card.tone !== undefined) return card.tone;
  const c = (card.valueStyle?.['color'] ?? '').toLowerCase();
  if (c === '#ff4d4f' || c === '#cf1322' || c === 'red') return 'danger';
  if (c === '#52c41a' || c === '#389e0d' || c === 'green') return 'success';
  return index;
}

export interface ActionButton {
  label: string;
  icon: string;
  type?: 'primary' | 'default' | 'dashed' | 'link' | 'text';
  danger?: boolean;
  permission?: string;
  visible?: boolean;
  /** Create/Add action: icon-first with the label revealed on hover / keyboard focus, plus a tooltip and aria-label. */
  reveal?: boolean;
  /** Translate key for the tooltip / aria-label of a `reveal` action (falls back to `label`). */
  labelKey?: string;
  handler: () => void;
}

export interface DescriptionItem {
  label: string;
  value: string | number | null;
  span?: number;
  routerLink?: string[];
}

@Component({
  selector: 'erp-detail-page',
  standalone: true,
  imports: [StatCardComponent, TranslatePipe, 
    CommonModule, RouterLink,
    NzButtonModule, NzCardModule, NzDescriptionsModule,
    NzIconModule, NzTabsModule, NzTagModule,
    HasPermissionDirective, RevealDirective, NzToolTipModule, RecordHistoryComponent
  ],
  styles: [`
    .erp-page-title {
      font-size: 24px;
      font-weight: 500;
      margin: 0 0 16px;
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .action-bar {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      margin-bottom: 16px;
    }
  `],
  template: `
    @if (backLink) {
      <p><a [routerLink]="backLink">← {{ backLabel }}</a></p>
    }
    
    <h1 class="erp-page-title">{{ title }}</h1>

    @if (actions.length > 0) {
      <div class="action-bar">
        @for (action of actions; track action.label) {
          @if (action.visible !== false) {
            <button 
              *erpHasPermission="action.permission ?? ''"
              nz-button 
              erpReveal [attr.data-reveal]="action.reveal ? 'on' : 'off'"
              [attr.aria-label]="action.reveal ? ((action.labelKey ?? '') | translate: action.label) : null"
              nz-tooltip [nzTooltipTitle]="action.reveal ? ((action.labelKey ?? '') | translate: action.label) : null"
              [nzType]="action.type ?? 'default'"
              [nzDanger]="action.danger ?? false"
              (click)="action.handler()">
              <span nz-icon [nzType]="action.icon"></span> {{ action.label }}
            </button>
          }
        }
      </div>
    }

    @if (kpiCards.length > 0) {
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="detail-kpis">
        @for (card of kpiCards; track card.title; let i = $index) {
          <erp-stat-card [label]="card.title" [value]="card.value" [format]="isNumber(card.value) ? 'number' : 'text'" [suffix]="card.suffix ?? null" [caption]="card.caption ?? null" [tone]="tone(card, i)" />
        }
      </div>
    }

    @if (descriptions.length > 0) {
      <nz-card style="margin-top: 16px;" [nzTitle]="detailsTitle">
        <nz-descriptions nzBordered [nzColumn]="2">
          @for (item of descriptions; track item.label) {
            <nz-descriptions-item [nzTitle]="item.label" [nzSpan]="item.span ?? 1">
              @if (item.routerLink) {
                <a [routerLink]="item.routerLink">{{ item.value }}</a>
              } @else {
                {{ shown(item.value) }}
              }
            </nz-descriptions-item>
          }
        </nz-descriptions>
      </nz-card>
    }

    <ng-content select="[content]"></ng-content>

    <nz-card style="margin-top: 24px;">
      <nz-tabset>
        <ng-content select="[tabs]"></ng-content>
        
        @if (showDocuments && hasDocumentsContent) {
          <nz-tab nzTitle="Documents">
            <div style="padding: 16px;">
              <ng-content select="[documents]"></ng-content>
            </div>
          </nz-tab>
        }
        
        @if (showHistory && canSeeHistory()) {
          <nz-tab [nzTitle]="'history.title' | translate: 'History'">
            <div style="padding: 16px;">
              <ng-content select="[history]"></ng-content>
              @if (recordType && recordId) {
                <erp-record-history [recordType]="recordType" [recordId]="recordId" />
              } @else if (!hasHistoryContent) {
                <div class="history-item">
                  <div class="history-timestamp">{{ createdAt }}</div>
                  <div>{{ entityName }} {{ 'ui.detail.createdLc' | translate: 'created' }}</div>
                </div>
              }
            </div>
          </nz-tab>
        }
      </nz-tabset>
    </nz-card>
  `
})
export class DetailPageComponent implements OnInit, OnChanges {
  readonly tone = kpiTone;
  isNumber(v: unknown): boolean { return typeof v === 'number'; }
  shown(v: string | number | null): string | number { return v === null || v === undefined ? '—' : displayDate(v) || v; }
  @Input() backLink?: string[];
  @Input() backLabel = 'Back';
  @Input() title = '';
  @Input() status?: string;
  @Input() actions: ActionButton[] = [];
  @Input() kpiCards: KpiCard[] = [];
  @Input() descriptions: DescriptionItem[] = [];
  @Input() detailsTitle = 'Details';
  @Input() showDocuments = true;
  @Input() showHistory = true;
  @Input() hasDocumentsContent = false;
  @Input() hasHistoryContent = false;
  @Input() entityName = 'Record';
  @Input() createdAt?: string;
  /** Audit record class ("Invoice", "Bill", "Party"...) and id: when both are set the History tab shows the record's change history. */
  @Input() recordType?: string;
  @Input() recordId?: string;
  private readonly session = inject(SessionStore);
  private readonly pageTitle = inject(PageTitleService);

  ngOnInit(): void {
    this.updateTopBar();
  }

  ngOnChanges(): void {
    this.updateTopBar();
  }

  private updateTopBar(): void {
    if (this.title) {
      this.pageTitle.setTitle(
        this.title,
        this.entityName !== 'Record' ? this.entityName : null,
        this.status ? { text: displayLabel(this.status), color: BADGE_COLOR[statusTone(this.status)] } : null
      );
    }
  }

  /** The change history is only for people who may read the audit log. */
  canSeeHistory(): boolean { return !this.recordType || this.session.hasPermission('core.audit.read'); }
}
