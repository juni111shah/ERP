using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class HireCandidateHandler(IReadRepository<JobOpening> openings, IRepository<Candidate> candidates, ISender sender, IClock clock) : ICommandHandler<HireCandidateCommand, HireResult>
{
    public async Task<HireResult> Handle(HireCandidateCommand command, CancellationToken ct)
    {
        // 1. Only a candidate with an offer.
        var c = await candidates.GetForUpdateAsync(command.Id, ct);
        if (c.Stage == CandidateStage.Hired) throw new ConflictException("This candidate was already hired.");
        if (c.Stage != CandidateStage.Offer) throw new ConflictException("Only a candidate with an offer can be hired.");

        // 2. The employee, through the normal employee rules and numbering.
        var h = command.Input;
        var opening = await openings.GetAsync(c.OpeningId, ct);
        var employee = await sender.Send(new CreateEmployeeCommand(new EmployeeInput(
            LegalEntityId: h.LegalEntityId, Number: null, FullName: c.FullName, Designation: h.Designation, Department: null, JoinDate: h.JoinDate, TerminationDate: null, Email: c.Email, Phone: c.Phone,
            Nationality: h.Nationality, Status: EmployeeStatus.Active, BasicSalary: h.BasicSalary, HousingAllowance: h.HousingAllowance, TransportAllowance: h.TransportAllowance, OtherAllowances: h.OtherAllowances,
            RecurringDeductions: 0, Iban: null, ManagerEmployeeId: h.ManagerEmployeeId, DepartmentId: h.DepartmentId ?? opening.DepartmentId, PositionId: h.PositionId ?? opening.PositionId,
            ContractType: h.ContractType, ProbationEndDate: h.ProbationEndDate)), ct);

        // 3. Mark the candidate hired, save, and start the onboarding checklist when one is given.
        c.Stage = CandidateStage.Hired; c.StageChangedAt = clock.UtcNow; c.EmployeeId = employee.Id;
        await candidates.SaveChangesAsync(ct);
        var tasks = 0;
        if (h.OnboardingTemplateId is { } templateId) tasks = (await sender.Send(new ApplyChecklistCommand(employee.Id, templateId), ct)).Count;
        return new HireResult(employee.Id, employee.Number, tasks);
    }
}
