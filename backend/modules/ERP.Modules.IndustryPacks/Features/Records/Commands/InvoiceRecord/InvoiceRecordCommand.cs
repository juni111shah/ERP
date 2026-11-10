using ERP.Application.Common;

namespace ERP.Modules.IndustryPacks.Features;

/// <summary>Raises a core invoice (and issues it, posting the ledger) from the whole record or the chosen lines.</summary>
public sealed record InvoiceRecordCommand(string Module, string Type, Guid Id, DateOnly? Date, Guid? TaxCodeId, IReadOnlyList<Guid>? LineIds) : RecordRequest(Module, Type), ICommand<RecordDto>
{
    public override string RequiredPermission => PackModule.ManagePermission(Module, Type);
}
