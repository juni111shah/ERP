using ERP.Application.Common;

namespace ERP.Modularity.Features;

/// <summary>The signed-in user's identity, workspace, permissions, legal entities, modules and subscription.</summary>
public sealed record GetMeQuery : IQuery<MeDto>;
