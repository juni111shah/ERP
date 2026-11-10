using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.IndustryPacks.Domain;
using ERP.Modules.IndustryPacks.Repositories;
using MediatR;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class InvoiceRecordHandler(IIndustryRecordRepository records, RecordEvents history, RecordDtoBuilder dtos, IClock clock, IUnitOfWork unitOfWork, ISender sender)
    : ICommandHandler<InvoiceRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(InvoiceRecordCommand cmd, CancellationToken ct)
    {
        // 1. The type must be invoiced; load the record, billable now and with a customer.
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        if (def.Billing?.Invoice != true) throw new ConflictException($"{def.Label} are not invoiced.");
        var record = await records.GetWithDetailsForUpdateAsync(cmd.Module, cmd.Type, cmd.Id, ct);
        RecordRules.EnsureBillable(record, def);
        if (record.PartyId is not { } partyId) throw new FieldValidationException("partyId", $"Choose the {(def.PartyLabel ?? "customer").ToLowerInvariant()} first.");

        // 2. The lines to invoice: the unbilled (chosen) lines when billed per line, else the whole record once.
        var date = cmd.Date ?? DateOnly.FromDateTime(clock.UtcNow);
        List<IndustryRecordLine> chosen;
        if (def.Billing.PerLine)
        {
            chosen = record.Lines.Where(l => l.InvoiceId is null && (cmd.LineIds is null || cmd.LineIds.Contains(l.Id))).OrderBy(l => l.Seq).ToList();
            if (chosen.Count == 0) throw new ConflictException("There is nothing left to bill on this record.");
        }
        else
        {
            if (record.InvoiceId is not null) throw new ConflictException("This record is already invoiced.");
            chosen = record.Lines.OrderBy(l => l.Seq).ToList();
        }
        var lines = chosen.Count > 0
            ? chosen.Select(l => new InvoiceLineInput($"{record.Number} {l.Description}", l.Quantity, l.UnitPrice, cmd.TaxCodeId, ItemId: l.ItemId)).ToList()
            : [new InvoiceLineInput($"{record.Number} {record.Title}", 1, record.Amount, cmd.TaxCodeId)];
        var due = chosen.Select(l => l.DueDate).FirstOrDefault(d => d is not null) ?? record.DueDate;

        // 3. Create and issue the invoice and link it, all or nothing.
        return await unitOfWork.InTransactionAsync(async _ =>
        {
            var invoice = await sender.Send(new CreateInvoiceCommand(new InvoiceInput(record.LegalEntityId, partyId, date, due is { } d && d >= date ? d : date, lines,
                Notes: $"{def.Singular} {record.Number}: {record.Title}", SourceType: RecordInvoiceVoidedHandler.SourceType, SourceId: record.Id, BusinessUnitId: record.BusinessUnitId)), ct);
            await sender.Send(new IssueInvoiceCommand(invoice.Id), ct);
            if (def.Billing.PerLine) foreach (var line in chosen) line.InvoiceId = invoice.Id;
            else record.InvoiceId = invoice.Id;
            history.MoveAfterBilling(record, def);
            await records.SaveChangesAsync(ct);
            return dtos.ToDto(record, def, record.Lines);
        }, ct);
    }
}
