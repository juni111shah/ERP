import { Signal } from '@angular/core';
import { CountryDto, CurrencyOption } from '@core/api/admin.api';
import { ClientDetail, IndustryOption, PlanView, groupIndustries } from '@core/api/platform.api';
import { toIsoDate } from '@core/format/dates';
import { displayDate, displayMoney } from '@core/format/display';
import { suggestWorkspaceCode } from '@core/format/subscription';
import { FieldSpec } from '@shared/components/record-drawer/record-drawer.component';
import { DrawerValue } from './platform-forms';
import { CYCLE_OPTIONS, START_OPTIONS, planOptions } from './platform-ui';

/** The dialogs of the client page. */
export type ClientDialogKind = 'assign' | 'plan' | 'trial' | 'activate' | 'invoice' | 'details';

/** What one client dialog shows: its title, button, fields and starting values. */
export interface ClientDialogSpec { title: string; submit: string; fields: FieldSpec[]; value: DrawerValue | null; warning?: string; intro?: string; }

/** What the dialogs read besides the client. Signals, so a dialog only depends on what it actually shows. */
export interface ClientDialogContext {
  plans: Signal<PlanView[]>;
  /** Starting values for the plan dialog (from a client's plan request), instead of the current plan. */
  prefill: Signal<DrawerValue | null>;
  /** The platform's billing currency. */
  currency: Signal<string>;
}

const planField = (plans: PlanView[], keep?: string | null): FieldSpec => ({ key: 'planId', label: 'Plan', type: 'select', required: true, options: planOptions(plans, keep) });
const CYCLE_FIELD: FieldSpec = { key: 'cycle', label: 'Billing cycle', type: 'select', required: true, default: 'monthly', options: CYCLE_OPTIONS };
const PRICE_FIELD: FieldSpec = { key: 'customPrice', label: 'Custom price per cycle', type: 'number', min: 0, hint: 'Leave empty to use the plan\'s price.' };

/** The dialog for `kind` on client `d`. */
export function clientDialogSpec(kind: ClientDialogKind | null, d: ClientDetail | null, ctx: ClientDialogContext): ClientDialogSpec {
  switch (kind) {
    case 'assign':
      return {
        title: `Assign a plan to ${d?.client.name ?? ''}`, submit: 'Assign plan', value: null, warning: 'Modules outside the plan are switched off. Their data is kept.',
        fields: [
          planField(ctx.plans()), CYCLE_FIELD,
          { key: 'startMode', label: 'How it starts', type: 'select', required: true, default: 'trial', options: START_OPTIONS, hint: 'Starting as active does not issue an invoice. Use Issue invoice afterwards.' },
          { key: 'trialDays', label: 'Trial days', type: 'number', min: 1, max: 365, precision: 0, hint: 'Leave empty to use the plan\'s trial.', hidden: (v) => v['startMode'] !== 'trial' },
          { ...PRICE_FIELD, hidden: (v) => v['startMode'] === 'complimentary' },
        ],
      };
    case 'plan':
      return {
        title: `Change plan for ${d?.client.name ?? ''}`, submit: 'Change plan', warning: 'Modules outside the new plan are switched off straight away. Their data is kept.',
        value: ctx.prefill() ?? { planId: d?.client.planId, cycle: d?.client.cycle, customPrice: d?.customPrice, isComplimentary: d?.client.isComplimentary },
        fields: [planField(ctx.plans(), d?.client.planId), CYCLE_FIELD, PRICE_FIELD, { key: 'isComplimentary', label: 'Free of charge', type: 'switch', hint: 'No invoices are issued and the subscription never lapses.' }],
      };
    case 'trial':
      return {
        title: 'Extend trial', submit: 'Extend trial', value: null, intro: d?.client.trialEndsOn ? `The trial ends on ${displayDate(d.client.trialEndsOn)}. A lapsed trial starts running again from today.` : 'The trial starts running from today.',
        fields: [{ key: 'days', label: 'More days', type: 'number', required: true, min: 1, max: 365, precision: 0, default: 14 }],
      };
    case 'activate':
      return {
        title: `Activate ${d?.client.name ?? ''}`, submit: 'Activate', value: null, intro: `Starts a paid period from today at ${displayMoney(d?.client.cyclePrice, ctx.currency())} per cycle.`,
        fields: [{ key: 'issueInvoice', label: 'Issue the first invoice', type: 'switch', default: true, hint: 'Turn this off when the period has already been paid for.' }],
      };
    case 'invoice':
      return {
        title: 'Issue invoice', submit: 'Issue invoice', value: null,
        fields: [
          { key: 'kind', label: 'What to bill', type: 'select', required: true, default: 'period', options: [{ value: 'period', label: 'The next billing period' }, { value: 'oneoff', label: 'A one-off amount' }] },
          { key: 'description', label: 'Description', type: 'text', required: true, maxLength: 300, hidden: (v) => v['kind'] !== 'oneoff' },
          { key: 'amount', label: 'Amount before tax', type: 'number', required: true, min: 0.01, hidden: (v) => v['kind'] !== 'oneoff' },
          { key: 'dueOn', label: 'Due date', type: 'date', hint: 'Leave empty to use the usual payment terms.' },
        ],
      };
    case 'details':
      return {
        title: 'Contact and notes', submit: 'Save', value: { billingEmail: d?.billingEmail, contactPhone: d?.contactPhone, notes: d?.notes },
        fields: [
          { key: 'billingEmail', label: 'Billing email', type: 'text', maxLength: 256 },
          { key: 'contactPhone', label: 'Phone', type: 'text', maxLength: 40 },
          { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 2000, hint: 'For your team only.' },
        ],
      };
    default:
      return { title: '', submit: 'Save', fields: [], value: null };
  }
}

/** A trial can be extended while it runs, after it lapsed, or when it was suspended by the billing run (not by the founder). */
export function canExtendTrial(d: ClientDetail | null): boolean {
  if (!d) return false;
  const s = d.client.status;
  return s === 'trialing' || s === 'expired' || (s === 'suspended' && !d.suspendedByFounder);
}

/** A paid period can start for a trial, a lapsed or billing-suspended client, or an active one whose period has ended. Never for a free one. */
export function canActivateClient(d: ClientDetail | null, today = toIsoDate(new Date())): boolean {
  if (!d || d.client.isComplimentary) return false;
  const s = d.client.status;
  if (s === 'trialing' || s === 'expired') return true;
  if (s === 'suspended') return !d.suspendedByFounder;
  return s === 'active' && !!d.client.currentPeriodEnd && d.client.currentPeriodEnd < today;
}

/** The pick lists of the new-client drawer. */
export interface NewClientLists { industries: IndustryOption[]; countries: CountryDto[]; currencies: CurrencyOption[]; plans: PlanView[]; }

/**
 * The fields of the new-client drawer. The workspace code follows the business name until it is typed over, and the currency
 * follows the country until a different one is picked.
 */
export function newClientFields({ industries, countries, currencies, plans }: NewClientLists): FieldSpec[] {
  return [
    { key: 'workspaceName', label: 'Business name', type: 'text', required: true, maxLength: 200, onChange: (v, form, prev) => {
      const typed = form['signInKey'] as string | null;
      return !typed || typed === suggestWorkspaceCode(prev['workspaceName'] as string) ? { signInKey: suggestWorkspaceCode(v as string) } : null;
    } },
    { key: 'signInKey', label: 'Workspace code', type: 'text', required: true, maxLength: 49, hint: 'What they use to reach this workspace. Letters, digits and dashes.' },
    { key: 'industry', label: 'Industry', type: 'select', required: true, searchable: true, options: groupIndustries(industries).flatMap((g) => g.items.map((i) => ({ value: i.key, label: i.label, group: g.group }))) },
    { key: 'countryCode', label: 'Country', type: 'select', required: true, searchable: true, options: countries.map((c) => ({ value: c.code, label: c.name })), onChange: (v, form, prev) => {
      const next = countries.find((c) => c.code === v)?.defaultCurrency;
      const before = countries.find((c) => c.code === prev['countryCode'])?.defaultCurrency;
      return next && (!form['baseCurrency'] || form['baseCurrency'] === before) ? { baseCurrency: next } : null;
    } },
    { key: 'baseCurrency', label: 'Currency', type: 'select', required: true, searchable: true, options: currencies.map((c) => ({ value: c.code, label: `${c.code} · ${c.name}` })) },
    { key: 'adminName', label: 'Administrator name', type: 'text', required: true, maxLength: 200 },
    { key: 'adminEmail', label: 'Administrator email', type: 'text', required: true, maxLength: 256 },
    { key: 'phone', label: 'Phone', type: 'text', maxLength: 40 },
    { key: 'planId', label: 'Plan', type: 'select', required: true, options: planOptions(plans) },
    { key: 'cycle', label: 'Billing cycle', type: 'select', required: true, default: 'monthly', options: CYCLE_OPTIONS },
    { key: 'startMode', label: 'How it starts', type: 'select', required: true, default: 'trial', options: START_OPTIONS, hint: 'Starting as active issues the first invoice straight away.' },
    { key: 'trialDays', label: 'Trial days', type: 'number', min: 1, max: 365, precision: 0, hint: 'Leave empty to use the plan\'s trial.', hidden: (v) => v['startMode'] !== 'trial' },
    { key: 'customPrice', label: 'Custom price per cycle', type: 'number', min: 0, hint: 'Leave empty to use the plan\'s price.', hidden: (v) => v['startMode'] === 'complimentary' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 2000, hint: 'For your team only.' },
  ];
}
