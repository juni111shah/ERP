import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { catchError, of, switchMap } from 'rxjs';
import { IndustryOption, PlatformApi, groupIndustries } from '../../core/api/platform.api';
import { BusinessProfile, ChangeIndustryResult, NumberSequenceView, SetupApi } from '../../core/api/setup.api';
import { AuthService } from '../../core/auth/auth.service';
import { doneText } from '../../core/format/done-text';
import { problemMessage } from '../../core/http/problem-details';
import { SessionStore } from '../../core/session/session.store';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { StatePanelComponent } from '../../shared/state-panel.component';

/** The text a number series produces: `{prefix}`, `{yyyy}`, `{yy}` and `{seq:N}` (the next number, padded to N digits) filled in. */
export function sequenceExample(pattern: string, prefix: string, next: number, today = new Date()): string {
  const year = String(today.getFullYear());
  return pattern
    .replace(/\{prefix\}/g, prefix).replace(/\{yyyy\}/g, year).replace(/\{yy\}/g, year.slice(2))
    .replace(/\{seq:(\d+)\}/g, (_, digits: string) => String(next).padStart(Number(digits), '0'));
}

interface SequenceRow extends NumberSequenceView { saved: NumberSequenceView; saving: boolean; error: string | null; }

/** What kind of business this is, which optional core areas it uses, its lists of units and item categories, and its document numbering. */
@Component({
  selector: 'erp-setup',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzAlertModule, NzButtonModule, NzCardModule, NzInputModule, NzModalModule, NzSelectModule, NzSkeletonModule, NzSwitchModule, NzTableModule,
    HasPermissionDirective, StatePanelComponent],
  styles: [`
    .intro { color: var(--erp-color-text-muted); margin: -8px 0 20px; max-width: 720px; }
    nz-card { margin-bottom: 16px; }
    .row { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
    .capability { display: flex; justify-content: space-between; align-items: center; gap: 16px; padding: 10px 0; border-bottom: 1px solid var(--erp-color-border-subtle); }
    .capability:last-child { border-bottom: 0; }
    .capability p { margin: 0; color: var(--erp-color-text-muted); font-size: 13px; }
    .lists { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; }
    .lists label { display: block; font-weight: 500; margin-bottom: 4px; }
    .muted { color: var(--erp-color-text-muted); } .example { font-family: var(--erp-font-mono, monospace); }
    ul.plain { margin: 4px 0 0; padding-inline-start: 20px; }
  `],
  template: `
    <h1 class="erp-page-title">Business setup</h1>
    <p class="intro">Tell the system what kind of business this is, which parts of it you use, and how your documents are numbered.</p>

    @if (failed()) {
      <erp-state-panel kind="error" title="Could not load the setup" hint="The server did not return your business profile." actionLabel="Try again" (action)="load()" />
    } @else if (profile(); as p) {
      <nz-card nzTitle="Business type" data-testid="setup-industry">
        <p>Your business is set up as <b data-testid="setup-current-industry">{{ p.industryLabel || p.industryCode }}</b>@if (p.industryGroup) { <span class="muted"> · {{ p.industryGroup }}</span> }.</p>
        @if (p.missingModules.length) {
          <nz-alert nzType="info" nzShowIcon nzMessage="Some modules for this business type are off" [nzDescription]="'Not enabled yet: ' + p.missingModules.join(', ') + '.'" style="margin-bottom:12px"></nz-alert>
        }
        <div class="row" *erpHasPermission="'core.modules.manage'">
          <nz-select style="min-width:320px" nzShowSearch nzPlaceHolder="Choose a business type" [ngModel]="industry()" (ngModelChange)="industry.set($event)" aria-label="Business type" data-testid="setup-industry-select">
            @for (g of groups(); track g.group) {
              <nz-option-group [nzLabel]="g.group">
                @for (i of g.items; track i.key) { <nz-option [nzValue]="i.key" [nzLabel]="i.label"></nz-option> }
              </nz-option-group>
            }
          </nz-select>
          <button nz-button nzType="primary" type="button" [disabled]="!industry() || industry() === p.industryCode" (click)="confirming.set(true)" data-testid="setup-industry-change">Change business type</button>
        </div>
        @if (result(); as r) {
          <nz-alert class="result" style="margin-top:16px" nzType="success" nzShowIcon nzMessage="Business type changed" data-testid="setup-industry-result" [nzDescription]="resultBody"></nz-alert>
          <ng-template #resultBody>
            <ul class="plain">
              <li>Modules turned on: {{ list(r.modulesEnabled) }}</li>
              <li>Roles added: {{ list(r.rolesAdded) }}</li>
              <li>Accounts added: {{ list(r.accountsAdded) }}</li>
              @if (r.modulesNotAllowed.length) { <li>Not allowed by your plan: {{ list(r.modulesNotAllowed) }}</li> }
            </ul>
          </ng-template>
        }
      </nz-card>

      <nz-modal [nzVisible]="confirming()" nzTitle="Change business type?" nzOkText="Change business type" nzCancelText="Cancel" [nzOkLoading]="changing()" (nzOnOk)="applyIndustry()" (nzOnCancel)="confirming.set(false)">
        <ng-container *nzModalContent>
          <p data-testid="setup-industry-confirm">Changing the business type only adds modules, roles and accounts that this type needs. It never removes or turns off anything you already have, and your data is kept.</p>
        </ng-container>
      </nz-modal>

      <nz-card nzTitle="Parts of the system you use" data-testid="setup-capabilities">
        @for (c of p.capabilities; track c.key) {
          <div class="capability">
            <div><b>{{ c.label }}</b><p>{{ c.description }}</p></div>
            <nz-switch [ngModel]="isOn(c.key)" (ngModelChange)="toggle(c.key, $event)" [nzDisabled]="!canWrite()" [attr.aria-label]="c.label" [attr.data-testid]="'setup-capability-' + c.key"></nz-switch>
          </div>
        }
        <div style="margin-top:12px" *erpHasPermission="'core.settings.write'">
          <button nz-button nzType="primary" type="button" [disabled]="!capabilitiesDirty()" [nzLoading]="saving() === 'capabilities'" (click)="saveCapabilities()" data-testid="setup-capabilities-save">Save</button>
        </div>
      </nz-card>

      <nz-card nzTitle="Units and item categories" data-testid="setup-lists">
        <div class="lists">
          <div>
            <label for="setup-units">Units of measure</label>
            <nz-select nzId="setup-units" nzMode="tags" nzPlaceHolder="Type a unit and press Enter" [ngModel]="units()" (ngModelChange)="units.set($event)" [nzDisabled]="!canWrite()" data-testid="setup-units"></nz-select>
          </div>
          <div>
            <label for="setup-categories">Item categories</label>
            <nz-select nzId="setup-categories" nzMode="tags" nzPlaceHolder="Type a category and press Enter" [ngModel]="categories()" (ngModelChange)="categories.set($event)" [nzDisabled]="!canWrite()" data-testid="setup-categories"></nz-select>
          </div>
        </div>
        <div style="margin-top:12px" *erpHasPermission="'core.settings.write'">
          <button nz-button nzType="primary" type="button" [disabled]="!listsDirty()" [nzLoading]="saving() === 'lists'" (click)="saveLists()" data-testid="setup-lists-save">Save lists</button>
        </div>
      </nz-card>

      <nz-card nzTitle="Document numbers" data-testid="setup-sequences">
        <p class="muted">The pattern may use <code>{{ '{prefix}' }}</code>, <code>{{ '{yyyy}' }}</code>, <code>{{ '{yy}' }}</code> and must contain <code>{{ '{seq:6}' }}</code> (the number of digits can change).</p>
        <nz-table [nzData]="sequences()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
          <thead><tr><th>Document</th><th>Prefix</th><th>Pattern</th><th>Restart every year</th><th>Next</th><th>Example</th><th></th></tr></thead>
          <tbody>
            @for (s of sequences(); track s.documentType) {
              <tr [attr.data-testid]="'setup-seq-' + s.documentType">
                <td>{{ s.documentType }}</td>
                <td><input nz-input style="width:100px" [ngModel]="s.prefix" (ngModelChange)="edit(s.documentType, { prefix: $event })" [disabled]="!canWrite()" [attr.aria-label]="'Prefix of ' + s.documentType" [attr.data-testid]="'setup-seq-prefix-' + s.documentType" /></td>
                <td><input nz-input style="width:200px" [ngModel]="s.pattern" (ngModelChange)="edit(s.documentType, { pattern: $event })" [disabled]="!canWrite()" [attr.aria-label]="'Pattern of ' + s.documentType" [attr.data-testid]="'setup-seq-pattern-' + s.documentType" /></td>
                <td><nz-switch [ngModel]="s.resetYearly" (ngModelChange)="edit(s.documentType, { resetYearly: $event })" [nzDisabled]="!canWrite()" [attr.aria-label]="'Restart every year: ' + s.documentType"></nz-switch></td>
                <td>{{ s.nextValue }}</td>
                <td class="example" [attr.data-testid]="'setup-seq-example-' + s.documentType">{{ exampleOf(s) }}</td>
                <td>
                  <button *erpHasPermission="'core.settings.write'" nz-button nzSize="small" type="button" [disabled]="!sequenceDirty(s) || s.saving" (click)="saveSequence(s)" [attr.data-testid]="'setup-seq-save-' + s.documentType">Save</button>
                  @if (s.error) { <div style="color:var(--erp-color-error-text);font-size:12px" role="alert">{{ s.error }}</div> }
                </td>
              </tr>
            }
          </tbody>
        </nz-table>
      </nz-card>
    } @else {
      <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 8 }" data-testid="setup-loading"></nz-skeleton>
    }`,
})
export class SetupPage {
  private readonly api = inject(SetupApi);
  private readonly auth = inject(AuthService);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);

  readonly profile = signal<BusinessProfile | null>(null);
  readonly failed = signal(false);
  readonly industries = toSignal(inject(PlatformApi).industries().pipe(catchError(() => of([] as IndustryOption[]))), { initialValue: [] as IndustryOption[] });
  readonly groups = computed(() => groupIndustries(this.industries()));
  readonly industry = signal<string | null>(null);
  readonly confirming = signal(false);
  readonly changing = signal(false);
  readonly result = signal<ChangeIndustryResult | null>(null);
  readonly disabled = signal<string[]>([]);
  readonly units = signal<string[]>([]);
  readonly categories = signal<string[]>([]);
  readonly sequences = signal<SequenceRow[]>([]);
  readonly saving = signal<'capabilities' | 'lists' | null>(null);

  readonly canWrite = computed(() => this.session.hasPermission('core.settings.write'));
  readonly capabilitiesDirty = computed(() => {
    const was = new Set(this.profile()?.capabilities.filter((c) => !c.enabled).map((c) => c.key));
    const now = new Set(this.disabled());
    return was.size !== now.size || [...now].some((k) => !was.has(k));
  });
  readonly listsDirty = computed(() => {
    const p = this.profile();
    return !!p && (JSON.stringify(this.units()) !== JSON.stringify(p.units) || JSON.stringify(this.categories()) !== JSON.stringify(p.itemCategories));
  });

  constructor() { this.load(); }

  load(): void {
    this.failed.set(false);
    this.api.profile().subscribe({ next: (p) => this.apply(p), error: () => this.failed.set(true) });
    this.api.sequences().pipe(catchError(() => of([] as NumberSequenceView[]))).subscribe((rows) => this.sequences.set(rows.map((s) => ({ ...s, saved: s, saving: false, error: null }))));
  }

  private apply(p: BusinessProfile): void {
    this.profile.set(p);
    this.industry.set(p.industryCode);
    this.disabled.set(p.capabilities.filter((c) => !c.enabled).map((c) => c.key));
    this.units.set([...p.units]);
    this.categories.set([...p.itemCategories]);
  }

  list(values: string[]): string { return values.length ? values.join(', ') : 'none'; }
  isOn(key: string): boolean { return !this.disabled().includes(key); }
  toggle(key: string, on: boolean): void { this.disabled.update((d) => (on ? d.filter((k) => k !== key) : d.includes(key) ? d : [...d, key])); }

  /** The session's menu and the modules this person can reach come from the sign-in token, so a change needs a fresh one. */
  private refreshSession(): void { this.auth.refresh().pipe(switchMap(() => this.auth.loadMe())).subscribe({ error: () => undefined }); }

  applyIndustry(): void {
    const key = this.industry();
    if (!key) return;
    this.changing.set(true);
    this.api.changeIndustry(key).subscribe({
      next: (r) => {
        this.changing.set(false);
        this.confirming.set(false);
        this.result.set(r);
        this.message.success(doneText('updated', 'Business type'));
        this.api.profile().subscribe((p) => this.apply(p));
        this.refreshSession();
      },
      error: (e) => { this.changing.set(false); this.confirming.set(false); this.message.error(problemMessage(e, 'Could not change the business type.', 'industry')); },
    });
  }

  saveCapabilities(): void {
    this.saving.set('capabilities');
    this.api.setCapabilities(this.disabled()).subscribe({
      next: (p) => { this.saving.set(null); this.apply(p); this.message.success(doneText('saved', 'Setup')); this.refreshSession(); },
      error: (e) => { this.saving.set(null); this.message.error(problemMessage(e, 'Could not save.')); },
    });
  }

  saveLists(): void {
    this.saving.set('lists');
    this.api.setLists({ units: this.units(), itemCategories: this.categories() }).subscribe({
      next: (p) => { this.saving.set(null); this.apply(p); this.message.success(doneText('saved', 'Lists')); },
      error: (e) => { this.saving.set(null); this.message.error(problemMessage(e, 'Could not save the lists.')); },
    });
  }

  edit(documentType: string, change: Partial<Pick<NumberSequenceView, 'prefix' | 'pattern' | 'resetYearly'>>): void {
    this.sequences.update((rows) => rows.map((r) => (r.documentType === documentType ? { ...r, ...change, error: null } : r)));
  }
  exampleOf(s: SequenceRow): string { return sequenceExample(s.pattern, s.prefix, s.nextValue); }
  sequenceDirty(s: SequenceRow): boolean { return s.prefix !== s.saved.prefix || s.pattern !== s.saved.pattern || s.resetYearly !== s.saved.resetYearly; }

  saveSequence(s: SequenceRow): void {
    this.sequences.update((rows) => rows.map((r) => (r.documentType === s.documentType ? { ...r, saving: true, error: null } : r)));
    this.api.saveSequence(s.documentType, { prefix: s.prefix, pattern: s.pattern, resetYearly: s.resetYearly }).subscribe({
      next: (saved) => {
        this.sequences.update((rows) => rows.map((r) => (r.documentType === s.documentType ? { ...saved, saved, saving: false, error: null } : r)));
        this.message.success(doneText('saved', 'Numbering', s.documentType));
      },
      error: (e) => this.sequences.update((rows) => rows.map((r) => (r.documentType === s.documentType ? { ...r, saving: false, error: problemMessage(e, 'Could not save this numbering.', 'pattern') } : r))),
    });
  }
}
