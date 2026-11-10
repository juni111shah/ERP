using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record JobOpeningInput(string Title, Guid? DepartmentId = null, Guid? PositionId = null, int Headcount = 1, OpeningStatus Status = OpeningStatus.Open, string? Description = null, DateOnly? ClosingDate = null);
public sealed record JobOpeningDto(Guid Id, string Title, Guid? DepartmentId, Guid? PositionId, int Headcount, OpeningStatus Status, string? Description, DateOnly? ClosingDate, int Candidates, int Hired)
{
    /// <summary>The vacancy with how many candidates it has (and how many were hired), from the stages of its candidates.</summary>
    public static JobOpeningDto From(JobOpening o, IReadOnlyCollection<CandidateStage> stages)
        => new(o.Id, o.Title, o.DepartmentId, o.PositionId, o.Headcount, o.Status, o.Description, o.ClosingDate, stages.Count, stages.Count(s => s == CandidateStage.Hired));
}
public sealed record JobOpeningRow(Guid Id, string Title, Guid? DepartmentId, int Headcount, OpeningStatus Status, DateOnly? ClosingDate)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<JobOpening, JobOpeningRow>> Projection = o => new JobOpeningRow(o.Id, o.Title, o.DepartmentId, o.Headcount, o.Status, o.ClosingDate);
}
