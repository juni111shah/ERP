import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

export interface NotificationDto {
  id: string;
  title: string;
  body?: string;
  link?: string;
  severity: string;
  createdAt: string;
  readAt?: string;
}

/** The latest notifications of the signed-in person and how many are unread. */
export interface NotificationInbox { unread: number; items: NotificationDto[]; }

/** The signed-in person's notifications (/api/v1/notifications). */
@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/notifications`;

  inbox(): Observable<NotificationInbox> { return this.http.get<NotificationInbox>(this.url); }
  markAllRead(): Observable<unknown> { return this.http.post(`${this.url}/read-all`, {}); }
  markRead(id: string): Observable<unknown> { return this.http.post(`${this.url}/${id}/read`, {}); }
}
