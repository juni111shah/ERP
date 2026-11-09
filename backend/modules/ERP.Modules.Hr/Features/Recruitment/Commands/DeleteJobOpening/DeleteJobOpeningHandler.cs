using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteJobOpeningHandler(IRepository<JobOpening> openings, IReadRepository<Candidate> candidates) : ICommandHandler<DeleteJobOpeningCommand>
{
    public async Task<Unit> Handle(DeleteJobOpeningCommand command, CancellationToken ct)
    {
        // 1. A vacancy with candidates stays.
        var o = await openings.GetForUpdateAsync(command.Id, ct);
        var id = o.Id;
        if (await candidates.AnyAsync(c => c.OpeningId == id, ct)) throw new ConflictException("This vacancy has candidates. Close it instead of deleting it.");

        // 2. Remove and save.
        openings.Remove(o);
        await openings.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
