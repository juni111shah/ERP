using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.IndustryPacks.Domain;
using ERP.Modules.IndustryPacks.Repositories;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class CreateRecordHandler(
    IIndustryRecordRepository records, RecordInputApplier inputs, RecordEvents history, RecordDtoBuilder dtos, ILegalEntityGuard legalEntities, INumberingService numbering)
    : ICommandHandler<CreateRecordCommand, RecordDto>
{
    public async Task<RecordDto> Handle(CreateRecordCommand cmd, CancellationToken ct)
    {
        // 1. Entity access; the record starts in the type's initial status with the next number.
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        var i = cmd.Input;
        await legalEntities.RequireAsync(i.LegalEntityId, ct);
        var record = new IndustryRecord
        {
            ModuleKey = cmd.Module, RecordType = cmd.Type, LegalEntityId = i.LegalEntityId, Status = def.InitialStatus,
            Number = await numbering.NextAsync(PackModule.SequenceKey(cmd.Module, cmd.Type), i.LegalEntityId, i.RecordDate, ct),
        };

        // 2. The input, the lines and the amount; the first event.
        await inputs.ApplyAsync(record, def, cmd.Module, i, ct);
        records.Add(record);
        RecordRules.AddLines(record, def, i.Lines);
        RecordRules.Recalculate(record, def, i.Amount);
        record.Events.Add(history.NewEvent(record, null, record.Status, null));

        // 3. Save.
        await records.SaveChangesAsync(ct);
        return dtos.ToDto(record, def, record.Lines);
    }
}
