using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>One depreciation run with the charge of each asset.</summary>
public sealed record GetDepreciationRunQuery(Guid Id) : IQuery<DepreciationRunDto>, IRequirePermission { public string RequiredPermission => Permissions.DepreciationRead; }
