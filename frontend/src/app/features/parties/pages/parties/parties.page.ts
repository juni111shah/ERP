import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, forkJoin, map, of } from 'rxjs';
import { PartyGroupsApi } from '@core/api/admin.api';
import { FilterCriterion, ListQuery } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { PartiesApi, PartyDto } from '@core/api/parties.api';
import { buildCustomFieldsGroup } from '@core/custom-fields/custom-field-form';
import { CustomFieldMetadataService } from '@core/custom-fields/custom-field-metadata.service';
import { FormSectionMeta } from '@core/custom-fields/custom-field.models';
import { DynamicFieldsComponent } from '@core/custom-fields/dynamic-field.component';
import { doneText } from '@core/format/done-text';
import { applyProblemDetailsToForm, asProblem, controlError } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { ErpLabelPipe } from '@shared/pipes/display.pipes';
import { KpiStats, countRows } from '@features/finance/finance-kpis';
import { PARTY_ROLES, PartyFormValue, groupFilter, partiesTitle, partyBody, partyForm, roleFilter, roleTagColor } from '../../parties-form';

/** The party counts on the cards. */
interface PartyCounts { total: number; active: number; companies: number; individuals: number; }

/** Customers, vendors and other parties: the list (filtered by role from the menu, or by group), counts, and the create / edit drawer. */
@Component({
  selector: 'erp-parties',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    NzButtonModule,
    NzDrawerModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzSelectModule,
    NzSwitchModule,
    NzTagModule,
    NzToolTipModule,
    DynamicFieldsComponent,
    PagedTableComponent,
    StatCardComponent,
    HasPermissionDirective,
    ErpLabelPipe,
  ],
  templateUrl: './parties.page.html',
})
export class PartiesPage implements OnInit {
  private readonly api = inject(PartiesApi);
  private readonly groupsApi = inject(PartyGroupsApi);
  private readonly meta = inject(CustomFieldMetadataService);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly session = inject(SessionStore);
  private readonly groupFilterId = signal<string | null>(null);
  private roleFilterValue: string | null = null;
  /** `?type=` known up front, so the first request is already filtered (no unfiltered load racing the filtered one). */
  private readonly initialType = this.route.snapshot.queryParamMap.get('type');

  readonly groups = toSignal(this.groupsApi.list({ page: 1, pageSize: 200, sort: 'code' }).pipe(map((r) => r.items), catchError(() => of([]))), { initialValue: [] });
  readonly roles = PARTY_ROLES;
  readonly restrictedHint = "Restricted: you don't have permission to view or edit this field";
  /** Editing an existing party without core.sensitive.read: ID number and IBAN arrive masked, so they are read-only. */
  readonly sensitiveLocked = signal(false);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code', filters: roleFilter(this.initialType) });
  readonly typeFilter = signal<string | null>(this.initialType);
  readonly pageTitle = signal(partiesTitle(this.initialType));
  /** Never say "No parties yet" while loading or after a failed load: both looked like an empty list. */
  readonly emptyText = computed(() => {
    if (this.list.loading()) return 'Loading…';
    if (this.list.error()) return 'Could not load the list. Reload the page to try again.';
    const type = this.typeFilter();
    return type === 'customer' ? 'No customers yet' : type === 'vendor' ? 'No vendors yet' : 'No parties yet';
  });
  readonly columns: TableColumn<PartyDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '130px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'roles', label: 'Roles', width: '160px' },
    { key: 'email', label: 'Email', sortField: 'email' },
    { key: 'isActive', label: 'Active', sortField: 'isActive', width: '110px' },
  ];
  private readonly kpis = new KpiStats<PartyCounts>();
  /** Counted by the server for the whole list, not the page on screen. */
  readonly stats = this.kpis.data;
  readonly statsLoading = this.kpis.loading;
  readonly drawerOpen = signal(false);
  readonly editing = signal<PartyDto | null>(null);
  readonly saving = signal(false);
  readonly sections = signal<FormSectionMeta[]>([]);
  readonly customGroup = signal<FormGroup>(new FormGroup({}));
  form = this.newForm();

  ngOnInit(): void {
    this.loadStats();
    // Re-apply on every query change: Customers -> Vendors from the menu reuses this component.
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((qp) => {
      const type = qp.get('type');
      if (type !== this.typeFilter()) {
        this.typeFilter.set(type);
        this.pageTitle.set(partiesTitle(type));
        this.list.setFilters([...roleFilter(type), ...groupFilter(this.groupFilterId())]);
        this.loadStats();
      }
      // `?edit=<id>` (from the party detail page's Edit) opens the edit drawer for that party.
      const editId = qp.get('edit');
      if (editId && !(this.drawerOpen() && this.editing()?.id === editId)) this.api.get(editId).subscribe((p) => this.edit(p));
    });
  }

  roleTagColor(role: string): string { return roleTagColor(role); }

  filterRole(role: string | null): void {
    this.roleFilterValue = role;
    this.list.setFilters([...roleFilter(role), ...groupFilter(this.groupFilterId())]);
  }

  filterGroup(id: string | null): void {
    this.groupFilterId.set(id);
    this.list.setFilters([...roleFilter(this.typeFilter() ?? this.roleFilterValue), ...groupFilter(id)]);
  }

  open(p: PartyDto | null): void {
    if (p) {
      void this.router.navigate(['/parties', p.id]);
      return;
    }
    this.editing.set(null);
    this.form = this.newForm(null);
    this.loadCustomFields(null, true);
    this.drawerOpen.set(true);
  }

  close(): void {
    this.drawerOpen.set(false);
    this.clearEditParam();
  }

  err(name: string): string { return controlError(this.form.get(name)); }

  save(): void {
    if (this.form.invalid) { revealErrors(this.form); return; }
    const v = this.form.getRawValue() as PartyFormValue;
    const p = this.editing();
    const body = partyBody(v, this.sensitiveLocked(), p?.creditLimit ?? null, this.sections());
    this.saving.set(true);
    (p ? this.api.update(p.id, body) : this.api.create(body)).subscribe({
      next: () => {
        this.saving.set(false);
        this.drawerOpen.set(false);
        this.message.success(doneText('saved', 'Party', v.name));
        this.list.reload();
        this.loadStats();
        if (p) void this.router.navigate(['/parties', p.id]); else this.clearEditParam();
      },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        const rest = problem ? applyProblemDetailsToForm(this.form, problem) : [];
        this.message.error(rest[0] ?? problem?.detail ?? problem?.title ?? 'Could not save');
      },
    });
  }

  private edit(p: PartyDto): void {
    this.editing.set(p);
    this.form = this.newForm(p);
    this.loadCustomFields(p.customFields, false);
    this.drawerOpen.set(true);
  }

  /** Adds the party's custom fields to the form once their sections load. */
  private loadCustomFields(values: PartyDto['customFields'] | null, isNew: boolean): void {
    this.meta.sections('party').subscribe((s) => {
      this.sections.set(s);
      const group = buildCustomFieldsGroup(s, values as never, isNew);
      this.form.setControl('customFields', group);
      this.customGroup.set(group);
    });
  }

  private loadStats(): void {
    const base = roleFilter(this.typeFilter());
    const count = (extra: FilterCriterion[]) => countRows((q: ListQuery) => this.api.list(q), [...base, ...extra]);
    this.kpis.load(forkJoin({
      total: count([]),
      active: count([{ field: 'isActive', op: 'eq', value: true }]),
      companies: count([{ field: 'kind', op: 'eq', value: 'company' }]),
      individuals: count([{ field: 'kind', op: 'eq', value: 'individual' }]),
    }));
  }

  /** Drop `?edit=<id>` once the drawer closes, so a reload or Back does not reopen it. */
  private clearEditParam(): void {
    if (this.route.snapshot.queryParamMap.has('edit')) void this.router.navigate([], { relativeTo: this.route, queryParams: { edit: null }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  private newForm(p?: PartyDto | null): FormGroup {
    const locked = !!p && !this.session.hasPermission('core.sensitive.read');
    this.sensitiveLocked.set(locked);
    return partyForm(this.fb, p, this.typeFilter() === 'vendor' ? 'Vendor' : 'Customer', locked);
  }
}
