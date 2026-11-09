namespace ERP.Application.Platform;

public sealed record PublicFeature(string Icon, string Title, string Text, string TitleAr, string TextAr);
public sealed record PublicFaq(string Q, string A, string QAr, string AAr);
public sealed record PublicContent(IReadOnlyList<PublicFeature> BuiltIn, IReadOnlyList<PublicFeature> Trust, IReadOnlyList<PublicFaq> Faqs, string? About);
