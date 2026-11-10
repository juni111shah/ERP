using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>The assets on the books at the end of a date, valued from the depreciation actually posted by then. Disposed assets leave on their disposal date.</summary>
public sealed record GetAssetRegisterQuery(DateOnly AsOf, Guid? LegalEntityId = null, Guid? CategoryId = null) : IQuery<AssetRegisterDto>, IRequirePermission { public string RequiredPermission => Permissions.ReportsRead; }
