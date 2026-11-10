import { ChangeDetectionStrategy, Component, HostListener, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { GlobalSearchApi, SearchHit } from '@core/api/search.api';
import { I18nService } from '@core/i18n/i18n.service';
import { ExpandSearchComponent } from '@shared/components/expand-search/expand-search.component';
import { ErpLabelPipe } from '@shared/pipes/display.pipes';

/** Fewer letters than this find nothing worth showing, so nothing is asked. */
const MIN_TERM = 2;

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
  private readonly searchApi = inject(GlobalSearchApi);
  private readonly router = inject(Router);
  readonly searchComp = viewChild<ExpandSearchComponent>('searchComp');
  readonly term = signal('');
  readonly open = signal(false);
  readonly hits = signal<SearchHit[]>([]);
  private readonly typed = new Subject<string>();

  constructor() {
    this.typed.pipe(
      debounceTime(250), distinctUntilChanged(),
      switchMap((q) => q.trim().length < MIN_TERM ? of({ hits: [] }) : this.searchApi.search(q.trim()).pipe(catchError(() => of({ hits: [] })))),
    ).subscribe((r) => this.hits.set(r.hits));
  }

  /** Ctrl+K (Cmd+K) anywhere opens the search. */
  @HostListener('window:keydown', ['$event'])
  handleGlobalHotkey(e: KeyboardEvent): void {
    if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
      e.preventDefault();
      this.searchComp()?.focus();
    }
  }

  t(key: string, fallback: string): string { return this.i18n.t(key, fallback); }
  type(value: string): void { this.term.set(value); this.open.set(true); this.typed.next(value); }
  /** Waits a moment, so a click on a hit lands before the panel goes. */
  close(): void { setTimeout(() => this.open.set(false), 150); }
  go(hit: SearchHit): void { this.open.set(false); this.term.set(''); this.hits.set([]); void this.router.navigateByUrl(hit.link); }
}
