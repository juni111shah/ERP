import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { StatusKind, statusOption } from '../../projects-ui';

/** A status as a coloured tag with its real name. */
@Component({
  selector: 'erp-prj-tag',
  standalone: true,
  imports: [NzTagModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nz-tag [nzColor]="option().color" [attr.title]="hint() || null">{{ option().label }}</nz-tag>`,
})
export class ProjectTagComponent {
  readonly kind = input.required<StatusKind>();
  readonly value = input<string | null | undefined>(null);
  readonly hint = input<string | null | undefined>(null);
  readonly option = computed(() => statusOption(this.kind(), this.value()));
}
