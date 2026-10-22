import { InjectionToken } from '@angular/core';

/** Versioned API root. Provide `{ provide: API_BASE_URL, useValue: environment.apiUrl + '/api/v1' }` in app.config. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => '/api/v1',
});
