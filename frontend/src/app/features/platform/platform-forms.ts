import { CountryDto } from '@core/api/admin.api';
import {
  BillingCycle, ClientNotesInput, ClientStartMode, CreateClientInput, IssueInvoiceInput, PlanInput, PlanView, PlatformModule, PlatformSettingsInput,
  RecordPaymentInput, SetClientPlanInput, SignupMode,
} from '@core/api/platform.api';
import { FieldSpec } from '@shared/components/record-drawer/record-drawer.component';

/** The values an `<erp-record-drawer>` hands back on submit. */
export type DrawerValue = Record<string, unknown>;

/** How a subscription starts: a trial takes its own length, a free one has no price. Empty numbers mean "use the plan's". */
function startTerms(v: DrawerValue): { startMode: ClientStartMode; trialDays: number | null; customPrice: number | null } {
  const startMode = v['startMode'] as ClientStartMode;
  return {
    startMode,
    trialDays: startMode === 'trial' ? (v['trialDays'] as number | null) ?? null : null,
    customPrice: startMode === 'complimentary' ? null : (v['customPrice'] as number | null) ?? null,
  };
}

// ---- Clients ------------------------------------------------------------------------------------------------------------

/** The new-client drawer as a create call. The workspace's time zone is its country's. */
export function toCreateClientInput(v: DrawerValue, countries: CountryDto[]): CreateClientInput {
  const { startMode, trialDays, customPrice } = startTerms(v);
  return {
    workspaceName: v['workspaceName'] as string, signInKey: v['signInKey'] as string, industry: v['industry'] as string, countryCode: v['countryCode'] as string, baseCurrency: v['baseCurrency'] as string,
    timeZone: countries.find((c) => c.code === v['countryCode'])?.defaultTimeZone ?? null, adminName: v['adminName'] as string, adminEmail: v['adminEmail'] as string, phone: v['phone'] as string | null,
    planId: v['planId'] as string, cycle: v['cycle'] as BillingCycle, startMode, trialDays, customPrice, notes: v['notes'] as string | null,
  };
}

/** The "assign a plan" dialog: a first plan for a client that has none. */
export function toAssignPlanInput(v: DrawerValue): SetClientPlanInput {
  const { startMode, trialDays, customPrice } = startTerms(v);
  return { planId: v['planId'] as string, cycle: v['cycle'] as BillingCycle, customPrice, isComplimentary: startMode === 'complimentary', startMode, trialDays };
}

/** The "change plan" dialog. */
export function toChangePlanInput(v: DrawerValue): SetClientPlanInput {
  return { planId: v['planId'] as string, cycle: v['cycle'] as BillingCycle, customPrice: (v['customPrice'] as number | null) ?? null, isComplimentary: !!v['isComplimentary'] };
}

/** The "issue invoice" dialog: the next billing period, or a one-off amount with its own description. */
export function toIssueInvoiceInput(v: DrawerValue): IssueInvoiceInput {
  const oneOff = v['kind'] === 'oneoff';
  return { description: oneOff ? (v['description'] as string) : null, amount: oneOff ? (v['amount'] as number) : null, dueOn: (v['dueOn'] as string | null) ?? null };
}

/** The "contact and notes" dialog. */
export function toClientNotesInput(v: DrawerValue): ClientNotesInput {
  return { billingEmail: v['billingEmail'] as string | null, contactPhone: v['contactPhone'] as string | null, notes: v['notes'] as string | null };
}

// ---- Invoices -----------------------------------------------------------------------------------------------------------

const PAYMENT_METHODS = ['Bank transfer', 'Cash', 'Cheque', 'Other'];

/** The fields of the "record payment" drawer. */
export function paymentFields(): FieldSpec[] {
  return [
    { key: 'paidOn', label: 'Paid on', type: 'date', required: true, default: new Date() },
    { key: 'method', label: 'Method', type: 'select', default: PAYMENT_METHODS[0], options: PAYMENT_METHODS.map((m) => ({ value: m, label: m })) },
    { key: 'reference', label: 'Reference', type: 'text', maxLength: 200, hint: 'For example the bank transfer number.' },
  ];
}

/** The "record payment" drawer as a payment. */
export function toPaymentInput(v: DrawerValue): RecordPaymentInput {
  return { paidOn: v['paidOn'] as string, method: v['method'] as string | null, reference: v['reference'] as string | null };
}

// ---- Plans --------------------------------------------------------------------------------------------------------------

/** The fields of the plan drawer; the module list offers the optional (non-core) modules. */
export function planFields(modules: PlatformModule[]): FieldSpec[] {
  return [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 39, hint: 'Lower-case letters, digits or dashes. Used in links such as /register?plan=starter.' },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 120 },
    { key: 'description', label: 'Description', type: 'textarea', maxLength: 500 },
    { key: 'monthlyPrice', label: 'Monthly price', type: 'number', required: true, min: 0, default: 0 },
    { key: 'annualPrice', label: 'Annual price', type: 'number', required: true, min: 0, default: 0, hint: 'For the whole year, not per month.' },
    { key: 'trialDays', label: 'Trial days', type: 'number', required: true, min: 0, max: 365, precision: 0, default: 14 },
    { key: 'sortOrder', label: 'Sort order', type: 'number', required: true, precision: 0, default: 100, hint: 'Lower numbers come first on the price list.' },
    { key: 'maxUsers', label: 'Maximum users', type: 'number', min: 1, precision: 0, hint: 'Leave empty for no limit.' },
    { key: 'maxLegalEntities', label: 'Maximum legal entities', type: 'number', min: 1, precision: 0, hint: 'Leave empty for no limit.' },
    { key: 'modules', label: 'Modules', type: 'multiselect', options: modules.map((m) => ({ value: m.key, label: m.name })), hint: 'Leave empty to include every module.' },
    { key: 'features', label: 'Features', type: 'textarea', maxLength: 2000, hint: 'One per line. These are the selling points on the pricing card.' },
    { key: 'isPublic', label: 'Public', type: 'switch', default: true, hint: 'Shown on the registration page.' },
    { key: 'isRecommended', label: 'Recommended', type: 'switch', hint: 'Highlighted on the pricing cards.' },
  ];
}

/** A plan as the drawer edits it: the features one per line. */
export function planDrawerValue(plan: PlanView): DrawerValue {
  return { ...plan, features: plan.features.join('\n') };
}

/** The plan drawer as a plan: empty numbers become 0 (limits stay empty), features one per line without blanks. */
export function toPlanInput(v: DrawerValue): PlanInput {
  return {
    code: v['code'] as string, name: v['name'] as string, description: v['description'] as string | null, isPublic: !!v['isPublic'], isRecommended: !!v['isRecommended'], sortOrder: (v['sortOrder'] as number) ?? 0,
    monthlyPrice: (v['monthlyPrice'] as number) ?? 0, annualPrice: (v['annualPrice'] as number) ?? 0, trialDays: (v['trialDays'] as number) ?? 0, maxUsers: (v['maxUsers'] as number | null) ?? null, maxLegalEntities: (v['maxLegalEntities'] as number | null) ?? null,
    modules: (v['modules'] as string[] | null) ?? [], features: String(v['features'] ?? '').split('\n').map((f) => f.trim()).filter(Boolean),
  };
}

/** The modules a plan includes, by name. */
export function planModuleText(plan: PlanView, modules: PlatformModule[]): string {
  if (!plan.modules.length) return 'Every module';
  const names = new Map(modules.map((m) => [m.key, m.name]));
  return plan.modules.map((k) => names.get(k) ?? k).join(', ');
}

// ---- Platform settings --------------------------------------------------------------------------------------------------

/** Who may register, as the settings page offers it. */
export const SIGNUP_MODES: { value: SignupMode; label: string; hint: string }[] = [
  { value: 'open', label: 'Open', hint: 'Anyone gets a workspace straight away.' },
  { value: 'review', label: 'Review', hint: 'You approve each one before a workspace is created.' },
  { value: 'closed', label: 'Closed', hint: 'The registration page says registration is closed and shows your support email.' },
];

/** The raw value of the platform settings form. */
export interface PlatformSettingsFormValue {
  signupMode: SignupMode | null; requireEmailVerification: boolean | null; defaultPlanId: string | null; currency: string | null; taxPercent: number | null; taxLabel: string | null;
  invoicePrefix: string | null; invoiceDueDays: number | null; renewalLeadDays: number | null; graceDays: number | null; suspendAfterDays: number | null; companyName: string | null;
  companyAddress: string | null; companyTaxNumber: string | null; supportEmail: string | null; termsUrl: string | null; paymentInstructions: string | null; aboutText: string | null;
  termsText: string | null; privacyText: string | null;
}

/** The settings form as the save call: empty optional texts are sent as null, empty numbers as their defaults. */
export function toPlatformSettingsInput(v: PlatformSettingsFormValue): PlatformSettingsInput {
  return {
    signupMode: v.signupMode!, requireEmailVerification: !!v.requireEmailVerification, defaultPlanId: v.defaultPlanId, currency: v.currency!, taxPercent: v.taxPercent ?? 0, taxLabel: v.taxLabel!, invoicePrefix: v.invoicePrefix!,
    invoiceDueDays: v.invoiceDueDays ?? 0, renewalLeadDays: v.renewalLeadDays ?? 0, graceDays: v.graceDays ?? 0, suspendAfterDays: v.suspendAfterDays ?? 1,
    companyName: v.companyName ?? '', companyAddress: v.companyAddress || null, companyTaxNumber: v.companyTaxNumber || null, supportEmail: v.supportEmail || null, termsUrl: v.termsUrl || null, paymentInstructions: v.paymentInstructions || null, aboutText: v.aboutText ?? '', termsText: v.termsText ?? '', privacyText: v.privacyText ?? '',
  };
}
