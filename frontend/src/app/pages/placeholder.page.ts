import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { map } from 'rxjs';

/** Stand-in for screens planned in later sprints (routes and permissions are already wired). */
@Component({
  selector: 'erp-placeholder',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzEmptyModule],
  template: `<h1 class="erp-page-title">{{ title() }}</h1><nz-empty nzNotFoundContent="This screen arrives in a later sprint." data-testid="placeholder"></nz-empty>`,
})
export class PlaceholderPage {
  readonly title = toSignal(inject(ActivatedRoute).data.pipe(map((d) => (d['title'] as string) ?? '')), { initialValue: '' });
}
