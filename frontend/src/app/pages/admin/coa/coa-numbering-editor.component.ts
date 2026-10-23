import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { AccountsApi, CoaNumbering } from '../../../core/api/admin.api';
import { asProblem } from '../../../core/http/problem-details';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { HasPermissionDirective } from '../../../shared/has-permission.directive';

/**
 * Finance setting: how account codes are numbered. Free keeps any code; hierarchical builds codes from the parent's
 * (1000 -> 1000-01 -> 1000-01-01) using the segment lengths and separator chosen here. Existing accounts are never renumbered.
 */
@Component({
  selector: 'erp-coa-numbering-editor',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzButtonModule, NzInputNumberModule, NzRadioModule, NzSelectModule, TranslatePipe, HasPermissionDirective],
  styles: [`
    .box { max-width: 640px; margin-bottom: 24px; }
    .row { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; padding: 8px 0; }
    .hint { color: var(--erp-color-text-secondary); }
    .mask { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; unicode-bidi: isolate; direction: ltr; }
  `],
  template: `
    <section class="box" data-testid="coa-numbering">
      <h3>{{ 'coa.numbering.title' | translate: 'Chart of accounts numbering' }}</h3>
      <div class="row">
        <nz-radio-group [ngModel]="mode()" (ngModelChange)="mode.set($event)" [nzDisabled]="!canEdit()" data-testid="coa-numbering-mode">
          <label nz-radio nzValue="free">{{ 'coa.numbering.free' | translate: 'Free (any code)' }}</label>
          <label nz-radio nzValue="hierarchical">{{ 'coa.numbering.hier' | translate: 'Hierarchical (sub-account codes follow the parent)' }}</label>
        </nz-radio-group>
      </div>
      @if (mode() === 'hierarchical') {
        <div class="row">
          <span>{{ 'coa.numbering.segments' | translate: 'Digits per level' }}</span>
          @for (n of segments(); track $index; let i = $index) {
            <nz-input-number [ngModel]="n" (ngModelChange)="setSegment(i, $event)" [nzMin]="1" [nzMax]="6" [nzPrecision]="0" [nzDisabled]="!canEdit()" [attr.data-testid]="'coa-seg-' + i"></nz-input-number>
          }
          <button nz-button nzSize="small" type="button" (click)="addSegment()" [disabled]="!canEdit() || segments().length >= 6" data-testid="coa-seg-add">+</button>
          <button nz-button nzSize="small" type="button" (click)="removeSegment()" [disabled]="!canEdit() || segments().length <= 1" data-testid="coa-seg-remove">−</button>
        </div>
        <div class="row">
          <span>{{ 'coa.numbering.separator' | translate: 'Separator' }}</span>
          <nz-select style="width:120px" [ngModel]="separator()" (ngModelChange)="separator.set($event)" [nzDisabled]="!canEdit()" data-testid="coa-numbering-sep">
            <nz-option nzValue="-" nzLabel="-"></nz-option><nz-option nzValue="." nzLabel="."></nz-option><nz-option nzValue="/" nzLabel="/"></nz-option>
            <nz-option nzValue="" [nzLabel]="'coa.numbering.none' | translate: 'none'"></nz-option>
          </nz-select>
        </div>
        <p class="hint">{{ 'coa.numbering.example' | translate: 'Example' }}: <span class="mask" data-testid="coa-numbering-pattern">{{ pattern() }}</span> ({{ length() }} {{ 'coa.numbering.chars' | translate: 'characters' }})</p>
      }
      <p class="hint">{{ 'coa.numbering.note' | translate: 'Existing accounts keep their codes. The mask applies to new accounts and to codes that are changed.' }}</p>
      <button *erpHasPermission="'core.settings.write'" nz-button nzType="primary" [nzLoading]="saving()" [disabled]="!canSave()" (click)="save()" data-testid="coa-numbering-save">{{ 'common.save' | translate: 'Save' }}</button>
    </section>`,
})
export class CoaNumberingEditorComponent {
  private readonly api = inject(AccountsApi);
  private readonly message = inject(NzMessageService);
  private readonly i18n = inject(I18nService);
  readonly mode = signal<'free' | 'hierarchical'>('free');
  readonly segments = signal<number[]>([4, 2, 2]);
  readonly separator = signal('-');
  readonly saving = signal(false);
  private readonly saved = signal<CoaNumbering | null>(null);

  readonly canEdit = computed(() => true);
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
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? this.i18n.t('coa.numbering.failed', 'Could not save the numbering')); },
    });
  }
}
