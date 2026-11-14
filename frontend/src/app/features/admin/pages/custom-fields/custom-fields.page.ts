import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, of } from 'rxjs';
import { CustomFieldDefinitionsApi, CustomFieldMetadataService } from '@core/custom-fields/custom-field-metadata.service';
import { CUSTOM_FIELD_TYPES, CustomFieldDefinition } from '@core/custom-fields/custom-field.models';
import { doneText } from '@core/format/done-text';
import { controlError } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { showFormSaveError } from '@shared/forms/save-errors';
import { customFieldFormValue, hasChoiceList, suggestedCustomFieldKey, toCustomFieldInput } from '../../admin-custom-fields';

/** Custom fields admin (Odoo Studio style): pick an entity type, list its fields, add / edit / deactivate in a drawer. */
@Component({
  selector: 'erp-custom-fields',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatePanelComponent, KeyboardClickDirective, TranslatePipe, NzToolTipModule, FormsModule, ReactiveFormsModule, HasPermissionDirective, NzTableModule, NzSelectModule, NzButtonModule, NzDrawerModule, NzFormModule,
    NzInputModule, NzInputNumberModule, NzSwitchModule, NzTagModule, NzIconModule],
  styleUrl: './custom-fields.page.scss',
  templateUrl: './custom-fields.page.html',
})
export class CustomFieldsPage {
  private readonly api = inject(CustomFieldDefinitionsApi);
  private readonly meta = inject(CustomFieldMetadataService);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly session = inject(SessionStore);
  readonly fieldTypes = CUSTOM_FIELD_TYPES;
  readonly types = toSignal(this.api.entityTypes().pipe(catchError(() => of([]))), { initialValue: [] });
  readonly entityType = signal<string | null>(null);
  readonly defs = signal<CustomFieldDefinition[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly canManage = computed(() => this.session.hasPermission('core.customfields.manage'));
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly editing = signal<CustomFieldDefinition | null>(null);
  readonly form = this.fb.nonNullable.group({
    label: ['', [Validators.required, Validators.maxLength(100)]],
    key: ['', [Validators.required, Validators.pattern(/^cf_[a-z0-9_]{1,60}$/)]],
    dataType: ['text' as CustomFieldDefinition['dataType'], Validators.required],
    choices: [''],
    section: [''],
    helpText: [''],
    order: [0],
    required: [false],
    showInList: [false],
    isSearchable: [false],
    isActive: [true],
  });
  private readonly dataType = toSignal(this.form.controls.dataType.valueChanges, { initialValue: 'text' as CustomFieldDefinition['dataType'] });
  readonly hasChoices = computed(() => hasChoiceList(this.dataType()));
  /** Set once the user types a key: a failed save also marks the control dirty, which must not stop the suggestion. */
  keyTyped = false;

  typeLabel(t: string): string { return this.fieldTypes.find((x) => x.value === t)?.label ?? t; }
  err(name: string): string { return controlError(this.form.get(name)); }

  selectType(t: string): void {
    this.entityType.set(t);
    this.load();
  }

  load(): void {
    const t = this.entityType();
    if (!t) return;
    this.loading.set(true);
    this.failed.set(false);
    this.api.list(t).subscribe({ next: (d) => { this.defs.set(d); this.loading.set(false); }, error: () => { this.failed.set(true); this.loading.set(false); } });
  }

  open(d: CustomFieldDefinition | null): void {
    this.keyTyped = false;
    this.editing.set(d);
    this.form.reset(customFieldFormValue(d, this.defs().length));
    // Without the manage permission a row opens the field to look at only.
    if (this.canManage()) this.form.enable();
    else this.form.disable();
    this.drawerOpen.set(true);
  }

  /** While a new field's key has not been typed, it follows the label. */
  suggestKey(): void {
    if (this.editing() || this.keyTyped) return;
    this.form.controls.key.setValue(suggestedCustomFieldKey(this.form.controls.label.value));
  }

  save(): void {
    if (!this.canManage()) return;
    if (this.form.invalid) { revealErrors(this.form); return; }
    const body = toCustomFieldInput(this.form.getRawValue(), this.entityType()!, this.hasChoices());
    const d = this.editing();
    this.saving.set(true);
    (d ? this.api.update(d.id, body) : this.api.create(body)).subscribe({
      next: () => {
        this.saving.set(false); this.drawerOpen.set(false); this.message.success(doneText('saved', 'Custom field', body.label));
        this.meta.invalidate(body.entityType); this.load();
      },
      error: (e) => {
        this.saving.set(false);
        showFormSaveError(this.form, e, this.message, 'Could not save the field');
      },
    });
  }
}
