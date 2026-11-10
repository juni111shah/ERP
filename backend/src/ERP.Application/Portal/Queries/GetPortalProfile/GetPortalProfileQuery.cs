using ERP.Application.Common;

namespace ERP.Application.Portal;

/// <summary>The signed-in portal party's name and contact details.</summary>
public sealed record GetPortalProfileQuery : IQuery<PortalProfileDto>;
