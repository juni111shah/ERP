import { ApplicationRef, ChangeDetectionStrategy, Component, EnvironmentInjector, Injectable, TemplateRef, createComponent, inject, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { NzMessageService } from 'ng-zorro-antd/message';

export interface ToastAction {
  /** The next step, in words: "View invoice", "Record another". */
  label: string;
  /** Where the step goes. */
  to?: unknown[];
  queryParams?: Record<string, string>;
  /** For a step that stays on the page. */
  run?: () => void;
}

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ng-template #content>{{ text }} <button type="button" class="erp-toast-action" (click)="act()" data-testid="toast-action">{{ label }}</button></ng-template>`,
})
class ToastContentComponent {
  readonly content = viewChild.required<TemplateRef<void>>('content');
  text = '';
  label = '';
  act: () => void = () => undefined;
}

/**
 * A success toast that offers the next step, for low-stakes work that stays on the same page. It leaves on its own after the app's
 * standard 4 seconds (provideNzConfig in app.config.ts), and waits while the pointer is on it. Money uses a success panel that stays
 * (SuccessPanelComponent) instead.
 */
@Injectable({ providedIn: 'root' })
export class SuccessToastService {
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly app = inject(ApplicationRef);
  private readonly injector = inject(EnvironmentInjector);

  show(text: string, action?: ToastAction): void {
    if (!action) { this.message.success(text); return; }
    const host = createComponent(ToastContentComponent, { environmentInjector: this.injector });
    host.instance.text = text;
    host.instance.label = action.label;
    this.app.attachView(host.hostView);
    host.changeDetectorRef.detectChanges();
    const ref = this.message.success(host.instance.content());
    host.instance.act = () => {
      this.message.remove(ref.messageId);
      if (action.run) action.run();
      if (action.to) void this.router.navigate(action.to, { queryParams: action.queryParams });
    };
    ref.onClose.subscribe(() => { this.app.detachView(host.hostView); host.destroy(); });
  }
}
