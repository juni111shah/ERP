using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>A work order with its operations and material issues; null when it does not exist.</summary>
public record GetWorkOrderByIdQuery(Guid Id) : IQuery<WorkOrderDto?>;
