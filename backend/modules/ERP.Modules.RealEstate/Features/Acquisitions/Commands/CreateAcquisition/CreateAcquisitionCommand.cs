using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Creates a draft acquisition of a property with its funding sources.</summary>
public sealed record CreateAcquisitionCommand(AcquisitionInput Input) : ICommand<AcquisitionDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsManage; }
