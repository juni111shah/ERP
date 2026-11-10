import { Injectable, inject } from '@angular/core';
import { Observable, map, shareReplay } from 'rxjs';
import { CustomFieldDefinitionsApi } from '../api/custom-field-definitions.api';
import { SessionStore } from '../session/session.store';
import { FormSectionMeta, toSections } from './custom-field.models';

export { CustomFieldDefinitionsApi } from '../api/custom-field-definitions.api';

/** Form metadata per entity type (cached per legal-entity scope). Call invalidate() after editing definitions. */
@Injectable({ providedIn: 'root' })
export class CustomFieldMetadataService {
  private readonly definitionsApi = inject(CustomFieldDefinitionsApi);
  private readonly session = inject(SessionStore);
  private readonly cache = new Map<string, Observable<FormSectionMeta[]>>();

  /** The form sections of an entity type, loaded once per type and legal-entity scope. */
  sections(entityType: string): Observable<FormSectionMeta[]> {
    const key = cacheKey(entityType, this.session.currentEntityId());
    let cached = this.cache.get(key);
    if (!cached) {
      cached = this.definitionsApi.form(entityType).pipe(map(toSections), shareReplay({ bufferSize: 1, refCount: false }));
      this.cache.set(key, cached);
    }
    return cached;
  }

  /** Forgets the cached forms of one entity type (every scope), or of all of them. */
  invalidate(entityType?: string): void {
    if (!entityType) return this.cache.clear();
    for (const k of [...this.cache.keys()]) if (k.startsWith(`${entityType}|`)) this.cache.delete(k);
  }
}

/** "core.party|<entity id>", or "core.party|*" for all legal entities. */
function cacheKey(entityType: string, entityId: string | null): string {
  return `${entityType}|${entityId ?? '*'}`;
}
