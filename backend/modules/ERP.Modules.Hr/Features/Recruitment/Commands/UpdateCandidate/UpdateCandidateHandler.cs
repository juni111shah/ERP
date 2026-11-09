using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateCandidateHandler(IRepository<Candidate> candidates) : ICommandHandler<UpdateCandidateCommand, CandidateDto>
{
    public async Task<CandidateDto> Handle(UpdateCandidateCommand command, CancellationToken ct)
    {
        var c = await candidates.GetForUpdateAsync(command.Id, ct);
        if (command.Input.OpeningId != c.OpeningId) throw new FieldValidationException("openingId", "A candidate cannot move to another vacancy.");
        RecruitmentMapping.Fill(c, command.Input);
        await candidates.SaveChangesAsync(ct);
        return CandidateDto.From(c);
    }
}
