using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>Takes the asset off the books: Dr accumulated depreciation and proceeds, Cr cost, and the difference to gain or loss.</summary>
public sealed record DisposeAssetCommand(Guid Id, DisposeInput Input) : ICommand<AssetDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => Permissions.RegisterDispose; }
