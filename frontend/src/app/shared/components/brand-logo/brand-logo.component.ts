import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { I18nService } from '@core/i18n/i18n.service';

@Component({
  selector: 'erp-brand-logo',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, NzDropDownModule, NzMenuModule, NzIconModule, NzToolTipModule],
  styleUrl: './brand-logo.component.scss',
  templateUrl: './brand-logo.component.html'
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
