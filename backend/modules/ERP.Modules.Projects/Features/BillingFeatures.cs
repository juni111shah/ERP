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
/// A list of ids chooses those lines only: left out means everything ready to bill, an empty list means none of that kind.
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

public sealed record BillableTimeDto(Guid Id, DateOnly Date, string Worker, string? Task, string? Description, decimal Hours, decimal Amount);
public sealed record BillableExpenseDto(Guid Id, DateOnly Date, string Category, string Description, decimal Amount);
public sealed record BillableMilestoneDto(Guid Id, string Title, DateOnly? AchievedDate, decimal Amount);
public sealed record BillableWorkDto(Guid ProjectId, ProjectBillingType BillingType, decimal Fee, decimal FixedFeeRemaining, IReadOnlyList<BillableTimeDto> Time, IReadOnlyList<BillableExpenseDto> Expenses, IReadOnlyList<BillableMilestoneDto> Milestones);
public sealed record ProjectInvoiceDto(Guid Id, string Number, DateOnly Date, DateOnly DueDate, DocumentStatus Status, decimal Total, decimal Paid);

/// <summary>Everything that could go on the next invoice of a project, line by line, so people can choose.</summary>
public sealed record GetBillableWork(Guid ProjectId) : IQuery<BillableWorkDto>, IRequirePermission { public string RequiredPermission => P.BillingRead; }
/// <summary>The invoices raised from a project.</summary>
public sealed record GetProjectInvoices(Guid ProjectId) : IQuery<IReadOnlyList<ProjectInvoiceDto>>, IRequirePermission { public string RequiredPermission => P.BillingRead; }
/// <summary>Decides not to charge for all the approved, unbilled time and expenses of a project.</summary>
public sealed record WriteOffProject(Guid ProjectId) : ICommand<int>, IRequirePermission { public string RequiredPermission => P.BillingManage; }

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
    IReadRepository<Invoice> invoices,
    ISender sender,
    IPostingService posting,
    ProjectAccess access,
    IUnitOfWork uow) :
    IRequestHandler<GetBillableWork, BillableWorkDto>,
    IRequestHandler<GetProjectInvoices, IReadOnlyList<ProjectInvoiceDto>>,
    IRequestHandler<WriteOffProject, int>,
    IRequestHandler<GetBillingOverview, IReadOnlyList<ProjectBillingSummaryDto>>,
    IRequestHandler<BillProject, BillProjectResultDto>,
    IRequestHandler<PreviewProjectBill, BillPreviewDto>,
    IRequestHandler<WriteOffExpenses, int>
{
    public async Task<BillableWorkDto> Handle(GetBillableWork q, CancellationToken ct)
    {
        var p = await projects.GetAsync(q.ProjectId, ct);
        await access.EnsureAsync(p, ct);
        var id = p.Id;
        var time = p.BillingType == ProjectBillingType.TimeAndMaterials
            ? await timesheets.Query(new Specification<ProjectTimesheet>(x => x.ProjectId == id && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.ApprovalStatus == TimesheetStatus.Approved)).OrderBy(x => x.Date).ToListAsync(ct)
            : [];
        var taskIds = time.Where(t => t.TaskId != null).Select(t => t.TaskId!.Value).Distinct().ToList();
        var titles = taskIds.Count == 0 ? new Dictionary<Guid, string>() : await tasks.Query(new Specification<ProjectTask>(t => taskIds.Contains(t.Id))).ToDictionaryAsync(t => t.Id, t => t.Title, ct);
        var costs = await expenses.Query(new Specification<ProjectExpense>(x => x.ProjectId == id && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.Status == ExpenseStatus.Approved)).OrderBy(x => x.Date).ToListAsync(ct);
        var ms = await milestones.Query(new Specification<ProjectMilestone>(x => x.ProjectId == id && x.Status == MilestoneStatus.Achieved)).OrderBy(x => x.DueDate).ToListAsync(ct);
        var fee = ProjectMath.Fee(p, await changeOrders.Query(new Specification<ProjectChangeOrder>(c => c.ProjectId == id)).ToListAsync(ct));
        var billed = await progressRows.Query(new Specification<ProjectProgressBilling>(x => x.ProjectId == id && !x.Released)).SumDecimalAsync(x => x.Amount, ct)
            + await milestones.Query(new Specification<ProjectMilestone>(x => x.ProjectId == id && x.Status == MilestoneStatus.Invoiced)).SumDecimalAsync(x => x.Amount, ct);
        var remaining = p.BillingType == ProjectBillingType.FixedFee ? Math.Max(0m, fee - billed - ms.Sum(m => m.Amount)) : 0m;
        return new BillableWorkDto(id, p.BillingType, fee, remaining,
            time.Select(t => new BillableTimeDto(t.Id, t.Date, t.WorkerName, t.TaskId is { } tid ? titles.GetValueOrDefault(tid) : null, t.Description, t.Hours, t.TotalAmount)).ToList(),
            costs.Select(e => new BillableExpenseDto(e.Id, e.Date, e.Category, e.Description, BillingLines.Charge(e))).ToList(),
            ms.Select(m => new BillableMilestoneDto(m.Id, m.Title, m.AchievedDate, m.Amount)).ToList());
    }

    public async Task<IReadOnlyList<ProjectInvoiceDto>> Handle(GetProjectInvoices q, CancellationToken ct)
    {
        await access.EnsureProjectAsync(q.ProjectId, ct);
        var id = q.ProjectId;
        var rows = await invoices.Query(new Specification<Invoice>(x => x.SourceType == ProjectInvoiceVoidedHandler.SourceType && x.SourceId == id)).ToListAsync(ct);
        return rows.OrderByDescending(x => x.InvoiceDate).Select(x => new ProjectInvoiceDto(x.Id, x.Number, x.InvoiceDate, x.DueDate, x.Status, x.Total, x.Paid)).ToList();
    }

    public Task<int> Handle(WriteOffProject cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var p = await projects.GetAsync(cmd.ProjectId, token);
        await access.EnsureAsync(p, token);
        var id = p.Id;
        var time = await timesheets.Tracked(new Specification<ProjectTimesheet>(x => x.ProjectId == id && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.ApprovalStatus == TimesheetStatus.Approved)).ToListAsync(token);
        var costs = await expenses.Tracked(new Specification<ProjectExpense>(x => x.ProjectId == id && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.Status == ExpenseStatus.Approved)).ToListAsync(token);
        await ReleaseWrittenOffAsync(p, time, costs, token);
        foreach (var t in time) t.WrittenOff = true;
        foreach (var e in costs) e.WrittenOff = true;
        await timesheets.SaveChangesAsync(token);
        await expenses.SaveChangesAsync(token);
        return time.Count + costs.Count;
    }, ct);

    private async Task ReleaseWrittenOffAsync(Project p, IReadOnlyList<ProjectTimesheet> time, IReadOnlyList<ProjectExpense> costs, CancellationToken ct)
    {
        var postedTime = time.Where(t => t.CostJournalId != null && t.WipReleaseJournalId == null).ToList();
        var postedCosts = costs.Where(e => e.CostJournalId != null && e.WipReleaseJournalId == null).ToList();
        var journal = await ProjectWip.ReleaseAsync(posting, p, postedTime.Sum(t => t.CostAmount) + postedCosts.Sum(e => e.Amount), p.Number, $"Project {p.Number} work written off, work in progress to cost of sales", ct);
        if (journal is null) return;
        foreach (var t in postedTime) t.WipReleaseJournalId = journal.Id;
        foreach (var e in postedCosts) e.WipReleaseJournalId = journal.Id;
    }

    public async Task<IReadOnlyList<ProjectBillingSummaryDto>> Handle(GetBillingOverview q, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
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
            .OrderBy(p => p.Name).Select(p => new { p.Id, p.Number, p.Name, p.BillingType, p.Status, p.BudgetAmount, p.ContractValue }).ToListAsync(ct);
        var billedFixed = (await progressRows.Query(new Specification<ProjectProgressBilling>(x => !x.Released)).ToListAsync(ct)).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var invoicedMs = (await milestones.Query(new Specification<ProjectMilestone>(x => x.Status == MilestoneStatus.Invoiced)).Select(x => new { x.ProjectId, x.Amount }).ToListAsync(ct)).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var changes = (await changeOrders.Query(new Specification<ProjectChangeOrder>(x => x.Status == ChangeOrderStatus.Approved)).ToListAsync(ct)).GroupBy(x => x.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.AmountChange));
        var list = new List<ProjectBillingSummaryDto>();
        foreach (var p in allProjects)
        {
            if (p.BillingType == ProjectBillingType.NonBillable || (visible != null && !visible.Contains(p.Id))) continue;
            var (hours, hoursAmount) = p.BillingType == ProjectBillingType.FixedFee ? default : unbilled.GetValueOrDefault(p.Id); // a fixed-fee project bills milestones only
            var (msCount, msAmount) = achieved.GetValueOrDefault(p.Id);
            var expenseAmount = costs.GetValueOrDefault(p.Id);
            var remaining = p.BillingType == ProjectBillingType.FixedFee
                ? Math.Max(0m, (p.ContractValue ?? p.BudgetAmount) + changes.GetValueOrDefault(p.Id) - billedFixed.GetValueOrDefault(p.Id) - invoicedMs.GetValueOrDefault(p.Id) - msAmount) : 0m;
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
                LegalEntityId: p.LegalEntityId, PartyId: p.CustomerId!.Value, InvoiceDate: DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), DueDate: i.DueDate, Lines: invoiceLines,
                Notes: notes, SourceType: "projects.project", SourceId: p.Id)), transactionCt);
            foreach (var ts in plan.Time) { ts.IsBilled = true; ts.InvoiceId = invoice.Id; }
            foreach (var e in plan.Expenses) { e.IsBilled = true; e.InvoiceId = invoice.Id; }
            foreach (var ms in plan.Milestones)
            {
                ProjectsTransitions.Milestone.Ensure(ms.Status, MilestoneStatus.Invoiced, isSystem: true);
                ms.Status = MilestoneStatus.Invoiced; ms.InvoiceId = invoice.Id;
            }
            // cost that was moved into work in progress goes to cost of sales now that the work is billed
            var releaseTime = plan.Time.Where(t => t.CostJournalId != null && t.WipReleaseJournalId == null).ToList();
            var releaseCosts = plan.Expenses.Where(e => e.CostJournalId != null && e.WipReleaseJournalId == null).ToList();
            var journal = await ProjectWip.ReleaseAsync(posting, p, releaseTime.Sum(t => t.CostAmount) + releaseCosts.Sum(e => e.Amount), invoice.Number, $"Project {p.Number} billed, work in progress to cost of sales", transactionCt);
            if (journal is not null)
            {
                foreach (var t in releaseTime) t.WipReleaseJournalId = journal.Id;
                foreach (var e in releaseCosts) e.WipReleaseJournalId = journal.Id;
            }
            if (plan.ProgressAmount > 0)
                progressRows.Add(new ProjectProgressBilling { ProjectId = p.Id, Date = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), Percent = i.ProgressPercent ?? 0m, Amount = plan.ProgressAmount, InvoiceId = invoice.Id });
            await timesheets.SaveChangesAsync(transactionCt);
            await expenses.SaveChangesAsync(transactionCt);
            await milestones.SaveChangesAsync(transactionCt);
            await progressRows.SaveChangesAsync(transactionCt);
            return new BillProjectResultDto(p.Id, plan.Time.Count, plan.Milestones.Count, plan.Subtotal - plan.Discount, invoice.Id, invoice.Number, plan.Expenses.Count);
        }, ct);
    }

    public Task<int> Handle(WriteOffExpenses cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var rows = await expenses.Tracked(new Specification<ProjectExpense>(e => cmd.Ids.Contains(e.Id))).ToListAsync(token);
        if (rows.Count != cmd.Ids.Distinct().Count()) throw new NotFoundException("One of the expenses was not found.");
        if (rows.Any(e => e.IsBilled)) throw new ConflictException("Billed expenses cannot be written off. Void the invoice first.");
        await access.EnsureProjectsAsync(rows.Select(e => e.ProjectId), token);
        foreach (var group in rows.GroupBy(e => e.ProjectId)) await ReleaseWrittenOffAsync(await projects.GetAsync(group.Key, token), [], group.ToList(), token);
        foreach (var e in rows) e.WrittenOff = true;
        await expenses.SaveChangesAsync(token);
        return rows.Count;
    }, ct);

    private sealed record Plan(Project Project, List<ProjectTimesheet> Time, List<ProjectMilestone> Milestones, List<ProjectExpense> Expenses, List<BillingLines.Line> Lines, decimal Subtotal, decimal Discount, decimal ProgressAmount);

    /// <summary>Checks the project and picks what is billable: approved, unbilled, not written off.</summary>
    private async Task<Plan> PlanAsync(BillProjectInput i, CancellationToken ct)
    {
        var p = await projects.GetAsync(i.ProjectId, ct);
        await access.EnsureAsync(p, ct);
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
        if (i.TimesheetIds is not null) { var ids = i.TimesheetIds.ToHashSet(); tsQuery = tsQuery.Where(x => ids.Contains(x.Id)); }
        var time = p.BillingType == ProjectBillingType.FixedFee ? new List<ProjectTimesheet>() : await tsQuery.OrderBy(x => x.Date).ToListAsync(ct); // a fixed-fee project bills milestones only

        var msQuery = milestones.Tracked(new Specification<ProjectMilestone>(x => x.ProjectId == p.Id && x.Status == MilestoneStatus.Achieved));
        if (i.MilestoneIds is not null) { var ids = i.MilestoneIds.ToHashSet(); msQuery = msQuery.Where(x => ids.Contains(x.Id)); }
        var ms = await msQuery.ToListAsync(ct);

        var exQuery = expenses.Tracked(new Specification<ProjectExpense>(x => x.ProjectId == p.Id && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.Status == ExpenseStatus.Approved));
        if (i.ExpenseIds is not null) { var ids = i.ExpenseIds.ToHashSet(); exQuery = exQuery.Where(x => ids.Contains(x.Id)); }
        var ex = await exQuery.OrderBy(x => x.Date).ToListAsync(ct);

        decimal progressAmount = 0m;
        if (i.ProgressPercent is { } pct)
        {
            var fee = ProjectMath.Fee(p, await changeOrders.Query(new Specification<ProjectChangeOrder>(c => c.ProjectId == p.Id)).ToListAsync(ct));
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

/// <summary>Voiding a project invoice puts its work back: the time, expenses, milestones and fixed-fee percentage can be billed again, and the cost goes back into work in progress.</summary>
public sealed class ProjectInvoiceVoidedHandler(IRepository<ProjectTimesheet> timesheets, IRepository<ProjectExpense> expenses, IRepository<ProjectMilestone> milestones, IRepository<ProjectProgressBilling> progress, IRepository<JournalEntry> journals, IReadRepository<Project> projects, IPostingService posting)
    : INotificationHandler<InvoiceVoided>
{
    public const string SourceType = "projects.project";

    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType != SourceType) return;
        var invoiceId = n.InvoiceId;
        var time = await timesheets.Tracked(new Specification<ProjectTimesheet>(x => x.InvoiceId == invoiceId)).ToListAsync(ct);
        var costs = await expenses.Tracked(new Specification<ProjectExpense>(x => x.InvoiceId == invoiceId)).ToListAsync(ct);
        // a closed project has no way to release work in progress a second time, so its cost stays in cost of sales
        var project = n.SourceId is { } projectId ? await projects.FindAsync(projectId, ct) : null;
        var closed = project?.Status is ProjectStatus.Completed or ProjectStatus.Cancelled;
        var releases = closed ? [] : time.Where(t => t.WipReleaseJournalId != null).Select(t => t.WipReleaseJournalId!.Value).Concat(costs.Where(e => e.WipReleaseJournalId != null).Select(e => e.WipReleaseJournalId!.Value)).Distinct().ToList();
        if (releases.Count > 0)
            foreach (var journal in await journals.Tracked(new Specification<JournalEntry>(j => releases.Contains(j.Id)).Include(j => j.Lines)).ToListAsync(ct))
                if (journal.Status == JournalStatus.Posted) await posting.ReverseTodayAsync(journal, $"Project invoice voided, cost back to work in progress", ct);
        foreach (var t in time) { t.IsBilled = false; t.InvoiceId = null; if (!closed) t.WipReleaseJournalId = null; }
        foreach (var e in costs) { e.IsBilled = false; e.InvoiceId = null; if (!closed) e.WipReleaseJournalId = null; }
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
