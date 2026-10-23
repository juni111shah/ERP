import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalService } from 'ng-zorro-antd/modal';

/** Modal body: one required reason textarea. */
@Component({
  selector: 'erp-reason-prompt',
  standalone: true,
  imports: [FormsModule, NzInputModule],
  template: `
    @if (hint) { <p>{{ hint }}</p> }
    <label for="erp-reason">Reason ({{ required ? 'required' : 'optional' }})</label>
    <textarea id="erp-reason" nz-input rows="3" [(ngModel)]="reason" data-testid="reason-input"></textarea>
  `,
})
export class ReasonPromptComponent {
  hint = '';
  required = true;
  reason = '';
}

/**
 * Opens a modal asking for a reason; resolves with the trimmed reason, or null if cancelled. OK stays disabled while empty.
 * With `required = false` OK is always enabled and an empty reason resolves as '' (cancel still resolves null).
 */
export function askReason(modal: NzModalService, title: string, okText: string, hint = '', required = true): Promise<string | null> {
  return new Promise((resolve) => {
    let done = false;
    const ref = modal.create<ReasonPromptComponent>({
      nzTitle: title,
      nzContent: ReasonPromptComponent,
      nzFooter: [
        { label: 'Cancel', onClick: () => ref.close() },
        {
          label: okText, type: 'primary', danger: true,
          disabled: (c?: ReasonPromptComponent) => required && !c?.reason?.trim(),
          onClick: (c?: ReasonPromptComponent) => { done = true; resolve((c?.reason ?? '').trim()); ref.close(); },
        },
      ],
    });
    if (ref.componentInstance) { ref.componentInstance.hint = hint; ref.componentInstance.required = required; }
    ref.afterClose.subscribe(() => { if (!done) resolve(null); });
  });
}
