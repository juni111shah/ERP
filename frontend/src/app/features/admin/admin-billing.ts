import { FieldSpec } from '@shared/components/record-drawer/record-drawer.component';

/** The fields of the "ask for this plan" drawer. */
export const PLAN_REQUEST_FIELDS: FieldSpec[] = [
  { key: 'cycle', label: 'Billing cycle', type: 'select', required: true, options: [{ value: 'monthly', label: 'Monthly' }, { value: 'annual', label: 'Annual' }] },
  { key: 'note', label: 'Note for our team (optional)', type: 'textarea', maxLength: 400 },
];

/** What is left of a trial, in words: "today" on the last day. */
export function trialLeftText(days: number): string {
  return days <= 0 ? 'today' : days === 1 ? '1 day left' : `${days} days left`;
}
