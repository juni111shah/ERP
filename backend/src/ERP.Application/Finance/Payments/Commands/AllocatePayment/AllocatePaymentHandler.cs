using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class AllocatePaymentHandler(
    ISettlementRepository settlements,
    ISettlementAllocationRepository allocationRows,
    PaymentIssuing issuing,
    IUnitOfWork uow,
    PaymentDtoBuilder dtos)
    : ICommandHandler<AllocatePaymentCommand, PaymentDto>
{
    public Task<PaymentDto> Handle(AllocatePaymentCommand command, CancellationToken ct) => uow.InTransactionAsync(token => AllocateAsync(command, token), ct);

    private async Task<PaymentDto> AllocateAsync(AllocatePaymentCommand command, CancellationToken ct)
    {
        // 1. Only a posted payment can be allocated.
        var s = await settlements.GetPaymentWithAllocationsForUpdateAsync(command.Id, ct);
        if (s.Status != DocumentStatus.Issued) throw new ConflictException("Only a posted payment can be allocated.");

        // 2. Apply the allocations to the bills and save.
        var applied = await issuing.AllocateAsync(s, command.Allocations, ct);
        allocationRows.AddRange(applied);
        await settlements.SaveChangesAsync(ct);
        return await dtos.BuildAsync(s, ct, s.Allocations.Concat(applied).ToList());
    }
}
