using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Edits a record that has not reached finance or stock yet; lines sent replace the record's lines.</summary>
public sealed record UpdateRecordCommand(string Module, string Type, Guid Id, RecordInput Input) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
