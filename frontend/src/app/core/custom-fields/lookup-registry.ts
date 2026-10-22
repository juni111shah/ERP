import { EnvironmentProviders, Injectable, inject, provideEnvironmentInitializer } from '@angular/core';
import { Observable, of } from 'rxjs';

export interface LookupOption {
  value: string;
  label: string;
}

/** Search function for a lookup target, e.g. (term) => customersApi.list({...}).pipe(map(...)). */
export type LookupSource = (term: string) => Observable<LookupOption[]>;

/** Resolves options for `lookup` custom fields by target entity type ("core.customer", "realestate.unit-type"). */
@Injectable({ providedIn: 'root' })
export class LookupRegistry {
  private readonly sources = new Map<string, LookupSource>();

  register(entityType: string, source: LookupSource): void {
    this.sources.set(entityType, source);
  }

  search(entityType: string | null | undefined, term: string): Observable<LookupOption[]> {
    const source = entityType ? this.sources.get(entityType) : undefined;
    return source ? source(term) : of([]);
  }
}

/** Register in a feature's route providers: providers: [provideLookupSource('realestate.unit-type', () => ...)] */
export function provideLookupSource(entityType: string, factory: () => LookupSource): EnvironmentProviders {
  // Angular 19: provideEnvironmentInitializer replaces the deprecated ENVIRONMENT_INITIALIZER token.
  return provideEnvironmentInitializer(() => inject(LookupRegistry).register(entityType, factory()));
}
