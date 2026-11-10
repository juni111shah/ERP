import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import { Subject, catchError, debounceTime, of, switchMap } from 'rxjs';
import { PosCustomerDto, PosTerminalApi } from '@core/api/pos.api';

/** The till's customer picker: a debounced search whose failure shows as no match. Provided by the terminal page. */
@Injectable()
export class PosCustomerSearch implements OnDestroy {
  private readonly terminalApi = inject(PosTerminalApi);
  private readonly terms = new Subject<string>();

  readonly results = signal<PosCustomerDto[]>([]);
  readonly loading = signal(false);

  constructor() {
    this.terms.pipe(
      debounceTime(220),
      switchMap((term) => {
        this.loading.set(true);
        return this.terminalApi.customers(term).pipe(catchError(() => of([] as PosCustomerDto[])));
      }),
    ).subscribe((rows) => {
      this.results.set(rows);
      this.loading.set(false);
    });
  }

  ngOnDestroy(): void {
    this.terms.complete();
  }

  /** Looks up customers by name, phone or code after a short pause in typing; an empty term lists the first ones. */
  search(term: string): void {
    this.terms.next(term);
  }
}
