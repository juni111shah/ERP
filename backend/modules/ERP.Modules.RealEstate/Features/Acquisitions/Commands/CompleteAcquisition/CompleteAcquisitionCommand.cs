using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Completes the purchase: Dr investment property (price + fees + costs), Cr bank (cash paid) and property financing payable (per funding source); the instalment schedules are generated.</summary>
public sealed record CompleteAcquisitionCommand(Guid Id) : ICommand<AcquisitionDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsComplete; }
