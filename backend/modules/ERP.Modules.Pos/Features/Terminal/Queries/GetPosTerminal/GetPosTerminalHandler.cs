using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class GetPosTerminalHandler(IPosRegisterRepository registers, IPosSessionRepository sessions, ISettingsService settings)
    : IQueryHandler<GetPosTerminalQuery, PosTerminalDto>
{
    public async Task<PosTerminalDto> Handle(GetPosTerminalQuery request, CancellationToken ct)
    {
        // 1. The active registers, each with its open shift.
        var active = await registers.ListActiveByNameAsync(ct);
        var ids = active.Select(r => r.Id).ToList();
        var open = await sessions.ListOpenOfRegistersAsync(ids, ct);
        var rows = active.Select(r => new PosTerminalRegisterDto(r.Id, r.LegalEntityId, r.Code, r.Name, r.WarehouseId, r.ReceiptHeader, r.ReceiptFooter,
            open.FirstOrDefault(s => s.RegisterId == r.Id)?.Id)).ToList();

        // 2. The till settings of the first register's legal entity.
        var entity = active.FirstOrDefault()?.LegalEntityId;
        return new PosTerminalDto(rows, new PosTerminalSettingsDto(
            await settings.GetAsync<decimal?>("pos.maxDiscountPercent", entity, ct) ?? 100m,
            await settings.GetAsync<decimal?>("pos.cashierDiscountPercent", entity, ct),
            await settings.GetAsync<bool?>("pos.allowPartialPayment", entity, ct) ?? false,
            await settings.GetAsync<bool?>("pos.allowNegativeDrawer", entity, ct) ?? false,
            await settings.GetAsync<bool?>("pos.allowNegativeStock", entity, ct) ?? true,
            await settings.GetAsync<bool?>("pos.enforceFloatAtOpen", entity, ct) ?? true));
    }
}
