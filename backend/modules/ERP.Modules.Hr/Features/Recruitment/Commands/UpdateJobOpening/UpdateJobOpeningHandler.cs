using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateJobOpeningHandler(IRepository<JobOpening> openings, ICandidateRepository candidates) : ICommandHandler<UpdateJobOpeningCommand, JobOpeningDto>
{
    public async Task<JobOpeningDto> Handle(UpdateJobOpeningCommand command, CancellationToken ct)
    {
        var o = await openings.GetForUpdateAsync(command.Id, ct);
        RecruitmentMapping.Fill(o, command.Input);
        await openings.SaveChangesAsync(ct);
        return JobOpeningDto.From(o, await candidates.ListStagesOfOpeningAsync(o.Id, ct));
    }
}
