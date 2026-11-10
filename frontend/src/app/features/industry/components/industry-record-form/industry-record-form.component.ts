import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { NzMessageService } from 'ng-zorro-antd/message';
import { catchError, of } from 'rxjs';
import { BusinessUnitsApi, ItemsApi } from '@core/api/admin.api';
import { ProblemDetails } from '@core/api/api.models';
import { IndustryApi, IndustryRecordDto, RecordTypeView } from '@core/api/industry.api';
import { SetupApi } from '@core/api/setup.api';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { tNow } from '@core/i18n/translate-now';
import { SessionStore } from '@core/session/session.store';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { RecordDrawerComponent, SelectOption } from '@shared/components/record-drawer/record-drawer.component';
import { RecordFormContext, recordFieldSpecs, toFormValue, toRecordInput } from '../../record-form';

/**
 * Create and edit drawer for any record type: the fields come from the type's definition, the pickers from the workspace.
 * The parent supplies the module, the type, and (to edit) the saved record; it hears back when a record was saved.
 */
@Component({
  selector: 'erp-industry-record-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RecordDrawerComponent],
  template: `
    <erp-record-drawer [title]="title()" [open]="open()" [fields]="fields()" [value]="formValue()" [saving]="saving()" [errors]="errors()" [width]="880"
                       testId="industry-form" (submitted)="submit($event)" (closed)="closed.emit()" />`,
})
export class IndustryRecordFormComponent {
  readonly module = input.required<string>();
  readonly type = input.required<RecordTypeView>();
  /** The saved record to edit; leave empty to create. */
  readonly record = input<IndustryRecordDto | null>(null);
  readonly open = input(false);
  readonly saved = output<IndustryRecordDto>();
  readonly closed = output<void>();

  private readonly api = inject(IndustryApi);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  private readonly businessUnits = signal<SelectOption[]>([]);
  private readonly items = signal<SelectOption[]>([]);
  private readonly units = signal<string[]>([]);

  readonly title = computed(() => (this.record() ? tNow('ui.industry.editTitle', 'Edit {name}', { name: this.type().singular.toLowerCase() }) : tNow('ui.industry.newTitle', 'New {name}', { name: this.type().singular.toLowerCase() })));
  readonly context = computed<RecordFormContext>(() => ({
    legalEntities: (this.session.me()?.legalEntities ?? []).map((e) => ({ value: e.id, label: `${e.code} · ${e.name}` })),
    defaultLegalEntityId: this.session.currentEntityId(),
    businessUnits: this.businessUnits(), items: this.items(), units: this.units(), currency: this.currencyService.currency(),
    partyLookup: (term) => this.api.partyLookup(this.type().partyRole, term),
    parentLookup: (term) => this.api.recordLookup(this.module(), this.type().parentType ?? '', term),
  }));
  readonly fields = computed(() => recordFieldSpecs(this.type(), this.context()));
  readonly formValue = computed(() => { const r = this.record(); return r ? toFormValue(r, this.type()) : null; });

  constructor() {
    const units$ = this.session.hasPermission('core.businessunits.read') ? inject(BusinessUnitsApi).list({ page: 1, pageSize: 100, sort: 'code' }).pipe(catchError(() => of(null))) : of(null);
    units$.subscribe((r) => this.businessUnits.set((r?.items ?? []).filter((u) => u.isActive).map((u) => ({ value: u.id, label: `${u.code} · ${u.name}` }))));
    const items$ = this.session.hasPermission('core.items.read') ? inject(ItemsApi).list({ page: 1, pageSize: 200, sort: 'sku' }).pipe(catchError(() => of(null))) : of(null);
    items$.subscribe((r) => this.items.set((r?.items ?? []).filter((i) => i.isActive).map((i) => ({ value: i.id, label: `${i.sku} · ${i.name}` }))));
    const profile$ = this.session.hasPermission('core.settings.read') ? inject(SetupApi).profile().pipe(catchError(() => of(null))) : of(null);
    profile$.subscribe((p) => this.units.set(p?.units ?? []));
  }

  submit(payload: Record<string, unknown>): void {
    const body = toRecordInput(payload, this.type());
    const existing = this.record();
    const call = existing ? this.api.update(this.module(), this.type().key, existing.id, body) : this.api.create(this.module(), this.type().key, body);
    this.saving.set(true);
    this.errors.set(null);
    call.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.message.success(doneText('saved', this.type().singular, saved?.number ?? body.title));
        this.saved.emit(saved);
      },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        this.errors.set(problem);
        if (!problem?.errors || Object.keys(problem.errors).length === 0) {
          this.message.error(problem?.detail ?? problem?.title ?? tNow('ui.industry.saveFailed', 'Could not save the {name}', { name: this.type().singular.toLowerCase() }));
        }
      },
    });
  }
}
