import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, HostListener, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { ExpandSearchComponent } from '@shared/components/expand-search/expand-search.component';
import { I18nService } from '@core/i18n/i18n.service';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { API_BASE_URL } from '@core/api/api-config';
import { ErpLabelPipe } from '@shared/pipes/display.pipes';

interface SearchHit { category: string; title: string; subtitle?: string; link: string; }

/** Header search box: one query across core and enabled modules; picking a hit navigates to the record. */
@Component({
  selector: 'erp-global-search',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, ExpandSearchComponent],
  styleUrl: './global-search.component.scss',
  templateUrl: './global-search.component.html',
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
