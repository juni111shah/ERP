using ERP.Application.Common;

namespace ERP.Modules.RealEstate.Features;

/// <summary>A maintenance request raised by the tenant; it becomes a work order for staff.</summary>
public sealed record CreatePortalRequestCommand(PortalRequestInput Input) : ICommand<PortalRequestDto>;
