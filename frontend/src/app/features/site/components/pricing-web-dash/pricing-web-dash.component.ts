import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { lastMonths } from '../../pricing-mockups';

const GROUPS = ['Sales', 'Purchases', 'Finance & Accounting', 'Master Data', 'CRM', 'Services', 'Inventory', 'Point of Sale', 'Retail', 'Distribution', 'Projects', 'Manufacturing', 'HR and Payroll', 'Setup'];

/** The web app's real Overview page, drawn at desktop size with the labels and layout it really has. Sample figures only. */
@Component({
  selector: 'erp-web-dash',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzIconModule, BrandLogoComponent],
  styleUrl: './pricing-web-dash.component.scss',
  templateUrl: './pricing-web-dash.component.html',
})
export class WebDashComponent {
  readonly groups = GROUPS;
  readonly months = lastMonths(12, true);
  readonly ticks = [{ label: '0', at: 0 }, { label: '10K', at: 20 }, { label: '20K', at: 40 }, { label: '30K', at: 60 }, { label: '40K', at: 80 }, { label: '50K', at: 100 }];
  readonly series = [[44, 40], [50, 46], [47, 49], [56, 50], [60, 58], [55, 57], [64, 60], [70, 62], [66, 69], [76, 70], [84, 74], [90, 86]];
}
