import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { FormSectionMeta } from '../custom-field.models';
import { DynamicFieldComponent } from '../dynamic-field/dynamic-field.component';

/** Renders all custom-field sections of a form; bind [group] to the nested `customFields` FormGroup. */
@Component({
  selector: 'erp-dynamic-fields',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DynamicFieldComponent],
  template: `
    @for (s of sections(); track s.key) {
      <section class="erp-form-section" [attr.data-testid]="'cf-section-' + s.key">
        <h3 class="erp-form-section__title">{{ s.label }}</h3>
        <div class="erp-field-grid">
          @for (f of s.fields; track f.key) { <erp-dynamic-field [field]="f" [group]="group()" /> }
        </div>
      </section>
    }`,
})
export class DynamicFieldsComponent {
  readonly sections = input.required<FormSectionMeta[]>();
  readonly group = input.required<FormGroup>();
}
