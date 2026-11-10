import { OpportunityStage, PipelineStageSummaryDto } from '@core/api/crm.api';

/** The pipeline's stages, in board order. */
export const STAGES: OpportunityStage[] = ['qualification', 'proposal', 'negotiation', 'closedWon', 'closedLost'];

/** The same moves the server allows (CrmTransitions.Opportunity). Won and lost go through the close form, which asks for the final value and reason. */
export const STAGE_MOVES: Record<OpportunityStage, OpportunityStage[]> = {
  qualification: ['proposal', 'closedLost'],
  proposal: ['negotiation', 'qualification', 'closedLost'],
  negotiation: ['closedWon', 'proposal', 'closedLost'],
  closedWon: [],
  closedLost: ['qualification'],
};

/** The win probability each open stage starts with. */
export const STAGE_PROBABILITY: Partial<Record<OpportunityStage, number>> = { qualification: 20, proposal: 50, negotiation: 80 };

/** Won or lost. */
export function isClosedStage(stage: OpportunityStage): boolean {
  return stage === 'closedWon' || stage === 'closedLost';
}

/** "1 deal", "3 open deals". */
export function dealsText(n: number, kind = ''): string {
  return `${n} ${kind ? kind + ' ' : ''}${n === 1 ? 'deal' : 'deals'}`;
}

/** The pipeline's headline figures from the per-stage summaries. */
export interface PipelineTotals {
  openCount: number;
  openValue: number;
  closedCount: number;
  /** Won share of the closed deals, one decimal. */
  winRate: number;
}

export function pipelineTotals(summaries: PipelineStageSummaryDto[]): PipelineTotals {
  const open = summaries.filter((s) => !isClosedStage(s.stage));
  const count = (stage: OpportunityStage) => summaries.find((s) => s.stage === stage)?.count ?? 0;
  const closedCount = count('closedWon') + count('closedLost');
  return {
    openCount: open.reduce((sum, s) => sum + s.count, 0),
    openValue: open.reduce((sum, s) => sum + s.totalExpectedRevenue, 0),
    closedCount,
    winRate: closedCount ? Math.round((count('closedWon') / closedCount) * 1000) / 10 : 0,
  };
}
