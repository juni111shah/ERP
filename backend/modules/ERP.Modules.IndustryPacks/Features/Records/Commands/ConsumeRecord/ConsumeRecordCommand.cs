using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Issues the lines' materials from stock to the definition's expense account.</summary>
public sealed record ConsumeRecordCommand(string Module, string Type, Guid Id, DateOnly? Date, Guid? WarehouseId) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
