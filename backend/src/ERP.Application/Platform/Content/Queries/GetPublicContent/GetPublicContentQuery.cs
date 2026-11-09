using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>What the public pages say about the product. One source, read by the pricing page and by the public assistant, so they cannot disagree.</summary>
public sealed record GetPublicContentQuery : IQuery<PublicContent>;
