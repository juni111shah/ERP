using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class ChangeListingStatusHandler(IRepository<Listing> listings, IClock clock) : ICommandHandler<ChangeListingStatusCommand, ListingDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<ListingDto> Handle(ChangeListingStatusCommand cmd, CancellationToken ct)
    {
        // 1. Only the allowed moves.
        var l = await listings.GetForUpdateAsync(cmd.Id, ct);
        var ok = (l.Status, cmd.Status) switch
        {
            (ListingStatus.Draft, ListingStatus.Published) => true,
            (ListingStatus.Published, ListingStatus.Closed) => true,
            (ListingStatus.Published, ListingStatus.Draft) => true,
            (ListingStatus.Closed, ListingStatus.Draft) => true,
            _ => false,
        };
        if (!ok) throw new ConflictException($"A {l.Status} listing cannot become {cmd.Status}.");

        // 2. Stamp the listed and closed dates, save.
        if (cmd.Status == ListingStatus.Published) { l.ListedOn ??= Today; l.ClosedOn = null; }
        if (cmd.Status == ListingStatus.Closed) l.ClosedOn = Today;
        if (cmd.Status == ListingStatus.Draft) l.ClosedOn = null;
        l.Status = cmd.Status;
        await listings.SaveChangesAsync(ct);
        return ListingDto.From(l);
    }
}
