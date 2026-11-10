import { ChangeDetectionStrategy, Component, TemplateRef, viewChild } from '@angular/core';

/** The body of a success toast with a next-step button; SuccessToastService creates it and hands its template to the message. */
@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ng-template #content>{{ text }} <button type="button" class="erp-toast-action" (click)="act()" data-testid="toast-action">{{ label }}</button></ng-template>`,
})
export class ToastContentComponent {
  readonly content = viewChild.required<TemplateRef<void>>('content');
  text = '';
  label = '';
  act: () => void = () => undefined;
}
