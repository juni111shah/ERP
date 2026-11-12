import { CustomerInteractionInput, InteractionType } from '@core/api/crm.api';

/** The activity form's raw value. */
export interface InteractionFormValue {
  legalEntityId: string;
  relatedEntityKind: string;
  relatedEntityId: string;
  type: InteractionType;
  subject: string;
  details: string | null;
  interactionDate: Date | null;
  followUpDate: Date | null;
}

const TYPE_COLORS: Record<InteractionType, string> = { call: 'blue', meeting: 'purple', email: 'orange', note: 'cyan', task: 'geekblue' };
const TYPE_ICONS: Record<InteractionType, string> = { call: 'phone', meeting: 'team', email: 'mail', note: 'file-text', task: 'check-square' };

/** The tag colour of an activity type. */
export function interactionColor(type: InteractionType): string {
  return TYPE_COLORS[type] ?? 'default';
}

/** The icon of an activity type. */
export function interactionIcon(type: InteractionType): string {
  return TYPE_ICONS[type] ?? 'message';
}

/** The activity as the API takes it: full timestamps, the activity date defaulting to now; a new activity is open. */
export function toInteractionInput(v: InteractionFormValue): Partial<CustomerInteractionInput> {
  return {
    legalEntityId: v.legalEntityId,
    relatedEntityKind: v.relatedEntityKind,
    relatedEntityId: v.relatedEntityId,
    type: v.type,
    subject: v.subject,
    details: v.details,
    interactionDate: v.interactionDate ? new Date(v.interactionDate).toISOString() : new Date().toISOString(),
    followUpDate: v.followUpDate ? new Date(v.followUpDate).toISOString() : null,
    isResolved: false,
  };
}
