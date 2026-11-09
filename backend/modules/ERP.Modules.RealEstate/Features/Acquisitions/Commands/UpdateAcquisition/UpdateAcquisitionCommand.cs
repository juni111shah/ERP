using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Edits a draft acquisition; its funding sources are replaced.</summary>
public sealed record UpdateAcquisitionCommand(Guid Id, AcquisitionInput Input) : ICommand<AcquisitionDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsManage; }
