import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NzResultModule } from 'ng-zorro-antd/result';

/** Stand-in for screens that are specified but not built yet; the title comes from the route's `data.title`. */
@Component({
  selector: 'erp-placeholder',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzResultModule],
  template: `<nz-result nzStatus="info" [nzTitle]="title() ?? 'Coming soon'" nzSubTitle="This screen is not available yet." data-testid="placeholder"></nz-result>`,
})
export class PlaceholderPage {
  // Bound from route data by withComponentInputBinding().
  readonly title = input<string>();
}
