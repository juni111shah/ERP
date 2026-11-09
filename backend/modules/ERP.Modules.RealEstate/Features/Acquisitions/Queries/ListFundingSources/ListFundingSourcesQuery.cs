using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Every funding source, earliest first due, without their schedules.</summary>
public sealed record ListFundingSourcesQuery : IQuery<IReadOnlyList<FundingSourceDto>>, IRequirePermission { public string RequiredPermission => P.AcquisitionsRead; }
