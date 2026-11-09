using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Modularity.Features;

public sealed record NumberSequenceView(string DocumentType, string Prefix, string Pattern, bool ResetYearly, long NextValue, string Example)
{
    internal static NumberSequenceView From(NumberSequence s, DateOnly today) => new(s.DocumentType, s.Prefix, s.Pattern, s.ResetYearly, s.NextValue, NumberFormatter.Format(s.Pattern, s.Prefix, today, s.NextValue));
}

public sealed record NumberSequenceInput(string Prefix, string Pattern, bool ResetYearly);
