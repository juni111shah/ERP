using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class MoveCandidateHandler(IRepository<Candidate> candidates, IClock clock) : ICommandHandler<MoveCandidateCommand, CandidateDto>
{
    public async Task<CandidateDto> Handle(MoveCandidateCommand command, CancellationToken ct)
    {
        var cmd = command;
        var c = await candidates.GetForUpdateAsync(cmd.Id, ct);
        if (!CandidatePipeline.CanMove(c.Stage, cmd.Input.Stage)) throw new ConflictException($"A candidate in stage '{c.Stage}' cannot move to '{cmd.Input.Stage}'.");
        c.Stage = cmd.Input.Stage; c.StageChangedAt = clock.UtcNow;
        if (!string.IsNullOrWhiteSpace(cmd.Input.Notes)) c.Notes = string.IsNullOrEmpty(c.Notes) ? cmd.Input.Notes.Trim() : c.Notes + "\n" + cmd.Input.Notes.Trim();
        await candidates.SaveChangesAsync(ct);
        return CandidateDto.From(c);
    }
}
