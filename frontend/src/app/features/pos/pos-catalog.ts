import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import { Observable, Subject, catchError, debounceTime, forkJoin, map, of, switchMap, tap } from 'rxjs';
import { PosCatalogItemDto, PosCatalogPage, PosTerminalApi } from '@core/api/pos.api';
import { CartLine } from './pos-cart';

const CATALOG_PAGE = 60;

/** What a scanned or typed code turned out to be. */
export type ScanMatch = { kind: 'item'; item: PosCatalogItemDto } | { kind: 'none' } | { kind: 'many' };

/**
 * The till's product tiles and code lookups. Searches are debounced; every item seen is remembered by barcode and SKU, so a
 * repeat scan goes on the ticket without a round trip. Provided by the terminal page (`providers: [PosCatalog]`).
 */
@Injectable()
export class PosCatalog implements OnDestroy {
  private readonly terminalApi = inject(PosTerminalApi);
  private readonly searches = new Subject<{ registerId: string; term: string }>();
  private readonly known = new Map<string, PosCatalogItemDto>();

  readonly items = signal<PosCatalogItemDto[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly failed = signal(false);

  constructor() {
    this.searches.pipe(
      debounceTime(180),
      switchMap(({ registerId, term }) => this.fetch(registerId, term)),
    ).subscribe();
  }

  ngOnDestroy(): void {
    this.searches.complete();
  }

  /** Shows the tiles for this search (empty for the first page of everything), after a short pause in typing. */
  search(registerId: string, term: string): void {
    this.searches.next({ registerId, term });
  }

  /** An item already seen with this barcode or SKU. */
  remembered(code: string): PosCatalogItemDto | undefined {
    return this.known.get(code.toLowerCase());
  }

  /** An exact barcode or SKU first; failing that, the one product whose name matches is what was meant. */
  resolveScan(registerId: string, code: string): Observable<ScanMatch> {
    return this.page(registerId, { code }).pipe(
      switchMap((exact) => (exact.items.length
        ? of(this.matched(exact.items[0]))
        : this.page(registerId, { search: code }).pipe(map((byName) => this.byName(byName))))),
    );
  }

  /** Today's catalogue entry of each line (null for a typed-in item or one that cannot be found), in the order of the lines. */
  current(registerId: string, lines: readonly CartLine[]): Observable<(PosCatalogItemDto | null)[]> {
    return forkJoin(lines.map((l) => (l.itemId
      ? this.page(registerId, { code: l.itemCode }).pipe(map((r) => r.items.find((i) => i.id === l.itemId) ?? null), catchError(() => of(null)))
      : of(null))));
  }

  private byName(found: PosCatalogPage): ScanMatch {
    if (found.items.length === 1) return this.matched(found.items[0]);
    return found.items.length === 0 ? { kind: 'none' } : { kind: 'many' };
  }

  private matched(item: PosCatalogItemDto): ScanMatch {
    this.remember(item);
    return { kind: 'item', item };
  }

  private page(registerId: string, query: { code?: string; search?: string }): Observable<PosCatalogPage> {
    return this.terminalApi.catalog({ registerId: registerId || undefined, ...query, pageSize: 2 });
  }

  private fetch(registerId: string, term: string): Observable<PosCatalogPage | null> {
    this.loading.set(true);
    this.failed.set(false);
    return this.terminalApi.catalog({ registerId: registerId || undefined, search: term.trim() || undefined, pageSize: CATALOG_PAGE }).pipe(
      tap((res) => {
        res.items.forEach((i) => this.remember(i));
        this.items.set(res.items);
        this.total.set(res.total);
        this.loading.set(false);
      }),
      catchError(() => {
        this.loading.set(false);
        this.failed.set(true);
        return of(null);
      }),
    );
  }

  private remember(item: PosCatalogItemDto): void {
    this.known.set(item.sku.toLowerCase(), item);
    if (item.barcode) this.known.set(item.barcode.toLowerCase(), item);
  }
}
