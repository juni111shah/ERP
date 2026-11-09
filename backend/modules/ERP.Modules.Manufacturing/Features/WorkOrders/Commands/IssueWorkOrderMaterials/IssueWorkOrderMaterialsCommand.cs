using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Puts material into the work order: Dr WIP, Cr inventory at the stock service's average cost. Without lines it issues what the BOM still needs for the planned quantity.</summary>
public record IssueWorkOrderMaterialsCommand(
    Guid WorkOrderId,
    List<MaterialLineInput>? Lines = null,
    DateOnly? Date = null,
    string? Notes = null) : ICommand<WorkOrderDto>;
