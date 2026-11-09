using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Which stage moves are allowed: forward through the pipeline, or out (rejected); hiring has its own command.</summary>
public static class CandidatePipeline
{
    public static bool CanMove(CandidateStage from, CandidateStage to)
    {
        if (from is CandidateStage.Hired) return false;                      // done
        if (to == CandidateStage.Hired) return false;                         // only through HireCandidate
        if (from == CandidateStage.Rejected) return to == CandidateStage.Applied; // a rejected candidate can be reopened
        return to == CandidateStage.Rejected || to > from || to == CandidateStage.Applied;
    }
}
