import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, HostListener, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { ExpandSearchComponent } from '../shared/expand-search.component';
import { I18nService } from '../core/i18n/i18n.service';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { API_BASE_URL } from '../core/api/api-config';

interface SearchHit { category: string; title: string; subtitle?: string; link: string; }

/** Header search box: one query across core and enabled modules; picking a hit navigates to the record. */
@Component({
  selector: 'erp-global-search',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ExpandSearchComponent],
  styles: [`
    :host { position: relative; display: inline-flex; align-items: center; }
    .panel { position: absolute; top: 44px; inset-inline-start: 0; width: 360px; max-width: 80vw; background: var(--erp-color-bg-surface, #ffffff); border: 1px solid var(--erp-color-border-subtle, #e2e8f0); border-radius: 10px; box-shadow: 0 10px 30px rgba(0,0,0,.15); max-height: 420px; overflow: auto; z-index: 1000; line-height: 1.4; padding: 4px; }
    .cat { padding: 8px 12px 4px; font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: .05em; color: var(--erp-brand-solid-light, #10955d); }
    .hit { padding: 8px 12px; cursor: pointer; border-radius: 6px; transition: background 0.15s ease; }
    .hit:hover { background: rgba(16, 149, 93, 0.08); }
    .hit-title { font-weight: 600; color: var(--erp-color-text-primary, #0f172a); }
    .sub { font-size: 12px; color: var(--erp-color-text-tertiary, #64748b); margin-top: 2px; }
    .empty { padding: 16px; text-align: center; color: var(--erp-color-text-tertiary, #64748b); font-size: 13px; }
    .search-kbd-hint {
      display: inline-flex;
      align-items: center;
      padding: 1px 5px;
      font-size: 10.5px;
      font-family: inherit;
      color: #94a3b8;
      background: #f1f5f9;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      margin-inline-start: 4px;
      pointer-events: none;
      line-height: 1.3;
    }
    :host-context([data-theme='dark']) .search-kbd-hint {
      background: #1e293b;
      border-color: #334155;
      color: #64748b;
    }
    @media (max-width: 768px) {
      .search-kbd-hint { display: none; }
    }
  `],
  template: `
    <erp-expand-search #searchComp [value]="term()" (valueChange)="type($event)" width="320px" testId="global-search" [label]="t('ui.search.records', 'Search records')"
                       (focusin)="open.set(true)" (focusout)="close()" />
    @if (!open() && !term()) {
      <kbd class="search-kbd-hint" aria-hidden="true">Ctrl K</kbd>
    }
    @if (open() && term().trim().length >= 2) {
      <div class="panel" data-testid="search-results">
        @for (h of hits(); track $index; let i = $index) {
          @if (i === 0 || hits()[i - 1].category !== h.category) { <div class="cat">{{ h.category }}</div> }
          <div class="hit" (mousedown)="go(h)" [attr.data-testid]="'search-hit'">
            <div class="hit-title">{{ h.title }}</div>
            @if (h.subtitle) { <div class="sub">{{ h.subtitle }}</div> }
          </div>
        } @empty { <div class="empty">{{ t('ui.search.noMatches', 'No matches') }}</div> }
      </div>
    }`,
})
export class GlobalSearchComponent {
  private readonly i18n = inject(I18nService);
  t(key: string, fallback: string): string { return this.i18n.t(key, fallback); }
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly router = inject(Router);
  readonly searchComp = viewChild<ExpandSearchComponent>('searchComp');
  readonly term = signal('');
  readonly open = signal(false);
  readonly hits = signal<SearchHit[]>([]);
  private readonly typed = new Subject<string>();

  @HostListener('window:keydown', ['$event'])
  handleGlobalHotkey(e: KeyboardEvent): void {
    if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
      e.preventDefault();
      this.searchComp()?.focus();
    }
  }

  constructor() {
    this.typed.pipe(
      debounceTime(250), distinctUntilChanged(),
      switchMap((q) => q.trim().length < 2 ? of({ hits: [] }) : this.http.get<{ hits: SearchHit[] }>(`${this.base}/search`, { params: { q: q.trim() } }).pipe(catchError(() => of({ hits: [] })))),
    ).subscribe((r) => this.hits.set(r.hits));
  }

  type(value: string): void { this.term.set(value); this.open.set(true); this.typed.next(value); }
  close(): void { setTimeout(() => this.open.set(false), 150); }
  go(hit: SearchHit): void { this.open.set(false); this.term.set(''); this.hits.set([]); void this.router.navigateByUrl(hit.link); }
}
