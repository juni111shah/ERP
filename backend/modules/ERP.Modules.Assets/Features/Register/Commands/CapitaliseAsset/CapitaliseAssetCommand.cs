using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>Puts the cost on the books: Dr asset account, Cr what the input names.</summary>
public sealed record CapitaliseAssetCommand(Guid Id, CapitaliseInput Input) : ICommand<AssetDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => Permissions.RegisterCapitalise; }
