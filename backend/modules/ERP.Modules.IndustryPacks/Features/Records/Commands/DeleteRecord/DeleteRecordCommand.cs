using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Deletes a record that has not moved on and that no other record points to.</summary>
public sealed record DeleteRecordCommand(string Module, string Type, Guid Id) : RecordRequest(Module, Type), ICommand
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
