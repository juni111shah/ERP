import { Component, Input, Output, EventEmitter, computed, inject, OnInit, OnChanges } from '@angular/core';
import { PageTitleService } from '@core/services/page-title.service';
import { SessionStore } from '@core/session/session.store';
import { RecordHistoryComponent } from '../record-history/record-history.component';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { HasPermissionDirective } from '../../directives/has-permission.directive';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { RevealDirective } from '../../directives/reveal.directive';
import { StatCardComponent, StatTone } from '../stat-card/stat-card.component';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { displayDate, displayLabel, statusTone, StatusTone as DisplayTone } from '@core/format/display';

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
  styleUrl: './detail-page.component.scss',
  templateUrl: './detail-page.component.html'
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
