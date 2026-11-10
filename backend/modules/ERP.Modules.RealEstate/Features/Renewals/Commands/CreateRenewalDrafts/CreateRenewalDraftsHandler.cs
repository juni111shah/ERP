using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using MediatR;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateRenewalDraftsHandler(IReadRepository<Lease> leases, ISender sender) : ICommandHandler<CreateRenewalDraftsCommand, IReadOnlyList<RenewalDraftResult>>
{
    public async Task<IReadOnlyList<RenewalDraftResult>> Handle(CreateRenewalDraftsCommand cmd, CancellationToken ct)
    {
        // One renewal per lease; a lease that cannot be renewed reports why and the others go on.
        var results = new List<RenewalDraftResult>();
        foreach (var id in cmd.LeaseIds.Distinct())
        {
            try
            {
                var lease = await leases.GetAsync(id, ct);
                var rent = Math.Round(lease.AnnualRent * (1 + cmd.UpliftPercent / 100m), 2);
                var created = await sender.Send(new RenewLeaseCommand(id, new RenewalInput(null, rent, null, null, cmd.UpliftPercent == 0 ? null : $"Renewal with {cmd.UpliftPercent:0.##}% rent change")), ct);
                results.Add(new(id, created.Id, null));
            }
            catch (Exception e) when (e is ConflictException or FieldValidationException or NotFoundException)
            {
                results.Add(new(id, null, e.Message));
            }
        }
        return results;
    }
}
