using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Sends unused material back to stock at the cost it was issued at: Dr inventory, Cr WIP.</summary>
public record ReturnWorkOrderMaterialsCommand(
    Guid WorkOrderId,
    List<MaterialLineInput> Lines,
    DateOnly? Date = null,
    string? Notes = null) : ICommand<WorkOrderDto>;
