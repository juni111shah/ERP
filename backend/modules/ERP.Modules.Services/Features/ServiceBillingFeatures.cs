using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Modules.Services.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Services.Features;

public sealed record RetainerInvoiceBody(DateOnly? PeriodDate = null, DateOnly? InvoiceDate = null, DateOnly? DueDate = null, Guid? TaxCodeId = null, string? Notes = null);

/// <summary>
/// What a period of a retainer invoiced: the recurring fee plus overage for the approved billable hours above the included hours. AlreadyInvoiced says this call found the period's
/// earlier invoice instead of raising a new one. UnapprovedHours are hours dated in the period that were not billed because nobody has approved them yet.
/// </summary>
public sealed record RetainerInvoiceDto(
    Guid RetainerId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal RecurringFee, decimal IncludedHours, decimal BillableHours, decimal OverageHours, decimal OverageHourlyRate, decimal OverageAmount,
    Guid InvoiceId, string InvoiceNumber, decimal Net, decimal Tax, decimal Total, bool AlreadyInvoiced, decimal UnapprovedHours);

/// <summary>
/// Raises the draft invoice of one retainer period (any date inside the period names it; none means the period containing today). Idempotent: asking again returns the same invoice;
/// voiding it frees the period. Approved time that arrives after its period was invoiced is billed with the next period.
/// </summary>
public sealed record GenerateRetainerInvoice(Guid RetainerId, DateOnly? PeriodDate = null, DateOnly? InvoiceDate = null, DateOnly? DueDate = null, Guid? TaxCodeId = null, string? Notes = null)
    : ICommand<RetainerInvoiceDto>, IRequirePermission { public string RequiredPermission => Permissions.RetainersManage; }

public sealed record EngagementTimeInvoiceBody(IReadOnlyList<Guid>? TimeEntryIds = null, DateOnly? From = null, DateOnly? To = null, DateOnly? InvoiceDate = null, DateOnly? DueDate = null, Guid? TaxCodeId = null, string? Notes = null);

/// <summary>Invoices the approved, billable, not yet invoiced time of a time-and-materials engagement, one line per entry. Ids (or a date range) limit it; none means everything ready. Voiding the invoice makes the time billable again.</summary>
public sealed record InvoiceEngagementTime(Guid EngagementId, IReadOnlyList<Guid>? TimeEntryIds = null, DateOnly? From = null, DateOnly? To = null, DateOnly? InvoiceDate = null, DateOnly? DueDate = null, Guid? TaxCodeId = null, string? Notes = null)
    : ICommand<InvoiceDto>, IRequirePermission { public string RequiredPermission => Permissions.EngagementsManage; }

public sealed class GenerateRetainerInvoiceValidator : AbstractValidator<GenerateRetainerInvoice>
{
    public GenerateRetainerInvoiceValidator()
    {
        RuleFor(x => x.RetainerId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class InvoiceEngagementTimeValidator : AbstractValidator<InvoiceEngagementTime>
{
    public InvoiceEngagementTimeValidator()
    {
        RuleFor(x => x.EngagementId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To >= x.From).WithName("to").WithMessage("The end date cannot be before the start date.");
    }
}

/// <summary>Retainer period arithmetic, anchored on the retainer's start date; no I/O.</summary>
public static class RetainerPeriods
{
    public static int MonthsIn(RetainerCycle cycle) => cycle switch { RetainerCycle.Quarterly => 3, RetainerCycle.Annual => 12, _ => 1 };

    /// <summary>The period holding a date. A date before the start or after the end of the retainer has none. The last period is cut short by the end date.</summary>
    public static (DateOnly Start, DateOnly End)? Containing(DateOnly start, DateOnly? end, RetainerCycle cycle, DateOnly date)
    {
        if (date < start || (end is { } last && date > last)) return null;
        var months = MonthsIn(cycle);
        var n = ((date.Year - start.Year) * 12 + date.Month - start.Month) / months;
        if (start.AddMonths(n * months) > date) n--;
        var periodEnd = start.AddMonths((n + 1) * months).AddDays(-1);
        return (start.AddMonths(n * months), end is { } e && e < periodEnd ? e : periodEnd);
    }

    /// <summary>Hours above the allowance, never below zero.</summary>
    public static decimal Overage(decimal billableHours, decimal includedHours) => Math.Max(0m, billableHours - includedHours);
}

internal sealed class ServiceBillingHandlers(
    IReadRepository<RetainerContract> retainers,
    IRepository<RetainerBillingPeriod> periods,
    IRepository<TimeEntryRecord> time,
    IReadRepository<ServiceEngagement> engagements,
    IReadRepository<Invoice> invoices,
    IReadRepository<TaxCode> taxCodes,
    ISender sender,
    IUnitOfWork uow) :
    IRequestHandler<GenerateRetainerInvoice, RetainerInvoiceDto>,
    IRequestHandler<InvoiceEngagementTime, InvoiceDto>
{
    public const string RetainerSourceType = "services.retainer";
    public const string TimeSourceType = "services.engagement.time";

    public Task<RetainerInvoiceDto> Handle(GenerateRetainerInvoice cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var r = await retainers.FindAsync(cmd.RetainerId, token) ?? throw new NotFoundException($"Retainer contract {cmd.RetainerId} not found.");
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var period = RetainerPeriods.Containing(r.StartDate, r.EndDate, r.Cycle, cmd.PeriodDate ?? today)
            ?? throw new FieldValidationException("periodDate", "That date is outside the term of the retainer.");

        var existing = await periods.Query(new Specification<RetainerBillingPeriod>(x => x.RetainerId == r.Id && x.PeriodStart == period.Start)).FirstOrDefaultAsync(token);
        if (existing is not null) return await ToDtoAsync(existing, true, await UnapprovedHoursAsync(r.EngagementId, period.Start, period.End, token), token);

        if (r.Status != RetainerStatus.Active) throw new ConflictException($"Retainer {r.RetainerNumber} is {r.Status.ToString().ToLowerInvariant()} and raises no new invoices.");
        if (period.Start > today) throw new ConflictException("A period cannot be invoiced before it starts.");
        var engagement = await engagements.FindAsync(r.EngagementId, token) ?? throw new ConflictException("The engagement of this retainer no longer exists.");
        if (engagement.Status == EngagementStatus.Cancelled) throw new ConflictException($"Engagement {engagement.EngagementCode} is cancelled.");

        // what the period counts: its own approved billable time, plus any that was approved after an earlier period had already been invoiced
        var billedStarts = await periods.Query(new Specification<RetainerBillingPeriod>(x => x.RetainerId == r.Id)).Select(x => new { x.PeriodStart, x.PeriodEnd }).ToListAsync(token);
        var candidates = await time.Tracked(new Specification<TimeEntryRecord>(t => t.EngagementId == r.EngagementId && t.IsApproved && t.IsBillable && t.InvoiceId == null && t.ActivityDate >= r.StartDate && t.ActivityDate <= period.End)).ToListAsync(token);
        var counted = candidates.Where(t => t.ActivityDate >= period.Start || billedStarts.Any(b => t.ActivityDate >= b.PeriodStart && t.ActivityDate <= b.PeriodEnd)).ToList();
        var hours = counted.Sum(t => t.Hours);
        var overage = RetainerPeriods.Overage(hours, r.IncludedHours);
        var overageAmount = Money.Round(overage * r.OverageHourlyRate);
        if (r.RecurringFee + overageAmount <= 0) throw new ConflictException("This period has nothing to invoice: no fee and no overage.");

        var taxId = await TaxCodeAsync(cmd.TaxCodeId, r.LegalEntityId, token);
        var lines = new List<InvoiceLineInput>();
        var label = $"{period.Start:dd MMM yyyy} to {period.End:dd MMM yyyy}";
        if (r.RecurringFee > 0) lines.Add(new InvoiceLineInput($"{r.RetainerNumber}: {r.Title}, retainer {label}", 1, r.RecurringFee, TaxCodeId: taxId));
        if (overageAmount > 0) lines.Add(new InvoiceLineInput($"{r.RetainerNumber}: {overage:0.##} hours above the {r.IncludedHours:0.##} included, {label}", overage, r.OverageHourlyRate, TaxCodeId: taxId));

        var row = new RetainerBillingPeriod
        {
            LegalEntityId = r.LegalEntityId, RetainerId = r.Id, PeriodStart = period.Start, PeriodEnd = period.End, RecurringFee = r.RecurringFee, IncludedHours = r.IncludedHours,
            BillableHours = hours, OverageHours = overage, OverageHourlyRate = r.OverageHourlyRate, OverageAmount = overageAmount,
        };
        var invoice = await sender.Send(new CreateInvoiceCommand(new InvoiceInput(
            LegalEntityId: r.LegalEntityId, PartyId: r.ClientId, InvoiceDate: cmd.InvoiceDate ?? today, DueDate: cmd.DueDate, Lines: lines,
            Notes: string.IsNullOrWhiteSpace(cmd.Notes) ? $"Retainer {r.RetainerNumber}, {label}" : cmd.Notes.Trim(),
            SourceType: RetainerSourceType, SourceId: row.Id)), token);
        row.InvoiceId = invoice.Id;
        periods.Add(row);
        foreach (var t in counted) t.InvoiceId = invoice.Id;
        await periods.SaveChangesAsync(token);
        return new RetainerInvoiceDto(r.Id, row.PeriodStart, row.PeriodEnd, row.RecurringFee, row.IncludedHours, row.BillableHours, row.OverageHours, row.OverageHourlyRate, row.OverageAmount,
            invoice.Id, invoice.Number, invoice.Net, invoice.Tax, invoice.Total, false, await UnapprovedHoursAsync(r.EngagementId, period.Start, period.End, token));
    }, ct);

    public Task<InvoiceDto> Handle(InvoiceEngagementTime cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var e = await engagements.FindAsync(cmd.EngagementId, token) ?? throw new NotFoundException($"Service engagement {cmd.EngagementId} not found.");
        if (e.Type != EngagementType.TimeAndMaterials) throw new ConflictException("Only a time-and-materials engagement bills its hours.");
        if (e.Status == EngagementStatus.Cancelled) throw new ConflictException($"Engagement {e.EngagementCode} is cancelled.");

        var query = time.Tracked(new Specification<TimeEntryRecord>(t => t.EngagementId == e.Id && t.IsApproved && t.IsBillable && t.InvoiceId == null));
        if (cmd.TimeEntryIds is { Count: > 0 } ids) query = query.Where(t => ids.Contains(t.Id));
        if (cmd.From is { } from) query = query.Where(t => t.ActivityDate >= from);
        if (cmd.To is { } to) query = query.Where(t => t.ActivityDate <= to);
        var rows = (await query.ToListAsync(token)).OrderBy(t => t.ActivityDate).ThenBy(t => t.ConsultantName).ToList();
        if (rows.Sum(t => t.TotalAmount) <= 0) throw new ConflictException("There is no approved billable time to invoice.");

        var taxId = await TaxCodeAsync(cmd.TaxCodeId, e.LegalEntityId, token);
        var lines = rows.Where(t => t.TotalAmount > 0)
            .Select(t => new InvoiceLineInput($"{e.EngagementCode}: {t.ConsultantName}, {t.ActivityDate:dd MMM yyyy}, {t.TaskDescription}", t.Hours, t.HourlyRate, TaxCodeId: taxId)).ToList();
        var invoice = await sender.Send(new CreateInvoiceCommand(new InvoiceInput(
            LegalEntityId: e.LegalEntityId, PartyId: e.ClientId, InvoiceDate: cmd.InvoiceDate ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), DueDate: cmd.DueDate, Lines: lines,
            Notes: string.IsNullOrWhiteSpace(cmd.Notes) ? $"Time billing for {e.EngagementCode}: {e.Title}" : cmd.Notes.Trim(),
            SourceType: TimeSourceType, SourceId: e.Id)), token);
        foreach (var t in rows) t.InvoiceId = invoice.Id;
        await time.SaveChangesAsync(token);
        return invoice;
    }, ct);

    /// <summary>The code asked for, or the workspace default, as project billing does.</summary>
    private async Task<Guid?> TaxCodeAsync(Guid? asked, Guid legalEntityId, CancellationToken ct)
    {
        if (asked is { } id) return id;
        return (await taxCodes.Query(new Specification<TaxCode>(t => t.IsActive && t.IsDefault && (t.LegalEntityId == null || t.LegalEntityId == legalEntityId))).FirstOrDefaultAsync(ct))?.Id;
    }

    private async Task<decimal> UnapprovedHoursAsync(Guid engagementId, DateOnly start, DateOnly end, CancellationToken ct)
        => (await time.Query(new Specification<TimeEntryRecord>(t => t.EngagementId == engagementId && !t.IsApproved && t.IsBillable && t.ActivityDate >= start && t.ActivityDate <= end)).Select(t => t.Hours).ToListAsync(ct)).Sum();

    private async Task<RetainerInvoiceDto> ToDtoAsync(RetainerBillingPeriod p, bool already, decimal unapproved, CancellationToken ct)
    {
        var inv = await invoices.Query(new Specification<Invoice>(i => i.Id == p.InvoiceId)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        return new RetainerInvoiceDto(p.RetainerId, p.PeriodStart, p.PeriodEnd, p.RecurringFee, p.IncludedHours, p.BillableHours, p.OverageHours, p.OverageHourlyRate, p.OverageAmount,
            inv.Id, inv.Number, inv.Net, inv.Tax, inv.Total, already, unapproved);
    }
}

/// <summary>Voiding (or deleting a draft of) an invoice raised here frees its time, and the retainer period it billed can be invoiced again.</summary>
public sealed class ServiceInvoiceVoidedHandler(IRepository<TimeEntryRecord> time, IRepository<RetainerBillingPeriod> periods) : INotificationHandler<InvoiceVoided>
{
    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType is not (ServiceBillingHandlers.RetainerSourceType or ServiceBillingHandlers.TimeSourceType)) return;
        var invoiceId = n.InvoiceId;
        foreach (var t in await time.Tracked(new Specification<TimeEntryRecord>(x => x.InvoiceId == invoiceId)).ToListAsync(ct)) t.InvoiceId = null;
        foreach (var p in await periods.Tracked(new Specification<RetainerBillingPeriod>(x => x.InvoiceId == invoiceId)).ToListAsync(ct)) periods.Remove(p);
        await time.SaveChangesAsync(ct);
    }
}
