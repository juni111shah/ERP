using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class SetCoaNumberingHandler(ISettingsService settings, IUnitOfWork unitOfWork) : ICommandHandler<SetCoaNumberingCommand, CoaNumberingDto>
{
    public async Task<CoaNumberingDto> Handle(SetCoaNumberingCommand cmd, CancellationToken ct)
    {
        var mask = new AccountCodeMask(cmd.Mode.ToLowerInvariant(), cmd.Segments.ToList(), cmd.Separator ?? "");
        await settings.SetRawAsync(AccountNumbering.SettingKey, AccountNumbering.ToJson(mask), ct: ct);
        await unitOfWork.SaveChangesAsync(ct);
        return CoaNumberingDto.From(mask);
    }
}
