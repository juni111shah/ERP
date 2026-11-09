using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Every message of one public assistant conversation, oldest first.</summary>
public sealed record GetBrandConversationQuery(Guid Id) : IQuery<IReadOnlyList<BrandMessageRow>>;
