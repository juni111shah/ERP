import { switchMap } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { API_BASE_URL } from '../core/api/api-config';
import { AuthService } from '../core/auth/auth.service';
import { asProblem } from '../core/http/problem-details';
import { I18nService } from '../core/i18n/i18n.service';
import { HasPermissionDirective } from '../shared/has-permission.directive';
import { StatePanelComponent } from '../shared/state-panel.component';

interface ModuleView { key: string; name: string; description: string; category: string; version: string; isCore: boolean; requires: string[]; uiRoute: string; state: 'enabled' | 'disabled' | 'unavailable'; notice?: string | null; }

const ASSISTANT_WORDS = {
  en: {
    title: 'Enable the AI assistant?',
    body: 'To answer each question, the assistant sends the information needed for that answer, which can include your business records, to an outside AI provider. People only get answers from what they are allowed to see, and nothing changes until they confirm it.',
    enable: 'Enable', cancel: 'Cancel',
    notices: {
      'assistant-off': 'The AI assistant is switched off for all workspaces at the moment, so your team will not see it even when it is enabled here.',
      'assistant-no-service': 'The AI assistant is not ready to answer yet, so your team will not see it even when it is enabled here. There is nothing you need to do.',
      'assistant-plan': 'Your plan does not include the AI assistant, so your team will not see it even when it is enabled here. Choose a plan that includes it to use it.',
    } as Record<string, string>,
  },
  ar: {
    title: 'هل تريد تفعيل المساعد الذكي؟',
    body: 'للإجابة عن كل سؤال، يرسل المساعد المعلومات اللازمة لتلك الإجابة، وقد تشمل سجلات عملك، إلى مزوّد خارجي للذكاء الاصطناعي. لا يحصل الأشخاص إلا على إجابات مما يحق لهم الاطلاع عليه، ولا يتغير شيء حتى يؤكدوه.',
    enable: 'تفعيل', cancel: 'إلغاء',
    notices: {
      'assistant-off': 'المساعد الذكي متوقف حالياً لجميع مساحات العمل، لذلك لن يظهر لفريقك حتى إذا فُعّل هنا.',
      'assistant-no-service': 'المساعد الذكي غير جاهز للإجابة بعد، لذلك لن يظهر لفريقك حتى إذا فُعّل هنا. لا يلزمك القيام بأي إجراء.',
      'assistant-plan': 'لا تشمل خطتك المساعد الذكي، لذلك لن يظهر لفريقك حتى إذا فُعّل هنا. اختر خطة تشمله لاستخدامه.',
    } as Record<string, string>,
  },
};

/** Workspace modules: core is always on; industry and functional modules can be enabled or disabled (data is kept). */
@Component({
  selector: 'erp-modules',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatePanelComponent, NzCardModule, NzGridModule, NzTagModule, NzButtonModule, NzModalModule, HasPermissionDirective],
  styles: [`
    .module-card {
      border-radius: 14px;
      border: 1px solid var(--erp-color-border-subtle);
      background: var(--erp-color-bg-surface);
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
      transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1);
      display: flex;
      flex-direction: column;
      padding: 20px;
      height: 100%;
    }
    .module-card:hover {
      box-shadow: 0 10px 25px -5px rgba(16, 149, 93, 0.12);
      border-color: var(--erp-color-primary, #10955d);
      transform: translateY(-2px);
    }
    .head { display:flex; justify-content:space-between; align-items:flex-start; gap:8px; margin-bottom:10px }
    .mod-title { font-size:16px; font-weight:700; color: var(--erp-color-text-primary); }
    .mod-desc { color: var(--erp-color-text-muted); font-size:13.5px; line-height:1.5; flex:1; min-height: 44px; margin-bottom:16px }
    .action-row { margin-top: auto; display: flex; align-items: center; justify-content: space-between; }
    .mod-notice { color: var(--erp-color-warning-text, #92400e); background: var(--erp-color-warning-subtle, #fffbeb); border-radius: 8px; padding: 8px 10px; font-size: 13px; line-height: 1.5; margin: 0 0 12px; }
  `],
  template: `
    <h1 class="erp-page-title">Modules</h1>
    <p style="color:var(--erp-color-text-muted); margin-top:-8px; margin-bottom:20px;">Extend your workspace with industry-specific modules and functional packs.</p>
    @if (failed()) {
      <erp-state-panel kind="error" title="Could not load the modules" hint="The server did not return the list." actionLabel="Try again" (action)="load()" />
    }
    <div nz-row [nzGutter]="[16, 16]">
      @for (m of modules(); track m.key) {
        <div nz-col [nzXs]="24" [nzMd]="12" [nzXl]="8" [attr.data-testid]="'module-row-' + m.key">
          <div class="module-card">
            <div class="head">
              <span class="mod-title">{{ m.name }}</span>
              <nz-tag [nzColor]="m.state === 'enabled' ? 'green' : m.state === 'disabled' ? 'default' : 'blue'">
                {{ m.isCore ? 'Always on' : m.state === 'unavailable' ? 'Coming soon' : m.state === 'enabled' ? 'Enabled' : 'Disabled' }}
              </nz-tag>
            </div>
            <p class="mod-desc">{{ m.description }}</p>
            @if (noticeText(m); as notice) { <p class="mod-notice" role="status" [attr.data-testid]="'module-notice-' + m.key">{{ notice }}</p> }
            <div class="action-row">
              <span style="font-size:12px; color:var(--erp-color-text-muted); font-weight:500;">v{{ m.version || '1.0' }}</span>
              @if (!m.isCore && m.state !== 'unavailable') {
                <button *erpHasPermission="'core.settings.write'" nz-button [nzType]="m.state === 'enabled' ? 'default' : 'primary'" [nzLoading]="busy() === m.key"
                        (click)="toggle(m)" [attr.data-testid]="'module-toggle-' + m.key">{{ m.state === 'enabled' ? 'Disable' : 'Enable module' }}</button>
              }
            </div>
          </div>
        </div>
      }
    </div>`,
})
export class ModulesPage {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly auth = inject(AuthService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly i18n = inject(I18nService);
  readonly modules = signal<ModuleView[]>([]);
  readonly busy = signal<string | null>(null);
  readonly failed = signal(false);

  constructor() { this.load(); }

  load(): void {
    this.failed.set(false);
    this.http.get<ModuleView[]>(`${this.base}/modules`).subscribe({ next: (m) => this.modules.set(m), error: () => this.failed.set(true) });
  }

  noticeText(m: ModuleView): string | null {
    return m.notice ? ASSISTANT_WORDS[this.i18n.lang()].notices[m.notice] ?? null : null;
  }

  toggle(m: ModuleView): void {
    if (m.key !== 'assistant' || m.state === 'enabled') { this.change(m); return; }
    // The workspace's data starts leaving for an outside service, so the administrator is told before it does.
    const w = ASSISTANT_WORDS[this.i18n.lang()];
    this.modal.confirm({ nzTitle: w.title, nzContent: w.body, nzOkText: w.enable, nzCancelText: w.cancel, nzOnOk: () => this.change(m) });
  }

  private change(m: ModuleView): void {
    const action = m.state === 'enabled' ? 'disable' : 'enable';
    this.busy.set(m.key);
    this.http.post(`${this.base}/modules/${m.key}/${action}`, {}).subscribe({
      next: () => {
        this.busy.set(null);
        this.message.success(`${m.name} ${action}d`);
        this.load();
        // Permissions travel in the sign-in token, so a newly enabled module needs a fresh one before its calls are allowed.
        this.auth.refresh().pipe(switchMap(() => this.auth.loadMe())).subscribe(); // refresh menu and module guards
      },
      error: (e) => { this.busy.set(null); this.message.error(asProblem(e)?.detail ?? 'Could not change the module'); },
    });
  }
}
