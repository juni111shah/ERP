using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;
using MediatR;

namespace ERP.Modules.Services.Features;

internal sealed class GenerateRetainerInvoiceHandler(
    IRetainerContractRepository retainers,
    IRetainerBillingPeriodRepository periods,
    ITimeEntryRecordRepository time,
    IServiceEngagementRepository engagements,
    IReadRepository<Invoice> invoices,
    ITaxCodeRepository taxCodes,
    ISender sender,
    IUnitOfWork uow)
    : ICommandHandler<GenerateRetainerInvoiceCommand, RetainerInvoiceDto>
{
    public Task<RetainerInvoiceDto> Handle(GenerateRetainerInvoiceCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Find the period; one already invoiced answers with its invoice.
        var r = await retainers.FindAsync(cmd.RetainerId, token) ?? throw new NotFoundException($"Retainer contract {cmd.RetainerId} not found.");
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var period = RetainerPeriods.Containing(r.StartDate, r.EndDate, r.Cycle, cmd.PeriodDate ?? today)
            ?? throw new FieldValidationException("periodDate", "That date is outside the term of the retainer.");

        var existing = await periods.FindForPeriodAsync(r.Id, period.Start, token);
        if (existing is not null) return await ToDtoAsync(existing, true, await time.UnapprovedBillableHoursAsync(r.EngagementId, period.Start, period.End, token), token);

        // 2. Only an active retainer on a live engagement invoices, and not before the period starts.
        if (r.Status != RetainerStatus.Active) throw new ConflictException($"Retainer {r.RetainerNumber} is {r.Status.ToString().ToLowerInvariant()} and raises no new invoices.");
        if (period.Start > today) throw new ConflictException("A period cannot be invoiced before it starts.");
        var engagement = await engagements.FindAsync(r.EngagementId, token) ?? throw new ConflictException("The engagement of this retainer no longer exists.");
        if (engagement.Status == EngagementStatus.Cancelled) throw new ConflictException($"Engagement {engagement.EngagementCode} is cancelled.");

        // 3. What the period counts: its own approved billable time, plus any that was approved after an earlier period had already been invoiced.
        var billedStarts = await periods.ListBilledPeriodsAsync(r.Id, token);
        var candidates = await time.ListUninvoicedApprovedForUpdateAsync(r.EngagementId, r.StartDate, period.End, token);
        var counted = candidates.Where(t => t.ActivityDate >= period.Start || billedStarts.Any(b => t.ActivityDate >= b.PeriodStart && t.ActivityDate <= b.PeriodEnd)).ToList();
        var hours = counted.Sum(t => t.Hours);
        var overage = RetainerPeriods.Overage(hours, r.IncludedHours);
        var overageAmount = Money.Round(overage * r.OverageHourlyRate);
        if (r.RecurringFee + overageAmount <= 0) throw new ConflictException("This period has nothing to invoice: no fee and no overage.");

        // 4. The fee line and the overage line.
        var taxId = cmd.TaxCodeId ?? await taxCodes.FindDefaultIdAsync(r.LegalEntityId, token); // the code asked for, or the workspace default, as project billing does
        var lines = new List<InvoiceLineInput>();
        var label = $"{period.Start:dd MMM yyyy} to {period.End:dd MMM yyyy}";
        if (r.RecurringFee > 0) lines.Add(new InvoiceLineInput($"{r.RetainerNumber}: {r.Title}, retainer {label}", 1, r.RecurringFee, TaxCodeId: taxId));
        if (overageAmount > 0) lines.Add(new InvoiceLineInput($"{r.RetainerNumber}: {overage:0.##} hours above the {r.IncludedHours:0.##} included, {label}", overage, r.OverageHourlyRate, TaxCodeId: taxId));

        // 5. Raise the draft invoice, record the period and mark the time invoiced.
        var row = new RetainerBillingPeriod
        {
            LegalEntityId = r.LegalEntityId, RetainerId = r.Id, PeriodStart = period.Start, PeriodEnd = period.End, RecurringFee = r.RecurringFee, IncludedHours = r.IncludedHours,
            BillableHours = hours, OverageHours = overage, OverageHourlyRate = r.OverageHourlyRate, OverageAmount = overageAmount,
        };
        var invoice = await sender.Send(new CreateInvoiceCommand(new InvoiceInput(
            LegalEntityId: r.LegalEntityId, PartyId: r.ClientId, InvoiceDate: cmd.InvoiceDate ?? today, DueDate: cmd.DueDate, Lines: lines,
            Notes: string.IsNullOrWhiteSpace(cmd.Notes) ? $"Retainer {r.RetainerNumber}, {label}" : cmd.Notes.Trim(),
            SourceType: ServiceBillingSources.RetainerSourceType, SourceId: row.Id)), token);
        row.InvoiceId = invoice.Id;
        periods.Add(row);
        foreach (var t in counted) t.InvoiceId = invoice.Id;
        await periods.SaveChangesAsync(token);
        return new RetainerInvoiceDto(r.Id, row.PeriodStart, row.PeriodEnd, row.RecurringFee, row.IncludedHours, row.BillableHours, row.OverageHours, row.OverageHourlyRate, row.OverageAmount,
            invoice.Id, invoice.Number, invoice.Net, invoice.Tax, invoice.Total, false, await time.UnapprovedBillableHoursAsync(r.EngagementId, period.Start, period.End, token));
    }, ct);

    private async Task<RetainerInvoiceDto> ToDtoAsync(RetainerBillingPeriod p, bool already, decimal unapproved, CancellationToken ct)
    {
        var inv = await invoices.GetAsync(p.InvoiceId, ct);
        return new RetainerInvoiceDto(p.RetainerId, p.PeriodStart, p.PeriodEnd, p.RecurringFee, p.IncludedHours, p.BillableHours, p.OverageHours, p.OverageHourlyRate, p.OverageAmount,
            inv.Id, inv.Number, inv.Net, inv.Tax, inv.Total, already, unapproved);
    }
}
