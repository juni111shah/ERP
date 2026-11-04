using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record JobOpeningInput(string Title, Guid? DepartmentId = null, Guid? PositionId = null, int Headcount = 1, OpeningStatus Status = OpeningStatus.Open, string? Description = null, DateOnly? ClosingDate = null);
public sealed record JobOpeningDto(Guid Id, string Title, Guid? DepartmentId, Guid? PositionId, int Headcount, OpeningStatus Status, string? Description, DateOnly? ClosingDate, int Candidates, int Hired);
public sealed record JobOpeningRow(Guid Id, string Title, Guid? DepartmentId, int Headcount, OpeningStatus Status, DateOnly? ClosingDate);
public sealed record CandidateInput(Guid OpeningId, string FullName, string? Email = null, string? Phone = null, string? Source = null, decimal? ExpectedSalary = null, string? Notes = null);
public sealed record CandidateDto(Guid Id, Guid OpeningId, string FullName, string? Email, string? Phone, CandidateStage Stage, string? Source, decimal? ExpectedSalary, string? Notes, DateTime StageChangedAt, Guid? EmployeeId);
public sealed record CandidateRow(Guid Id, Guid OpeningId, string FullName, string? Email, CandidateStage Stage, DateTime StageChangedAt);
public sealed record MoveCandidateInput(CandidateStage Stage, string? Notes = null);
/// <summary>Turns an accepted candidate into an employee (through the normal employee rules and numbering) and optionally starts an onboarding checklist.</summary>
public sealed record HireInput(Guid LegalEntityId, DateOnly JoinDate, string Designation, decimal BasicSalary, decimal HousingAllowance = 0, decimal TransportAllowance = 0, decimal OtherAllowances = 0,
    Guid? DepartmentId = null, Guid? PositionId = null, Guid? ManagerEmployeeId = null, ContractType? ContractType = null, DateOnly? ProbationEndDate = null, string? Nationality = null, Guid? OnboardingTemplateId = null);
public sealed record HireResult(Guid EmployeeId, string EmployeeNumber, int TasksCreated);

public sealed record GetJobOpening(Guid Id) : IQuery<JobOpeningDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentRead; }
public sealed record CreateJobOpening(JobOpeningInput Input) : ICommand<JobOpeningDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
public sealed record UpdateJobOpening(Guid Id, JobOpeningInput Input) : ICommand<JobOpeningDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
public sealed record DeleteJobOpening(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
public sealed record GetCandidate(Guid Id) : IQuery<CandidateDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentRead; }
public sealed record CreateCandidate(CandidateInput Input) : ICommand<CandidateDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
public sealed record UpdateCandidate(Guid Id, CandidateInput Input) : ICommand<CandidateDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
public sealed record MoveCandidate(Guid Id, MoveCandidateInput Input) : ICommand<CandidateDto>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }
public sealed record HireCandidate(Guid Id, HireInput Input) : ICommand<HireResult>, IRequirePermission { public string RequiredPermission => P.RecruitmentManage; }

public sealed class JobOpeningInputValidator : AbstractValidator<JobOpeningInput>
{
    public JobOpeningInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Headcount).InclusiveBetween(1, 500);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Status).IsInEnum();
    }
}
public sealed class CreateJobOpeningValidator : AbstractValidator<CreateJobOpening> { public CreateJobOpeningValidator() => RuleFor(x => x.Input).SetValidator(new JobOpeningInputValidator()); }
public sealed class UpdateJobOpeningValidator : AbstractValidator<UpdateJobOpening> { public UpdateJobOpeningValidator() => RuleFor(x => x.Input).SetValidator(new JobOpeningInputValidator()); }
public sealed class CandidateInputValidator : AbstractValidator<CandidateInput>
{
    public CandidateInputValidator()
    {
        RuleFor(x => x.OpeningId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50); RuleFor(x => x.Source).MaximumLength(100); RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.ExpectedSalary!.Value).GreaterThanOrEqualTo(0).When(x => x.ExpectedSalary is not null).WithName("ExpectedSalary");
    }
}
public sealed class CreateCandidateValidator : AbstractValidator<CreateCandidate> { public CreateCandidateValidator() => RuleFor(x => x.Input).SetValidator(new CandidateInputValidator()); }
public sealed class UpdateCandidateValidator : AbstractValidator<UpdateCandidate> { public UpdateCandidateValidator() => RuleFor(x => x.Input).SetValidator(new CandidateInputValidator()); }
public sealed class HireCandidateValidator : AbstractValidator<HireCandidate>
{
    public HireCandidateValidator()
    {
        RuleFor(x => x.Input.LegalEntityId).NotEmpty();
        RuleFor(x => x.Input.Designation).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Input.BasicSalary).GreaterThan(0);
    }
}

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

internal sealed class RecruitmentHandlers(
    IRepository<JobOpening> openings, IRepository<Candidate> candidates, ISender sender, IClock clock) :
    IRequestHandler<GetJobOpening, JobOpeningDto>,
    IRequestHandler<CreateJobOpening, JobOpeningDto>,
    IRequestHandler<UpdateJobOpening, JobOpeningDto>,
    IRequestHandler<DeleteJobOpening, Unit>,
    IRequestHandler<GetCandidate, CandidateDto>,
    IRequestHandler<CreateCandidate, CandidateDto>,
    IRequestHandler<UpdateCandidate, CandidateDto>,
    IRequestHandler<MoveCandidate, CandidateDto>,
    IRequestHandler<HireCandidate, HireResult>
{
    public async Task<JobOpeningDto> Handle(GetJobOpening q, CancellationToken ct) => await ToDtoAsync(await openings.GetAsync(q.Id, ct), ct);

    public async Task<JobOpeningDto> Handle(CreateJobOpening cmd, CancellationToken ct)
    {
        var o = new JobOpening();
        Fill(o, cmd.Input);
        openings.Add(o);
        await openings.SaveChangesAsync(ct);
        return await ToDtoAsync(o, ct);
    }

    public async Task<JobOpeningDto> Handle(UpdateJobOpening cmd, CancellationToken ct)
    {
        var o = await openings.GetForUpdateAsync(cmd.Id, ct);
        Fill(o, cmd.Input);
        await openings.SaveChangesAsync(ct);
        return await ToDtoAsync(o, ct);
    }

    public async Task<Unit> Handle(DeleteJobOpening cmd, CancellationToken ct)
    {
        var o = await openings.GetForUpdateAsync(cmd.Id, ct);
        var id = o.Id;
        if (await candidates.AnyAsync(c => c.OpeningId == id, ct)) throw new ConflictException("This vacancy has candidates. Close it instead of deleting it.");
        openings.Remove(o);
        await openings.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<CandidateDto> Handle(GetCandidate q, CancellationToken ct) => ToDto(await candidates.GetAsync(q.Id, ct));

    public async Task<CandidateDto> Handle(CreateCandidate cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var opening = await openings.FindAsync(i.OpeningId, ct) ?? throw new FieldValidationException("openingId", "Choose a vacancy.");
        if (opening.Status == OpeningStatus.Closed) throw new ConflictException("This vacancy is closed.");
        var c = new Candidate { OpeningId = opening.Id, Stage = CandidateStage.Applied, StageChangedAt = clock.UtcNow };
        Fill(c, i);
        candidates.Add(c);
        await candidates.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<CandidateDto> Handle(UpdateCandidate cmd, CancellationToken ct)
    {
        var c = await candidates.GetForUpdateAsync(cmd.Id, ct);
        if (cmd.Input.OpeningId != c.OpeningId) throw new FieldValidationException("openingId", "A candidate cannot move to another vacancy.");
        Fill(c, cmd.Input);
        await candidates.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<CandidateDto> Handle(MoveCandidate cmd, CancellationToken ct)
    {
        var c = await candidates.GetForUpdateAsync(cmd.Id, ct);
        if (!CandidatePipeline.CanMove(c.Stage, cmd.Input.Stage)) throw new ConflictException($"A candidate in stage '{c.Stage}' cannot move to '{cmd.Input.Stage}'.");
        c.Stage = cmd.Input.Stage; c.StageChangedAt = clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(cmd.Input.Notes)) c.Notes = string.IsNullOrEmpty(c.Notes) ? cmd.Input.Notes.Trim() : c.Notes + "\n" + cmd.Input.Notes.Trim();
        await candidates.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<HireResult> Handle(HireCandidate cmd, CancellationToken ct)
    {
        var c = await candidates.GetForUpdateAsync(cmd.Id, ct);
        if (c.Stage == CandidateStage.Hired) throw new ConflictException("This candidate was already hired.");
        if (c.Stage != CandidateStage.Offer) throw new ConflictException("Only a candidate with an offer can be hired.");
        var h = cmd.Input;
        var opening = await openings.GetAsync(c.OpeningId, ct);
        var employee = await sender.Send(new CreateEmployee(new EmployeeInput(
            LegalEntityId: h.LegalEntityId, Number: null, FullName: c.FullName, Designation: h.Designation, Department: null, JoinDate: h.JoinDate, TerminationDate: null, Email: c.Email, Phone: c.Phone,
            Nationality: h.Nationality, Status: EmployeeStatus.Active, BasicSalary: h.BasicSalary, HousingAllowance: h.HousingAllowance, TransportAllowance: h.TransportAllowance, OtherAllowances: h.OtherAllowances,
            RecurringDeductions: 0, Iban: null, ManagerEmployeeId: h.ManagerEmployeeId, DepartmentId: h.DepartmentId ?? opening.DepartmentId, PositionId: h.PositionId ?? opening.PositionId,
            ContractType: h.ContractType, ProbationEndDate: h.ProbationEndDate)), ct);
        c.Stage = CandidateStage.Hired; c.StageChangedAt = clock.UtcNow; c.EmployeeId = employee.Id;
        await candidates.SaveChangesAsync(ct);
        var tasks = 0;
        if (h.OnboardingTemplateId is { } templateId) tasks = (await sender.Send(new ApplyChecklist(employee.Id, templateId), ct)).Count;
        return new HireResult(employee.Id, employee.Number, tasks);
    }

    private static void Fill(JobOpening o, JobOpeningInput i)
    {
        o.Title = i.Title.Trim(); o.DepartmentId = i.DepartmentId; o.PositionId = i.PositionId; o.Headcount = i.Headcount; o.Status = i.Status; o.Description = i.Description?.Trim(); o.ClosingDate = i.ClosingDate;
    }

    private static void Fill(Candidate c, CandidateInput i)
    {
        c.FullName = i.FullName.Trim(); c.Email = string.IsNullOrWhiteSpace(i.Email) ? null : i.Email.Trim(); c.Phone = i.Phone?.Trim(); c.Source = i.Source?.Trim(); c.ExpectedSalary = i.ExpectedSalary; c.Notes = i.Notes?.Trim();
    }

    private async Task<JobOpeningDto> ToDtoAsync(JobOpening o, CancellationToken ct)
    {
        var id = o.Id;
        var stages = await candidates.Query(new Specification<Candidate>(c => c.OpeningId == id)).Select(c => c.Stage).ToListAsync(ct);
        return new JobOpeningDto(o.Id, o.Title, o.DepartmentId, o.PositionId, o.Headcount, o.Status, o.Description, o.ClosingDate, stages.Count, stages.Count(s => s == CandidateStage.Hired));
    }

    private static CandidateDto ToDto(Candidate c) => new(c.Id, c.OpeningId, c.FullName, c.Email, c.Phone, c.Stage, c.Source, c.ExpectedSalary, c.Notes, c.StageChangedAt, c.EmployeeId);
}
