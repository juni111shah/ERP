import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { displayLabel, statusTone } from '../core/format/display';

/** A status as a coloured tag: the words come from the value, the colour from its meaning. The text is always shown, so colour is never the only signal. */
@Component({
  selector: 'erp-status',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzTagModule],
  template: `<nz-tag class="erp-tone" [attr.data-tone]="tone()">{{ text() }}</nz-tag>`,
})
export class StatusTagComponent {
  readonly value = input<unknown>();
  /** Words to show instead of the value's own. */
  readonly label = input<string | null>(null);
  readonly tone = computed(() => statusTone(this.value()));
  readonly text = computed(() => this.label() ?? displayLabel(this.value()));
}
