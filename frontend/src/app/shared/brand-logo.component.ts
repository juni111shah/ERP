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

    .size-sm .emblem-wrapper { width: 26px; height: 26px; border-radius: 7px; }
    .size-md .emblem-wrapper { width: 34px; height: 34px; border-radius: 9px; }
    .size-lg .emblem-wrapper { width: 44px; height: 44px; border-radius: 11px; }

    .emblem-svg {
      width: 100%;
      height: 100%;
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
      max-width: 220px;
      opacity: 1;
      transform: translateX(0);
      transition: opacity 0.18s cubic-bezier(0.2, 0, 0, 1),
                  max-width 0.24s cubic-bezier(0.2, 0, 0, 1),
                  margin-inline-start 0.24s cubic-bezier(0.2, 0, 0, 1),
                  transform 0.2s cubic-bezier(0.2, 0, 0, 1);
    }

    .brand-container.collapsed .brand-text {
      max-width: 0;
      margin-inline-start: 0;
      opacity: 0;
      transform: translateX(-8px);
      pointer-events: none;
    }

    .title-row {
      display: flex;
      align-items: baseline;
      gap: 5px;
    }

    .name-main {
      font-weight: 800;
      letter-spacing: -0.025em;
      color: var(--erp-color-text-primary, #0f231e);
      font-size: 17px;
    }

    .name-erp {
      font-weight: 800;
      letter-spacing: -0.01em;
      color: #10955d;
      font-size: 17px;
    }

    .size-sm .name-main,
    .size-sm .name-erp { font-size: 14px; }

    .size-lg .name-main,
    .size-lg .name-erp { font-size: 22px; }

    .brand-sub {
      font-size: 8.5px;
      font-weight: 600;
      letter-spacing: 0.16em;
      color: var(--erp-color-text-tertiary, #4a635b);
      text-transform: uppercase;
      margin-top: 2px;
    }

    .size-sm .brand-sub { font-size: 7.5px; letter-spacing: 0.12em; }
    .size-lg .brand-sub { font-size: 10.5px; letter-spacing: 0.18em; }

    /* Custom dark overrides if host/parent has dark theme */
    :host-context([data-theme='dark']) .name-main,
    :host-context(.dark) .name-main {
      color: #f1f5f9;
    }
    :host-context([data-theme='dark']) .name-erp,
    :host-context(.dark) .name-erp {
      color: #22c55e;
    }
    :host-context([data-theme='dark']) .brand-sub,
    :host-context(.dark) .brand-sub {
      color: #94a3b8;
    }
  `],
  template: `
    <div class="brand-container" [class.collapsed]="collapsed()" [class]="'size-' + size()">
      <!-- Vector Emblem -->
      <div class="emblem-wrapper" [title]="titleText()">
        <svg class="emblem-svg" viewBox="0 0 256 256" fill="none" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="blTileBg" x1="0%" y1="0%" x2="0%" y2="100%">
              <stop offset="0%" stop-color="#143b2f" />
              <stop offset="100%" stop-color="#1b493b" />
            </linearGradient>
            <linearGradient id="blGearBody" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#17443a" />
              <stop offset="100%" stop-color="#0f2f27" />
            </linearGradient>
            <linearGradient id="blScreenGrad" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#19685b" />
              <stop offset="50%" stop-color="#134e44" />
              <stop offset="100%" stop-color="#0b322a" />
            </linearGradient>
            <linearGradient id="blLineGrad" x1="0%" y1="100%" x2="100%" y2="0%">
              <stop offset="0%" stop-color="#34d399" />
              <stop offset="60%" stop-color="#2dd4bf" />
              <stop offset="100%" stop-color="#a7f3d0" />
            </linearGradient>
          </defs>

          <!-- Outer App Tile Squircle -->
          <rect width="256" height="256" rx="58" fill="url(#blTileBg)" />

          <!-- Gear Outer Frame -->
          <!-- Top / Bottom Teeth -->
          <rect x="76" y="34" width="28" height="20" rx="5" fill="#18463b" />
          <rect x="152" y="34" width="28" height="20" rx="5" fill="#18463b" />
          <rect x="76" y="202" width="28" height="20" rx="5" fill="#11362c" />
          <rect x="152" y="202" width="28" height="20" rx="5" fill="#11362c" />
          <!-- Left / Right Teeth -->
          <rect x="34" y="76" width="20" height="28" rx="5" fill="#154035" />
          <rect x="34" y="152" width="20" height="28" rx="5" fill="#12382f" />
          <rect x="202" y="76" width="20" height="28" rx="5" fill="#174439" />
          <rect x="202" y="152" width="20" height="28" rx="5" fill="#123a31" />

          <!-- Gear Main Hull -->
          <rect x="46" y="46" width="164" height="164" rx="34" fill="url(#blGearBody)" stroke="#225c4e" stroke-width="2.5" />

          <!-- Inner Screen Display -->
          <rect x="58" y="58" width="140" height="140" rx="22" fill="url(#blScreenGrad)" stroke="#287f6e" stroke-width="2.5" />
          <rect x="62" y="62" width="132" height="132" rx="18" fill="none" stroke="#2dd4bf" stroke-width="1.2" stroke-opacity="0.25" />

          <!-- Rising Line Chart Path -->
          <path d="M 80 152 L 112 118 L 142 138 L 176 80" 
                fill="none" 
                stroke="url(#blLineGrad)" 
                stroke-width="11" 
                stroke-linecap="round" 
                stroke-linejoin="round" />

          <!-- Chart Nodes -->
          <circle cx="80" cy="152" r="7.5" fill="#2dd4bf" stroke="#134e44" stroke-width="2" />
          <circle cx="112" cy="118" r="7.5" fill="#2dd4bf" stroke="#134e44" stroke-width="2" />
          <circle cx="142" cy="138" r="7.5" fill="#2dd4bf" stroke="#134e44" stroke-width="2" />
          <circle cx="176" cy="80" r="14" fill="#0f3d33" stroke="url(#blLineGrad)" stroke-width="6.5" />
          <circle cx="176" cy="80" r="4" fill="#5eead4" />
        </svg>
      </div>

      <!-- Brand Typography (smoothly fades and slides in/out on collapse) -->
      <div class="brand-text" [attr.aria-hidden]="collapsed()">
        <div class="title-row">
          <span class="name-main">highersquare</span>
          <span class="name-erp">ERP</span>
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

  readonly titleText = computed(() => 'highersquare ERP');
  readonly subtitleText = computed(() => {
    const custom = this.subtitle();
    return custom && custom.trim().length > 0 ? custom : 'ENTERPRISE RESOURCE PLANNING';
  });
}
