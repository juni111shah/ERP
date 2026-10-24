import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'erp-brand-logo',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
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
      overflow: hidden;
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

    .title-row {
      display: flex;
      align-items: baseline;
      gap: 5px;
    }

    .name-higher {
      font-weight: 700;
      letter-spacing: -0.02em;
      color: #359867;
      font-size: 17px;
    }

    .name-360 {
      font-weight: 800;
      letter-spacing: -0.01em;
      color: #215a67;
      font-size: 17px;
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
    }

    .size-sm .brand-sub { font-size: 7.5px; letter-spacing: 0.1em; }
    .size-lg .brand-sub { font-size: 10.5px; letter-spacing: 0.16em; }

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
    :host-context(.dark) .brand-sub {
      color: #cbd5e1;
    }
  `],
  template: `
    <div class="brand-container" [class.collapsed]="collapsed()" [class]="'size-' + size()">
      <!-- Brand Emblem Image -->
      <div class="emblem-wrapper" [title]="titleText()">
        <img src="highersquare-emblem.png" class="emblem-img" alt="HigherSquare 360" />
      </div>

      <!-- Brand Typography (smoothly fades and slides in/out on collapse) -->
      <div class="brand-text" [attr.aria-hidden]="collapsed()">
        <div class="title-row">
          <span class="name-higher">HigherSquare</span>
          <span class="name-360">360</span>
        </div>
        <span class="brand-sub">{{ subtitleText() }}</span>
      </div>
    </div>
  `
})
export class BrandLogoComponent {
  readonly collapsed = input<boolean>(false);
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly subtitle = input<string | null>(null);

  readonly titleText = computed(() => 'HigherSquare 360');
  readonly subtitleText = computed(() => {
    const custom = this.subtitle();
    return custom && custom.trim().length > 0 ? custom : 'ENTERPRISE RESOURCE PLANNING';
  });
}
