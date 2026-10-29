import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';

export type WorkOrderStatus = 'Planned' | 'Released' | 'InProgress' | 'Completed' | 'Cancelled';
export type OperationStatus = 'Pending' | 'InProgress' | 'Completed' | 'Skipped';

export interface BomComponentDto {
  id?: string;
  componentItemId: string;
  componentItemCode: string;
  componentItemName: string;
  quantityPerUnit: number;
  unitOfMeasure: string;
  unitCost: number;
  extendedCost: number;
  sequenceOrder: number;
  scrapPercentage?: number | null;
  notes?: string | null;
}

export interface BomComponentInput {
  componentItemId: string;
  componentItemCode: string;
  componentItemName: string;
  quantityPerUnit: number;
  unitOfMeasure: string;
  unitCost: number;
  sequenceOrder: number;
  scrapPercentage?: number | null;
  notes?: string | null;
}

export interface BomDto {
  id: string;
  legalEntityId: string;
  code: string;
  outputItemId: string;
  outputItemCode: string;
  outputItemName: string;
  baseQuantity: number;
  unitOfMeasure: string;
  standardCostPerUnit: number;
  totalStandardCost: number;
  version: number;
  routingNotes?: string | null;
  isActive: boolean;
  components: BomComponentDto[];
  customFields?: string | null;
  createdAt: string;
}

export interface BomSummaryDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  outputItemId: string;
  outputItemCode: string;
  outputItemName: string;
  baseQuantity: number;
  unitOfMeasure: string;
  standardCostPerUnit: number;
  version: number;
  isActive: boolean;
  componentCount: number;
  createdAt: string;
}

export interface BomInput {
  legalEntityId?: string;
  code: string;
  name: string;
  outputItemId: string;
  outputItemCode: string;
  outputItemName: string;
  baseQuantity: number;
  unitOfMeasure: string;
  version?: number;
  routingNotes?: string | null;
  isActive?: boolean;
  components: BomComponentInput[];
  customFields?: string | null;
}

export interface WorkOrderOperationDto {
  id: string;
  workOrderId: string;
  sequenceNumber: number;
  operationName: string;
  workCenter?: string | null;
  plannedMinutes: number;
  actualMinutes: number;
  status: OperationStatus;
  notes?: string | null;
}

export interface WorkOrderOperationInput {
  sequenceNumber: number;
  operationName: string;
  workCenter?: string | null;
  plannedMinutes: number;
  notes?: string | null;
}

export interface WorkOrderDto {
  id: string;
  legalEntityId: string;
  orderNumber: string;
  workOrderNumber: string;
  bomId: string;
  bomCode: string;
  outputItemId: string;
  outputItemCode: string;
  outputItemName: string;
  plannedQuantity: number;
  completedQuantity: number;
  scrappedQuantity: number;
  uom: string;
  status: WorkOrderStatus;
  plannedStartDate?: string | null;
  plannedCompletionDate?: string | null;
  dueDate?: string | null;
  actualStartDate?: string | null;
  actualCompletionDate?: string | null;
  standardUnitCost: number;
  totalPlannedCost: number;
  actualTotalCost: number;
  productionCostVariance: number;
  warehouseId?: string | null;
  assignedSupervisor?: string | null;
  notes?: string | null;
  operations: WorkOrderOperationDto[];
  createdAt: string;
}

export interface WorkOrderInput {
  legalEntityId?: string;
  bomId: string;
  outputItemId: string;
  plannedQuantity: number;
  plannedStartDate?: string | null;
  plannedCompletionDate?: string | null;
  warehouseId?: string | null;
  assignedSupervisor?: string | null;
  notes?: string | null;
  operations?: WorkOrderOperationInput[];
}

export interface CompleteWorkOrderInput {
  completedQuantity: number;
  scrappedQuantity?: number;
  actualTotalCost: number;
}

export interface CostRollupDto {
  bomId: string;
  bomCode: string;
  outputItemCode: string;
  outputItemName: string;
  baseQuantity: number;
  unitOfMeasure: string;
  componentCost: number;
  laborAndOverheadCost: number;
  totalStandardCost: number;
  calculatedStandardUnitCost: number;
  breakdown: BomComponentDto[];
}

export interface VarianceAnalysisDto {
  workOrderId: string;
  orderNumber: string;
  outputItemCode: string;
  outputItemName: string;
  completedQuantity: number;
  scrappedQuantity: number;
  totalPlannedCost: number;
  actualTotalCost: number;
  varianceAmount: number;
  variancePercent: number;
  isFavorable: boolean;
}

export interface ManufacturingKpisDto {
  activeBoms: number;
  plannedWorkOrders: number;
  inProgressWorkOrders: number;
  completedWorkOrders: number;
  totalPlannedUnits: number;
  totalCompletedUnits: number;
  totalScrappedUnits: number;
  yieldRatePercentage: number;
  monthProductionCost: number;
}

@Injectable({ providedIn: 'root' })
export class ManufacturingBomsApi extends CrudApiService<BomSummaryDto, Partial<BomInput>> {
  protected readonly resource = 'm/manufacturing/boms';

  getDetail(id: string): Observable<{ data: BomDto }> {
    return this.http.get<{ data: BomDto }>(`${this.url}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class ManufacturingWorkOrdersApi extends CrudApiService<WorkOrderDto, Partial<WorkOrderInput>> {
  protected readonly resource = 'm/manufacturing/workorders';

  release(id: string): Observable<{ data: boolean }> {
    return this.http.post<{ data: boolean }>(`${this.url}/${id}/release`, {});
  }

  complete(id: string, payload: CompleteWorkOrderInput): Observable<{ data: boolean }> {
    return this.http.post<{ data: boolean }>(`${this.url}/${id}/complete`, payload);
  }

  cancel(id: string): Observable<{ data: boolean }> {
    return this.http.post<{ data: boolean }>(`${this.url}/${id}/cancel`, {});
  }
}

@Injectable({ providedIn: 'root' })
export class ManufacturingCostingApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  rollup(bomId: string): Observable<{ data: CostRollupDto }> {
    return this.http.get<{ data: CostRollupDto }>(`${this.base}/m/manufacturing/costing/rollup/${bomId}`);
  }

  variances(legalEntityId?: string): Observable<{ data: VarianceAnalysisDto[] }> {
    const params = legalEntityId ? `?legalEntityId=${legalEntityId}` : '';
    return this.http.get<{ data: VarianceAnalysisDto[] }>(`${this.base}/m/manufacturing/costing/variances${params}`);
  }
}

@Injectable({ providedIn: 'root' })
export class ManufacturingKpiApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  getKpis(legalEntityId?: string): Observable<{ data: ManufacturingKpisDto }> {
    const params = legalEntityId ? `?legalEntityId=${legalEntityId}` : '';
    return this.http.get<{ data: ManufacturingKpisDto }>(`${this.base}/m/manufacturing/analytics/kpis${params}`);
  }
}
