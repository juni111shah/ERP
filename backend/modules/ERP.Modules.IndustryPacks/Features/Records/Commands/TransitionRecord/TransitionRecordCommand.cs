using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Moves a record to another status along one of its type's transitions (approval and reasons as the transition requires).</summary>
public sealed record TransitionRecordCommand(string Module, string Type, Guid Id, string To, string? Reason) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
