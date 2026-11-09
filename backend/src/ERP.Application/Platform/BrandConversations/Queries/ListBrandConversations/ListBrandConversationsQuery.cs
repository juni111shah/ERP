using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The public assistant's conversations, those needing review first, with each visitor's first message.</summary>
public sealed record ListBrandConversationsQuery : PagedQuery, IQuery<PagedResult<BrandConversationRow>>;
