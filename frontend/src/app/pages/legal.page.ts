import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { LegalDocuments, PlatformApi } from '../core/api/platform.api';
import { BrandLogoComponent } from '../shared/brand-logo.component';
import { ErpDatePipe } from '../shared/display.pipes';
import { StatePanelComponent } from '../shared/state-panel.component';

export interface LegalBlock { heading: boolean; text: string; }

/** The founder's plain text, with "## " marking a heading. It only ever becomes text on the page, never markup. */
export function legalBlocks(text: string): LegalBlock[] {
  return text.split(/\r?\n/).map((l) => l.trim()).filter((l) => l.length > 0)
    .map((l) => (l.startsWith('## ') ? { heading: true, text: l.slice(3).trim() } : { heading: false, text: l }));
}

/** The public Terms of Service and Privacy Policy. Route data says which. */
@Component({
  selector: 'erp-legal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzSpinModule, BrandLogoComponent, ErpDatePipe, StatePanelComponent],
  styles: [`
    :host { display: block; min-height: 100vh; background: var(--erp-color-bg-canvas); }
    .top { display: flex; align-items: center; justify-content: space-between; gap: 12px; max-width: 820px; margin: 0 auto; padding: 18px 24px; }
    .top a.logo { display: inline-flex; min-width: 0; }
    .top erp-brand-logo { width: auto; }
    .top a.back { font-weight: 600; white-space: nowrap; }
    main { max-width: 820px; margin: 0 auto; padding: 8px 24px 56px; }
    article { background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); border-radius: 16px; padding: 36px 40px; }
    h1 { font-size: 28px; font-weight: 800; letter-spacing: -.02em; margin: 0 0 4px; color: var(--erp-color-text-primary); }
    .updated { margin: 0 0 24px; color: var(--erp-color-text-muted); font-size: 13.5px; }
    h2 { font-size: 17px; font-weight: 700; margin: 26px 0 8px; color: var(--erp-color-text-primary); }
    p { margin: 0 0 12px; color: var(--erp-color-text-secondary); line-height: 1.65; overflow-wrap: anywhere; }
    nav { display: flex; flex-wrap: wrap; gap: 6px 18px; margin-top: 20px; font-size: 14px; }
    @media (max-width: 767px) {
      .top { padding: 14px 16px; }
      main { padding: 4px 16px 40px; }
      article { padding: 22px 18px; border-radius: 12px; }
      h1 { font-size: 23px; }
    }
    @media print { .top, nav { display: none; } article { border: 0; padding: 0; } :host { background: none; } }
  `],
  template: `
    <header class="top">
      <a class="logo" routerLink="/" aria-label="Home"><erp-brand-logo size="lg"></erp-brand-logo></a>
      <a class="back" routerLink="/pricing">Pricing</a>
    </header>
    <main>
      @if (loading()) {
        <div style="padding:64px;text-align:center"><nz-spin nzSimple></nz-spin></div>
      } @else if (failed()) {
        <erp-state-panel kind="error" title="This page could not load" hint="Please try again in a moment." actionLabel="Try again" (action)="load()" />
      } @else if (docs(); as d) {
        <article [attr.data-testid]="'legal-' + kind">
          <h1>{{ title }}</h1>
          @if (d.updatedAt) { <p class="updated">Last updated {{ d.updatedAt | erpDate }}</p> }
          @if (kind === 'terms' && d.termsUrl) {
            <p>Our terms of service are published at <a [href]="d.termsUrl" rel="noopener" data-testid="legal-external">{{ d.termsUrl }}</a>.</p>
          } @else {
            @for (b of blocks(); track $index) {
              @if (b.heading) { <h2>{{ b.text }}</h2> } @else { <p>{{ b.text }}</p> }
            }
          }
          <nav>
            @if (kind === 'terms') { <a routerLink="/privacy">Privacy Policy</a> } @else { <a routerLink="/terms">Terms of Service</a> }
            <a routerLink="/register">Create a workspace</a>
          </nav>
        </article>
      }
    </main>`,
})
export class LegalPage {
  private readonly api = inject(PlatformApi);
  readonly kind: 'terms' | 'privacy' = inject(ActivatedRoute).snapshot.data['kind'] === 'privacy' ? 'privacy' : 'terms';
  readonly title = this.kind === 'privacy' ? 'Privacy Policy' : 'Terms of Service';
  readonly docs = signal<LegalDocuments | null>(null);
  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly blocks = computed(() => {
    const d = this.docs();
    return d ? legalBlocks(this.kind === 'privacy' ? d.privacy : d.terms) : [];
  });

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api.legal().subscribe({
      next: (d) => { this.docs.set(d); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }
}
