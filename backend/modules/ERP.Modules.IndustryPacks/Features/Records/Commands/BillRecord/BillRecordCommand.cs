using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Raises a core vendor bill and posts it, unless an approval policy holds it.</summary>
public sealed record BillRecordCommand(string Module, string Type, Guid Id, DateOnly? Date, Guid? TaxCodeId, string? VendorReference) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
