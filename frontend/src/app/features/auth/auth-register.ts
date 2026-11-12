import { ProblemDetails } from '@core/api/api.models';
import { BillingCycle, PublicPlan, SignupInput } from '@core/api/platform.api';
import { displayMoney } from '@core/format/display';
import { yearlySaving } from '@core/format/subscription';
import { PASSWORD_HINT } from './auth-layout';

/** A workspace code: 2 to 49 letters, digits or dashes, starting with a letter or digit. */
export const CODE_RULE = /^[a-zA-Z0-9][a-zA-Z0-9-]{1,48}$/;

/** What each registration field says when it is not filled in right. */
export const FIELD_MESSAGES: Record<string, string> = {
  businessName: 'Enter the business name.', signInKey: 'Use 2-49 letters, digits or dashes.', industry: 'Choose an industry.', countryCode: 'Choose a country.', baseCurrency: 'Choose a currency.',
  contactName: 'Enter your name.', email: 'Enter a valid email address.', password: PASSWORD_HINT, acceptTerms: 'Please accept the terms to continue.',
};

/** The raw value of the registration form. */
export interface RegisterFormValue {
  businessName: string; signInKey: string; industry: string; countryCode: string; baseCurrency: string; contactName: string;
  phone: string; email: string; password: string; acceptTerms: boolean; website: string;
}

/** The registration as the server takes it: texts trimmed, empty optional ones as null. */
export function toSignupInput(v: RegisterFormValue, timeZone: string, planId: string | null, cycle: BillingCycle): SignupInput {
  return {
    businessName: v.businessName.trim(), signInKey: v.signInKey.trim(), industry: v.industry, countryCode: v.countryCode, baseCurrency: v.baseCurrency,
    timeZone, contactName: v.contactName.trim(), email: v.email.trim(), phone: v.phone.trim() || null, password: v.password,
    planId, cycle, acceptTerms: v.acceptTerms, website: v.website || null,
  };
}

/**
 * The message above the form after a failed registration; null when every problem is shown on its own field.
 * `rest` are the server errors that matched no field.
 */
export function registrationFailureText(p: ProblemDetails | null, rest: string[]): string | null {
  if (p?.status === 429) return 'Too many attempts. Please wait a minute and try again.';
  if (p?.errors && !rest.length) return null;
  return rest[0] ?? p?.detail ?? p?.title ?? 'Could not create the workspace. Please try again.';
}

/** The biggest yearly saving (in percent) any plan offers, or null when none is cheaper by the year. */
export function bestYearlySaving(plans: PublicPlan[]): number | null {
  const best = Math.max(0, ...plans.map((p) => yearlySaving(p.monthlyPrice, p.annualPrice)?.percent ?? 0));
  return best > 0 ? best : null;
}

/** The chosen plan's price for the cycle: "AED 99 a month" or "AED 990 a year". */
export function planPriceText(plan: PublicPlan, cycle: BillingCycle): string {
  return cycle === 'annual' ? `${displayMoney(plan.annualPrice, plan.currency)} a year` : `${displayMoney(plan.monthlyPrice, plan.currency)} a month`;
}
