using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.IndustryPacks.Repositories;
using MediatR;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class BillRecordHandler(IIndustryRecordRepository records, RecordEvents history, RecordDtoBuilder dtos, IPostingService posting, IClock clock, IUnitOfWork unitOfWork, ISender sender)
    : ICommandHandler<BillRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(BillRecordCommand cmd, CancellationToken ct)
    {
        // 1. The type must raise bills; load the record, billable now, not billed yet and with a vendor.
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        if (def.Billing?.Bill != true) throw new ConflictException($"{def.Label} do not raise vendor bills.");
        var record = await records.GetWithDetailsForUpdateAsync(cmd.Module, cmd.Type, cmd.Id, ct);
        RecordRules.EnsureBillable(record, def);
        if (record.BillId is not null) throw new ConflictException("This record is already billed.");
        if (record.PartyId is not { } partyId) throw new FieldValidationException("partyId", $"Choose the {(def.PartyLabel ?? "vendor").ToLowerInvariant()} first.");

        // 2. The bill lines, coded to the definition's expense account.
        var account = await posting.AccountForRoleAsync(def.Billing.ExpenseAccountRole ?? "purchases.expense", ct);
        var date = cmd.Date ?? DateOnly.FromDateTime(clock.UtcNow);
        var lines = record.Lines.Count > 0
            ? record.Lines.OrderBy(l => l.Seq).Select(l => new BillLineInput($"{record.Number} {l.Description}", l.Quantity, l.UnitPrice, account, cmd.TaxCodeId)).ToList()
            : [new BillLineInput($"{record.Number} {record.Title}", 1, record.Amount, account, cmd.TaxCodeId)];

        // 3. Create and post the bill and link it, all or nothing.
        return await unitOfWork.InTransactionAsync(async _ =>
        {
            var bill = await sender.Send(new CreateBillCommand(new BillInput(record.LegalEntityId, partyId, cmd.VendorReference ?? record.Number, date, record.DueDate is { } d && d >= date ? d : date, lines,
                $"{def.Singular} {record.Number}: {record.Title}", record.BusinessUnitId)), ct);
            await sender.Send(new PostBillCommand(bill.Id), ct);
            record.BillId = bill.Id;
            history.MoveAfterBilling(record, def);
            await records.SaveChangesAsync(ct);
            return dtos.ToDto(record, def, record.Lines);
        }, ct);
    }
}
