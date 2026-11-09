using ERP.Application.Common;
using ERP.Modules.IndustryPacks.Repositories;
using MediatR;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class DeleteRecordHandler(IIndustryRecordRepository records) : ICommandHandler<DeleteRecordCommand>
{
    public async Task<Unit> Handle(DeleteRecordCommand cmd, CancellationToken ct)
    {
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        var record = await records.GetWithDetailsForUpdateAsync(cmd.Module, cmd.Type, cmd.Id, ct);
        if (record.Status != def.InitialStatus || record.InvoiceId is not null || record.BillId is not null || record.ConsumedOn is not null || record.Lines.Any(l => l.InvoiceId is not null))
            throw new ConflictException("Only a record that has not moved on can be deleted. Cancel it instead.");
        if (await records.AnyAsync(r => r.ParentId == record.Id, ct)) throw new ConflictException("Other records point to this one, so it cannot be deleted.");
        records.Remove(record);
        await records.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
