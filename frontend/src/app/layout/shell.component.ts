import { WorkspaceAssistantComponent, WorkspaceAssistantState } from './workspace-assistant.component';
import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition, OverlayModule } from '@angular/cdk/overlay';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, ElementRef, Injector, afterNextRender, afterRenderEffect, computed, inject, linkedSignal, signal, viewChild, DestroyRef } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
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
import { catchError, combineLatest, filter, interval, map, of, startWith, switchMap } from 'rxjs';
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
import { SubscriptionBannerComponent } from './subscription-banner.component';
import { WorkspaceBlockedComponent } from './workspace-blocked.component';
import { blockedPageApplies } from '../core/format/subscription';
import { KeyboardClickDirective } from '../shared/keyboard-click.directive';
import { NavGroup, currentGroupId, groupIcon, nextFocusIndex, resolveOpenGroup, scrollDeltaToReveal } from './sidebar-nav';

/** The category whose menu is showing beside the collapsed sidebar. A pinned flyout (click or keyboard) ignores the pointer leaving. */
interface NavFlyout { id: string; origin: CdkOverlayOrigin; pinned: boolean; }

/** App shell: white sidebar with the server-driven menu, header with workspace/entity switchers, notifications, theme and user menu. */
@Component({
  selector: 'erp-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [WorkspaceAssistantComponent, KeyboardClickDirective, Dir, OverlayModule, TranslatePipe, RouterOutlet, RouterLink, RouterLinkActive, FormsModule, NzLayoutModule, NzMenuModule, NzIconModule, NzButtonModule,
    NzDropDownModule, NzAvatarModule, NzBadgeModule, NzTagModule, NzSelectModule, NzInputModule, NzToolTipModule, GlobalSearchComponent, NotificationDrawerComponent, BrandLogoComponent, SubscriptionBannerComponent, WorkspaceBlockedComponent],
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
    .sider.as-drawer {
      position: fixed !important;
      inset-block: 0;
      inset-inline-start: 0;
      z-index: 1000;
      transform: translateX(calc(-100% * var(--erp-direction-sign, 1)));
      transition: transform 0.25s cubic-bezier(0.4, 0, 0.2, 1);
    }
    .sider.as-drawer.is-open {
      transform: none;
      box-shadow: var(--erp-shadow-3);
    }
    /* phone top bar */
    @media (max-width: 767px) {
      .header { padding: 0 var(--erp-space-3, 12px); gap: var(--erp-space-1, 4px); }
      .header-control-box, .topbar-divider { display: none !important; }
      .topbar-title-wrap { flex: 1 1 auto; min-width: 0; }
    }
    .nav-backdrop {
      position: fixed;
      inset: 0;
      z-index: 999;
      background: var(--erp-color-bg-overlay);
    }
    @media (min-width: 1200px) { .main-layout.with-assistant { margin-inline-end: 420px; } }
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
      scrollbar-gutter: stable; /* a page keeps its width when its data arrives and it starts to scroll */
      overflow-x: hidden;
      background: var(--erp-color-bg-canvas);
      flex: 1;
      min-height: 0;
      min-width: 0;
    }
    .group-title {
      display: flex;
      align-items: center;
      gap: 9px;
      width: 100%;
      border: 0;
      background: none;
      font: inherit;
      text-align: start;
      cursor: pointer;
      padding: 8px 18px;
      font-size: 11px;
      font-weight: 600;
      color: var(--erp-color-text-tertiary);
      text-transform: uppercase;
      letter-spacing: .06em;
      white-space: nowrap;
      height: 36px;
      line-height: 20px;
    }
    .group-title:hover, .group-title[aria-expanded='true'] { color: var(--erp-color-text-primary); }
    .group-title:focus-visible { outline: 2px solid var(--erp-color-focus-ring); outline-offset: -2px; border-radius: 4px; }
    .group-icon { font-size: 15px; flex-shrink: 0; color: var(--erp-color-nav-icon); }
    .group-title[aria-expanded='true'] .group-icon { color: var(--erp-color-primary-text); }
    .group-label { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; }
    .group-chevron { font-size: 10px; flex-shrink: 0; transition: transform 0.2s cubic-bezier(0.4, 0, 0.2, 1); }
    .group-title[aria-expanded='true'] .group-chevron { transform: rotate(90deg); }
    :host-context([dir='rtl']) .group-title[aria-expanded='false'] .group-chevron { transform: rotate(180deg); }

    /* Collapsed sidebar: one button per category. A scrollbar would push the icons off centre in so narrow a column. */
    .nav-scroll-area.is-rail { scrollbar-width: none; }
    .nav-rail {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      padding-top: 4px;
    }
    .rail-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 44px;
      height: 44px;
      flex-shrink: 0;
      padding: 0;
      border: 1px solid transparent;
      border-radius: 12px;
      background: transparent;
      color: var(--erp-color-nav-icon);
      font-size: 20px;
      cursor: pointer;
      transition: background-color 0.2s ease, color 0.2s ease, border-color 0.2s ease;
    }
    .rail-btn:hover, .rail-btn.is-open {
      background: rgba(16, 149, 93, 0.08);
      color: var(--erp-color-primary-text);
    }
    .rail-btn.is-current {
      background: var(--erp-color-primary-subtle);
      border-color: var(--erp-color-primary-border);
      color: var(--erp-color-primary-text);
    }
    .rail-btn:focus-visible { outline: 2px solid var(--erp-color-focus-ring); outline-offset: 1px; }

    /* Category menu beside the rail. The wrapper's padding spans the gap, so the pointer can cross it. */
    .nav-flyout-wrap {
      display: flex;
      max-height: calc(100vh - 16px);
      padding-inline-start: 16px;
    }
    .nav-flyout {
      display: flex;
      flex-direction: column;
      min-width: 220px;
      max-width: 300px;
      min-height: 0;
      overflow: hidden;
      border: 1px solid var(--erp-color-nav-border);
      border-radius: 10px;
      background: var(--erp-color-nav-bg);
      box-shadow: var(--erp-shadow-3);
    }
    .nav-flyout-title {
      display: flex;
      align-items: center;
      gap: 9px;
      flex-shrink: 0;
      padding: 11px 14px;
      border-bottom: 1px solid var(--erp-color-border-subtle);
      font-size: 13px;
      font-weight: 600;
      color: var(--erp-color-text-primary);
    }
    .nav-flyout-title .anticon { font-size: 16px; color: var(--erp-color-primary-text); }
    .nav-flyout-list {
      flex: 1 1 auto;
      min-height: 0;
      margin: 0;
      padding: 6px;
      overflow-y: auto;
      list-style: none;
    }
    .nav-flyout-link {
      display: flex;
      align-items: center;
      gap: 10px;
      min-height: 38px;
      padding: 6px 10px;
      border: 1px solid transparent;
      border-radius: 8px;
      color: var(--erp-color-nav-text);
      font-size: 14px;
      line-height: 1.3;
      text-decoration: none;
      transition: background-color 0.15s ease, color 0.15s ease;
    }
    .nav-flyout-link .anticon { font-size: 16px; flex-shrink: 0; color: var(--erp-color-nav-icon); }
    .nav-flyout-link:hover { background: rgba(16, 149, 93, 0.08); color: var(--erp-color-primary-text); }
    .nav-flyout-link:hover .anticon { color: var(--erp-color-primary-text); }
    .nav-flyout-link:focus-visible { outline: 2px solid var(--erp-color-focus-ring); outline-offset: -2px; }
    .nav-flyout-link.is-active {
      background: var(--erp-color-primary-subtle);
      border-color: var(--erp-color-primary-border);
      color: var(--erp-color-primary-text);
      font-weight: 600;
    }
    .nav-flyout-link.is-active .anticon { color: var(--erp-color-primary-text); }

    /* Polished, Fluidly Animated Menu Items */
    .sider ::ng-deep {
      .ant-menu {
        background: transparent !important;
        border-inline-end: none !important;
        width: 100% !important;
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
          background: var(--erp-color-primary-subtle) !important;
          color: var(--erp-color-primary-text) !important;
          border: 1px solid var(--erp-color-primary-border) !important;
          font-weight: 600 !important;

          .nav-icon {
            color: var(--erp-color-primary-text) !important;
          }

          .nav-label {
            color: #10955d !important;
          }
        }
      }
    }
  `],
  template: `
    @if (blocked()) {
      <erp-workspace-blocked />
    } @else {
    <nz-layout class="root" [dir]="i18n.dir()">
      <nz-sider class="sider" [class.as-drawer]="narrow()" [class.is-open]="drawerOpen()" (keydown.escape)="drawerOpen.set(false)" nzCollapsible [nzCollapsed]="collapsed()" [nzTrigger]="null" nzWidth="248px" [nzCollapsedWidth]="64" nzTheme="light" data-testid="sidebar">
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
              [attr.aria-label]="'shell.searchMenu' | translate: 'Search menu...'"
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
        <div class="nav-scroll-area" [class.is-rail]="collapsed()" #navScroll (scroll)="closeFlyout()">
          @if (collapsed()) {
            <div class="nav-rail" data-testid="nav-rail">
              @for (g of rawMenu(); track g.id) {
                <button type="button" class="rail-btn" cdkOverlayOrigin #railOrigin="cdkOverlayOrigin"
                        [class.is-current]="currentGroup() === g.id" [class.is-open]="flyout()?.id === g.id"
                        [attr.aria-label]="i18n.t('menu.group.' + g.id, g.label)"
                        [attr.aria-expanded]="flyout()?.id === g.id"
                        [attr.aria-controls]="flyout()?.id === g.id ? 'nav-flyout' : null"
                        [attr.aria-current]="currentGroup() === g.id ? 'true' : null"
                        (click)="onRailClick(g, railOrigin, $event)" (mouseenter)="onRailEnter(g, railOrigin)" (mouseleave)="scheduleFlyoutClose()"
                        [attr.data-nav-group]="g.id" [attr.data-testid]="'nav-rail-' + g.id">
                  <span nz-icon [nzType]="groupIcon(g.id)" aria-hidden="true"></span>
                </button>
              }
            </div>
          } @else {
            @for (g of menu(); track g.id) {
              <button type="button" class="group-title" [attr.aria-expanded]="isGroupOpen(g)" (click)="toggleGroup(g)" [attr.data-nav-group]="g.id" [attr.data-testid]="'nav-group-' + g.id">
                <span class="group-icon" nz-icon [nzType]="groupIcon(g.id)" aria-hidden="true"></span>
                <span class="group-label">{{ i18n.t('menu.group.' + g.id, g.label) }}</span>
                <span class="group-chevron" nz-icon nzType="right" aria-hidden="true"></span>
              </button>
              <ul nz-menu nzMode="inline" [nzInlineIndent]="0" [hidden]="!isGroupOpen(g)">
                @for (i of g.items; track i.id) {
                  <li nz-menu-item
                      [routerLink]="linkPath(i.route)" [queryParams]="linkQuery(i.route)" routerLinkActive="ant-menu-item-selected" [routerLinkActiveOptions]="{ exact: i.route === '/' }"
                      [attr.data-testid]="'nav-' + i.id">
                    <div class="nav-item-inner">
                      <span nz-icon [nzType]="icon(i.icon)" class="nav-icon"></span>
                      <span class="nav-label">{{ i18n.t('menu.' + i.id, i.label) }}</span>
                    </div>
                  </li>
                }
              </ul>
            }
          }
          @if (!collapsed() && menuSearch().trim() && menu().length === 0) {
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
      @if (flyoutGroup(); as g) {
        <ng-template cdkConnectedOverlay [cdkConnectedOverlayOpen]="true" [cdkConnectedOverlayOrigin]="flyout()!.origin"
                     [cdkConnectedOverlayPositions]="flyoutPositions" [cdkConnectedOverlayPush]="true" [cdkConnectedOverlayViewportMargin]="8"
                     [cdkConnectedOverlayDisableClose]="true"
                     (overlayKeydown)="onFlyoutOverlayKeydown($event)" (overlayOutsideClick)="onFlyoutOutsideClick($event)">
          <div class="nav-flyout-wrap" (mouseenter)="keepFlyout()" (mouseleave)="scheduleFlyoutClose()">
            <nav class="nav-flyout" id="nav-flyout" #flyoutPanel aria-labelledby="nav-flyout-title" (keydown)="onFlyoutKeydown($event)" data-testid="nav-flyout">
              <div class="nav-flyout-title" id="nav-flyout-title">
                <span nz-icon [nzType]="groupIcon(g.id)" aria-hidden="true"></span>
                <span>{{ i18n.t('menu.group.' + g.id, g.label) }}</span>
              </div>
              <ul class="nav-flyout-list">
                @for (i of g.items; track i.id) {
                  <li>
                    <a class="nav-flyout-link" [routerLink]="linkPath(i.route)" [queryParams]="linkQuery(i.route)"
                       routerLinkActive="is-active" ariaCurrentWhenActive="page" [routerLinkActiveOptions]="{ exact: i.route === '/' }"
                       (click)="closeFlyout()" [attr.data-testid]="'nav-flyout-' + i.id">
                      <span nz-icon [nzType]="icon(i.icon)" aria-hidden="true"></span>
                      <span>{{ i18n.t('menu.' + i.id, i.label) }}</span>
                    </a>
                  </li>
                }
              </ul>
            </nav>
          </div>
        </ng-template>
      }
      @if (narrow() && drawerOpen()) { <div class="nav-backdrop" erpKeyboardClick aria-label="Close menu" (click)="drawerOpen.set(false)" data-testid="nav-backdrop"></div> }
      <nz-layout class="main-layout" [class.with-assistant]="assistant.available() && assistant.open()">
        <nz-header class="header">
          @if (narrow()) {
            <button type="button" class="topbar-action-btn" (click)="drawerOpen.set(true)" [attr.aria-expanded]="drawerOpen()" [attr.aria-label]="'shell.openMenu' | translate: 'Open menu'" data-testid="nav-open">
              <span nz-icon nzType="menu-unfold" class="action-btn-icon"></span>
            </button>
          }
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
              @if (unreadShown() > 0) {
                <span class="bell-badge-count">{{ unreadShown() > 99 ? '99+' : unreadShown() }}</span>
              }
            </button>

            <nz-dropdown-menu #notificationsMenu="nzDropdownMenu">
              <erp-notification-drawer [open]="notificationsOpen()" (closed)="notificationsOpen.set(false)" (changed)="bump()" (unreadShift)="shiftUnread($event)" />
            </nz-dropdown-menu>

            @if (assistant.available()) {
              <button type="button" class="topbar-action-btn" (click)="assistant.toggle()" [attr.aria-label]="'shell.assistant' | translate: 'Assistant'" [attr.aria-pressed]="assistant.open()" nz-tooltip [nzTooltipTitle]="'shell.assistant' | translate: 'Assistant'" data-testid="workspace-assistant-toggle">
                <img src="highersquare-assistant.svg" alt="" width="26" height="26" style="display:block" />
              </button>
            }
            <button type="button" class="topbar-avatar-btn" nz-dropdown nzTrigger="click" [nzDropdownMenu]="userMenu" nzPlacement="bottomRight" data-testid="user-menu" [attr.aria-label]="session.me()?.user?.fullName || 'User menu'">
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
              @if (session.me()?.platformAdmin) {
                <li nz-menu-item routerLink="/platform" data-testid="platform-console"><span nz-icon nzType="dashboard"></span> {{ 'shell.platformConsole' | translate: 'Platform console' }}</li>
              }
              <li nz-menu-divider></li>
              <li nz-menu-item (click)="i18n.toggle()" data-no-i18n data-testid="lang-toggle"><span nz-icon nzType="global"></span> {{ i18n.lang() === 'ar' ? 'English' : 'العربية' }}</li>
              <li nz-menu-item (click)="theme.toggleTheme()" data-testid="theme-toggle"><span nz-icon nzType="bulb"></span> {{ theme.resolvedTheme() === 'dark' ? ('shell.lightMode' | translate: 'Light mode') : ('shell.darkMode' | translate: 'Dark mode') }}</li>
              <li nz-menu-divider></li>
              <li nz-menu-item (click)="auth.logout()" data-testid="sign-out"><span nz-icon nzType="logout"></span> {{ 'shell.signOut' | translate: 'Sign out' }}</li>
            </ul>
          </nz-dropdown-menu>
        </nz-header>
        <erp-subscription-banner />
        <nz-content class="content"><router-outlet /></nz-content>
      </nz-layout>
    </nz-layout>
    <erp-workspace-assistant />
    }`,
})
export class ShellComponent {
  readonly assistant = inject(WorkspaceAssistantState);
  readonly pageTitle = inject(PageTitleService);
  readonly session = inject(SessionStore);
  readonly i18n = inject(I18nService);
  readonly auth = inject(AuthService);
  readonly theme = inject(TenantThemeService);
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);

  readonly collapsed = signal(false);
  /** Phone width: the sidebar is a drawer over the page rather than a column beside it. */
  readonly narrow = signal(false);
  readonly drawerOpen = signal(false);

  constructor() {
    afterRenderEffect(() => this.revealActiveNav());
    if (typeof window === 'undefined' || !window.matchMedia) return;
    const phone = window.matchMedia('(max-width: 767px)');
    const apply = () => {
      this.narrow.set(phone.matches);
      if (phone.matches) this.collapsed.set(false);
      else this.drawerOpen.set(false);
    };
    apply();
    // Tablets start with the icon-only sidebar, to leave room for tables.
    if (!phone.matches && window.matchMedia('(max-width: 1100px)').matches) this.collapsed.set(true);
    phone.addEventListener('change', apply);
    inject(DestroyRef).onDestroy(() => phone.removeEventListener('change', apply));
    // Choosing a page closes the drawer and the flyout.
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed()).subscribe(() => {
      this.drawerOpen.set(false);
      this.closeFlyout();
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.flyoutTimer));
  }
  readonly notificationsOpen = signal(false);
  readonly refresh = signal(0);
  readonly workspaceName = computed(() => this.session.me()?.workspace.name ?? 'ERP');
  readonly initials = computed(() => (this.session.me()?.user.fullName ?? '?').split(' ').map((p) => p[0]).slice(0, 2).join('').toUpperCase());
  readonly toggleIcon = computed(() => this.i18n.dir() === 'rtl' ? (this.collapsed() ? 'double-left' : 'double-right') : (this.collapsed() ? 'double-right' : 'double-left'));

  toggleSidebar(): void {
    if (this.narrow()) { this.drawerOpen.set(false); return; }
    this.collapsed.set(!this.collapsed());
    if (typeof window !== 'undefined') {
      window.dispatchEvent(new Event('resize'));
      setTimeout(() => window.dispatchEvent(new Event('resize')), 150);
      setTimeout(() => window.dispatchEvent(new Event('resize')), 320);
    }
  }

  readonly menuSearch = signal('');

  // ---------- sidebar groups: one open at a time, the choice is remembered, the group of the current page always opens ----------
  private readonly openGroupKey = 'erp.nav.openGroup';
  private readonly currentUrl = toSignal(this.router.events.pipe(
    filter((e): e is NavigationEnd => e instanceof NavigationEnd),
    map((e) => e.urlAfterRedirects),
    startWith(this.router.url),
  ), { initialValue: '/' });
  private readonly currentPath = computed(() => this.currentUrl().split('?')[0]);

  /** A suspended or cancelled workspace sees the blocked page everywhere but its billing page. */
  readonly blocked = computed(() => blockedPageApplies(this.session.me()?.subscription?.access, this.currentPath()));

  readonly groupIcon = groupIcon;
  /** The group holding the current page, marked on the icon rail. */
  readonly currentGroup = computed(() => currentGroupId(this.rawMenu(), this.currentUrl()));
  /** Worked out again on every navigation and menu load; between those, toggleGroup sets it. */
  private readonly openGroupId = linkedSignal<{ url: string; groups: NavGroup[] }, string | null | undefined>({
    source: () => ({ url: this.currentUrl(), groups: this.rawMenu() }),
    computation: ({ url, groups }, previous) => resolveOpenGroup(groups, url, previous ? previous.value : this.readOpenGroup()),
  });
  private readonly navScroll = viewChild<ElementRef<HTMLElement>>('navScroll');

  /** Keeps the open group in view (it can land past the end of the sidebar as the one above closes), or the current page's category on the rail. */
  private revealActiveNav(): void {
    const collapsed = this.collapsed();
    const id = collapsed ? this.currentGroup() : this.openGroupId();
    const area = this.navScroll()?.nativeElement;
    this.rawMenu(); // nothing is drawn until the menu has loaded
    if (!id || !area || (!collapsed && this.menuSearch().trim())) return;
    const first = area.querySelector(`[data-nav-group="${CSS.escape(id)}"]`);
    const last = collapsed ? first : first?.nextElementSibling;
    if (!first || !last) return;
    const view = area.getBoundingClientRect();
    area.scrollTop += scrollDeltaToReveal(view.top, view.bottom, first.getBoundingClientRect().top, last.getBoundingClientRect().bottom);
  }

  /** A search shows the items of every matching group. */
  isGroupOpen(g: NavGroup): boolean {
    return !!this.menuSearch().trim() || this.openGroupId() === g.id;
  }

  toggleGroup(g: NavGroup): void {
    if (this.menuSearch().trim()) return;
    const next = this.openGroupId() === g.id ? null : g.id;
    this.openGroupId.set(next);
    try {
      if (next) localStorage.setItem(this.openGroupKey, next);
      else localStorage.removeItem(this.openGroupKey);
    } catch { /* storage unavailable */ }
  }

  private readOpenGroup(): string | undefined {
    try { return localStorage.getItem(this.openGroupKey) ?? undefined; } catch { return undefined; }
  }

  // ---------- icon rail: hovering or clicking a category shows its menu beside the collapsed sidebar ----------
  /** Start and end follow the language, so in Arabic the flyout opens on the left. */
  readonly flyoutPositions: ConnectedPosition[] = [
    { originX: 'end', originY: 'top', overlayX: 'start', overlayY: 'top' },
    { originX: 'end', originY: 'bottom', overlayX: 'start', overlayY: 'bottom' },
  ];
  /** Back to closed whenever the sidebar is collapsed or expanded. */
  readonly flyout = linkedSignal<boolean, NavFlyout | null>({ source: this.collapsed, computation: () => null });
  readonly flyoutGroup = computed(() => {
    const open = this.flyout();
    return open && this.collapsed() ? this.rawMenu().find((g) => g.id === open.id) ?? null : null;
  });
  private readonly flyoutOverlay = viewChild(CdkConnectedOverlay);
  private readonly flyoutPanel = viewChild<ElementRef<HTMLElement>>('flyoutPanel');
  private flyoutTimer?: ReturnType<typeof setTimeout>;

  onRailEnter(g: NavGroup, origin: CdkOverlayOrigin): void {
    this.keepFlyout();
    if (this.flyout()?.id === g.id) return;
    this.flyoutTimer = setTimeout(() => this.showFlyout({ id: g.id, origin, pinned: false }), 100);
  }

  /** The first click pins a menu the pointer opened; a click on a pinned menu closes it. A click from the keyboard moves into the menu. */
  onRailClick(g: NavGroup, origin: CdkOverlayOrigin, event: MouseEvent): void {
    this.keepFlyout();
    const open = this.flyout();
    if (open?.id === g.id && open.pinned) { this.closeFlyout(); return; }
    this.showFlyout({ id: g.id, origin, pinned: true }, event.detail === 0);
  }

  /** Cancels a pending open or close. */
  keepFlyout(): void { clearTimeout(this.flyoutTimer); }

  scheduleFlyoutClose(): void {
    this.keepFlyout();
    const open = this.flyout();
    if (open && !open.pinned) this.flyoutTimer = setTimeout(() => this.closeFlyout(), 200);
  }

  /** Focus inside the menu goes back to its category button, so it is not lost with the menu. */
  closeFlyout(): void {
    this.keepFlyout();
    const open = this.flyout();
    if (!open) return;
    if (this.flyoutPanel()?.nativeElement.contains(document.activeElement)) (open.origin.elementRef.nativeElement as HTMLElement).focus();
    this.flyout.set(null);
  }

  onFlyoutOverlayKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') this.closeFlyout();
  }

  /** A click on another category switches the menu instead of closing it first. */
  onFlyoutOutsideClick(event: MouseEvent): void {
    if (!(event.target as Element | null)?.closest?.('.nav-rail')) this.closeFlyout();
  }

  /** The panel sits at the end of the page, so tabbing past either end returns to the category button. */
  onFlyoutKeydown(event: KeyboardEvent): void {
    const links = Array.from((event.currentTarget as HTMLElement).querySelectorAll<HTMLElement>('a'));
    const at = links.indexOf(document.activeElement as HTMLElement);
    if (event.key === 'Tab') {
      if (event.shiftKey ? at <= 0 : at === links.length - 1) { event.preventDefault(); this.closeFlyout(); }
      return;
    }
    const next = nextFocusIndex(event.key, at, links.length);
    if (next !== null) { event.preventDefault(); links[next].focus(); }
  }

  private showFlyout(next: NavFlyout, moveFocus = false): void {
    this.flyout.set(next);
    afterNextRender(() => {
      this.flyoutOverlay()?.overlayRef?.updatePosition(); // the height changes with the category
      if (!moveFocus) return;
      // routerLinkActive marks the current page's link in a microtask.
      queueMicrotask(() => {
        const panel = this.flyoutPanel()?.nativeElement;
        (panel?.querySelector<HTMLElement>('a.is-active') ?? panel?.querySelector<HTMLElement>('a'))?.focus();
      });
    }, { injector: this.injector });
  }

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
    switchMap((me) => (me ? this.http.get<NavGroup[]>(`${this.base}/navigation/menu`).pipe(catchError(() => of([]))) : of([]))),
  ), { initialValue: [] as NavGroup[] });

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
    // A blocked workspace answers 402 to everything but its billing page, so there is nothing to ask.
    switchMap((me) => (me && me.subscription?.access !== 'blocked' ? this.http.get<{ unread: number }>(`${this.base}/notifications`).pipe(catchError(() => of({ unread: 0 }))) : of({ unread: 0 }))),
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

  /** Marks the drawer shows at once, on top of the count the server last gave. The next count replaces them. */
  private readonly unreadShiftBy = linkedSignal<{ unread: number }, number>({ source: () => this.unread(), computation: () => 0 });
  readonly unreadShown = computed(() => Math.max(0, this.unread().unread + this.unreadShiftBy()));
  shiftUnread(by: number): void { this.unreadShiftBy.update((n) => n + by); }

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
