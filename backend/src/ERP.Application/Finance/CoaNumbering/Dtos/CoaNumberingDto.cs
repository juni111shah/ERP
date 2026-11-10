using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record CoaNumberingDto(string Mode, IReadOnlyList<int> Segments, string Separator, string Pattern, int MaxLength)
{
    public static CoaNumberingDto From(AccountCodeMask m) => new(m.Mode, m.Segments, m.Separator, m.Pattern, m.MaxLength);
}

public sealed record NextAccountCodeDto(string Code);
