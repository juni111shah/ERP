import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { map } from 'rxjs';
import { TranslatePipe } from '@core/i18n/translate.pipe';

/**
 * Enterprise error & status screen for 403 (Forbidden / Access Restricted),
 * 404 (Page Not Found), and planned module stand-ins.
 */
@Component({
  selector: 'erp-placeholder',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzButtonModule, NzIconModule, NzTagModule, TranslatePipe],
  templateUrl: './placeholder.page.html',
  styleUrl: './placeholder.page.scss'
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
