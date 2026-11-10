using ERP.Application.Common;
using ERP.Modules.IndustryPacks.Repositories;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class UpdateRecordProgressHandler(IIndustryRecordRepository records, RecordDtoBuilder dtos) : ICommandHandler<UpdateRecordProgressCommand, RecordDto>
{
    public async Task<RecordDto> Handle(UpdateRecordProgressCommand cmd, CancellationToken ct)
    {
        // 1. The type must track progress; load the record.
        var def = PackCatalog.Require(cmd.Module, cmd.Type);
        if (!def.TracksProgress) throw new ConflictException($"{def.Label} do not track progress.");
        var record = await records.GetWithDetailsForUpdateAsync(cmd.Module, cmd.Type, cmd.Id, ct);

        // 2. Each line's completed quantity, between 0 and its quantity; save.
        foreach (var input in cmd.Lines)
        {
            var line = record.Lines.FirstOrDefault(l => l.Id == input.LineId) ?? throw new FieldValidationException("lines", "One of the lines is not on this record.");
            if (input.CompletedQuantity < 0 || input.CompletedQuantity > line.Quantity)
                throw new FieldValidationException("lines", $"'{line.Description}' can be completed up to {line.Quantity:0.##}.");
            line.CompletedQuantity = input.CompletedQuantity;
        }
        await records.SaveChangesAsync(ct);
        return dtos.ToDto(record, def, record.Lines);
    }
}
