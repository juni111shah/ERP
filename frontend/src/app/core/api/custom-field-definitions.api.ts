import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CustomFieldDefinition, CustomFieldDefinitionInput, CustomizableEntityType } from '../custom-fields/custom-field.models';
import { API_BASE_URL } from './api-config';

/** Custom-field definitions (/api/v1/custom-fields): the active form of an entity type, and their management (core.customfields.manage). */
@Injectable({ providedIn: 'root' })
export class CustomFieldDefinitionsApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  /** The definitions a form of this entity type shows, in the current legal-entity scope. */
  form(entityType: string): Observable<CustomFieldDefinition[]> {
    return this.http.get<CustomFieldDefinition[]>(`${this.base}/custom-fields/form/${encodeURIComponent(entityType)}`);
  }
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
