import { CompleteWorkOrderInput, WorkOrderInput, WorkOrderSummaryDto } from '@core/api/manufacturing.api';
import { toIsoDate } from '@core/format/dates';

/** The new-work-order form's raw value. */
export interface WorkOrderFormValue {
  legalEntityId: string;
  bomId: string | null;
  plannedQuantity: number;
  warehouseId: string | null;
  startDate: Date | null;
  dueDate: Date | null;
  notes: string;
}

/** The complete-work-order form's raw value. */
export interface CompleteFormValue {
  completedQuantity: number;
  scrappedQuantity: number | null;
  completionDate: Date | null;
  notes: string;
}

/** A work order can be cancelled while open, and only until something was produced. */
export function canCancelWorkOrder(row: Pick<WorkOrderSummaryDto, 'status' | 'completedQuantity'>): boolean {
  return (row.status === 'draft' || row.status === 'planned' || row.status === 'inProgress') && row.completedQuantity === 0;
}

/** The due day falls before the start day (compared as calendar days). */
export function dueBeforeStart(startDate: Date | null, dueDate: Date | null): boolean {
  return !!startDate && !!dueDate && toIsoDate(new Date(dueDate)) < toIsoDate(new Date(startDate));
}

/** The new work order as the API takes it (the form only submits complete). */
export function toWorkOrderInput(v: WorkOrderFormValue): WorkOrderInput {
  return {
    legalEntityId: v.legalEntityId,
    bomId: v.bomId as string,
    plannedQuantity: v.plannedQuantity,
    warehouseId: v.warehouseId as string,
    startDate: toIsoDate(new Date(v.startDate as Date)),
    dueDate: toIsoDate(new Date(v.dueDate as Date)),
    notes: v.notes || null,
  };
}

/** The completion as the API takes it; the server works out the actual cost, so the estimate is sent. */
export function toCompleteInput(v: CompleteFormValue, order: WorkOrderSummaryDto): CompleteWorkOrderInput {
  return {
    completedQuantity: v.completedQuantity,
    scrappedQuantity: v.scrappedQuantity || 0,
    actualTotalCost: order.estimatedTotalCost,
    completionDate: v.completionDate ? toIsoDate(v.completionDate) : null,
    notes: v.notes || null,
  };
}
