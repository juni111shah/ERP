import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TenantThemeService } from '../theme/tenant-theme.service';
import { TableCellTipService } from '@core/services/table-cell-tip.service';
import { TableDensityService } from '@core/services/table-density.service';

@Component({
  selector: 'erp-root',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class AppComponent {
  // Instantiated at startup so theme, density and direction are applied before the first paint.
  readonly theme = inject(TenantThemeService);
  // The chosen row height and the tooltip for shortened cells apply to every table on every page.
  readonly tableDensity = inject(TableDensityService);

  constructor() { inject(TableCellTipService).start(); }
}
