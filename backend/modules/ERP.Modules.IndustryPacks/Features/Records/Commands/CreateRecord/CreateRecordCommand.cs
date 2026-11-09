using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Creates a record of one type in its initial status, numbered from the type's sequence.</summary>
public sealed record CreateRecordCommand(string Module, string Type, RecordInput Input) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
