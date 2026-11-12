import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';

export type WorkOrderStatus = 'draft' | 'planned' | 'inProgress' | 'completed' | 'cancelled' | 'reversed';
export type OperationStatus = 'pending' | 'inProgress' | 'completed';

export interface BomComponentDto {
  id: string;
  bomId: string;
  componentItemId: string;
  componentItemCode: string;
  componentItemName: string;
  quantityRequired: number;
  uom: string;
  scrapPercentage: number;
  unitCost: number;
  extendedCost: number;
  notes?: string | null;
}

export interface BomComponentInput {
  componentItemId: string;
  componentItemCode: string;
  componentItemName: string;
  quantityRequired: number;
  uom: string;
  scrapPercentage: number;
  unitCost: number;
  notes?: string | null;
}

export interface BomDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  producedItemId: string;
  producedItemCode: string;
  producedItemName: string;
  outputQuantity: number;
  uom: string;
  version: string;
  laborCostPerUnit: number;
  overheadCostPerUnit: number;
  totalMaterialCost: number;
  totalUnitCost: number;
  isActive: boolean;
  description?: string | null;
  componentCount: number;
  components: BomComponentDto[];
  customFields?: string | null;
  createdAt: string;
}

export interface BomSummaryDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  producedItemCode: string;
  producedItemName: string;
  outputQuantity: number;
  uom: string;
  version: string;
  totalUnitCost: number;
  isActive: boolean;
  componentCount: number;
  createdAt: string;
}

/** Create body. Update takes the same fields; the server ignores the legal entity, code and produced item on update. */
export interface BomInput {
  legalEntityId: string;
  code: string;
  name: string;
  producedItemId: string;
  producedItemCode: string;
  producedItemName: string;
  outputQuantity: number;
  uom: string;
  version: string;
  laborCostPerUnit: number;
  overheadCostPerUnit: number;
  isActive: boolean;
  description?: string | null;
  components: BomComponentInput[];
  customFields?: string | null;
}

export interface WorkOrderOperationDto {
  id: string;
  workOrderId: string;
  sequenceOrder: number;
  operationName: string;
  workCenterName: string;
  plannedHours: number;
  actualHours: number;
  hourlyRate: number;
  status: OperationStatus;
}

export interface WorkOrderMaterialIssueDto {
  id: string; itemId: string; itemCode: string; itemName: string; quantity: number; unitCost: number; value: number; issueDate: string; backflushed: boolean; notes?: string | null; reversedAt?: string | null;
}

/** One work order with its operations and the material issued to it. */
export interface WorkOrderDto extends WorkOrderSummaryDto {
  bomId: string; producedItemId: string; scrappedQuantity: number; warehouseId: string; startDate: string; completionDate?: string | null; notes?: string | null;
  operations: WorkOrderOperationDto[]; materialCost: number; conversionCost: number; scrapCost: number; materialIssues: WorkOrderMaterialIssueDto[] | null;
  reversedAt?: string | null; reversedByUserId?: string | null; reversalReason?: string | null;
}

export interface MaterialLineInput { itemId: string; quantity: number; notes?: string | null; }
export interface RequirementRow {
  itemId: string; itemCode: string; itemName: string; uom: string; level: number; isManufactured: boolean; grossRequired: number; onHand: number; onOrder: number; inProduction: number; netRequired: number;
}
export interface RequirementsDto { legalEntityId: string; warehouseId?: string | null; openWorkOrders: number; openPurchaseNetted: boolean; rows: RequirementRow[]; shortages: RequirementRow[]; }

export interface WorkOrderSummaryDto {
  id: string;
  legalEntityId: string;
  workOrderNumber: string;
  bomCode: string;
  producedItemCode: string;
  producedItemName: string;
  plannedQuantity: number;
  completedQuantity: number;
  uom: string;
  dueDate: string;
  status: WorkOrderStatus;
  estimatedTotalCost: number;
  actualTotalCost: number;
  createdAt: string;
}

export interface WorkOrderInput {
  legalEntityId: string;
  bomId: string;
  plannedQuantity: number;
  warehouseId: string;
  startDate: string;
  dueDate: string;
  notes?: string | null;
}

export interface CompleteWorkOrderInput {
  completedQuantity: number;
  scrappedQuantity: number;
  /** The day the goods came out of production. Left out, today. */
  completionDate?: string | null;
  /** Hours actually worked per operation, recorded together with the completion. */
  operationHours?: { operationId: string; actualHours: number }[];
  /** Required by the request, but the server works out the actual cost itself from the stock issued and the BOM. */
  actualTotalCost: number;
  notes?: string | null;
}

export interface CostComponentBreakdownDto {
  componentItemId: string;
  componentItemCode: string;
  componentItemName: string;
  quantityRequired: number;
  unitCost: number;
  totalCost: number;
  costSharePercentage: number;
}

export interface CostRollupDto {
  producedItemId: string;
  producedItemCode: string;
  producedItemName: string;
  outputQuantity: number;
  materialCost: number;
  laborCost: number;
  overheadCost: number;
  totalStandardUnitCost: number;
  componentBreakdown: CostComponentBreakdownDto[];
}

export interface CostVarianceDto {
  itemId: string;
  itemCode: string;
  itemName: string;
  standardUnitCost: number;
  actualAverageUnitCost: number;
  varianceAmount: number;
  variancePercentage: number;
  completedWorkOrdersCount: number;
}

export interface ManufacturingKpisDto {
  activeBomsCount: number;
  openWorkOrdersCount: number;
  completedWorkOrdersMonth: number;
  totalProducedQuantityMonth: number;
  averageYieldRatePercentage: number;
  totalManufacturingCostMonth: number;
}

/** Bills of materials. */
@Injectable({ providedIn: 'root' })
export class ManufacturingBomsApi extends CrudApiService<BomSummaryDto, BomInput> {
  protected readonly resource = 'm/manufacturing/boms';

  getDetail(id: string): Observable<BomDto> {
    return this.http.get<BomDto>(`${this.url}/${id}`);
  }
}

/** Work orders: CRUD, release, material issue and return, hours, completion, reversal and requirements. */
@Injectable({ providedIn: 'root' })
export class ManufacturingWorkOrdersApi extends CrudApiService<WorkOrderSummaryDto, WorkOrderInput> {
  protected readonly resource = 'm/manufacturing/workorders';
  private readonly base = inject(API_BASE_URL);

  detail(id: string): Observable<WorkOrderDto> {
    return this.http.get<WorkOrderDto>(`${this.url}/${id}`);
  }

  release(id: string): Observable<unknown> {
    return this.http.post<unknown>(`${this.url}/${id}/release`, {});
  }

  /** Puts material into the order. Without lines it issues what the bill of materials still needs. */
  issueMaterials(id: string, body: { lines?: MaterialLineInput[]; date?: string | null; notes?: string | null }): Observable<WorkOrderDto> {
    return this.http.post<WorkOrderDto>(`${this.url}/${id}/issue`, body);
  }

  returnMaterials(id: string, body: { lines: MaterialLineInput[]; date?: string | null; notes?: string | null }): Observable<WorkOrderDto> {
    return this.http.post<WorkOrderDto>(`${this.url}/${id}/return-materials`, body);
  }

  recordHours(id: string, operationId: string, actualHours: number, status?: OperationStatus | null): Observable<WorkOrderDto> {
    return this.http.put<WorkOrderDto>(`${this.url}/${id}/operations/${operationId}/hours`, { actualHours, status: status ?? null });
  }

  /** Completed orders only. The reason (up to 500 characters) goes to the audit log. */
  reverse(id: string, reason: string): Observable<WorkOrderDto> {
    return this.http.post<WorkOrderDto>(`${this.url}/${id}/reverse`, { reason });
  }

  requirements(legalEntityId: string, opts: { warehouseId?: string | null; itemId?: string | null; quantity?: number | null } = {}): Observable<RequirementsDto> {
    let params = new HttpParams().set('legalEntityId', legalEntityId);
    if (opts.warehouseId) params = params.set('warehouseId', opts.warehouseId);
    if (opts.itemId && opts.quantity) params = params.set('itemId', opts.itemId).set('quantity', opts.quantity);
    return this.http.get<RequirementsDto>(`${this.base}/m/manufacturing/requirements`, { params });
  }

  complete(id: string, payload: CompleteWorkOrderInput): Observable<unknown> {
    return this.http.post<unknown>(`${this.url}/${id}/complete`, payload);
  }

  cancel(id: string, reason?: string | null): Observable<unknown> {
    return this.http.post<unknown>(`${this.url}/${id}/cancel`, { reason: reason || null });
  }
}

/** Standard cost roll-ups and cost variances. */
@Injectable({ providedIn: 'root' })
export class ManufacturingCostingApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  rollup(bomId: string): Observable<CostRollupDto | null> {
    return this.http.get<CostRollupDto | null>(`${this.base}/m/manufacturing/costing/rollup/${bomId}`);
  }

  /** Standard against actual unit cost of one produced item, across its completed work orders. */
  variance(legalEntityId: string, itemId: string): Observable<CostVarianceDto | null> {
    const params = new HttpParams().set('legalEntityId', legalEntityId).set('itemId', itemId);
    return this.http.get<CostVarianceDto | null>(`${this.base}/m/manufacturing/costing/variance`, { params });
  }
}

/** The manufacturing KPI summary. */
@Injectable({ providedIn: 'root' })
export class ManufacturingKpiApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  getKpis(): Observable<ManufacturingKpisDto> {
    return this.http.get<ManufacturingKpisDto>(`${this.base}/m/manufacturing/kpis`);
  }
}
