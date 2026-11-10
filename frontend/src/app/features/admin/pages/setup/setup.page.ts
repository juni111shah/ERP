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
import { IndustryOption, PlatformApi, groupIndustries } from '@core/api/platform.api';
import { BusinessProfile, ChangeIndustryResult, NumberSequenceView, SetupApi } from '@core/api/setup.api';
import { AuthService } from '@core/auth/auth.service';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';

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
  styleUrl: './setup.page.scss',
  templateUrl: './setup.page.html',
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
