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
    decimal TotalUnbilledAmount,
    decimal UnbilledExpensesAmount = 0m,
    decimal AwaitingApprovalAmount = 0m,
    decimal FixedFeeRemaining = 0m);

/// <summary>
/// What to put on the invoice. Grouping: "detailed" (a line per entry), "person", "task" or "project" (one line each). DiscountPercent adds a
/// discount line. ProgressPercent (fixed-fee projects only) bills that share of the fee without a milestone.
/// </summary>
public sealed record BillProjectInput(
    Guid ProjectId,
    IReadOnlyList<Guid>? TimesheetIds = null,
    IReadOnlyList<Guid>? MilestoneIds = null,
    IReadOnlyList<Guid>? ExpenseIds = null,
    string Grouping = "detailed",
    decimal DiscountPercent = 0m,
    decimal? ProgressPercent = null,
    string? Notes = null,
    DateOnly? DueDate = null);

public sealed record BillProjectResultDto(
    Guid ProjectId,
    int BilledTimesheetsCount,
    int BilledMilestonesCount,
    decimal TotalAmountBilled,
    Guid? InvoiceId = null,
    string? InvoiceNumber = null,
    int BilledExpensesCount = 0);

public sealed record BillPreviewLineDto(string Description, decimal Quantity, decimal Price, decimal Amount);
public sealed record BillPreviewDto(Guid ProjectId, IReadOnlyList<BillPreviewLineDto> Lines, decimal Subtotal, decimal Discount, decimal Total, int Timesheets, int Milestones, int Expenses);

public sealed record GetBillingOverview() : IQuery<IReadOnlyList<ProjectBillingSummaryDto>>, IRequirePermission
{
    public string RequiredPermission => P.BillingRead;
}

public sealed record BillProject(BillProjectInput Input) : ICommand<BillProjectResultDto>, IRequirePermission
{
    public string RequiredPermission => P.BillingManage;
}

/// <summary>Shows the invoice lines that billing would create, without creating anything.</summary>
public sealed record PreviewProjectBill(BillProjectInput Input) : IQuery<BillPreviewDto>, IRequirePermission
{
    public string RequiredPermission => P.BillingRead;
}

/// <summary>Marks unbilled expenses as not to be charged.</summary>
public sealed record WriteOffExpenses(IReadOnlyList<Guid> Ids) : ICommand<int>, IRequirePermission
{
    public string RequiredPermission => P.BillingManage;
}

public sealed class BillProjectInputValidator : AbstractValidator<BillProjectInput>
{
    public BillProjectInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Grouping).Must(g => g is "detailed" or "person" or "task" or "project").WithMessage("Group by detailed, person, task or project.");
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.ProgressPercent).GreaterThan(0).LessThanOrEqualTo(100).When(x => x.ProgressPercent != null);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class BillProjectValidator : AbstractValidator<BillProject> { public BillProjectValidator() => RuleFor(x => x.Input).SetValidator(new BillProjectInputValidator()); }
public sealed class PreviewProjectBillValidator : AbstractValidator<PreviewProjectBill> { public PreviewProjectBillValidator() => RuleFor(x => x.Input).SetValidator(new BillProjectInputValidator()); }
public sealed class WriteOffExpensesValidator : AbstractValidator<WriteOffExpenses> { public WriteOffExpensesValidator() => RuleFor(x => x.Ids).NotEmpty().Must(i => i.Count <= 500); }

/// <summary>Turns billable work into invoice lines; no I/O.</summary>
public static class BillingLines
{
    public sealed record Line(string Description, decimal Quantity, decimal Price);

    public static decimal Charge(ProjectExpense e) => ProjectMath.Round(e.Amount * (1m + e.MarkupPercent / 100m));

    public static List<Line> Build(Project p, IReadOnlyList<ProjectTimesheet> time, IReadOnlyList<ProjectMilestone> milestones, IReadOnlyList<ProjectExpense> expenses,
        IReadOnlyDictionary<Guid, string> taskTitles, string grouping, decimal? progressAmount, decimal? progressPercent)
    {
        var lines = new List<Line>();
        switch (grouping)
        {
            case "person":
                foreach (var g in time.GroupBy(t => t.WorkerName).OrderBy(g => g.Key))
                    lines.Add(new($"{p.Number}: {g.Key} ({g.Sum(t => t.Hours):0.##} hrs)", 1, g.Sum(t => t.TotalAmount)));
                break;
            case "task":
                foreach (var g in time.GroupBy(t => t.TaskId).OrderBy(g => g.Key.HasValue ? taskTitles.GetValueOrDefault(g.Key.Value) : ""))
                    lines.Add(new($"{p.Number}: {(g.Key is { } id && taskTitles.TryGetValue(id, out var title) ? title : "General work")} ({g.Sum(t => t.Hours):0.##} hrs)", 1, g.Sum(t => t.TotalAmount)));
                break;
            case "project":
                if (time.Count > 0) lines.Add(new($"{p.Number}: {p.Name}, professional time ({time.Sum(t => t.Hours):0.##} hrs)", 1, time.Sum(t => t.TotalAmount)));
                break;
            default:
                foreach (var ts in time)
                    lines.Add(new($"{p.Number}: {ts.Description ?? "Timesheet"} ({ts.Hours:0.##} hrs @ {ts.HourlyRate:0.##})", ts.Hours, ts.HourlyRate));
                break;
        }
        foreach (var e in expenses) lines.Add(new($"{p.Number}: {e.Category}, {e.Description}", 1, Charge(e)));
        foreach (var m in milestones) lines.Add(new($"{p.Number}: {m.Title} (Milestone)", 1, m.Amount));
        if (progressAmount is { } pa && pa > 0) lines.Add(new($"{p.Number}: progress billing {progressPercent:0.##}% of the fixed fee", 1, pa));
        return lines.Where(l => l.Price != 0 || l.Quantity != 0).ToList();
    }

    public static decimal Discount(decimal subtotal, decimal percent) => percent <= 0 ? 0m : ProjectMath.Round(subtotal * percent / 100m);
}

internal sealed class BillingHandlers(
    IRepository<Project> projects,
    IRepository<ProjectTimesheet> timesheets,
    IRepository<ProjectMilestone> milestones,
    IRepository<ProjectExpense> expenses,
    IRepository<ProjectProgressBilling> progressRows,
    IReadRepository<ProjectChangeOrder> changeOrders,
    IReadRepository<ProjectTask> tasks,
    IReadRepository<TaxCode> taxCodes,
    ISender sender,
    IUnitOfWork uow) :
    IRequestHandler<GetBillingOverview, IReadOnlyList<ProjectBillingSummaryDto>>,
    IRequestHandler<BillProject, BillProjectResultDto>,
    IRequestHandler<PreviewProjectBill, BillPreviewDto>,
    IRequestHandler<WriteOffExpenses, int>
{
    public async Task<IReadOnlyList<ProjectBillingSummaryDto>> Handle(GetBillingOverview q, CancellationToken ct)
    {
        // Only the columns the summary needs, grouped by project once (no per-project rescans of whole tables).
        var approvedTime = (await timesheets.Query(new Specification<ProjectTimesheet>(x => x.IsBillable && !x.IsBilled && !x.WrittenOff)).Select(x => new { x.ProjectId, x.Hours, x.TotalAmount, x.ApprovalStatus }).ToListAsync(ct));
        var unbilled = approvedTime.Where(x => x.ApprovalStatus == TimesheetStatus.Approved).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => (Hours: g.Sum(x => x.Hours), Amount: g.Sum(x => x.TotalAmount)));
        var waiting = approvedTime.Where(x => x.ApprovalStatus != TimesheetStatus.Approved).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount));
        var achieved = (await milestones.Query(new Specification<ProjectMilestone>(x => x.Status == MilestoneStatus.Achieved)).Select(x => new { x.ProjectId, x.Amount }).ToListAsync(ct))
            .GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => (Count: g.Count(), Amount: g.Sum(x => x.Amount)));
        var costs = (await expenses.Query(new Specification<ProjectExpense>(x => x.IsBillable && !x.IsBilled && !x.WrittenOff && x.Status == ExpenseStatus.Approved)).ToListAsync(ct))
            .GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(BillingLines.Charge));
        var billable = unbilled.Keys.Concat(achieved.Keys).Concat(costs.Keys).ToHashSet();
        var allProjects = await projects.Query(new Specification<Project>(p => !p.IsTemplate && !p.IsArchived && (p.Status == ProjectStatus.Active || billable.Contains(p.Id))))
            .OrderBy(p => p.Name).Select(p => new { p.Id, p.Number, p.Name, p.BillingType, p.Status, p.BudgetAmount }).ToListAsync(ct);
        var billedFixed = (await progressRows.Query(new Specification<ProjectProgressBilling>(x => !x.Released)).ToListAsync(ct)).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var invoicedMs = (await milestones.Query(new Specification<ProjectMilestone>(x => x.Status == MilestoneStatus.Invoiced)).Select(x => new { x.ProjectId, x.Amount }).ToListAsync(ct)).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var changes = (await changeOrders.Query(new Specification<ProjectChangeOrder>(x => x.Status == ChangeOrderStatus.Approved)).ToListAsync(ct)).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.AmountChange));
        var list = new List<ProjectBillingSummaryDto>();
        foreach (var p in allProjects)
        {
            if (p.BillingType == ProjectBillingType.NonBillable) continue;
            var (hours, hoursAmount) = p.BillingType == ProjectBillingType.FixedFee ? default : unbilled.GetValueOrDefault(p.Id); // a fixed-fee project bills milestones only
            var (msCount, msAmount) = achieved.GetValueOrDefault(p.Id);
            var expenseAmount = costs.GetValueOrDefault(p.Id);
            var remaining = p.BillingType == ProjectBillingType.FixedFee
                ? Math.Max(0m, p.BudgetAmount + changes.GetValueOrDefault(p.Id) - billedFixed.GetValueOrDefault(p.Id) - invoicedMs.GetValueOrDefault(p.Id) - msAmount) : 0m;
            var total = hoursAmount + msAmount + expenseAmount;
            if (total > 0 || p.Status == ProjectStatus.Active)
                list.Add(new ProjectBillingSummaryDto(p.Id, p.Number, p.Name, p.BillingType, hours, hoursAmount, msCount, msAmount, total, expenseAmount, p.BillingType == ProjectBillingType.FixedFee ? 0m : waiting.GetValueOrDefault(p.Id), remaining));
        }
        return list;
    }

    public async Task<BillPreviewDto> Handle(PreviewProjectBill q, CancellationToken ct)
    {
        var plan = await PlanAsync(q.Input, ct);
        var lines = plan.Lines.Select(l => new BillPreviewLineDto(l.Description, l.Quantity, l.Price, ProjectMath.Round(l.Quantity * l.Price))).ToList();
        return new BillPreviewDto(plan.Project.Id, lines, plan.Subtotal, plan.Discount, plan.Subtotal - plan.Discount, plan.Time.Count, plan.Milestones.Count, plan.Expenses.Count);
    }

    public async Task<BillProjectResultDto> Handle(BillProject cmd, CancellationToken ct)
    {
        var plan = await PlanAsync(cmd.Input, ct);
        var p = plan.Project; var i = cmd.Input;
        var taxId = p.TaxCodeId ?? (await taxCodes.Query(new Specification<TaxCode>(t => t.IsActive && t.IsDefault && (t.LegalEntityId == null || t.LegalEntityId == p.LegalEntityId))).FirstOrDefaultAsync(ct))?.Id;
        var invoiceLines = plan.Lines.Select(l => new InvoiceLineInput(l.Description, l.Quantity, l.Price, TaxCodeId: taxId)).ToList();
        if (plan.Discount > 0) invoiceLines.Add(new InvoiceLineInput($"{p.Number}: discount {i.DiscountPercent:0.##}%", 1, -plan.Discount, TaxCodeId: taxId));

        return await uow.InTransactionAsync(async transactionCt =>
        {
            var notes = string.IsNullOrWhiteSpace(i.Notes) ? $"Generated for Project {p.Number}: {p.Name}" : $"{i.Notes.Trim()} (Project {p.Number}: {p.Name})";
            var invoice = await sender.Send(new CreateInvoice(new InvoiceInput(
                LegalEntityId: p.LegalEntityId, PartyId: p.CustomerId!.Value, InvoiceDate: DateOnly.FromDateTime(DateTime.UtcNow), DueDate: i.DueDate, Lines: invoiceLines,
                Notes: notes, SourceType: "projects.project", SourceId: p.Id)), transactionCt);
            foreach (var ts in plan.Time) { ts.IsBilled = true; ts.InvoiceId = invoice.Id; }
            foreach (var e in plan.Expenses) { e.IsBilled = true; e.InvoiceId = invoice.Id; }
            foreach (var ms in plan.Milestones)
            {
                ProjectsTransitions.Milestone.Ensure(ms.Status, MilestoneStatus.Invoiced, isSystem: true);
                ms.Status = MilestoneStatus.Invoiced; ms.InvoiceId = invoice.Id;
            }
            if (plan.ProgressAmount > 0)
                progressRows.Add(new ProjectProgressBilling { ProjectId = p.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow), Percent = i.ProgressPercent ?? 0m, Amount = plan.ProgressAmount, InvoiceId = invoice.Id });
            await timesheets.SaveChangesAsync(transactionCt);
            await expenses.SaveChangesAsync(transactionCt);
            await milestones.SaveChangesAsync(transactionCt);
            await progressRows.SaveChangesAsync(transactionCt);
            return new BillProjectResultDto(p.Id, plan.Time.Count, plan.Milestones.Count, plan.Subtotal - plan.Discount, invoice.Id, invoice.Number, plan.Expenses.Count);
        }, ct);
    }

    public async Task<int> Handle(WriteOffExpenses cmd, CancellationToken ct)
    {
        var rows = await expenses.Tracked(new Specification<ProjectExpense>(e => cmd.Ids.Contains(e.Id))).ToListAsync(ct);
        if (rows.Count != cmd.Ids.Distinct().Count()) throw new NotFoundException("One of the expenses was not found.");
        if (rows.Any(e => e.IsBilled)) throw new ConflictException("Billed expenses cannot be written off. Void the invoice first.");
        foreach (var e in rows) e.WrittenOff = true;
        await expenses.SaveChangesAsync(ct);
        return rows.Count;
    }

    private sealed record Plan(Project Project, List<ProjectTimesheet> Time, List<ProjectMilestone> Milestones, List<ProjectExpense> Expenses, List<BillingLines.Line> Lines, decimal Subtotal, decimal Discount, decimal ProgressAmount);

    /// <summary>Checks the project and picks what is billable: approved, unbilled, not written off.</summary>
    private async Task<Plan> PlanAsync(BillProjectInput i, CancellationToken ct)
    {
        var p = await projects.GetAsync(i.ProjectId, ct);
        if (p.IsTemplate) throw new ConflictException($"'{p.Name}' is a template and cannot be invoiced.");
        if (p.BillingType == ProjectBillingType.NonBillable)
            throw new ConflictException($"Project '{p.Name}' is non-billable and cannot be invoiced.");
        if (p.Status is ProjectStatus.Planning or ProjectStatus.Cancelled)
            throw new ConflictException($"Project '{p.Name}' is {p.Status.ToString().ToLowerInvariant()} and cannot be invoiced.");
        if (!p.CustomerId.HasValue)
            throw new FieldValidationException("customerId", $"Project '{p.Name}' has no customer assigned. An accounting invoice cannot be generated.");
        if (i.ProgressPercent is not null && p.BillingType != ProjectBillingType.FixedFee)
            throw new FieldValidationException("progressPercent", "Progress billing is only for fixed-fee projects.");

        var tsQuery = timesheets.Tracked(new Specification<ProjectTimesheet>(x => x.ProjectId == p.Id && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.ApprovalStatus == TimesheetStatus.Approved));
        if (i.TimesheetIds is { Count: > 0 }) { var ids = i.TimesheetIds.ToHashSet(); tsQuery = tsQuery.Where(x => ids.Contains(x.Id)); }
        var time = p.BillingType == ProjectBillingType.FixedFee ? new List<ProjectTimesheet>() : await tsQuery.OrderBy(x => x.Date).ToListAsync(ct); // a fixed-fee project bills milestones only

        var msQuery = milestones.Tracked(new Specification<ProjectMilestone>(x => x.ProjectId == p.Id && x.Status == MilestoneStatus.Achieved));
        if (i.MilestoneIds is { Count: > 0 }) { var ids = i.MilestoneIds.ToHashSet(); msQuery = msQuery.Where(x => ids.Contains(x.Id)); }
        var ms = await msQuery.ToListAsync(ct);

        var exQuery = expenses.Tracked(new Specification<ProjectExpense>(x => x.ProjectId == p.Id && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.Status == ExpenseStatus.Approved));
        if (i.ExpenseIds is { Count: > 0 }) { var ids = i.ExpenseIds.ToHashSet(); exQuery = exQuery.Where(x => ids.Contains(x.Id)); }
        var ex = await exQuery.OrderBy(x => x.Date).ToListAsync(ct);

        decimal progressAmount = 0m;
        if (i.ProgressPercent is { } pct)
        {
            var fee = ProjectMath.EffectiveBudget(p.BudgetAmount, await changeOrders.Query(new Specification<ProjectChangeOrder>(c => c.ProjectId == p.Id)).ToListAsync(ct));
            if (fee <= 0) throw new ConflictException("The project has no fixed fee to bill a percentage of.");
            var already = await progressRows.Query(new Specification<ProjectProgressBilling>(x => x.ProjectId == p.Id && !x.Released)).SumDecimalAsync(x => x.Amount, ct);
            var invoicedMilestones = await milestones.Query(new Specification<ProjectMilestone>(x => x.ProjectId == p.Id && x.Status == MilestoneStatus.Invoiced)).SumDecimalAsync(x => x.Amount, ct);
            progressAmount = ProjectMath.Round(fee * pct / 100m);
            var thisMilestones = ms.Sum(m => m.Amount);
            if (already + invoicedMilestones + thisMilestones + progressAmount > fee)
                throw new ConflictException($"That would bill {already + invoicedMilestones + thisMilestones + progressAmount:N2}, more than the fixed fee of {fee:N2}.");
        }

        if (time.Count == 0 && ms.Count == 0 && ex.Count == 0 && progressAmount == 0)
            throw new ConflictException("No unbilled timesheets or achieved milestones found to bill.");

        var taskIds = time.Where(t => t.TaskId != null).Select(t => t.TaskId!.Value).Distinct().ToList();
        var titles = taskIds.Count == 0 ? new Dictionary<Guid, string>() : await tasks.Query(new Specification<ProjectTask>(t => taskIds.Contains(t.Id))).ToDictionaryAsync(t => t.Id, t => t.Title, ct);
        var lines = BillingLines.Build(p, time, ms, ex, titles, i.Grouping, progressAmount, i.ProgressPercent);
        var subtotal = ProjectMath.Round(lines.Sum(l => l.Quantity * l.Price));
        var discount = BillingLines.Discount(subtotal, i.DiscountPercent);
        return new Plan(p, time, ms, ex, lines, subtotal, discount, progressAmount);
    }
}

/// <summary>Voiding a project invoice puts its work back: the time, expenses, milestones and fixed-fee percentage can be billed again.</summary>
public sealed class ProjectInvoiceVoidedHandler(IRepository<ProjectTimesheet> timesheets, IRepository<ProjectExpense> expenses, IRepository<ProjectMilestone> milestones, IRepository<ProjectProgressBilling> progress)
    : INotificationHandler<InvoiceVoided>
{
    public const string SourceType = "projects.project";

    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType != SourceType) return;
        var invoiceId = n.InvoiceId;
        foreach (var t in await timesheets.Tracked(new Specification<ProjectTimesheet>(x => x.InvoiceId == invoiceId)).ToListAsync(ct)) { t.IsBilled = false; t.InvoiceId = null; }
        foreach (var e in await expenses.Tracked(new Specification<ProjectExpense>(x => x.InvoiceId == invoiceId)).ToListAsync(ct)) { e.IsBilled = false; e.InvoiceId = null; }
        foreach (var m in await milestones.Tracked(new Specification<ProjectMilestone>(x => x.InvoiceId == invoiceId)).ToListAsync(ct))
        {
            m.Status = MilestoneStatus.Achieved; m.InvoiceId = null; // a system step: only a void puts an invoiced milestone back
        }
        foreach (var r in await progress.Tracked(new Specification<ProjectProgressBilling>(x => x.InvoiceId == invoiceId)).ToListAsync(ct)) r.Released = true;
        await timesheets.SaveChangesAsync(ct);
        await expenses.SaveChangesAsync(ct);
        await milestones.SaveChangesAsync(ct);
        await progress.SaveChangesAsync(ct);
    }
}
