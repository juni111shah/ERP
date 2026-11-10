using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modularity.Features;

/// <summary>The workspace's document numbering: the current year's sequence of each document type.</summary>
public sealed record ListNumberSequencesQuery : IQuery<IReadOnlyList<NumberSequenceView>>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsRead; }
