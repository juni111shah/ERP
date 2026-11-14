import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { AccountsApi, CoaNumbering } from '@core/api/admin.api';
import { problemMessage } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';

/**
 * Finance setting: how account codes are numbered. Free keeps any code; hierarchical builds codes from the parent's
 * (1000 -> 1000-01 -> 1000-01-01) using the segment lengths and separator chosen here. Existing accounts are never renumbered.
 */
@Component({
  selector: 'erp-coa-numbering-editor',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzButtonModule, NzInputNumberModule, NzRadioModule, NzSelectModule, TranslatePipe, HasPermissionDirective],
  styleUrl: './coa-numbering-editor.component.scss',
  templateUrl: './coa-numbering-editor.component.html',
})
export class CoaNumberingEditorComponent {
  private readonly api = inject(AccountsApi);
  private readonly message = inject(NzMessageService);
  private readonly i18n = inject(I18nService);
  private readonly session = inject(SessionStore);
  readonly mode = signal<'free' | 'hierarchical'>('free');
  readonly segments = signal<number[]>([4, 2, 2]);
  readonly separator = signal('-');
  readonly saving = signal(false);
  private readonly saved = signal<CoaNumbering | null>(null);

  /** The template's single switch for a read-only editor: saving needs core.settings.write (PUT finance/coa-numbering). */
  readonly canEdit = computed(() => this.session.hasPermission('core.settings.write'));
  readonly length = computed(() => this.segments().reduce((a, b) => a + b, 0) + (this.segments().length - 1) * this.separator().length);
  readonly pattern = computed(() => this.segments().map((s) => '0'.repeat(s)).join(this.separator()));
  readonly canSave = computed(() => {
    const s = this.saved();
    const changed = !s || s.mode !== this.mode() || s.separator !== this.separator() || s.segments.join() !== this.segments().join();
    return changed && (this.mode() === 'free' || this.length() <= 20);
  });

  constructor() { this.load(); }

  setSegment(i: number, value: number | null): void { this.segments.update((l) => l.map((n, idx) => (idx === i ? Math.min(6, Math.max(1, value ?? 1)) : n))); }
  addSegment(): void { this.segments.update((l) => [...l, 2]); }
  removeSegment(): void { this.segments.update((l) => l.slice(0, -1)); }

  private load(): void {
    this.api.numbering().subscribe((n) => { this.saved.set(n); this.mode.set(n.mode); this.segments.set([...n.segments]); this.separator.set(n.separator); });
  }

  save(): void {
    this.saving.set(true);
    this.api.setNumbering({ mode: this.mode(), segments: this.segments(), separator: this.separator() }).subscribe({
      next: (n) => { this.saving.set(false); this.saved.set(n); this.message.success(this.i18n.t('coa.numbering.saved', 'Numbering saved')); },
      error: (e) => { this.saving.set(false); this.message.error(problemMessage(e, this.i18n.t('coa.numbering.failed', 'Could not save the numbering'))); },
    });
  }
}
