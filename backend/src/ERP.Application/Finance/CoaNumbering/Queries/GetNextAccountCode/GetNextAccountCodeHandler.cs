using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class GetNextAccountCodeHandler(AccountNumbering numbering) : IQueryHandler<GetNextAccountCodeQuery, NextAccountCodeDto>
{
    public async Task<NextAccountCodeDto> Handle(GetNextAccountCodeQuery q, CancellationToken ct)
    {
        AccountType? type = null;
        if (!string.IsNullOrWhiteSpace(q.Type))
            type = Enum.TryParse<AccountType>(q.Type, ignoreCase: true, out var t) && Enum.IsDefined(t) && !int.TryParse(q.Type, out _) ? t
                : throw new FieldValidationException("type", "Unknown account type. Use asset, liability, equity, income or expense.");
        return new(await numbering.NextCodeAsync(q.ParentId, type, ct));
    }
}
