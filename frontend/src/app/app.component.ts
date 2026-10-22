import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TenantThemeService } from '../theme/tenant-theme.service';

@Component({
  selector: 'erp-root',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class AppComponent {
  // Instantiated at startup so theme, density and direction are applied before the first paint.
  readonly theme = inject(TenantThemeService);
}
