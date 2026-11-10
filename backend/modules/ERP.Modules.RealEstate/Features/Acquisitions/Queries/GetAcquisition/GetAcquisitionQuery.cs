using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>One acquisition with its funding sources and their schedules.</summary>
public sealed record GetAcquisitionQuery(Guid Id) : IQuery<AcquisitionDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsRead; }
