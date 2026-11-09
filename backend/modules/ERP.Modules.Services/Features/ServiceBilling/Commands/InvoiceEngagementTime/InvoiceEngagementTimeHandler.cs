using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Repositories;
using MediatR;

namespace ERP.Modules.Services.Features;

internal sealed class InvoiceEngagementTimeHandler(
    ITimeEntryRecordRepository time,
    IServiceEngagementRepository engagements,
    ITaxCodeRepository taxCodes,
    ISender sender,
    IUnitOfWork uow)
    : ICommandHandler<InvoiceEngagementTimeCommand, InvoiceDto>
{
    public Task<InvoiceDto> Handle(InvoiceEngagementTimeCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Only a live time-and-materials engagement bills its hours.
        var e = await engagements.FindAsync(cmd.EngagementId, token) ?? throw new NotFoundException($"Service engagement {cmd.EngagementId} not found.");
        if (e.Type != EngagementType.TimeAndMaterials) throw new ConflictException("Only a time-and-materials engagement bills its hours.");
        if (e.Status == EngagementStatus.Cancelled) throw new ConflictException($"Engagement {e.EngagementCode} is cancelled.");

        // 2. The time ready to invoice, limited by the ids and dates asked for.
        var rows = (await time.ListReadyToInvoiceForUpdateAsync(e.Id, cmd.TimeEntryIds, cmd.From, cmd.To, token)).OrderBy(t => t.ActivityDate).ThenBy(t => t.ConsultantName).ToList();
        if (rows.Sum(t => t.TotalAmount) <= 0) throw new ConflictException("There is no approved billable time to invoice.");

        // 3. One line per entry; raise the draft invoice and mark the time invoiced.
        var taxId = cmd.TaxCodeId ?? await taxCodes.FindDefaultIdAsync(e.LegalEntityId, token); // the code asked for, or the workspace default, as project billing does
        var lines = rows.Where(t => t.TotalAmount > 0)
            .Select(t => new InvoiceLineInput($"{e.EngagementCode}: {t.ConsultantName}, {t.ActivityDate:dd MMM yyyy}, {t.TaskDescription}", t.Hours, t.HourlyRate, TaxCodeId: taxId)).ToList();
        var invoice = await sender.Send(new CreateInvoiceCommand(new InvoiceInput(
            LegalEntityId: e.LegalEntityId, PartyId: e.ClientId, InvoiceDate: cmd.InvoiceDate ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), DueDate: cmd.DueDate, Lines: lines,
            Notes: string.IsNullOrWhiteSpace(cmd.Notes) ? $"Time billing for {e.EngagementCode}: {e.Title}" : cmd.Notes.Trim(),
            SourceType: ServiceBillingSources.TimeSourceType, SourceId: e.Id)), token);
        foreach (var t in rows) t.InvoiceId = invoice.Id;
        await time.SaveChangesAsync(token);
        return invoice;
    }, ct);
}
