using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Whether the public assistant is shown, and the support email it offers.</summary>
public sealed record GetBrandAssistantStatusQuery : IQuery<BrandAssistantStatus>;
