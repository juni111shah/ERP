using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Assets.Features;

/// <summary>Deletes an asset that was never capitalised.</summary>
public sealed record DeleteAssetCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => Permissions.RegisterManage; }
