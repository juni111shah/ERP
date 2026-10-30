using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

public sealed record ProjectBillingSummaryDto(
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    ProjectBillingType BillingType,
    decimal UnbilledHours,
    decimal UnbilledHoursAmount,
    int AchievedMilestonesCount,
    decimal AchievedMilestonesAmount,
    decimal TotalUnbilledAmount);

public sealed record BillProjectInput(
    Guid ProjectId,
    IReadOnlyList<Guid>? TimesheetIds = null,
    IReadOnlyList<Guid>? MilestoneIds = null);

public sealed record BillProjectResultDto(
    Guid ProjectId,
    int BilledTimesheetsCount,
    int BilledMilestonesCount,
    decimal TotalAmountBilled,
    Guid? InvoiceId = null,
    string? InvoiceNumber = null);

public sealed record GetBillingOverview() : IQuery<IReadOnlyList<ProjectBillingSummaryDto>>, IRequirePermission
{
    public string RequiredPermission => P.BillingRead;
}

public sealed record BillProject(BillProjectInput Input) : ICommand<BillProjectResultDto>, IRequirePermission
{
    public string RequiredPermission => P.BillingManage;
}

public sealed class BillProjectValidator : AbstractValidator<BillProject>
{
    public BillProjectValidator()
    {
        RuleFor(x => x.Input.ProjectId).NotEmpty();
    }
}

internal sealed class BillingHandlers(
    IRepository<Project> projects,
    IRepository<ProjectTimesheet> timesheets,
    IRepository<ProjectMilestone> milestones,
    IReadRepository<TaxCode> taxCodes,
    ISender sender,
    IUnitOfWork uow) :
    IRequestHandler<GetBillingOverview, IReadOnlyList<ProjectBillingSummaryDto>>,
    IRequestHandler<BillProject, BillProjectResultDto>
{
    public async Task<IReadOnlyList<ProjectBillingSummaryDto>> Handle(GetBillingOverview q, CancellationToken ct)
    {
        // Only the columns the summary needs, grouped by project once (no per-project rescans of whole tables).
        var unbilled = (await timesheets.Query(new Specification<ProjectTimesheet>(x => x.IsBillable && !x.IsBilled)).Select(x => new { x.ProjectId, x.Hours, x.TotalAmount }).ToListAsync(ct))
            .GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => (Hours: g.Sum(x => x.Hours), Amount: g.Sum(x => x.TotalAmount)));
        var achieved = (await milestones.Query(new Specification<ProjectMilestone>(x => x.Status == MilestoneStatus.Achieved)).Select(x => new { x.ProjectId, x.Amount }).ToListAsync(ct))
            .GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => (Count: g.Count(), Amount: g.Sum(x => x.Amount)));
        var billable = unbilled.Keys.Concat(achieved.Keys).ToHashSet();
        var allProjects = await projects.Query(new Specification<Project>(p => p.Status == ProjectStatus.Active || billable.Contains(p.Id)))
            .OrderBy(p => p.Name).Select(p => new { p.Id, p.Number, p.Name, p.BillingType, p.Status }).ToListAsync(ct);

        var list = new List<ProjectBillingSummaryDto>();
        foreach (var p in allProjects)
        {
            if (p.BillingType == ProjectBillingType.NonBillable) continue;
            var (hours, hoursAmount) = p.BillingType == ProjectBillingType.FixedFee ? default : unbilled.GetValueOrDefault(p.Id); // a fixed-fee project bills milestones only
            var (msCount, msAmount) = achieved.GetValueOrDefault(p.Id);
            var total = hoursAmount + msAmount;
            if (total > 0 || p.Status == ProjectStatus.Active)
                list.Add(new ProjectBillingSummaryDto(p.Id, p.Number, p.Name, p.BillingType, hours, hoursAmount, msCount, msAmount, total));
        }

        return list;
    }

    public async Task<BillProjectResultDto> Handle(BillProject cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);

        if (p.BillingType == ProjectBillingType.NonBillable)
            throw new ConflictException($"Project '{p.Name}' is non-billable and cannot be invoiced.");
        if (p.Status is ProjectStatus.Planning or ProjectStatus.Cancelled)
            throw new ConflictException($"Project '{p.Name}' is {p.Status.ToString().ToLowerInvariant()} and cannot be invoiced.");

        if (!p.CustomerId.HasValue)
        {
            throw new FieldValidationException("customerId", $"Project '{p.Name}' has no customer assigned. An accounting invoice cannot be generated.");
        }

        var tsQuery = timesheets.Tracked(new Specification<ProjectTimesheet>(x => x.ProjectId == p.Id && x.IsBillable && !x.IsBilled));
        if (i.TimesheetIds != null && i.TimesheetIds.Count > 0)
        {
            var ids = i.TimesheetIds.ToHashSet();
            tsQuery = tsQuery.Where(x => ids.Contains(x.Id));
        }
        var tsToBill = p.BillingType == ProjectBillingType.FixedFee ? new List<ProjectTimesheet>() : await tsQuery.ToListAsync(ct); // a fixed-fee project bills milestones only

        var msQuery = milestones.Tracked(new Specification<ProjectMilestone>(x => x.ProjectId == p.Id && x.Status == MilestoneStatus.Achieved));
        if (i.MilestoneIds != null && i.MilestoneIds.Count > 0)
        {
            var ids = i.MilestoneIds.ToHashSet();
            msQuery = msQuery.Where(x => ids.Contains(x.Id));
        }
        var msToBill = await msQuery.ToListAsync(ct);

        if (tsToBill.Count == 0 && msToBill.Count == 0)
        {
            throw new ConflictException("No unbilled timesheets or achieved milestones found to bill.");
        }

        var defaultTax = await taxCodes.Query(new Specification<TaxCode>(t => t.IsActive && t.IsDefault && (t.LegalEntityId == null || t.LegalEntityId == p.LegalEntityId))).FirstOrDefaultAsync(ct);
        var defaultTaxCodeId = defaultTax?.Id;

        decimal totalAmount = 0m;
        var invoiceLines = new List<InvoiceLineInput>();

        foreach (var ts in tsToBill)
        {
            totalAmount += ts.TotalAmount;
            invoiceLines.Add(new InvoiceLineInput(
                $"{p.Number}: {ts.Description ?? "Timesheet"} ({ts.Hours:0.##} hrs @ {ts.HourlyRate:0.##})",
                ts.Hours,
                ts.HourlyRate,
                TaxCodeId: defaultTaxCodeId));
        }

        foreach (var ms in msToBill)
        {
            totalAmount += ms.Amount;
            invoiceLines.Add(new InvoiceLineInput(
                $"{p.Number}: {ms.Title} (Milestone)",
                1,
                ms.Amount,
                TaxCodeId: defaultTaxCodeId));
        }

        return await uow.InTransactionAsync(async transactionCt =>
        {
            var invoice = await sender.Send(new CreateInvoice(new InvoiceInput(
                LegalEntityId: p.LegalEntityId,
                PartyId: p.CustomerId.Value,
                InvoiceDate: DateOnly.FromDateTime(DateTime.UtcNow),
                DueDate: null,
                Lines: invoiceLines,
                Notes: $"Generated for Project {p.Number}: {p.Name}",
                SourceType: "projects.project",
                SourceId: p.Id)), transactionCt);

            foreach (var ts in tsToBill)
            {
                ts.IsBilled = true;
                ts.InvoiceId = invoice.Id;
            }

            foreach (var ms in msToBill)
            {
                ProjectsTransitions.Milestone.Ensure(ms.Status, MilestoneStatus.Invoiced, isSystem: true);
                ms.Status = MilestoneStatus.Invoiced;
            }

            await timesheets.SaveChangesAsync(transactionCt);
            await milestones.SaveChangesAsync(transactionCt);

            return new BillProjectResultDto(p.Id, tsToBill.Count, msToBill.Count, totalAmount, invoice.Id, invoice.Number);
        }, ct);
    }
}
