import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TableCellTipService } from '@core/services/table-cell-tip.service';
import { TableDensityService } from '@core/services/table-density.service';
import { TenantThemeService } from '../theme/tenant-theme.service';

/** The root of the app: only the router outlet, plus the app-wide services that must start before the first paint. */
@Component({
  selector: 'erp-root',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class AppComponent {
  /** Instantiated at startup so theme, density and direction are applied before the first paint. */
  readonly theme = inject(TenantThemeService);
  /** The chosen row height and the tooltip for shortened cells apply to every table on every page. */
  readonly tableDensity = inject(TableDensityService);

  constructor() { inject(TableCellTipService).start(); }
}
