import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { AssistantActionCard, AssistantStreamEvent } from './assistant-stream';
import { PlatformApi } from './platform.api';

export interface AssistantConversationRow { id: string; title: string; lastMessageAt: string; }
export interface AssistantConversationDetail {
  id: string; title: string;
  messages: { at: string; fromUser: boolean; text: string }[];
  actions: (AssistantActionCard & { createdAt: string })[];
}

/** The workspace assistant module. Replies stream through the same interceptors as every other call, so sign-in and legal entity work as usual. */
@Injectable({ providedIn: 'root' })
export class WorkspaceAssistantApi {
  private readonly http = inject(HttpClient);
  private readonly platform = inject(PlatformApi);
  private readonly base = `${inject(API_BASE_URL)}/m/assistant`;

  status(): Observable<{ available: boolean }> { return this.http.get<{ available: boolean }>(`${this.base}/status`); }
  /** Counts only, for people with the usage permission. */
  usage(): Observable<{ messagesThisMonth: number; monthlyAllowance: number | null; peopleThisMonth: number }> { return this.http.get<{ messagesThisMonth: number; monthlyAllowance: number | null; peopleThisMonth: number }>(`${this.base}/usage`); }
  conversations(): Observable<AssistantConversationRow[]> { return this.http.get<AssistantConversationRow[]>(`${this.base}/conversations`); }
  conversation(id: string): Observable<AssistantConversationDetail> { return this.http.get<AssistantConversationDetail>(`${this.base}/conversations/${id}`); }
  deleteConversation(id: string): Observable<unknown> { return this.http.delete(`${this.base}/conversations/${id}`); }
  ask(input: { conversationId: string | null; text: string; page: string | null; language: string }): Observable<AssistantStreamEvent> { return this.platform.assistantStream(`${this.base}/messages`, input); }
  /** The key makes a double click or a retry harmless: the server runs the change once and repeats its answer. */
  confirm(id: string): Observable<AssistantActionCard> {
    return this.http.post<AssistantActionCard>(`${this.base}/actions/${id}/confirm`, {}, { headers: new HttpHeaders({ 'Idempotency-Key': `assistant-action-${id}` }) });
  }
  cancel(id: string): Observable<AssistantActionCard> { return this.http.post<AssistantActionCard>(`${this.base}/actions/${id}/cancel`, {}); }
}
