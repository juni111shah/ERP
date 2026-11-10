using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateCandidateHandler(IReadRepository<JobOpening> openings, IRepository<Candidate> candidates, IClock clock) : ICommandHandler<CreateCandidateCommand, CandidateDto>
{
    public async Task<CandidateDto> Handle(CreateCandidateCommand command, CancellationToken ct)
    {
        // 1. Only for a vacancy that is not closed.
        var i = command.Input;
        var opening = await openings.FindAsync(i.OpeningId, ct) ?? throw new FieldValidationException("openingId", "Choose a vacancy.");
        if (opening.Status == OpeningStatus.Closed) throw new ConflictException("This vacancy is closed.");

        // 2. Add and save.
        var c = new Candidate { OpeningId = opening.Id, Stage = CandidateStage.Applied, StageChangedAt = clock.UtcNow };
        RecruitmentMapping.Fill(c, i);
        candidates.Add(c);
        await candidates.SaveChangesAsync(ct);
        return CandidateDto.From(c);
    }
}
