import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { STATUS_COLORS, humanize } from './realestate.api';

/** Coloured tag for any status/stage value ("holdingOver" -> "Holding over"). */
@Component({
  selector: 'erp-status-tag',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzTagModule],
  template: `<nz-tag [nzColor]="color()" [attr.data-testid]="'status-' + status()">{{ label() }}</nz-tag>`,
})
export class StatusTagComponent {
  readonly status = input.required<string>();
  readonly color = computed(() => STATUS_COLORS[this.status()] ?? 'default');
  readonly label = computed(() => humanize(this.status()));
}
