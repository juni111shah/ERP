import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { SessionStore } from '../core/session/session.store';

@Component({
  selector: 'erp-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzCardModule, NzGridModule, NzStatisticModule],
  template: `
    @if (session.me(); as me) {
      <h1 class="erp-page-title" data-testid="dashboard-title">Welcome, {{ me.user.fullName }}</h1>
      <p>{{ me.workspace.name }} · base currency {{ me.workspace.baseCurrency }}</p>
      <div nz-row [nzGutter]="16">
        <div nz-col [nzSpan]="6"><nz-card><nz-statistic nzTitle="Legal entities" [nzValue]="me.legalEntities.length"></nz-statistic></nz-card></div>
        <div nz-col [nzSpan]="6"><nz-card><nz-statistic nzTitle="Enabled modules" [nzValue]="me.enabledModules.length"></nz-statistic></nz-card></div>
        <div nz-col [nzSpan]="6"><nz-card><nz-statistic nzTitle="Permissions" [nzValue]="me.permissions.length"></nz-statistic></nz-card></div>
        <div nz-col [nzSpan]="6"><nz-card><nz-statistic nzTitle="Workspaces" [nzValue]="me.workspaces.length"></nz-statistic></nz-card></div>
      </div>
    }`,
})
export class DashboardPage {
  readonly session = inject(SessionStore);
}
