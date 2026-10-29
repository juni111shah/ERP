import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzLayoutModule } from 'ng-zorro-antd/layout';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, combineLatest, interval, map, of, startWith, switchMap } from 'rxjs';
import { toObservable } from '@angular/core/rxjs-interop';
import { TenantThemeService } from '../../theme/tenant-theme.service';
import { API_BASE_URL } from '../core/api/api-config';
import { AuthService } from '../core/auth/auth.service';
import { SessionStore } from '../core/session/session.store';
import { KNOWN_ICONS } from '../icons';
import { Dir } from '@angular/cdk/bidi';
import { I18nService } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { PageTitleService } from '../core/services/page-title.service';
import { BrandLogoComponent } from '../shared/brand-logo.component';
import { GlobalSearchComponent } from './global-search.component';
import { NotificationDrawerComponent } from './notification-drawer.component';

interface MenuGroup { id: string; label: string; order: number; items: { id: string; label: string; route: string; icon?: string; module: string }[]; }

/** App shell: white sidebar with the server-driven menu, header with workspace/entity switchers, notifications, theme and user menu. */
@Component({
  selector: 'erp-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Dir, TranslatePipe, RouterOutlet, RouterLink, RouterLinkActive, FormsModule, NzLayoutModule, NzMenuModule, NzIconModule, NzButtonModule,
    NzDropDownModule, NzAvatarModule, NzBadgeModule, NzTagModule, NzSelectModule, NzInputModule, NzToolTipModule, GlobalSearchComponent, NotificationDrawerComponent, BrandLogoComponent],
  styles: [`
    :host { display: block; height: 100vh; }
    nz-layout.root {
      height: 100vh;
      overflow: hidden;
      display: flex;
      flex-direction: row;
    }
    .sider {
      background: var(--erp-color-nav-bg);
      border-inline-end: 1px solid var(--erp-color-nav-border);
      overflow: visible !important;
      height: 100vh;
      position: relative;
      z-index: 100;
      transition: width 0.3s cubic-bezier(0.4, 0, 0.2, 1),
                  min-width 0.3s cubic-bezier(0.4, 0, 0.2, 1),
                  max-width 0.3s cubic-bezier(0.4, 0, 0.2, 1),
                  flex 0.3s cubic-bezier(0.4, 0, 0.2, 1) !important;
    }
    .sider ::ng-deep .ant-layout-sider-children {
      display: flex;
      flex-direction: column;
      height: 100%;
      overflow: visible !important;
      position: relative;
    }
    .brand {
      height: 60px;
      display: flex;
      align-items: center;
      padding: 0 15px;
      border-bottom: 1px solid var(--erp-color-border-subtle);
      position: relative;
      overflow: visible;
      flex-shrink: 0;
      transition: padding 0.28s cubic-bezier(0.4, 0, 0.2, 1);
    }
    .brand.collapsed {
      padding: 0;
      justify-content: center;
    }
    .brand erp-brand-logo {
      flex: 1;
      min-width: 0;
    }
    .brand.collapsed erp-brand-logo {
      width: 100%;
      display: flex;
      justify-content: center;
    }

    .sidebar-toggle-btn {
      position: absolute;
      top: 50%;
      transform: translateY(-50%);
      inset-inline-end: 14px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 28px;
      height: 28px;
      padding: 0;
      margin: 0;
      border: 1px solid transparent;
      border-radius: 6px;
      background: transparent;
      color: var(--erp-color-text-secondary, #64748b);
      cursor: pointer;
      flex-shrink: 0;
      transition: inset-inline-end 0.28s cubic-bezier(0.4, 0, 0.2, 1),
                  width 0.28s cubic-bezier(0.4, 0, 0.2, 1),
                  height 0.28s cubic-bezier(0.4, 0, 0.2, 1),
                  border-radius 0.28s cubic-bezier(0.4, 0, 0.2, 1),
                  background-color 0.2s ease,
                  border-color 0.2s ease,
                  box-shadow 0.28s cubic-bezier(0.4, 0, 0.2, 1),
                  color 0.2s ease,
                  transform 0.2s ease;
      outline: none;
      z-index: 101;
    }
    .sidebar-toggle-btn:hover {
      background: rgba(16, 149, 93, 0.08);
      color: #10955d;
      border-color: rgba(16, 149, 93, 0.2);
    }
    .sidebar-toggle-btn:active {
      transform: translateY(-50%) scale(0.94);
    }
    .sidebar-toggle-btn .anticon {
      font-size: 13px;
      line-height: 1;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      transition: font-size 0.2s ease;
    }

    .sidebar-toggle-btn.collapsed {
      inset-inline-end: -13px;
      width: 26px;
      height: 26px;
      border-radius: 50%;
      background: #ffffff;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.08), 0 1px 2px rgba(0, 0, 0, 0.04);
      color: var(--erp-color-text-secondary, #475569);
    }
    .sidebar-toggle-btn.collapsed:hover {
      background: #ffffff;
      color: #10955d;
      border-color: rgba(16, 149, 93, 0.4);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.14), 0 2px 4px rgba(0, 0, 0, 0.06);
      transform: translateY(-50%) scale(1.08);
    }
    .sidebar-toggle-btn.collapsed:active {
      transform: translateY(-50%) scale(0.96);
    }
    .sidebar-toggle-btn.collapsed .anticon {
      font-size: 11px;
    }

    :host-context([data-theme='dark']) .sidebar-toggle-btn.collapsed,
    :host-context(.dark) .sidebar-toggle-btn.collapsed {
      background: #1e293b;
      border-color: #334155;
      color: #94a3b8;
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.35);
    }
    :host-context([data-theme='dark']) .sidebar-toggle-btn.collapsed:hover,
    :host-context(.dark) .sidebar-toggle-btn.collapsed:hover {
      background: #1e293b;
      color: #34d399;
      border-color: rgba(52, 211, 153, 0.4);
    }

    /* Sidebar Search Field */
    .sidebar-search-container {
      padding: 10px 14px 6px;
      flex-shrink: 0;
      transition: padding 0.28s cubic-bezier(0.4, 0, 0.2, 1);
    }
    .sidebar-search-container.collapsed {
      padding: 8px 0;
      display: flex;
      justify-content: center;
    }
    .sidebar-search-input-wrap {
      display: flex;
      align-items: center;
      gap: 8px;
      height: 34px;
      padding: 0 10px;
      border-radius: 8px;
      background: var(--erp-color-bg-subtle, #f8fafc);
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
    }
    .sidebar-search-input-wrap:focus-within {
      background: #ffffff;
      border-color: #10955d;
      box-shadow: 0 0 0 3px rgba(16, 149, 93, 0.12);
    }
    :host-context([data-theme='dark']) .sidebar-search-input-wrap,
    :host-context(.dark) .sidebar-search-input-wrap {
      background: rgba(255, 255, 255, 0.04);
      border-color: rgba(255, 255, 255, 0.1);
    }
    :host-context([data-theme='dark']) .sidebar-search-input-wrap:focus-within,
    :host-context(.dark) .sidebar-search-input-wrap:focus-within {
      background: rgba(255, 255, 255, 0.08);
      border-color: #34d399;
      box-shadow: 0 0 0 3px rgba(52, 211, 153, 0.2);
    }
    .sidebar-search-icon {
      font-size: 13px;
      color: #94a3b8;
      flex-shrink: 0;
      transition: color 0.2s ease;
    }
    .sidebar-search-input-wrap:focus-within .sidebar-search-icon {
      color: #10955d;
    }
    .sidebar-search-input {
      border: none;
      outline: none;
      background: transparent;
      font-size: 12.5px;
      line-height: 1.4;
      width: 100%;
      min-width: 0;
      color: var(--erp-color-text, #1e293b);
      font-family: inherit;
      padding: 0;
    }
    .sidebar-search-input::placeholder {
      color: #94a3b8;
      font-size: 12px;
    }
    .sidebar-search-clear {
      border: none;
      background: transparent;
      padding: 0;
      margin: 0;
      cursor: pointer;
      color: #94a3b8;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-size: 13px;
      transition: color 0.15s ease, transform 0.15s ease;
      outline: none;
      flex-shrink: 0;
    }
    .sidebar-search-clear:hover {
      color: #64748b;
      transform: scale(1.1);
    }
    .sidebar-search-collapsed-btn {
      width: 36px;
      height: 36px;
      border-radius: 8px;
      border: 1px solid transparent;
      background: transparent;
      color: var(--erp-color-text-secondary, #64748b);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      font-size: 15px;
      padding: 0;
      transition: all 0.2s ease;
      outline: none;
    }
    .sidebar-search-collapsed-btn:hover {
      background: rgba(16, 149, 93, 0.08);
      color: #10955d;
      border-color: rgba(16, 149, 93, 0.2);
    }

    /* Sidebar empty results */
    .sidebar-no-results {
      padding: 28px 16px;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
    }
    .sidebar-no-results .no-results-icon {
      font-size: 24px;
      color: #cbd5e1;
    }
    .sidebar-no-results .no-results-text {
      font-size: 12px;
      color: #94a3b8;
      line-height: 1.4;
    }
    .sidebar-no-results .no-results-clear {
      margin-top: 4px;
      padding: 3px 10px;
      font-size: 11.5px;
      border-radius: 5px;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      background: #ffffff;
      color: #10955d;
      cursor: pointer;
      font-weight: 500;
      transition: all 0.2s ease;
    }
    .sidebar-no-results .no-results-clear:hover {
      background: rgba(16, 149, 93, 0.08);
      border-color: #10955d;
    }

    .nav-scroll-area {
      flex: 1;
      overflow-y: auto;
      overflow-x: hidden;
      min-height: 0;
      padding: 4px 0 24px;
      scrollbar-width: thin;
      scrollbar-color: transparent transparent;
      transition: scrollbar-color 0.2s ease;
    }
    .nav-scroll-area:hover {
      scrollbar-color: var(--erp-scrollbar-thumb, rgba(16, 149, 93, 0.35)) transparent;
    }
    .nav-scroll-area::-webkit-scrollbar {
      width: 4px;
    }
    .nav-scroll-area::-webkit-scrollbar-track {
      background: transparent;
    }
    .nav-scroll-area::-webkit-scrollbar-thumb {
      background: transparent;
      border-radius: 9999px;
      transition: background-color 0.2s ease;
    }
    .nav-scroll-area:hover::-webkit-scrollbar-thumb {
      background: var(--erp-scrollbar-thumb, rgba(16, 149, 93, 0.35));
    }
    .nav-scroll-area::-webkit-scrollbar-thumb:hover {
      background: var(--erp-scrollbar-thumb-hover, rgba(16, 149, 93, 0.65)) !important;
    }
    .main-layout {
      display: flex;
      flex-direction: column;
      height: 100vh;
      min-width: 0;
      overflow: hidden;
      background: var(--erp-color-bg-canvas);
      flex: 1 1 auto;
      transition: width 0.28s cubic-bezier(0.4, 0, 0.2, 1),
                  flex 0.28s cubic-bezier(0.4, 0, 0.2, 1);
    }
    .header {
      background: rgba(255, 255, 255, 0.92);
      backdrop-filter: blur(12px);
      -webkit-backdrop-filter: blur(12px);
      border-bottom: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 0 20px;
      height: 58px;
      flex-shrink: 0;
      min-width: 0;
      position: relative;
      z-index: 50;
      transition: background-color 0.25s ease, border-color 0.25s ease;
    }

    :host-context([data-theme='dark']) .header,
    :host-context(.dark) .header {
      background: rgba(15, 23, 42, 0.92);
      border-bottom-color: #334155;
    }

    .spacer { flex: 1; min-width: 8px; }
    .search { max-width: 360px; }

    /* Expandable Header Control Box (Entity Switcher: Icon only -> Slides open on hover) */
    .header-control-box {
      display: inline-flex;
      align-items: center;
      justify-content: flex-start;
      height: 34px;
      width: 34px;
      min-width: 34px;
      border-radius: 8px;
      border: 1px solid rgba(16, 149, 93, 0.22);
      background: rgba(255, 255, 255, 0.95);
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
      padding: 0;
      overflow: hidden;
      cursor: pointer;
      color: var(--erp-color-text-primary, #0f172a);
      transition: width 0.3s cubic-bezier(0.4, 0, 0.2, 1),
                  border-color 0.2s ease,
                  box-shadow 0.2s ease;
    }
    .header-control-box:hover,
    .header-control-box:focus-within,
    .header-control-box:has(.ant-select-open) {
      width: 210px;
      border-color: #10955d;
      box-shadow: 0 2px 8px rgba(16, 149, 93, 0.18);
    }

    .control-box-icon {
      width: 34px;
      height: 34px;
      font-size: 15px;
      color: #10955d;
      flex-shrink: 0;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      transition: transform 0.2s ease;
    }
    .header-control-box:hover .control-box-icon,
    .header-control-box:focus-within .control-box-icon,
    .header-control-box:has(.ant-select-open) .control-box-icon {
      transform: scale(1.06);
    }

    .header-entity-select {
      width: 170px !important;
      opacity: 0 !important;
      pointer-events: none !important;
      transform: translateX(-8px);
      transition: opacity 0.22s cubic-bezier(0.4, 0, 0.2, 1) 0.05s,
                  transform 0.25s cubic-bezier(0.4, 0, 0.2, 1) 0.05s;
    }
    :host-context([dir='rtl']) .header-entity-select {
      transform: translateX(8px);
    }

    .header-control-box:hover .header-entity-select,
    .header-control-box:focus-within .header-entity-select,
    .header-control-box:has(.ant-select-open) .header-entity-select {
      opacity: 1 !important;
      pointer-events: auto !important;
      transform: translateX(0) !important;
    }

    .header-entity-select ::ng-deep .ant-select-selector {
      border: none !important;
      background: transparent !important;
      box-shadow: none !important;
      padding: 0 8px 0 0 !important;
      height: 32px !important;
      display: flex !important;
      align-items: center !important;
    }
    :host-context([dir='rtl']) .header-entity-select ::ng-deep .ant-select-selector {
      padding: 0 0 0 8px !important;
    }
    .header-entity-select ::ng-deep .ant-select-selection-item,
    .header-entity-select ::ng-deep .ant-select-selection-placeholder {
      font-size: 13px !important;
      font-weight: 600 !important;
      color: var(--erp-color-text-primary, #0f172a) !important;
      line-height: normal !important;
      white-space: nowrap !important;
    }
    .header-entity-select ::ng-deep .ant-select-arrow {
      color: #64748b;
    }

    :host-context([data-theme='dark']) .header-control-box,
    :host-context(.dark) .header-control-box {
      background: #1e293b;
      border-color: #334155;
      color: #f1f5f9;
    }

    /* Expandable Quick Create (+ New Button: Icon only -> Slides open on hover) */
    .topbar-quick-create-btn {
      display: inline-flex;
      align-items: center;
      justify-content: flex-start;
      height: 34px;
      width: 34px;
      min-width: 34px;
      padding: 0;
      border-radius: 8px;
      border: 1px solid #10955d;
      background: #10955d;
      color: #ffffff;
      font-size: 13px;
      font-weight: 600;
      cursor: pointer;
      box-shadow: 0 1px 3px rgba(16, 149, 93, 0.25);
      overflow: hidden;
      white-space: nowrap;
      transition: width 0.3s cubic-bezier(0.4, 0, 0.2, 1),
                  background-color 0.2s ease,
                  border-color 0.2s ease,
                  box-shadow 0.2s ease,
                  transform 0.2s ease;
    }
    .topbar-quick-create-btn:hover,
    .topbar-quick-create-btn:focus-visible,
    .topbar-quick-create-btn.ant-dropdown-open,
    .topbar-quick-create-btn[aria-expanded='true'] {
      width: 92px;
      background: #0d7a4c;
      border-color: #0d7a4c;
      box-shadow: 0 3px 8px rgba(16, 149, 93, 0.35);
      transform: translateY(-1px);
    }
    .topbar-quick-create-btn:active {
      transform: translateY(0);
    }
    .quick-create-icon {
      width: 34px;
      height: 34px;
      font-size: 15px;
      font-weight: 700;
      flex-shrink: 0;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      line-height: 1;
    }
    .quick-create-label {
      opacity: 0;
      font-size: 13px;
      font-weight: 600;
      line-height: 1;
      transform: translateX(-6px);
      transition: opacity 0.22s cubic-bezier(0.4, 0, 0.2, 1) 0.05s,
                  transform 0.25s cubic-bezier(0.4, 0, 0.2, 1) 0.05s;
      white-space: nowrap;
    }
    :host-context([dir='rtl']) .quick-create-label {
      transform: translateX(6px);
    }
    .quick-create-caret {
      opacity: 0;
      font-size: 9.5px;
      margin-inline-start: 4px;
      margin-inline-end: 6px;
      transform: scale(0.7);
      transition: opacity 0.22s cubic-bezier(0.4, 0, 0.2, 1) 0.05s,
                  transform 0.25s cubic-bezier(0.4, 0, 0.2, 1) 0.05s;
    }
    .topbar-quick-create-btn:hover .quick-create-label,
    .topbar-quick-create-btn:focus-visible .quick-create-label,
    .topbar-quick-create-btn.ant-dropdown-open .quick-create-label,
    .topbar-quick-create-btn[aria-expanded='true'] .quick-create-label {
      opacity: 1;
      transform: translateX(0);
    }
    .topbar-quick-create-btn:hover .quick-create-caret,
    .topbar-quick-create-btn:focus-visible .quick-create-caret,
    .topbar-quick-create-btn.ant-dropdown-open .quick-create-caret,
    .topbar-quick-create-btn[aria-expanded='true'] .quick-create-caret {
      opacity: 0.9;
      transform: scale(1);
    }

    .quick-create-dropdown-panel {
      background: #ffffff;
      border-radius: 12px;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      box-shadow: 0 12px 36px rgba(0, 0, 0, 0.16);
      width: 600px;
      max-width: 90vw;
      padding: 14px 16px 16px;
    }
    :host-context([data-theme='dark']) .quick-create-dropdown-panel {
      background: #0f172a;
      border-color: #334155;
    }
    .quick-create-header {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 12px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: #10955d;
      padding-bottom: 10px;
      margin-bottom: 12px;
      border-bottom: 1px solid var(--erp-color-border-subtle, #f1f5f9);
    }
    .quick-create-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px;
    }
    @media (max-width: 640px) {
      .quick-create-grid { grid-template-columns: repeat(2, 1fr); }
    }
    .quick-create-col-title {
      font-size: 11px;
      font-weight: 700;
      color: #94a3b8;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      margin-bottom: 6px;
      padding-inline-start: 4px;
    }
    .quick-create-item {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 8px;
      border-radius: 6px;
      font-size: 12.5px;
      font-weight: 500;
      color: var(--erp-color-text-primary, #1e293b);
      text-decoration: none;
      transition: background 0.15s ease, color 0.15s ease;
    }
    .quick-create-item:hover {
      background: rgba(16, 149, 93, 0.08);
      color: #10955d;
    }
    .item-icon {
      font-size: 14px;
    }
    .item-icon.green { color: #10955d; }
    .item-icon.blue { color: #0284c7; }
    .item-icon.orange { color: #ea580c; }
    .item-icon.purple { color: #7c3aed; }
    .item-icon.red { color: #dc2626; }
    .item-icon.teal { color: #0d9488; }
    .item-icon.slate { color: #475569; }

    /* Topbar Divider */
    .topbar-divider {
      width: 1px;
      height: 22px;
      background: var(--erp-color-border-subtle, #e2e8f0);
      margin: 0 10px;
      flex-shrink: 0;
    }
    :host-context([data-theme='dark']) .topbar-divider,
    :host-context(.dark) .topbar-divider {
      background: #334155;
    }

    /* Actions Group */
    .topbar-actions-group {
      display: inline-flex;
      align-items: center;
      gap: 6px;
    }
    .topbar-action-btn {
      width: 36px;
      height: 36px;
      border-radius: 8px;
      border: 1px solid transparent;
      background: transparent;
      color: var(--erp-color-text-secondary, #64748b);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
      padding: 0;
      outline: none;
    }
    .topbar-action-btn:hover {
      background: rgba(16, 149, 93, 0.08);
      color: #10955d;
      border-color: rgba(16, 149, 93, 0.2);
      transform: translateY(-1px);
    }
    .topbar-action-btn:active {
      transform: translateY(0) scale(0.96);
    }
    .action-btn-icon {
      font-size: 16px;
      line-height: 1;
    }
    .bell-btn {
      position: relative;
      overflow: visible;
    }
    .bell-badge-count {
      position: absolute;
      top: -3px;
      inset-inline-end: -3px;
      height: 18px;
      min-width: 18px;
      padding: 0 4.5px;
      line-height: 18px;
      font-size: 10px;
      font-weight: 700;
      font-family: inherit;
      font-variant-numeric: tabular-nums;
      background: linear-gradient(135deg, #ef4444, #dc2626);
      color: #ffffff;
      border-radius: 9999px;
      box-shadow: 0 0 0 2px var(--erp-color-bg-surface, #ffffff), 0 2px 4px rgba(239, 68, 68, 0.35);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      z-index: 10;
      pointer-events: none;
      white-space: nowrap;
      letter-spacing: 0;
    }
    :host-context([data-theme='dark']) .bell-badge-count,
    :host-context(.dark) .bell-badge-count {
      box-shadow: 0 0 0 2px #0f172a, 0 2px 4px rgba(0, 0, 0, 0.5);
    }
    .lang-btn .lang-text {
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.02em;
      line-height: 1;
    }

    .topbar-avatar-btn {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      padding: 2px 4px 2px 2px;
      border-radius: 9999px;
      background: transparent;
      border: 1px solid transparent;
      cursor: pointer;
      outline: none;
      transition: all 0.2s ease;
      margin-left: 2px;
    }
    .topbar-avatar-btn:hover {
      background: rgba(16, 149, 93, 0.08);
      border-color: rgba(16, 149, 93, 0.2);
    }
    .user-avatar-ring {
      border-radius: 50%;
      padding: 1.5px;
      background: linear-gradient(135deg, #10955d, #34bc86);
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .header-avatar {
      background: #ffffff !important;
      color: #10955d !important;
      font-weight: 700 !important;
      font-size: 11px !important;
    }
    :host-context([data-theme='dark']) .header-avatar,
    :host-context(.dark) .header-avatar {
      background: #0f172a !important;
      color: #34d399 !important;
    }
    .avatar-caret {
      font-size: 10px;
      color: #64748b;
      margin-right: 2px;
    }
    .content {
      padding: 24px;
      overflow-y: auto;
      overflow-x: hidden;
      background: var(--erp-color-bg-canvas);
      flex: 1;
      min-height: 0;
      min-width: 0;
    }
    .group-title {
      padding: 14px 18px 4px;
      font-size: 11px;
      font-weight: 600;
      color: var(--erp-color-text-tertiary);
      text-transform: uppercase;
      letter-spacing: .06em;
      white-space: nowrap;
      overflow: hidden;
      height: 32px;
      line-height: 20px;
      opacity: 1;
      transform: translateX(0);
      transition: opacity 0.2s cubic-bezier(0.4, 0, 0.2, 1),
                  height 0.28s cubic-bezier(0.4, 0, 0.2, 1),
                  padding 0.28s cubic-bezier(0.4, 0, 0.2, 1),
                  transform 0.25s cubic-bezier(0.4, 0, 0.2, 1);
    }
    .group-title.collapsed {
      height: 0;
      padding-top: 0;
      padding-bottom: 0;
      opacity: 0;
      transform: translateX(-6px);
      pointer-events: none;
    }

    /* Polished, Fluidly Animated Menu Items */
    .sider ::ng-deep {
      .ant-menu {
        background: transparent !important;
        border-inline-end: none !important;
        width: 100% !important;
      }

      /* Collapsed Menu Items: perfectly centered 44x44px pill with NO Ant Design left-padding offset */
      .ant-menu-inline-collapsed {
        width: 64px !important;

        > .ant-menu-item {
          width: 44px !important;
          height: 44px !important;
          line-height: 44px !important;
          margin: 5px auto !important;
          padding: 0 !important;
          padding-left: 0 !important;
          padding-right: 0 !important;
          padding-inline: 0 !important;
          border-radius: 12px !important;
          display: flex !important;
          align-items: center !important;
          justify-content: center !important;
          text-align: center !important;
          overflow: visible !important;
          transition: background-color 0.2s ease, color 0.2s ease, border-color 0.2s ease !important;

          .ant-menu-title-content {
            width: 100% !important;
            height: 100% !important;
            margin: 0 !important;
            padding: 0 !important;
            display: flex !important;
            align-items: center !important;
            justify-content: center !important;
            overflow: visible !important;
          }

          .nav-item-inner {
            width: 100% !important;
            height: 100% !important;
            margin: 0 !important;
            padding: 0 !important;
            display: flex !important;
            align-items: center !important;
            justify-content: center !important;
            overflow: visible !important;
          }

          .nav-icon {
            width: 44px !important;
            min-width: 44px !important;
            height: 44px !important;
            margin: 0 !important;
            padding: 0 !important;
            display: flex !important;
            align-items: center !important;
            justify-content: center !important;
            font-size: 20px !important;
            line-height: 1 !important;
            flex-shrink: 0 !important;

            .anticon {
              margin: 0 !important;
              display: flex !important;
              align-items: center !important;
              justify-content: center !important;
              font-size: 20px !important;
              width: 20px !important;
              height: 20px !important;
            }
          }

          .nav-label {
            display: none !important;
            width: 0 !important;
            height: 0 !important;
            margin: 0 !important;
            padding: 0 !important;
            pointer-events: none !important;
          }
        }
      }

      .ant-menu-rtl .ant-menu-item .anticon,
      .ant-menu-rtl .ant-menu-submenu-title .anticon,
      .ant-menu-rtl .ant-menu-item .ant-menu-item-icon {
        margin: 0 !important;
      }

      /* Expanded Menu Items: comfortable rounded row */
      .ant-menu-inline:not(.ant-menu-inline-collapsed) {
        width: 100% !important;

        > .ant-menu-item {
          height: 42px !important;
          line-height: 42px !important;
          margin: 3px 10px !important;
          padding: 0 12px !important;
          padding-left: 12px !important;
          padding-right: 12px !important;
          padding-inline: 12px !important;
          width: calc(100% - 20px) !important;
          border-radius: 9px !important;
          display: flex !important;
          align-items: center !important;
          overflow: hidden !important;
          white-space: nowrap !important;
          transition: background-color 0.2s ease, color 0.2s ease, border-color 0.2s ease !important;

          .ant-menu-title-content {
            width: 100% !important;
            height: 100% !important;
            display: flex !important;
            align-items: center !important;
          }

          .nav-item-inner {
            display: flex !important;
            align-items: center !important;
            gap: 10px !important;
            width: 100% !important;
          }

          .nav-icon {
            width: 20px !important;
            min-width: 20px !important;
            height: 20px !important;
            font-size: 17px !important;
            display: inline-flex !important;
            align-items: center !important;
            justify-content: center !important;
            flex-shrink: 0 !important;
            margin: 0 !important;

            .anticon {
              display: inline-flex !important;
              align-items: center !important;
              justify-content: center !important;
              margin: 0 !important;
            }
          }

          .nav-label {
            display: inline-block !important;
            white-space: nowrap !important;
            overflow: hidden !important;
            text-overflow: ellipsis !important;
            font-size: 14px !important;
            opacity: 1 !important;
            max-width: 170px !important;
            margin: 0 !important;
            text-align: start !important;
          }
        }
      }

      .ant-menu-item {
        &::after {
          display: none !important;
        }

        &:not(.ant-menu-item-selected):hover {
          background: rgba(16, 149, 93, 0.08) !important;
          color: #10955d !important;

          .nav-icon {
            color: #10955d !important;
            transform: scale(1.08);
          }
        }

        &.ant-menu-item-selected {
          background: #e6f7ef !important;
          color: #10955d !important;
          border: 1px solid rgba(16, 149, 93, 0.25) !important;
          font-weight: 600 !important;

          .nav-icon {
            color: #10955d !important;
          }

          .nav-label {
            color: #10955d !important;
          }
        }
      }
    }
  `],
  template: `
    <nz-layout class="root" [dir]="i18n.dir()">
      <nz-sider class="sider" nzCollapsible [nzCollapsed]="collapsed()" [nzTrigger]="null" nzWidth="248px" [nzCollapsedWidth]="64" nzTheme="light" data-testid="sidebar">
        <div class="brand" [class.collapsed]="collapsed()" data-testid="sidebar-brand">
          <erp-brand-logo
            [collapsed]="collapsed()"
            [subtitle]="workspaceName()"
            [workspaces]="session.me()?.workspaces || []"
            [currentWorkspaceId]="session.me()?.workspace?.id || null"
            (workspaceChange)="switchWorkspace($event)"
          ></erp-brand-logo>
          <button
            type="button"
            class="sidebar-toggle-btn"
            [class.collapsed]="collapsed()"
            (click)="toggleSidebar()"
            [attr.aria-label]="collapsed() ? ('shell.expandSidebar' | translate: 'Expand sidebar') : ('shell.collapseSidebar' | translate: 'Collapse sidebar')"
            [attr.title]="collapsed() ? ('shell.expandSidebar' | translate: 'Expand sidebar') : ('shell.collapseSidebar' | translate: 'Collapse sidebar')"
            data-testid="nav-toggle">
            <span nz-icon [nzType]="toggleIcon()"></span>
          </button>
        </div>
        <div class="sidebar-search-container" [class.collapsed]="collapsed()" data-testid="sidebar-search-box">
          @if (!collapsed()) {
            <div class="sidebar-search-input-wrap">
              <span nz-icon nzType="search" class="sidebar-search-icon"></span>
              <input
                type="text"
                class="sidebar-search-input"
                [placeholder]="'shell.searchMenu' | translate: 'Search menu...'"
                [value]="menuSearch()"
                (input)="onSearchInput($event)"
                [attr.aria-label]="'shell.searchMenu' | translate: 'Search menu...'"
                data-testid="sidebar-search-input"
              />
              @if (menuSearch()) {
                <button type="button" class="sidebar-search-clear" (click)="clearSearch()" aria-label="Clear">
                  <span nz-icon nzType="close-circle" nzTheme="fill"></span>
                </button>
              }
            </div>
          } @else {
            <button
              type="button"
              class="sidebar-search-collapsed-btn"
              (click)="expandAndSearch()"
              nz-tooltip
              [nzTooltipTitle]="'shell.searchMenu' | translate: 'Search menu...'"
              [nzTooltipPlacement]="i18n.dir() === 'rtl' ? 'left' : 'right'"
              data-testid="sidebar-search-collapsed-btn">
              <span nz-icon nzType="search"></span>
            </button>
          }
        </div>
        <div class="nav-scroll-area">
          @for (g of menu(); track g.id) {
            <div class="group-title" [class.collapsed]="collapsed()">{{ i18n.t('menu.group.' + g.id, g.label) }}</div>
            <ul nz-menu nzMode="inline" [nzInlineCollapsed]="collapsed()" [nzInlineIndent]="0">
              @for (i of g.items; track i.id) {
                <li nz-menu-item nz-tooltip [nzTooltipTitle]="collapsed() ? i.label : null" [nzTooltipPlacement]="i18n.dir() === 'rtl' ? 'left' : 'right'"
                    [routerLink]="linkPath(i.route)" [queryParams]="linkQuery(i.route)" routerLinkActive="ant-menu-item-selected" [routerLinkActiveOptions]="{ exact: i.route === '/' }"
                    [attr.data-testid]="'nav-' + i.id">
                  <div class="nav-item-inner">
                    <span nz-icon [nzType]="icon(i.icon)" class="nav-icon"></span>
                    <span class="nav-label" [class.collapsed]="collapsed()">{{ i18n.t('menu.' + i.id, i.label) }}</span>
                  </div>
                </li>
              }
            </ul>
          }
          @if (menuSearch().trim() && menu().length === 0) {
            <div class="sidebar-no-results" data-testid="sidebar-no-results">
              <span nz-icon nzType="frown" class="no-results-icon"></span>
              <div class="no-results-text">{{ 'shell.noMenuItems' | translate: 'No menu items found' }}</div>
              <button type="button" class="no-results-clear" (click)="clearSearch()">
                {{ 'shell.clearFilter' | translate: 'Clear search' }}
              </button>
            </div>
          }
        </div>
      </nz-sider>
      <nz-layout class="main-layout">
        <nz-header class="header">
          <div class="topbar-title-wrap" data-testid="topbar-title-wrap">
            @if (pageTitle.module(); as mod) {
              <span class="topbar-module-badge">
                <span class="badge-dot"></span>
                <span>{{ mod }}</span>
              </span>
              <span class="topbar-breadcrumb-slash">/</span>
            }
            <h1 class="topbar-title" data-testid="topbar-title">{{ pageTitle.title() }}</h1>
            @if (pageTitle.badge(); as b) {
              <nz-tag [nzColor]="b.color ?? 'default'" class="topbar-badge">{{ b.text }}</nz-tag>
            }
            @if (pageTitle.subtitle(); as sub) {
              <span class="topbar-subtitle" nz-tooltip [nzTooltipTitle]="sub">{{ sub }}</span>
            }
          </div>
          <span class="spacer"></span>
          <erp-global-search />

          <button type="button" class="topbar-quick-create-btn" nz-dropdown [nzDropdownMenu]="quickCreateMenu" nzPlacement="bottomRight"
                  [attr.aria-label]="'shell.quickActions' | translate: 'Quick Create (+)'" data-testid="topbar-quick-create">
            <span nz-icon nzType="plus" class="quick-create-icon"></span>
            <span class="quick-create-label">{{ 'shell.quickCreate' | translate: 'New' }}</span>
            <span nz-icon nzType="down" class="quick-create-caret"></span>
          </button>

          <nz-dropdown-menu #quickCreateMenu="nzDropdownMenu">
            <div class="quick-create-dropdown-panel" data-testid="quick-create-menu">
              <div class="quick-create-header">
                <span nz-icon nzType="thunderbolt" class="quick-create-header-icon"></span>
                <span>{{ 'shell.quickActions' | translate: 'Quick Create' }}</span>
              </div>
              <div class="quick-create-grid">
                <!-- Sales Group -->
                <div class="quick-create-col">
                  <div class="quick-create-col-title">{{ 'menu.group.sales' | translate: 'Sales' }}</div>
                  <a routerLink="/finance/invoices" class="quick-create-item">
                    <span nz-icon nzType="file-text" class="item-icon green"></span>
                    <span>{{ 'menu.finance.invoices' | translate: 'Invoice' }}</span>
                  </a>
                  <a routerLink="/sales/quotes" class="quick-create-item">
                    <span nz-icon nzType="file-done" class="item-icon green"></span>
                    <span>{{ 'menu.sales.quotes' | translate: 'Quotation' }}</span>
                  </a>
                  <a routerLink="/sales/orders" class="quick-create-item">
                    <span nz-icon nzType="shopping-cart" class="item-icon green"></span>
                    <span>{{ 'menu.sales.orders' | translate: 'Sales Order' }}</span>
                  </a>
                  <a routerLink="/parties" [queryParams]="{ type: 'customer' }" class="quick-create-item">
                    <span nz-icon nzType="user-add" class="item-icon blue"></span>
                    <span>{{ 'menu.sales.customers' | translate: 'Customer' }}</span>
                  </a>
                </div>

                <!-- Purchasing Group -->
                <div class="quick-create-col">
                  <div class="quick-create-col-title">{{ 'menu.group.purchasing' | translate: 'Purchasing' }}</div>
                  <a routerLink="/purchasing/orders" class="quick-create-item">
                    <span nz-icon nzType="shopping" class="item-icon orange"></span>
                    <span>{{ 'menu.purchasing.orders' | translate: 'Purchase Order' }}</span>
                  </a>
                  <a routerLink="/finance/bills" class="quick-create-item">
                    <span nz-icon nzType="audit" class="item-icon orange"></span>
                    <span>{{ 'menu.finance.bills' | translate: 'Vendor Bill' }}</span>
                  </a>
                  <a routerLink="/purchasing/receipts" class="quick-create-item">
                    <span nz-icon nzType="inbox" class="item-icon orange"></span>
                    <span>{{ 'menu.purchasing.receipts' | translate: 'Goods Receipt' }}</span>
                  </a>
                  <a routerLink="/parties" [queryParams]="{ type: 'vendor' }" class="quick-create-item">
                    <span nz-icon nzType="team" class="item-icon blue"></span>
                    <span>{{ 'menu.purchases.vendors' | translate: 'Vendor' }}</span>
                  </a>
                </div>

                <!-- Finance Group -->
                <div class="quick-create-col">
                  <div class="quick-create-col-title">{{ 'menu.group.finance' | translate: 'Finance' }}</div>
                  <a routerLink="/finance/journals" class="quick-create-item">
                    <span nz-icon nzType="book" class="item-icon purple"></span>
                    <span>{{ 'menu.finance.journals' | translate: 'Journal Entry' }}</span>
                  </a>
                  <a routerLink="/finance/receipts" class="quick-create-item">
                    <span nz-icon nzType="dollar-circle" class="item-icon green"></span>
                    <span>{{ 'menu.finance.receipts' | translate: 'Receive Money' }}</span>
                  </a>
                  <a routerLink="/finance/payments" class="quick-create-item">
                    <span nz-icon nzType="transaction" class="item-icon red"></span>
                    <span>{{ 'menu.finance.payments' | translate: 'Make Payment' }}</span>
                  </a>
                  <a routerLink="/finance/accounts" class="quick-create-item">
                    <span nz-icon nzType="apartment" class="item-icon purple"></span>
                    <span>{{ 'menu.finance.accounts' | translate: 'Account' }}</span>
                  </a>
                </div>

                <!-- Operations & Retail Group -->
                <div class="quick-create-col">
                  <div class="quick-create-col-title">{{ 'menu.group.pos' | translate: 'Operations' }}</div>
                  <a routerLink="/pos/terminal" class="quick-create-item">
                    <span nz-icon nzType="calculator" class="item-icon teal"></span>
                    <span>{{ 'menu.pos.terminal' | translate: 'POS Terminal' }}</span>
                  </a>
                  <a routerLink="/catalog/items" class="quick-create-item">
                    <span nz-icon nzType="barcode" class="item-icon teal"></span>
                    <span>{{ 'menu.core.items' | translate: 'Item / Product' }}</span>
                  </a>
                  <a routerLink="/manufacturing/workorders" class="quick-create-item">
                    <span nz-icon nzType="tool" class="item-icon slate"></span>
                    <span>{{ 'menu.manufacturing.workorders' | translate: 'Work Order' }}</span>
                  </a>
                  <a routerLink="/real-estate/leases" class="quick-create-item">
                    <span nz-icon nzType="home" class="item-icon slate"></span>
                    <span>{{ 'menu.realestate.leases' | translate: 'Lease Contract' }}</span>
                  </a>
                </div>
              </div>
            </div>
          </nz-dropdown-menu>

          @if (session.me(); as me) {
            @if (me.legalEntities.length > 1 || me.allLegalEntities) {
              <div class="header-control-box" data-testid="header-entity-box">
                <span nz-icon nzType="bank" class="control-box-icon"></span>
                <nz-select
                  class="header-entity-select"
                  [ngModel]="session.currentEntityId()"
                  (ngModelChange)="selectEntity($event)"
                  [nzPlaceHolder]="'shell.allEntities' | translate: 'All legal entities'"
                  [nzDropdownMatchSelectWidth]="false"
                  data-testid="entity-switcher">
                  @if (me.allLegalEntities) {
                    <nz-option [nzValue]="null" [nzLabel]="'shell.allEntities' | translate: 'All legal entities'"></nz-option>
                  }
                  @for (e of me.legalEntities; track e.id) {
                    <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option>
                  }
                </nz-select>
              </div>
            }

          }

          <div class="topbar-divider"></div>

          <div class="topbar-actions-group">
            <button type="button" class="topbar-action-btn bell-btn" aria-label="Notifications"
                    nz-dropdown [nzDropdownMenu]="notificationsMenu" nzTrigger="click" nzPlacement="bottomRight"
                    [nzClickHide]="false"
                    [nzVisible]="notificationsOpen()" (nzVisibleChange)="notificationsOpen.set($event)"
                    data-testid="notifications-bell">
              <span nz-icon nzType="bell" class="action-btn-icon"></span>
              @if (unread().unread > 0) {
                <span class="bell-badge-count">{{ unread().unread > 99 ? '99+' : unread().unread }}</span>
              }
            </button>

            <nz-dropdown-menu #notificationsMenu="nzDropdownMenu">
              <erp-notification-drawer [open]="notificationsOpen()" (closed)="notificationsOpen.set(false)" (changed)="bump()" />
            </nz-dropdown-menu>

            <button type="button" class="topbar-action-btn lang-btn" (click)="i18n.toggle()" aria-label="Language" nz-tooltip [nzTooltipTitle]="i18n.lang() === 'ar' ? 'Switch to English' : 'التحويل إلى العربية'" data-testid="lang-toggle">
              <span class="lang-text">{{ i18n.lang() === 'ar' ? 'EN' : 'العربية' }}</span>
            </button>
            <button type="button" class="topbar-action-btn" (click)="theme.toggleTheme()" aria-label="Toggle dark mode" nz-tooltip [nzTooltipTitle]="theme.resolvedTheme() === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'" data-testid="theme-toggle">
              <span nz-icon nzType="bulb" class="action-btn-icon" [style.color]="theme.resolvedTheme() === 'dark' ? '#fbbf24' : 'inherit'"></span>
            </button>
            <button type="button" class="topbar-avatar-btn" nz-dropdown [nzDropdownMenu]="userMenu" data-testid="user-menu" [attr.aria-label]="session.me()?.user?.fullName || 'User menu'">
              <div class="user-avatar-ring">
                <nz-avatar nzSize="small" [nzText]="initials()" class="header-avatar"></nz-avatar>
              </div>
              <span nz-icon nzType="down" class="avatar-caret"></span>
            </button>
          </div>

          <nz-dropdown-menu #userMenu="nzDropdownMenu">
            <ul nz-menu>
              <li nz-menu-item nzDisabled>{{ session.me()?.user?.email }}</li>
              <li nz-menu-item routerLink="/account/security" data-testid="account-security"><span nz-icon nzType="lock"></span> {{ 'shell.accountSecurity' | translate: 'Account security' }}</li>
              <li nz-menu-divider></li>
              <li nz-menu-item (click)="auth.logout()" data-testid="sign-out"><span nz-icon nzType="logout"></span> {{ 'shell.signOut' | translate: 'Sign out' }}</li>
            </ul>
          </nz-dropdown-menu>
        </nz-header>
        <nz-content class="content"><router-outlet /></nz-content>
      </nz-layout>
    </nz-layout>`,
})
export class ShellComponent {
  readonly pageTitle = inject(PageTitleService);
  readonly session = inject(SessionStore);
  readonly i18n = inject(I18nService);
  readonly auth = inject(AuthService);
  readonly theme = inject(TenantThemeService);
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly router = inject(Router);

  readonly collapsed = signal(false);
  readonly notificationsOpen = signal(false);
  readonly refresh = signal(0);
  readonly workspaceName = computed(() => this.session.me()?.workspace.name ?? 'ERP');
  readonly initials = computed(() => (this.session.me()?.user.fullName ?? '?').split(' ').map((p) => p[0]).slice(0, 2).join('').toUpperCase());
  readonly toggleIcon = computed(() => this.i18n.dir() === 'rtl' ? (this.collapsed() ? 'double-left' : 'double-right') : (this.collapsed() ? 'double-right' : 'double-left'));

  toggleSidebar(): void {
    this.collapsed.set(!this.collapsed());
    if (typeof window !== 'undefined') {
      window.dispatchEvent(new Event('resize'));
      setTimeout(() => window.dispatchEvent(new Event('resize')), 150);
      setTimeout(() => window.dispatchEvent(new Event('resize')), 320);
    }
  }

  readonly menuSearch = signal('');

  onSearchInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.menuSearch.set(target.value);
  }

  clearSearch(): void {
    this.menuSearch.set('');
  }

  expandAndSearch(): void {
    this.collapsed.set(false);
    setTimeout(() => {
      const input = document.querySelector('[data-testid="sidebar-search-input"]') as HTMLInputElement;
      input?.focus();
    }, 120);
  }

  // Menu reloads whenever the session changes (workspace switch, module enabled/disabled).
  readonly rawMenu = toSignal(toObservable(this.session.me).pipe(
    switchMap((me) => (me ? this.http.get<MenuGroup[]>(`${this.base}/navigation/menu`).pipe(catchError(() => of([]))) : of([]))),
  ), { initialValue: [] as MenuGroup[] });

  // Filtered menu based on menuSearch signal
  readonly menu = computed(() => {
    const term = this.menuSearch().trim().toLowerCase();
    const groups = this.rawMenu();
    if (!term) return groups;

    return groups.map((g) => {
      const groupLabel = (g.label || '').toLowerCase();
      const groupTranslated = (this.i18n.t('menu.group.' + g.id, g.label) || '').toLowerCase();
      const groupMatches = groupLabel.includes(term) || groupTranslated.includes(term);

      const filteredItems = g.items.filter((item) => {
        if (groupMatches) return true;
        const label = (item.label || '').toLowerCase();
        const translated = (this.i18n.t('menu.' + item.id, item.label) || '').toLowerCase();
        const route = (item.route || '').toLowerCase();
        return label.includes(term) || translated.includes(term) || route.includes(term);
      });

      return { ...g, items: filteredItems };
    }).filter((g) => g.items.length > 0);
  });

  // Unread count: on session change, after the drawer changes something, and every minute.
  readonly unread = toSignal(combineLatest([toObservable(this.session.me), toObservable(this.refresh), interval(60_000).pipe(startWith(0))]).pipe(
    map(([me]) => me),
    switchMap((me) => (me ? this.http.get<{ unread: number }>(`${this.base}/notifications`).pipe(catchError(() => of({ unread: 0 }))) : of({ unread: 0 }))),
  ), { initialValue: { unread: 0 } }) as () => { unread: number };

  /** Menu routes may carry a query string (e.g. /parties?type=customer); routerLink would encode the "?" and miss the route. */
  linkPath(route: string): string { return (route ?? '').split('?')[0] || '/'; }
  linkQuery(route: string): Record<string, string> | null {
    const q = (route ?? '').split('?')[1];
    if (!q) return null;
    const out: Record<string, string> = {};
    new URLSearchParams(q).forEach((v, k) => (out[k] = v));
    return out;
  }

  bump(): void { this.refresh.update((n) => n + 1); }

  icon(name?: string): string {
    return name && KNOWN_ICONS.has(name) ? name : 'appstore';
  }

  selectEntity(id: string | null): void {
    this.session.setEntity(id);
    // Reload the current screen so lists re-query in the new scope.
    const url = this.router.url;
    void this.router.navigateByUrl('/', { skipLocationChange: true }).then(() => this.router.navigateByUrl(url));
  }

  switchWorkspace(id: string): void {
    this.auth.switchWorkspace(id).subscribe(() => void this.router.navigateByUrl('/'));
  }
}
