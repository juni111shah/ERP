import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, shareReplay } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';
import { SessionStore } from '../session/session.store';
import { CustomFieldDefinition, CustomFieldDefinitionInput, CustomizableEntityType, FormSectionMeta, toSections } from './custom-field.models';

/** Form metadata per entity type (cached per legal-entity scope). Call invalidate() after editing definitions. */
@Injectable({ providedIn: 'root' })
export class CustomFieldMetadataService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly session = inject(SessionStore);
  private readonly cache = new Map<string, Observable<FormSectionMeta[]>>();

  sections(entityType: string): Observable<FormSectionMeta[]> {
    const key = `${entityType}|${this.session.currentEntityId() ?? '*'}`;
    let cached = this.cache.get(key);
    if (!cached) {
      cached = this.http.get<CustomFieldDefinition[]>(`${this.base}/custom-fields/form/${encodeURIComponent(entityType)}`)
        .pipe(map(toSections), shareReplay({ bufferSize: 1, refCount: false }));
      this.cache.set(key, cached);
    }
    return cached;
  }

  invalidate(entityType?: string): void {
    if (!entityType) return this.cache.clear();
    for (const k of [...this.cache.keys()]) if (k.startsWith(`${entityType}|`)) this.cache.delete(k);
  }
}

/** Definition management (core.customfields.manage). */
@Injectable({ providedIn: 'root' })
export class CustomFieldDefinitionsApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  entityTypes(): Observable<CustomizableEntityType[]> {
    return this.http.get<CustomizableEntityType[]>(`${this.base}/custom-fields/entity-types`);
  }
  list(entityType: string): Observable<CustomFieldDefinition[]> {
    return this.http.get<CustomFieldDefinition[]>(`${this.base}/custom-fields`, { params: { entityType, includeInactive: true } });
  }
  create(body: CustomFieldDefinitionInput): Observable<CustomFieldDefinition> {
    return this.http.post<CustomFieldDefinition>(`${this.base}/custom-fields`, body);
  }
  update(id: string, body: CustomFieldDefinitionInput): Observable<CustomFieldDefinition> {
    return this.http.put<CustomFieldDefinition>(`${this.base}/custom-fields/${id}`, body);
  }
  deactivate(id: string): Observable<CustomFieldDefinition> {
    return this.http.delete<CustomFieldDefinition>(`${this.base}/custom-fields/${id}`);
  }
}
