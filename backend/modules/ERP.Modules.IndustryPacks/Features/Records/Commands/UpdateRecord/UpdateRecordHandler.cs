using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.IndustryPacks.Domain;
using ERP.Modules.IndustryPacks.Repositories;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class UpdateRecordHandler(
    IIndustryRecordRepository records, IRepository<IndustryRecordLine> recordLines, RecordInputApplier inputs, RecordDtoBuilder dtos, ILegalEntityGuard legalEntities)
    : ICommandHandler<UpdateRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(UpdateRecordCommand cmd, CancellationToken ct)
    {
        // 1. Load the record; once it reached finance or stock, or for another legal entity, it cannot change.
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        var record = await records.GetWithDetailsForUpdateAsync(cmd.Module, cmd.Type, cmd.Id, ct);
        if (record.InvoiceId is not null || record.BillId is not null || record.ConsumedOn is not null)
            throw new ConflictException("This record has already reached finance or stock, so it cannot be edited. Void that document first.");
        await legalEntities.RequireAsync(cmd.Input.LegalEntityId, ct);
        if (cmd.Input.LegalEntityId != record.LegalEntityId) throw new FieldValidationException("legalEntityId", "A record cannot move to another legal entity.");

        // 2. Apply the input; lines sent replace the old ones unless some are billed.
        await inputs.ApplyAsync(record, def, cmd.Module, cmd.Input, ct);
        if (cmd.Input.Lines is not null)
        {
            if (record.Lines.Any(l => l.InvoiceId is not null)) throw new ConflictException("Some lines are already billed, so the lines cannot be replaced.");
            foreach (var line in record.Lines.ToList()) { record.Lines.Remove(line); recordLines.Remove(line); }
            RecordRules.AddLines(record, def, cmd.Input.Lines);
        }
        RecordRules.Recalculate(record, def, cmd.Input.Amount);

        // 3. Save.
        await records.SaveChangesAsync(ct);
        return dtos.ToDto(record, def, record.Lines);
    }
}
