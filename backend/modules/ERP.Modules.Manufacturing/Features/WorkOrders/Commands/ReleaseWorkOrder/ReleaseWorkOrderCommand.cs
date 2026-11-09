using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>Starts production, optionally issuing the BOM. IssueMaterials overrides the <c>manufacturing.issueMaterialsOnRelease</c> setting for this release.</summary>
public record ReleaseWorkOrderCommand(Guid Id, bool? IssueMaterials = null) : ICommand<WorkOrderDto>;
