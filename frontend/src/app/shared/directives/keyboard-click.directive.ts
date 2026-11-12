import { Directive, ElementRef, inject, input } from '@angular/core';

/**
 * Makes a clickable non-button element (card, chip, table row) operable from the keyboard: focusable, announced as a button
 * (pass `erpKeyboardClickRole=""` to keep the element's own role, for example a table row) and activated by Enter or Space.
 */
@Directive({
  selector: '[erpKeyboardClick]',
  standalone: true,
  host: {
    '[attr.tabindex]': "enabled() ? '0' : null",
    '[attr.role]': "enabled() ? (role() || null) : null",
    '(keydown.enter)': 'activate($event)',
    '(keydown.space)': 'activate($event)',
  },
})
export class KeyboardClickDirective {
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);
  /** Bind a condition (`[erpKeyboardClick]="clickable()"`) when only some elements are clickable. */
  readonly enabled = input(true, { alias: 'erpKeyboardClick', transform: (v: boolean | '') => v !== false });
  readonly role = input<string>('button', { alias: 'erpKeyboardClickRole' });

  activate(event: Event): void {
    if (!this.enabled() || event.target !== this.el.nativeElement) return; // keys typed in a nested input or button keep their own meaning
    event.preventDefault();
    this.el.nativeElement.click();
  }
}
