using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class AllocateReceiptHandler(
    ISettlementRepository settlements,
    ISettlementAllocationRepository allocationRows,
    ISettlementService settlementService,
    ReceiptDtoBuilder dtos)
    : ICommandHandler<AllocateReceiptCommand, ReceiptDto>
{
    public async Task<ReceiptDto> Handle(AllocateReceiptCommand command, CancellationToken ct)
    {
        // 1. Only a posted receipt can be allocated.
        var s = await settlements.GetReceiptWithAllocationsForUpdateAsync(command.Id, ct);
        if (s.Status != DocumentStatus.Issued) throw new ConflictException("Only a posted receipt can be allocated.");

        // 2. Apply the allocations and save.
        var applied = await settlementService.AllocateAsync(s, command.Allocations, ct);
        allocationRows.AddRange(applied);
        await settlements.SaveChangesAsync(ct);
        return await dtos.BuildAsync(s, ct, s.Allocations.Concat(applied).ToList());
    }
}
