using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.IndustryPacks.Repositories;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class TransitionRecordHandler(IIndustryRecordRepository records, IStockPostingService stock, RecordEvents history, RecordDtoBuilder dtos, ICurrentUser user)
    : ICommandHandler<TransitionRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(TransitionRecordCommand cmd, CancellationToken ct)
    {
        // 1. Load the record; the move must be a declared transition, with its reason and approval.
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        var record = await records.GetWithDetailsForUpdateAsync(cmd.Module, cmd.Type, cmd.Id, ct);
        var step = def.Transitions.FirstOrDefault(t => t.From == record.Status && t.To == cmd.To)
            ?? throw new ConflictException($"A record in status '{record.Status}' cannot move to '{cmd.To}'.");
        if (step.ReasonRequired && string.IsNullOrWhiteSpace(cmd.Reason)) throw new FieldValidationException("reason", "Enter a reason.");
        if (step.Approval)
        {
            if (!user.HasPermission(PackModule.ApprovePermission(cmd.Module, cmd.Type))) throw new ForbiddenException();
            if (record.CreatedBy is { } maker && maker == user.UserId) throw new ConflictException("The person who created a record cannot approve it.");
        }

        // 2. Cancelling needs the finance documents voided first, and gives issued materials back.
        var isCancellation = cmd.To is "cancelled" or "voided";
        if (isCancellation && (record.InvoiceId is not null || record.BillId is not null || record.Lines.Any(l => l.InvoiceId is not null)))
            throw new ConflictException("Void the finance document first, then cancel this record.");
        if (isCancellation && record.ConsumedOn is not null)
        {
            await stock.ReverseIssueAsync(record.Number, ct);
            record.ConsumedOn = null;
        }

        // 3. Move it, record the event and save.
        var from = record.Status;
        record.Status = cmd.To;
        record.Events.Add(history.NewEvent(record, from, cmd.To, cmd.Reason?.Trim()));
        await records.SaveChangesAsync(ct);
        return dtos.ToDto(record, def, record.Lines);
    }
}
