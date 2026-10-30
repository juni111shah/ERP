import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { map } from 'rxjs';
import { TranslatePipe } from '../core/i18n/translate.pipe';

/**
 * Enterprise error & status screen for 403 (Forbidden / Access Restricted),
 * 404 (Page Not Found), and planned module stand-ins.
 */
@Component({
  selector: 'erp-placeholder',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzButtonModule, NzIconModule, NzTagModule, TranslatePipe],
  template: `
    <div class="status-container" data-testid="placeholder">
      <div class="status-card">
        <div class="status-icon-badge" [class.forbidden]="isForbidden()">
          <span nz-icon [nzType]="isForbidden() ? 'safety' : 'compass'" nzTheme="outline"></span>
        </div>

        <div class="badge-row">
          <nz-tag [nzColor]="isForbidden() ? 'red' : 'default'">
            {{ isForbidden() ? '403 · FORBIDDEN' : '404 · NOT FOUND' }}
          </nz-tag>
        </div>

        <h1 class="status-title">{{ computedTitle() }}</h1>
        <p class="status-desc">{{ computedDesc() }}</p>

        <div class="status-actions">
          <a routerLink="/" nz-button nzType="primary" nzSize="large">
            <span nz-icon nzType="dashboard"></span>
            {{ 'ui.nav.backDashboard' | translate: 'Return to Dashboard' }}
          </a>
          <button nz-button nzSize="large" (click)="goBack()">
            <span nz-icon nzType="arrow-left"></span>
            {{ 'ui.nav.goBack' | translate: 'Go Back' }}
          </button>
        </div>

        <div class="status-tips">
          <span>{{ 'ui.nav.needHelpTip' | translate: 'Need help or missing access? Contact your workspace administrator.' }}</span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .status-container {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: calc(100vh - 160px);
      padding: 32px 16px;
    }
    .status-card {
      background: var(--erp-card-bg, #ffffff);
      border: 1px solid var(--erp-border, #e5e7eb);
      border-radius: 16px;
      padding: 48px 36px;
      max-width: 520px;
      width: 100%;
      text-align: center;
      box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.05), 0 8px 10px -6px rgba(0, 0, 0, 0.05);
    }
    .status-icon-badge {
      width: 72px;
      height: 72px;
      margin: 0 auto 20px auto;
      border-radius: 50%;
      background: rgba(16, 149, 93, 0.1);
      color: var(--erp-brand-600, #10955d);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 36px;
    }
    .status-icon-badge.forbidden {
      background: rgba(239, 68, 68, 0.1);
      color: #ef4444;
    }
    .badge-row {
      margin-bottom: 12px;
    }
    .status-title {
      font-size: 22px;
      font-weight: 700;
      color: var(--erp-text, #111827);
      margin: 0 0 12px 0;
      line-height: 1.3;
    }
    .status-desc {
      font-size: 14px;
      color: var(--erp-text-secondary, #6b7280);
      margin: 0 0 28px 0;
      line-height: 1.6;
    }
    .status-actions {
      display: flex;
      gap: 12px;
      justify-content: center;
      flex-wrap: wrap;
      margin-bottom: 24px;
    }
    .status-tips {
      font-size: 12px;
      color: var(--erp-text-muted, #9ca3af);
      border-top: 1px solid var(--erp-border, #f3f4f6);
      padding-top: 16px;
    }
  `]
})
export class PlaceholderPage {
  private readonly route = inject(ActivatedRoute);

  readonly title = toSignal(
    this.route.data.pipe(map((d) => (d['title'] as string) ?? '')),
    { initialValue: '' }
  );

  readonly isForbidden = computed(() => {
    const t = (this.title() || '').toLowerCase();
    const url = this.route.snapshot.url.map((u) => u.path).join('/');
    return t.includes('access') || t.includes('forbidden') || url.includes('forbidden');
  });

  readonly computedTitle = computed(() => {
    if (this.isForbidden()) {
      return this.title() || 'Access Restricted';
    }
    return this.title() || 'Page Not Found';
  });

  readonly computedDesc = computed(() => {
    if (this.isForbidden()) {
      return 'You do not have permission to view this resource or this module is not enabled for your workspace. Please contact your organization administrator.';
    }
    return 'The page or resource you are looking for might have been moved, renamed, or is temporarily unavailable.';
  });

  goBack(): void {
    window.history.back();
  }
}
