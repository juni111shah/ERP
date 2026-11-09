using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The business types a workspace can start from, with the modules each suggests.</summary>
public sealed record ListIndustriesQuery : IQuery<IReadOnlyList<IndustryView>>;
