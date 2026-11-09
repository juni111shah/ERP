using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Totals of the live acquisitions: consideration, down payments, financing, what is still owed and what is unfunded.</summary>
public sealed record GetAcquisitionOverviewQuery : IQuery<AcquisitionOverviewDto>, IRequirePermission { public string RequiredPermission => P.AcquisitionsRead; }
