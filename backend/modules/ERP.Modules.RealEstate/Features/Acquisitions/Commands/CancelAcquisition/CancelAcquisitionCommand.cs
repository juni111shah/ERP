using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Cancels a draft acquisition.</summary>
public sealed record CancelAcquisitionCommand(Guid Id) : ICommand<AcquisitionDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsManage; }
