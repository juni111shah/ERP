using ERP.Application.Common;

namespace ERP.Modules.RealEstate.Features;

internal sealed class ProcessLeaseExpiriesHandler(LeaseExpiryProcessor expiry, LeaseDtoFactory factory) : ICommandHandler<ProcessLeaseExpiriesCommand, ExpiryRunResultDto>
{
    private DateOnly Today => factory.Today;

    public Task<ExpiryRunResultDto> Handle(ProcessLeaseExpiriesCommand cmd, CancellationToken ct) => expiry.RunAsync(Today, ct);
}
