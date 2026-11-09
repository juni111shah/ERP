using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record CandidateInput(Guid OpeningId, string FullName, string? Email = null, string? Phone = null, string? Source = null, decimal? ExpectedSalary = null, string? Notes = null);
public sealed record CandidateDto(Guid Id, Guid OpeningId, string FullName, string? Email, string? Phone, CandidateStage Stage, string? Source, decimal? ExpectedSalary, string? Notes, DateTime StageChangedAt, Guid? EmployeeId)
{
    public static CandidateDto From(Candidate c) => new(c.Id, c.OpeningId, c.FullName, c.Email, c.Phone, c.Stage, c.Source, c.ExpectedSalary, c.Notes, c.StageChangedAt, c.EmployeeId);
}
public sealed record CandidateRow(Guid Id, Guid OpeningId, string FullName, string? Email, CandidateStage Stage, DateTime StageChangedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Candidate, CandidateRow>> Projection = c => new CandidateRow(c.Id, c.OpeningId, c.FullName, c.Email, c.Stage, c.StageChangedAt);
}
public sealed record MoveCandidateInput(CandidateStage Stage, string? Notes = null);
/// <summary>Turns an accepted candidate into an employee (through the normal employee rules and numbering) and optionally starts an onboarding checklist.</summary>
public sealed record HireInput(Guid LegalEntityId, DateOnly JoinDate, string Designation, decimal BasicSalary, decimal HousingAllowance = 0, decimal TransportAllowance = 0, decimal OtherAllowances = 0,
    Guid? DepartmentId = null, Guid? PositionId = null, Guid? ManagerEmployeeId = null, ContractType? ContractType = null, DateOnly? ProbationEndDate = null, string? Nationality = null, Guid? OnboardingTemplateId = null);
public sealed record HireResult(Guid EmployeeId, string EmployeeNumber, int TasksCreated);
