import { Injectable, TemplateRef } from '@angular/core';
import { NzMessageDataOptions, NzMessageRef, NzMessageService } from 'ng-zorro-antd/message';

const SAME_MESSAGE_MS = 1500;

/**
 * The same warning or error twice in a moment is one message. A page's own error handler and the subscription gate
 * can both report one failed call, and they use the server's words, so they would otherwise stack two identical toasts.
 */
@Injectable()
export class OneToastMessageService extends NzMessageService {
  private lastText: string | null = null;
  private lastAt = 0;
  private lastRef: NzMessageRef | null = null;

  private show(content: string | TemplateRef<void>, create: () => NzMessageRef): NzMessageRef {
    const now = Date.now();
    if (typeof content === 'string' && content === this.lastText && now - this.lastAt < SAME_MESSAGE_MS && this.lastRef) return this.lastRef;
    this.lastRef = create();
    this.lastText = typeof content === 'string' ? content : null;
    this.lastAt = now;
    return this.lastRef;
  }

  override error(content: string | TemplateRef<void>, options?: NzMessageDataOptions): NzMessageRef {
    return this.show(content, () => super.error(content, options));
  }

  override warning(content: string | TemplateRef<void>, options?: NzMessageDataOptions): NzMessageRef {
    return this.show(content, () => super.warning(content, options));
  }
}
