import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { FilterCriterion } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { LeaseDto, LeaseListItem, LeasesApi, RealEstateLookups } from '@core/api/realestate.api';
import { toIsoDate } from '@core/format/dates';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';
import { leaseFields } from '../../realestate-forms';
import { leaseStats } from '../../realestate-stats';

/** The list filters behind the view buttons (all, active, draft, past, expiring within 60 days). */
function leaseViewFilters(view: string): FilterCriterion[] {
  const inDays = (n: number) => { const d = new Date(); d.setDate(d.getDate() + n); return toIsoDate(d); };
  const filters: Record<string, FilterCriterion[]> = {
    all: [],
    active: [{ field: 'status', op: 'eq', value: 'active' }],
    draft: [{ field: 'status', op: 'eq', value: 'draft' }],
    past: [{ field: 'status', op: 'in', value: ['ended', 'terminated', 'renewed', 'cancelled'] }],
    expiring: [{ field: 'status', op: 'eq', value: 'active' }, { field: 'endDate', op: 'lte', value: inDays(60) }],
  };
  return filters[view] ?? [];
}

/** Leases: KPI cards, the list with its views, and the new-lease drawer; a row opens the lease's detail page. */
@Component({
  selector: 'erp-leases',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzTagModule, StatCardComponent, TranslatePipe, NzToolTipModule, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule],
  templateUrl: './leases.page.html',
})
export class LeasesPage {
  private readonly i18n = inject(I18nService);
  private readonly leasesApi = inject(LeasesApi);
  private readonly router = inject(Router);
  view = 'all';
  readonly list = createPagedList((q) => this.leasesApi.list(q), { sort: '-startDate' });
  readonly stats = computed(() => leaseStats(this.list.items(), this.list.total()));
  readonly fields = leaseFields(inject(RealEstateLookups), false);
  readonly state = new MasterDataState<LeaseDto>({
    save: (_, body) => this.leasesApi.saveDraft(null, body), reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Lease',
  });
  readonly columns: TableColumn<LeaseListItem>[] = [
    { key: 'number', label: 'Lease', sortField: 'number', width: '150px' },
    { key: 'tenantName', label: 'Tenant' },
    { key: 'propertyName', label: 'Property' },
    { key: 'unitNumber', label: 'Unit', width: '90px' },
    { key: 'startDate', label: 'Start', sortField: 'startDate' },
    { key: 'endDate', label: 'End', sortField: 'endDate' },
    { key: 'daysToExpiry', label: 'Days left', value: (l) => this.daysLeftText(l.daysToExpiry) },
    { key: 'annualRent', label: 'Annual rent', sortField: 'annualRent', align: 'right', value: (l) => l.annualRent.toLocaleString() },
    { key: 'status', label: 'Status', sortField: 'status' },
  ];

  setView(view: string): void {
    this.view = view;
    this.list.setFilters(leaseViewFilters(view));
  }

  open(l: LeaseListItem): void { void this.router.navigate(['/real-estate/leases', l.id]); }

  /** "45 days left", "ended 3 days ago", or blank. */
  private daysLeftText(days: number | undefined): string {
    if (days == null) return '';
    return days >= 0 ? `${days} ${this.i18n.t('ui.re.daysLeft', 'days left')}` : `${this.i18n.t('ui.re.endedAgo', 'ended')} ${-days} ${this.i18n.t('ui.re.daysAgo', 'days ago')}`;
  }
}
