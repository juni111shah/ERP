import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { WorkspaceAssistantApi } from '@core/api/workspace-assistant.api';
import { SessionStore } from '@core/session/session.store';

const MODULE_KEY = 'assistant';
const PERMISSION = 'assistant.chats.use';
/** A till needs the whole screen, and nobody should chat mid-sale. */
const HIDDEN_ON = ['/pos/terminal'];

/** Whether the assistant is offered here, and whether its panel is open. The top bar's button and the panel share this. */
@Injectable({ providedIn: 'root' })
export class WorkspaceAssistantState {
  private readonly api = inject(WorkspaceAssistantApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly url = toSignal(this.router.events.pipe(filter((e) => e instanceof NavigationEnd), map(() => this.router.url), startWith(this.router.url)), { initialValue: this.router.url });

  private readonly serviceReady = signal(false);
  readonly open = signal(false);
  private readonly allowed = computed(() => this.session.isModuleEnabled(MODULE_KEY) && this.session.hasPermission(PERMISSION));
  readonly available = computed(() => this.allowed() && this.serviceReady() && !HIDDEN_ON.some((p) => this.url().startsWith(p)));

  constructor() {
    // Asked only of people who could use it, so nobody else causes a "module disabled" answer. It runs again after a workspace switch.
    effect(() => {
      const me = this.session.me();
      const allowed = this.allowed();
      untracked(() => {
        this.serviceReady.set(false);
        if (!me || !allowed) { this.open.set(false); return; }
        this.api.status().subscribe({ next: (s) => this.serviceReady.set(s.available), error: () => this.serviceReady.set(false) });
      });
    });
  }

  toggle(): void { this.open.update((o) => !o); }
}
