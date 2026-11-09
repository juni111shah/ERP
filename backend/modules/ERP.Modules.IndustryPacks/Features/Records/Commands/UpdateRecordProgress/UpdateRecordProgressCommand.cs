using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Records cumulative progress on lines; a line cannot exceed its quantity or go backwards without a reason in the notes.</summary>
public sealed record UpdateRecordProgressCommand(string Module, string Type, Guid Id, IReadOnlyList<RecordProgressLine> Lines) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
