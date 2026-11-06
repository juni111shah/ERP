import { HttpClient, HttpDownloadProgressEvent, HttpEventType } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { SubscriptionSummary } from '../session/session.store';
import { CountryDto, CurrencyOption } from './admin.api';
import { API_BASE_URL } from './api-config';
import { AssistantStreamEvent, AssistantStreamParser } from './assistant-stream';
import { ListQuery, PagedResult } from './api.models';
import { toHttpParams } from './crud-api.service';

export type SubscriptionStatus = NonNullable<SubscriptionSummary['status']>;
export type AccessLevel = SubscriptionSummary['access'];
export type BillingCycle = 'monthly' | 'annual';
export type InvoiceStatus = 'open' | 'paid' | 'void';
export type SignupStatus = 'pendingEmail' | 'pendingReview' | 'provisioned' | 'rejected' | 'expired';
export type SignupMode = 'open' | 'review' | 'closed';
export type ClientStartMode = 'trial' | 'active' | 'complimentary';

// ---- Public side ---------------------------------------------------------------------------------------------------------

export interface PublicPlan {
  id: string; code: string; name: string; description: string | null; monthlyPrice: number; annualPrice: number; currency: string; trialDays: number;
  maxUsers: number | null; maxLegalEntities: number | null; modules: string[]; features: string[]; isRecommended: boolean;
}
export interface SignupConfig { mode: SignupMode; emailVerification: boolean; defaultPlanId: string | null; currency: string; supportEmail: string | null; termsUrl: string | null; plans: PublicPlan[]; }
export interface SignupInput {
  businessName: string; signInKey: string; industry: string; countryCode: string; baseCurrency: string; timeZone: string | null;
  contactName: string; email: string; phone: string | null; password: string; planId: string | null; cycle: BillingCycle; acceptTerms: boolean; website: string | null;
}
export interface SignupResult { status: SignupStatus; message: string; signInKey: string | null; }
export interface SignInKeyAvailability { available: boolean; reason: string | null; }
export interface PublicModule { key: string; name: string; description: string; category: string; isCore: boolean; requires: string[]; }
export interface PublicFeature { icon: string; title: string; text: string; titleAr: string; textAr: string; }
export interface PublicFaq { q: string; a: string; qAr: string; aAr: string; }
export interface PublicContent { builtIn: PublicFeature[]; trust: PublicFeature[]; faqs: PublicFaq[]; about: string | null; }
export interface BrandLeadInput { token: string | null; name: string; email: string; phone: string | null; company: string | null; need: string | null; consent: boolean; website: string | null; }
export interface LegalDocuments { companyName: string; supportEmail: string | null; termsUrl: string | null; terms: string; privacy: string; updatedAt: string | null; }
export interface IndustryOption { key: string; label: string; description: string; availability: string; suggestedModules: string[]; }

// ---- A client's own billing ----------------------------------------------------------------------------------------------

export interface InvoiceRow { id: string; number: string; status: InvoiceStatus; description: string; total: number; currency: string; issuedOn: string; dueOn: string; paidOn: string | null; isOverdue: boolean; }
export interface InvoiceDocument {
  id: string; number: string; status: InvoiceStatus; orgId: string; clientName: string; clientCode: string; billingEmail: string | null; description: string; periodStart: string | null; periodEnd: string | null;
  subtotal: number; taxPercent: number; taxLabel: string; taxAmount: number; total: number; currency: string; issuedOn: string; dueOn: string; paidOn: string | null;
  paymentMethod: string | null; paymentReference: string | null; voidReason: string | null; companyName: string; companyAddress: string | null; companyTaxNumber: string | null; supportEmail: string | null; paymentInstructions: string | null;
}
export interface RequestedChange { planId: string; planName: string; cycle: BillingCycle; note: string | null; requestedAt: string; }
export interface BillingOverview {
  summary: SubscriptionSummary; planId: string | null; cycle: BillingCycle | null; cyclePrice: number | null; currency: string; activeUsers: number; maxUsers: number | null; legalEntities: number; maxLegalEntities: number | null;
  requested: RequestedChange | null; plans: PublicPlan[]; invoices: InvoiceRow[]; paymentInstructions: string | null; supportEmail: string | null; termsUrl: string | null;
}

// ---- The founder's console -----------------------------------------------------------------------------------------------

export interface PlanView {
  id: string; code: string; name: string; description: string | null; isPublic: boolean; isActive: boolean; isRecommended: boolean; sortOrder: number; monthlyPrice: number; annualPrice: number;
  currency: string; trialDays: number; maxUsers: number | null; maxLegalEntities: number | null; modules: string[]; features: string[]; clientCount: number;
}
export interface PlanInput {
  code: string; name: string; description: string | null; isPublic: boolean; isRecommended: boolean; sortOrder: number; monthlyPrice: number; annualPrice: number; trialDays: number;
  maxUsers: number | null; maxLegalEntities: number | null; modules: string[]; features: string[];
}
export interface ClientRow {
  orgId: string; name: string; code: string; industryCode: string; countryCode: string; createdAt: string; planId: string | null; planName: string | null; status: SubscriptionStatus | null; access: AccessLevel;
  cycle: BillingCycle | null; cyclePrice: number | null; monthlyValue: number; isComplimentary: boolean; trialEndsOn: string | null; currentPeriodEnd: string | null; activeUsers: number; maxUsers: number | null;
  lastSignInAt: string | null; openBalance: number; hasPlanRequest: boolean;
}
export interface WorkspaceMember { userId: string; email: string; fullName: string; isActive: boolean; isAdministrator: boolean; lastSignInAt: string | null; twoFactorEnabled: boolean; }
export interface ActivityRow { id: string; at: string; actor: string; action: string; orgId: string | null; clientName: string | null; detail: string | null; }
export interface ClientDetail {
  client: ClientRow; baseCurrency: string; timeZone: string; billingEmail: string | null; contactPhone: string | null; notes: string | null; customPrice: number | null; startedOn: string | null; currentPeriodStart: string | null;
  lapsedOn: string | null; suspendedReason: string | null; suspendedByFounder: boolean; cancelledAt: string | null; cancelReason: string | null; requested: RequestedChange | null; legalEntities: number; maxLegalEntities: number | null;
  enabledModules: string[]; planModules: string[] | null; members: WorkspaceMember[]; invoices: InvoiceRow[]; activity: ActivityRow[];
}
export interface CreateClientInput {
  workspaceName: string; signInKey: string; industry: string; countryCode: string; baseCurrency: string; timeZone: string | null; adminName: string; adminEmail: string; phone: string | null;
  planId: string; cycle: BillingCycle; startMode: ClientStartMode; trialDays: number | null; customPrice: number | null; notes: string | null;
}
export interface CreatedClient { orgId: string; signInKey: string; adminEmail: string; temporaryPassword: string | null; }
export interface SetClientPlanInput { planId: string; cycle: BillingCycle; customPrice: number | null; isComplimentary: boolean; startMode?: ClientStartMode | null; trialDays?: number | null; }
export interface ClientNotesInput { billingEmail: string | null; contactPhone: string | null; notes: string | null; }
export interface IssueInvoiceInput { description: string | null; amount: number | null; dueOn: string | null; }
export interface RecordPaymentInput { paidOn: string | null; method: string | null; reference: string | null; }
export interface PlatformInvoiceRow {
  id: string; number: string; status: InvoiceStatus; orgId: string; clientName: string; description: string; total: number; currency: string; issuedOn: string; dueOn: string; paidOn: string | null;
  paymentMethod: string | null; paymentReference: string | null; isOverdue: boolean;
}
export interface SignupRow {
  id: string; status: SignupStatus; businessName: string; signInKey: string; industry: string; countryCode: string; baseCurrency: string; contactName: string; email: string; phone: string | null;
  planId: string; planName: string; cycle: BillingCycle; emailVerifiedAt: string | null; createdAt: string; reviewedAt: string | null; rejectReason: string | null; provisionedOrgId: string | null;
}
export interface PlanCount { planId: string; planName: string; clients: number; monthlyValue: number; }
export interface MonthAmount { month: string; amount: number; }
export interface ExpiringTrial { orgId: string; name: string; trialEndsOn: string; daysLeft: number; }
export interface PlatformDashboard {
  currency: string; monthlyRecurring: number; annualRecurring: number; totalClients: number; trialing: number; active: number; pastDue: number; expired: number; suspended: number; cancelled: number; withoutPlan: number;
  outstanding: number; overdue: number; collectedThisMonth: number; collectedTotal: number; pendingSignups: number; newClients30Days: number; planRequests: number;
  byPlan: PlanCount[]; collected: MonthAmount[]; trialsEnding: ExpiringTrial[]; recentActivity: ActivityRow[];
}
export interface LifecycleResult { trialsExpired: number; invoicesIssued: number; markedPastDue: number; suspended: number; remindersSent: number; signupsExpired: number; }
export interface PlatformModule { key: string; name: string; description: string; category: string; isCore: boolean; requires: string[]; }
export interface PlatformSettingsView {
  signupMode: SignupMode; requireEmailVerification: boolean; emailServerConfigured: boolean; defaultPlanId: string | null; currency: string; taxPercent: number; taxLabel: string; invoicePrefix: string; nextInvoiceNumber: number;
  invoiceDueDays: number; renewalLeadDays: number; graceDays: number; suspendAfterDays: number; companyName: string; companyAddress: string | null; companyTaxNumber: string | null; supportEmail: string | null; termsUrl: string | null; paymentInstructions: string | null; termsText: string; privacyText: string; legalIsStarterText: boolean; aboutText: string | null;
}
export type PlatformSettingsInput = Omit<PlatformSettingsView, 'emailServerConfigured' | 'nextInvoiceNumber' | 'legalIsStarterText'>;
export interface PlatformTeamMember { userId: string; email: string; fullName: string; lastSignInAt: string | null; twoFactorEnabled: boolean; }
export interface NewTeamMember { member: PlatformTeamMember; temporaryPassword: string | null; }
export interface AiProviderView {
  id: string; name: string; baseUrl: string; model: string; keyHint: string; keyNeedsReentry: boolean; useForBrand: boolean; useForWorkspace: boolean;
  lastTestedAt: string | null; lastTestOk: boolean | null; lastTestMessage: string | null;
}
export type BrandLeadStatus = 'new' | 'contacted' | 'won' | 'lost';
export interface BrandLeadRow { id: string; createdAt: string; status: BrandLeadStatus; name: string; email: string; phone: string | null; company: string | null; need: string | null; conversationId: string | null; hasRegistered: boolean; }
export interface BrandConversationRow { id: string; startedAt: string; lastMessageAt: string; visitorMessages: number; needsReview: boolean; firstMessage: string | null; }
export interface BrandMessageRow { at: string; fromVisitor: boolean; text: string; }
export type AssistantBot = 'brand' | 'workspace';
export interface AssistantUsageRow { bot: AssistantBot; providerId: string; providerName: string; requests: number; failures: number; inputTokens: number; outputTokens: number; lastFailure: string | null; }
export interface AssistantConsoleView {
  brandEnabled: boolean; workspaceEnabled: boolean; canManage: boolean; providers: AiProviderView[];
  brandDailyTokens: number; workspaceDailyTokens: number; usageToday: AssistantUsageRow[];
}
/** apiKey is write-only: leave it null when editing to keep the stored key. */
export interface AiProviderInput { name: string; baseUrl: string; model: string; apiKey: string | null; useForBrand: boolean; useForWorkspace: boolean; }

/** Registration, a client's own billing, and the founder's console. Every console route sits behind the server's platform-admin policy. */
@Injectable({ providedIn: 'root' })
export class PlatformApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private get admin(): string { return `${this.base}/platform-admin`; }

  /** Registrations waiting for review: the badge in the console's menu. */
  readonly pendingSignups = signal(0);

  // ---- Public --------------------------------------------------------------------------------------------------------
  publicModules(): Observable<PublicModule[]> { return this.http.get<PublicModule[]>(`${this.base}/public/modules`); }
  publicContent(): Observable<PublicContent> { return this.http.get<PublicContent>(`${this.base}/public/content`); }
  brandAssistantStatus(): Observable<{ visible: boolean; supportEmail: string | null }> { return this.http.get<{ visible: boolean; supportEmail: string | null }>(`${this.base}/public/assistant/status`); }
  askBrandAssistant(input: { token: string | null; text: string; website: string | null }): Observable<AssistantStreamEvent> { return this.assistantStream(`${this.base}/public/assistant/messages`, input); }
  submitBrandLead(input: BrandLeadInput): Observable<unknown> { return this.http.post(`${this.base}/public/assistant/leads`, input); }
  legal(): Observable<LegalDocuments> { return this.http.get<LegalDocuments>(`${this.base}/public/legal`); }
  signupConfig(): Observable<SignupConfig> { return this.http.get<SignupConfig>(`${this.base}/public/signup-config`); }
  signInKeyAvailable(key: string): Observable<SignInKeyAvailability> { return this.http.get<SignInKeyAvailability>(`${this.base}/public/sign-in-key-available`, { params: { key } }); }
  register(input: SignupInput): Observable<SignupResult> { return this.http.post<SignupResult>(`${this.base}/public/signups`, input); }
  verifySignup(token: string): Observable<SignupResult> { return this.http.post<SignupResult>(`${this.base}/public/signups/verify`, { token }); }
  industries(): Observable<IndustryOption[]> { return this.http.get<IndustryOption[]>(`${this.base}/platform/industries`); }
  countries(): Observable<CountryDto[]> { return this.http.get<CountryDto[]>(`${this.base}/platform/countries`); }
  currencies(): Observable<CurrencyOption[]> { return this.http.get<CurrencyOption[]>(`${this.base}/platform/currencies`); }

  // ---- A client's own billing -----------------------------------------------------------------------------------------
  billing(): Observable<BillingOverview> { return this.http.get<BillingOverview>(`${this.base}/billing/subscription`); }
  myInvoice(id: string): Observable<InvoiceDocument> { return this.http.get<InvoiceDocument>(`${this.base}/billing/invoices/${id}`); }
  requestPlanChange(input: { planId: string; cycle: BillingCycle; note: string | null }): Observable<BillingOverview> { return this.http.post<BillingOverview>(`${this.base}/billing/change-request`, input); }
  withdrawPlanChange(): Observable<BillingOverview> { return this.http.delete<BillingOverview>(`${this.base}/billing/change-request`); }

  // ---- Console: dashboard ---------------------------------------------------------------------------------------------
  dashboard(): Observable<PlatformDashboard> { return this.http.get<PlatformDashboard>(`${this.admin}/dashboard`).pipe(tap((d) => this.pendingSignups.set(d.pendingSignups))); }
  runBilling(): Observable<LifecycleResult> { return this.http.post<LifecycleResult>(`${this.admin}/billing-run`, {}); }
  modules(): Observable<PlatformModule[]> { return this.http.get<PlatformModule[]>(`${this.admin}/modules`); }

  // ---- Console: plans -------------------------------------------------------------------------------------------------
  plans(): Observable<PlanView[]> { return this.http.get<PlanView[]>(`${this.admin}/plans`); }
  createPlan(input: PlanInput): Observable<PlanView> { return this.http.post<PlanView>(`${this.admin}/plans`, input); }
  updatePlan(id: string, input: PlanInput): Observable<PlanView> { return this.http.put<PlanView>(`${this.admin}/plans/${id}`, input); }
  setPlanArchived(id: string, archived: boolean): Observable<PlanView> { return this.http.post<PlanView>(`${this.admin}/plans/${id}/${archived ? 'archive' : 'restore'}`, {}); }

  // ---- Console: clients -----------------------------------------------------------------------------------------------
  clients(q: ListQuery): Observable<PagedResult<ClientRow>> { return this.http.get<PagedResult<ClientRow>>(`${this.admin}/clients`, { params: toHttpParams(q) }); }
  createClient(input: CreateClientInput): Observable<CreatedClient> { return this.http.post<CreatedClient>(`${this.admin}/clients`, input); }
  client(id: string): Observable<ClientDetail> { return this.http.get<ClientDetail>(`${this.admin}/clients/${id}`); }
  setClientPlan(id: string, input: SetClientPlanInput): Observable<ClientDetail> { return this.http.put<ClientDetail>(`${this.admin}/clients/${id}/plan`, input); }
  updateClientDetails(id: string, input: ClientNotesInput): Observable<ClientDetail> { return this.http.put<ClientDetail>(`${this.admin}/clients/${id}/details`, input); }
  extendTrial(id: string, days: number): Observable<ClientDetail> { return this.http.post<ClientDetail>(`${this.admin}/clients/${id}/extend-trial`, { days }); }
  activateClient(id: string, issueInvoice: boolean): Observable<ClientDetail> { return this.http.post<ClientDetail>(`${this.admin}/clients/${id}/activate`, { issueInvoice }); }
  suspendClient(id: string, reason: string | null): Observable<ClientDetail> { return this.http.post<ClientDetail>(`${this.admin}/clients/${id}/suspend`, { reason }); }
  reactivateClient(id: string): Observable<ClientDetail> { return this.http.post<ClientDetail>(`${this.admin}/clients/${id}/reactivate`, {}); }
  cancelClient(id: string, reason: string | null): Observable<ClientDetail> { return this.http.post<ClientDetail>(`${this.admin}/clients/${id}/cancel`, { reason }); }
  declinePlanRequest(id: string): Observable<ClientDetail> { return this.http.post<ClientDetail>(`${this.admin}/clients/${id}/decline-plan-request`, {}); }
  issueInvoice(id: string, input: IssueInvoiceInput): Observable<InvoiceRow> { return this.http.post<InvoiceRow>(`${this.admin}/clients/${id}/invoices`, input); }
  issueTemporaryPassword(id: string, userId: string): Observable<{ password: string }> { return this.http.post<{ password: string }>(`${this.admin}/clients/${id}/users/${userId}/temporary-password`, {}); }
  setClientModule(id: string, key: string, enabled: boolean): Observable<unknown> { return this.http.post(`${this.admin}/clients/${id}/modules/${key}/${enabled ? 'enable' : 'disable'}`, {}); }

  // ---- Console: invoices ----------------------------------------------------------------------------------------------
  invoices(q: ListQuery): Observable<PagedResult<PlatformInvoiceRow>> { return this.http.get<PagedResult<PlatformInvoiceRow>>(`${this.admin}/invoices`, { params: toHttpParams(q) }); }
  invoice(id: string): Observable<InvoiceDocument> { return this.http.get<InvoiceDocument>(`${this.admin}/invoices/${id}`); }
  recordPayment(id: string, input: RecordPaymentInput): Observable<InvoiceDocument> { return this.http.post<InvoiceDocument>(`${this.admin}/invoices/${id}/payment`, input); }
  voidInvoice(id: string, reason: string | null): Observable<InvoiceDocument> { return this.http.post<InvoiceDocument>(`${this.admin}/invoices/${id}/void`, { reason }); }
  reopenInvoice(id: string): Observable<InvoiceDocument> { return this.http.post<InvoiceDocument>(`${this.admin}/invoices/${id}/reopen`, {}); }

  // ---- Console: registrations -----------------------------------------------------------------------------------------
  signups(q: ListQuery): Observable<PagedResult<SignupRow>> { return this.http.get<PagedResult<SignupRow>>(`${this.admin}/signups`, { params: toHttpParams(q) }); }
  approveSignup(id: string): Observable<SignupRow> { return this.http.post<SignupRow>(`${this.admin}/signups/${id}/approve`, {}); }
  rejectSignup(id: string, reason: string | null): Observable<SignupRow> { return this.http.post<SignupRow>(`${this.admin}/signups/${id}/reject`, { reason }); }

  // ---- Console: settings, activity, team ------------------------------------------------------------------------------
  settings(): Observable<PlatformSettingsView> { return this.http.get<PlatformSettingsView>(`${this.admin}/settings`); }
  saveSettings(input: PlatformSettingsInput): Observable<PlatformSettingsView> { return this.http.put<PlatformSettingsView>(`${this.admin}/settings`, input); }
  activity(q: ListQuery): Observable<PagedResult<ActivityRow>> { return this.http.get<PagedResult<ActivityRow>>(`${this.admin}/activity`, { params: toHttpParams(q) }); }
  team(): Observable<PlatformTeamMember[]> { return this.http.get<PlatformTeamMember[]>(`${this.admin}/team`); }
  addTeamMember(input: { email: string; fullName: string }): Observable<NewTeamMember> { return this.http.post<NewTeamMember>(`${this.admin}/team`, input); }
  removeTeamMember(userId: string): Observable<unknown> { return this.http.delete(`${this.admin}/team/${userId}`); }

  // ---- Console: leads and public chats --------------------------------------------------------------------------------
  leads(q: ListQuery): Observable<PagedResult<BrandLeadRow>> { return this.http.get<PagedResult<BrandLeadRow>>(`${this.admin}/leads`, { params: toHttpParams(q) }); }
  setLeadStatus(id: string, status: BrandLeadStatus): Observable<BrandLeadRow> { return this.http.put<BrandLeadRow>(`${this.admin}/leads/${id}/status/${status}`, {}); }
  deleteLead(id: string): Observable<unknown> { return this.http.delete(`${this.admin}/leads/${id}`); }
  brandConversations(q: ListQuery): Observable<PagedResult<BrandConversationRow>> { return this.http.get<PagedResult<BrandConversationRow>>(`${this.admin}/assistant/conversations`, { params: toHttpParams(q) }); }
  brandConversation(id: string): Observable<BrandMessageRow[]> { return this.http.get<BrandMessageRow[]>(`${this.admin}/assistant/conversations/${id}`); }

  // ---- Console: AI assistant ------------------------------------------------------------------------------------------
  assistant(): Observable<AssistantConsoleView> { return this.http.get<AssistantConsoleView>(`${this.admin}/assistant`); }
  setAssistantVisibility(input: { brandEnabled: boolean; workspaceEnabled: boolean }): Observable<AssistantConsoleView> { return this.http.put<AssistantConsoleView>(`${this.admin}/assistant/visibility`, input); }
  setAssistantLimits(input: { brandDailyTokens: number; workspaceDailyTokens: number }): Observable<AssistantConsoleView> { return this.http.put<AssistantConsoleView>(`${this.admin}/assistant/limits`, input); }
  tryAssistant(input: { bot: AssistantBot; messages: { role: 'user' | 'assistant'; text: string }[] }): Observable<AssistantStreamEvent> { return this.assistantStream(`${this.admin}/assistant/try`, input); }
  /** Reads a reply as it is written. Unsubscribing cancels the request, which also stops the reply at the AI service. */
  assistantStream(url: string, body: unknown): Observable<AssistantStreamEvent> {
    return new Observable((subscriber) => {
      const parser = new AssistantStreamParser();
      let seen = 0;
      const read = (text: string) => {
        for (const event of parser.push(text.slice(seen))) subscriber.next(event);
        seen = text.length;
      };
      return this.http.post(url, body, { observe: 'events', reportProgress: true, responseType: 'text' }).subscribe({
        next: (e) => {
          if (e.type === HttpEventType.DownloadProgress) read((e as HttpDownloadProgressEvent).partialText ?? '');
          else if (e.type === HttpEventType.Response) read(e.body ?? '');
        },
        error: (e) => subscriber.error(e),
        complete: () => subscriber.complete(),
      });
    });
  }
  createAiProvider(input: AiProviderInput): Observable<AssistantConsoleView> { return this.http.post<AssistantConsoleView>(`${this.admin}/assistant/providers`, input); }
  updateAiProvider(id: string, input: AiProviderInput): Observable<AssistantConsoleView> { return this.http.put<AssistantConsoleView>(`${this.admin}/assistant/providers/${id}`, input); }
  deleteAiProvider(id: string): Observable<AssistantConsoleView> { return this.http.delete<AssistantConsoleView>(`${this.admin}/assistant/providers/${id}`); }
  reorderAiProviders(ids: string[]): Observable<AssistantConsoleView> { return this.http.put<AssistantConsoleView>(`${this.admin}/assistant/providers/order`, { ids }); }
  testAiProvider(id: string): Observable<AssistantConsoleView> { return this.http.post<AssistantConsoleView>(`${this.admin}/assistant/providers/${id}/test`, {}); }
}
