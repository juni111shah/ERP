import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { controlError } from '../../http/problem-details';
import { FormFieldMeta } from '../custom-field.models';
import { LookupOption, LookupRegistry } from '../lookup-registry';

/** Renders one custom field from its metadata (Odoo Studio pattern: the UI is driven by the field registry). */
@Component({
  selector: 'erp-dynamic-field',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, NzFormModule, NzInputModule, NzInputNumberModule, NzDatePickerModule, NzSelectModule, NzSwitchModule, NzCheckboxModule],
  templateUrl: './dynamic-field.component.html',
})
export class DynamicFieldComponent {
  readonly field = input.required<FormFieldMeta>();
  readonly group = input.required<FormGroup>();
  readonly lookupOptions = signal<LookupOption[]>([]);
  private readonly lookups = inject(LookupRegistry);

  /** The message under the field: the first validation or server error of its control. */
  error(): string {
    return controlError(this.group().get(this.field().key));
  }

  /** Server-side search for a `lookup` field, through the source registered for its target entity type. */
  search(term: string): void {
    this.lookups.search(this.field().lookupEntityType, term).subscribe((o) => this.lookupOptions.set(o));
  }
}
