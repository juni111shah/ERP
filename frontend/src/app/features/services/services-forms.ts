import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { EngagementSummaryDto, EngagementType, RetainerCycle } from '@core/api/services.api';
import { toIsoDate } from '@core/format/dates';

/** The end date field must not fall before the start date field of the same form. */
export const notBeforeStart = (startKey: string): ValidatorFn => (c: AbstractControl): ValidationErrors | null => {
  const start = c.parent?.get(startKey)?.value;
  return start && c.value && toIsoDate(new Date(c.value)) < toIsoDate(new Date(start)) ? { beforeStart: true } : null;
};

/** Engagements that can still take milestones and retainers: draft, active or on hold. */
export function openEngagements(engagements: EngagementSummaryDto[]): EngagementSummaryDto[] {
  return engagements.filter((e) => e.status === 'draft' || e.status === 'active' || e.status === 'onHold');
}

/** The new-engagement form's raw value (as it is once valid: a client is chosen). */
export interface EngagementFormValue {
  legalEntityId: string;
  title: string;
  clientId: string;
  clientName: string;
  type: EngagementType;
  contractValue: number;
  budgetedHours: number;
  startDate: Date;
  targetEndDate: Date | null;
  leadConsultantName: string;
  description: string;
}

/** The new-milestone form's raw value. */
export interface MilestoneFormValue {
  engagementId: string;
  title: string;
  amount: number;
  dueDate: Date;
  notes: string;
}

/** The new-retainer form's raw value. */
export interface RetainerFormValue {
  engagementId: string;
  title: string;
  cycle: RetainerCycle;
  recurringFee: number;
  includedHours: number;
  overageHourlyRate: number;
  startDate: Date;
  endDate: Date | null;
  terms: string;
}
