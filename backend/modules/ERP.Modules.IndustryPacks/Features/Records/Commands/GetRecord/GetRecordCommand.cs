using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>One record with its lines and history; opening a restricted record is written to the audit trail, so this is a command.</summary>
public sealed record GetRecordCommand(string Module, string Type, Guid Id) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ReadPermission(Module, Type);
}
