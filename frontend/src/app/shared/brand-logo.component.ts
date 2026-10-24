import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { I18nService } from '../core/i18n/i18n.service';

@Component({
  selector: 'erp-brand-logo',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, NzDropDownModule, NzMenuModule, NzIconModule, NzToolTipModule],
  styles: [`
    :host {
      display: flex;
      align-items: center;
      width: 100%;
      text-decoration: none;
      user-select: none;
    }

    .brand-container {
      display: inline-flex;
      align-items: center;
      width: 100%;
      overflow: hidden;
    }

    .emblem-wrapper {
      position: relative;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      border-radius: 9px;
      overflow: visible;
      transition: transform 0.2s ease, box-shadow 0.2s ease;
    }

    .brand-container:hover .emblem-wrapper {
      transform: scale(1.04);
    }

    .size-sm .emblem-wrapper { width: 28px; height: 28px; border-radius: 7px; }
    .size-md .emblem-wrapper { width: 36px; height: 36px; border-radius: 9px; }
    .size-lg .emblem-wrapper { width: 48px; height: 48px; border-radius: 12px; }

    .emblem-img {
      width: 100%;
      height: 100%;
      object-fit: contain;
      display: block;
    }

    .collapsed-ws-dot {
      position: absolute;
      bottom: -2px;
      right: -2px;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #10955d;
      border: 2px solid #ffffff;
      box-shadow: 0 0 0 1px rgba(16, 149, 93, 0.3);
    }
    :host-context([dir='rtl']) .collapsed-ws-dot {
      right: auto;
      left: -2px;
    }

    .emblem-wrapper.collapsed-clickable {
      cursor: pointer;
    }

    .brand-text {
      display: flex;
      flex-direction: column;
      justify-content: center;
      line-height: 1.15;
      overflow: hidden;
      white-space: nowrap;
      margin-inline-start: 10px;
      max-width: 240px;
      opacity: 1;
      transform: translateX(0);
      transition: max-width 0.3s cubic-bezier(0.4, 0, 0.2, 1),
                  opacity 0.2s cubic-bezier(0.4, 0, 0.2, 1),
                  margin-inline-start 0.3s cubic-bezier(0.4, 0, 0.2, 1),
                  transform 0.25s cubic-bezier(0.4, 0, 0.2, 1);
    }

    .brand-container.collapsed {
      justify-content: center;
    }

    .brand-container.collapsed .brand-text {
      max-width: 0;
      margin-inline-start: 0;
      opacity: 0;
      transform: translateX(-6px);
      pointer-events: none;
    }

    :host-context([dir='rtl']) .brand-container.collapsed .brand-text {
      transform: translateX(6px);
    }

    .title-row {
      display: flex;
      align-items: baseline;
      gap: 5px;
      direction: ltr;
      unicode-bidi: isolate;
    }

    :host-context([dir='rtl']) .title-row {
      direction: rtl;
    }

    .name-higher {
      font-weight: 700;
      letter-spacing: -0.02em;
      color: #359867;
      font-size: 17px;
    }

    :host-context([dir='rtl']) .name-higher {
      letter-spacing: normal;
      font-family: 'Cairo', 'Segoe UI', system-ui, sans-serif;
    }

    .name-360 {
      font-weight: 800;
      letter-spacing: -0.01em;
      color: #215a67;
      font-size: 17px;
    }

    :host-context([dir='rtl']) .name-360 {
      letter-spacing: normal;
      font-family: inherit;
    }

    .size-sm .name-higher,
    .size-sm .name-360 { font-size: 14px; }

    .size-lg .name-higher,
    .size-lg .name-360 { font-size: 22px; }

    .brand-sub {
      font-size: 8.5px;
      font-weight: 700;
      letter-spacing: 0.14em;
      color: #112338;
      text-transform: uppercase;
      margin-top: 2px;
      text-align: start;
    }

    :host-context([dir='rtl']) .brand-sub {
      letter-spacing: 0.02em;
      text-align: start;
      font-family: 'Cairo', 'Segoe UI', system-ui, sans-serif;
    }

    .size-sm .brand-sub { font-size: 7.5px; letter-spacing: 0.1em; }
    .size-lg .brand-sub { font-size: 10.5px; letter-spacing: 0.16em; }

    /* Interactive Workspace Switcher below Logo */
    .brand-sub-btn {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 1px 5px 1px 4px;
      margin-top: 2px;
      margin-inline-start: -4px;
      border-radius: 4px;
      border: 1px solid transparent;
      background: transparent;
      cursor: pointer;
      text-align: start;
      max-width: 160px;
      transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
      outline: none;
    }
    .brand-sub-btn:hover {
      background: rgba(16, 149, 93, 0.1);
      border-color: rgba(16, 149, 93, 0.25);
    }
    .brand-sub-btn:active {
      transform: scale(0.98);
    }
    .brand-sub-dot {
      width: 5px;
      height: 5px;
      border-radius: 50%;
      background: #10955d;
      flex-shrink: 0;
      box-shadow: 0 0 0 1.5px rgba(16, 149, 93, 0.25);
    }
    .brand-sub-label {
      font-size: 9px;
      font-weight: 700;
      letter-spacing: 0.08em;
      color: #112338;
      text-transform: uppercase;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      line-height: 1.2;
    }
    :host-context([dir='rtl']) .brand-sub-label {
      letter-spacing: normal;
      font-family: 'Cairo', 'Segoe UI', system-ui, sans-serif;
    }
    .brand-sub-arrow {
      font-size: 8px;
      color: #64748b;
      transition: transform 0.2s ease, color 0.2s ease;
      flex-shrink: 0;
    }
    .brand-sub-btn:hover .brand-sub-arrow {
      color: #10955d;
      transform: translateY(1px);
    }

    /* Workspace Dropdown Panel */
    .workspace-dropdown-panel {
      background: #ffffff;
      border-radius: 10px;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      box-shadow: 0 12px 30px rgba(0, 0, 0, 0.14), 0 2px 6px rgba(0, 0, 0, 0.04);
      min-width: 210px;
      padding: 6px;
      margin-top: 4px;
    }
    :host-context([data-theme='dark']) .workspace-dropdown-panel {
      background: #0f172a;
      border-color: #334155;
    }
    .ws-dropdown-header {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 6px 10px 8px;
      font-size: 11px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: #64748b;
      border-bottom: 1px solid var(--erp-color-border-subtle, #f1f5f9);
      margin-bottom: 4px;
    }
    :host-context([dir='rtl']) .ws-dropdown-header {
      letter-spacing: normal;
      font-family: 'Cairo', 'Segoe UI', system-ui, sans-serif;
    }
    .ws-dropdown-menu {
      border: none !important;
      background: transparent !important;
      padding: 0 !important;
    }
    .ws-menu-item {
      border-radius: 6px !important;
      padding: 7px 10px !important;
      height: auto !important;
      line-height: normal !important;
      margin-bottom: 2px !important;
      transition: all 0.15s ease !important;
    }
    .ws-menu-item:hover {
      background: rgba(16, 149, 93, 0.08) !important;
      color: #10955d !important;
    }
    .ws-item-row {
      display: flex;
      align-items: center;
      gap: 8px;
      width: 100%;
    }
    .ws-item-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: #cbd5e1;
      flex-shrink: 0;
    }
    .ws-item-dot.active {
      background: #10955d;
      box-shadow: 0 0 0 2px rgba(16, 149, 93, 0.25);
    }
    .ws-item-name {
      font-size: 13px;
      font-weight: 600;
      color: var(--erp-color-text-primary, #0f172a);
      flex: 1;
    }
    .ws-menu-item:hover .ws-item-name {
      color: #10955d;
    }
    .ws-item-check {
      color: #10955d;
      font-size: 12px;
      margin-inline-start: auto;
    }

    /* Custom dark overrides if host/parent has dark theme */
    :host-context([data-theme='dark']) .name-higher,
    :host-context(.dark) .name-higher {
      color: #4ade80;
    }
    :host-context([data-theme='dark']) .name-360,
    :host-context(.dark) .name-360 {
      color: #38bdf8;
    }
    :host-context([data-theme='dark']) .brand-sub,
    :host-context(.dark) .brand-sub,
    :host-context([data-theme='dark']) .brand-sub-label,
    :host-context(.dark) .brand-sub-label {
      color: #cbd5e1;
    }
    :host-context([data-theme='dark']) .brand-sub-btn:hover,
    :host-context(.dark) .brand-sub-btn:hover {
      background: rgba(52, 211, 153, 0.15);
      border-color: rgba(52, 211, 153, 0.3);
    }
  `],
  template: `
    <div class="brand-container" [class.collapsed]="collapsed()" [class]="'size-' + size()">
      <!-- Brand Emblem Image -->
      @if (workspaces().length > 1 && collapsed()) {
        <div
          class="emblem-wrapper collapsed-clickable"
          nz-dropdown
          [nzDropdownMenu]="wsDropdownCollapsed"
          [nzTrigger]="'click'"
          [nzPlacement]="isArabic() ? 'bottomRight' : 'bottomLeft'"
          nz-tooltip
          [nzTooltipTitle]="subtitleText() + ' (' + (isArabic() ? 'تبديل مساحة العمل' : 'Switch workspace') + ')'"
          [nzTooltipPlacement]="isArabic() ? 'left' : 'right'"
          data-testid="sidebar-workspace-emblem"
        >
          <img src="highersquare-emblem.png" class="emblem-img" [alt]="titleText()" />
          <span class="collapsed-ws-dot"></span>
        </div>

        <nz-dropdown-menu #wsDropdownCollapsed="nzDropdownMenu">
          <div class="workspace-dropdown-panel" data-testid="sidebar-workspace-dropdown-collapsed">
            <div class="ws-dropdown-header">
              <span nz-icon nzType="appstore" class="ws-head-icon"></span>
              <span>{{ isArabic() ? 'مساحات العمل' : 'Workspaces' }}</span>
            </div>
            <ul nz-menu class="ws-dropdown-menu">
              @for (w of workspaces(); track w.id) {
                <li
                  nz-menu-item
                  [nzSelected]="w.id === currentWorkspaceId()"
                  (click)="onSelectWorkspace(w.id)"
                  class="ws-menu-item"
                >
                  <div class="ws-item-row">
                    <span class="ws-item-dot" [class.active]="w.id === currentWorkspaceId()"></span>
                    <span class="ws-item-name">{{ w.name }}</span>
                    @if (w.id === currentWorkspaceId()) {
                      <span nz-icon nzType="check" class="ws-item-check"></span>
                    }
                  </div>
                </li>
              }
            </ul>
          </div>
        </nz-dropdown-menu>
      } @else {
        <div class="emblem-wrapper" [title]="titleText()">
          <img src="highersquare-emblem.png" class="emblem-img" [alt]="titleText()" />
        </div>
      }

      <!-- Brand Typography with Workspace Switcher below the logo -->
      <div class="brand-text" [attr.aria-hidden]="collapsed()">
        <div class="title-row">
          <span class="name-higher">{{ isArabic() ? 'هاير سكوير' : 'HigherSquare' }}</span>
          <span class="name-360">360</span>
        </div>

        @if (workspaces().length > 1) {
          <button
            type="button"
            class="brand-sub-btn"
            nz-dropdown
            [nzDropdownMenu]="wsDropdown"
            [nzTrigger]="'click'"
            [nzPlacement]="isArabic() ? 'bottomRight' : 'bottomLeft'"
            [attr.title]="isArabic() ? 'تبديل مساحة العمل' : 'Switch workspace'"
            data-testid="sidebar-workspace-switcher"
          >
            <span class="brand-sub-dot"></span>
            <span class="brand-sub-label">{{ subtitleText() }}</span>
            <span nz-icon nzType="down" class="brand-sub-arrow"></span>
          </button>

          <nz-dropdown-menu #wsDropdown="nzDropdownMenu">
            <div class="workspace-dropdown-panel" data-testid="sidebar-workspace-dropdown">
              <div class="ws-dropdown-header">
                <span nz-icon nzType="appstore" class="ws-head-icon"></span>
                <span>{{ isArabic() ? 'مساحات العمل' : 'Workspaces' }}</span>
              </div>
              <ul nz-menu class="ws-dropdown-menu">
                @for (w of workspaces(); track w.id) {
                  <li
                    nz-menu-item
                    [nzSelected]="w.id === currentWorkspaceId()"
                    (click)="onSelectWorkspace(w.id)"
                    [attr.data-testid]="'workspace-option-' + (w.code || w.id)"
                    class="ws-menu-item"
                  >
                    <div class="ws-item-row">
                      <span class="ws-item-dot" [class.active]="w.id === currentWorkspaceId()"></span>
                      <span class="ws-item-name">{{ w.name }}</span>
                      @if (w.id === currentWorkspaceId()) {
                        <span nz-icon nzType="check" class="ws-item-check"></span>
                      }
                    </div>
                  </li>
                }
              </ul>
            </div>
          </nz-dropdown-menu>
        } @else {
          <span class="brand-sub">{{ subtitleText() }}</span>
        }
      </div>
    </div>
  `
})
export class BrandLogoComponent {
  private readonly i18n = inject(I18nService, { optional: true });

  readonly collapsed = input<boolean>(false);
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly subtitle = input<string | null>(null);
  readonly workspaces = input<Array<{ id: string; name: string; code?: string }>>([]);
  readonly currentWorkspaceId = input<string | null>(null);
  readonly workspaceChange = output<string>();

  readonly isArabic = computed(() => this.i18n?.lang() === 'ar');
  readonly titleText = computed(() => (this.isArabic() ? 'هاير سكوير 360' : 'HigherSquare 360'));
  readonly subtitleText = computed(() => {
    const custom = this.subtitle();
    if (custom && custom.trim().length > 0) {
      if (this.isArabic()) {
        return this.i18n?.t(custom, custom) ?? custom;
      }
      return custom;
    }
    return this.isArabic() ? 'تخطيط موارد المؤسسات' : 'ENTERPRISE RESOURCE PLANNING';
  });

  onSelectWorkspace(id: string): void {
    if (id && id !== this.currentWorkspaceId()) {
      this.workspaceChange.emit(id);
    }
  }
}
