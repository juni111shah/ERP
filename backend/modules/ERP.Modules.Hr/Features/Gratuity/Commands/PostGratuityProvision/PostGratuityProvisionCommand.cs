using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Trues the provision up to the gratuity liability: Dr end-of-service expense, Cr provision (reversed when the liability fell).</summary>
public sealed record PostGratuityProvisionCommand(Guid LegalEntityId, DateOnly AsOf) : ICommand<GratuityProvisionDto>, IRequirePermission { public string RequiredPermission => P.PayrollPost; }
