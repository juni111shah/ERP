import { Component, Input, Output, EventEmitter, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { HasPermissionDirective } from './has-permission.directive';
import { TranslatePipe } from '../core/i18n/translate.pipe';

export interface KpiCard {
  title: string;
  value: number | string;
  suffix?: string;
  valueStyle?: Record<string, string>;
}

export interface ActionButton {
  label: string;
  icon: string;
  type?: 'primary' | 'default' | 'dashed' | 'link' | 'text';
  danger?: boolean;
  permission?: string;
  visible?: boolean;
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
  imports: [TranslatePipe, 
    CommonModule, DatePipe, CurrencyPipe, RouterLink,
    NzButtonModule, NzCardModule, NzDescriptionsModule, NzGridModule,
    NzIconModule, NzStatisticModule, NzTabsModule, NzTagModule,
    HasPermissionDirective
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
    .status-tag {
      font-size: 12px;
      padding: 2px 8px;
      border-radius: 4px;
      text-transform: uppercase;
    }
    .status-draft { background: #f0f0f0; color: #666; }
    .status-approved { background: #e6f7ff; color: #1890ff; }
    .status-issued { background: #f6ffed; color: #52c41a; }
    .status-partial { background: #fff7e6; color: #fa8c16; }
    .status-paid { background: #f6ffed; color: #52c41a; }
    .status-overdue { background: #fff1f0; color: #f5222d; }
    .status-voided { background: #f5f5f5; color: #999; }
    .status-posted { background: #f6ffed; color: #52c41a; }
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
    
    <h1 class="erp-page-title">
      {{ title }}
      @if (status) {
        <span class="status-tag" [class]="'status-' + (status + '').toLowerCase()">
          {{ status }}
        </span>
      }
    </h1>

    @if (actions.length > 0) {
      <div class="action-bar">
        @for (action of actions; track action.label) {
          @if (action.visible !== false) {
            <button 
              *erpHasPermission="action.permission ?? ''"
              nz-button 
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
      <div nz-row [nzGutter]="[16, 16]">
        @for (card of kpiCards; track card.title) {
          <div nz-col [nzXs]="24" [nzMd]="6">
            <nz-card>
              <nz-statistic 
                [nzTitle]="card.title"
                [nzValue]="card.value"
                [nzSuffix]="card.suffix ?? ''"
                [nzValueStyle]="card.valueStyle ?? {}">
              </nz-statistic>
            </nz-card>
          </div>
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
                {{ item.value ?? '—' }}
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
        
        @if (showDocuments) {
          <nz-tab nzTitle="Documents">
            <div style="padding: 16px;">
              <ng-content select="[documents]"></ng-content>
              @if (!hasDocumentsContent) {
                <p><i>Document attachments will be displayed here</i></p>
              }
            </div>
          </nz-tab>
        }
        
        @if (showHistory) {
          <nz-tab nzTitle="History">
            <div style="padding: 16px;">
              <ng-content select="[history]"></ng-content>
              @if (!hasHistoryContent) {
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
export class DetailPageComponent {
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
}
