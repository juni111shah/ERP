using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateJobOpeningHandler(IRepository<JobOpening> openings, ICandidateRepository candidates) : ICommandHandler<CreateJobOpeningCommand, JobOpeningDto>
{
    public async Task<JobOpeningDto> Handle(CreateJobOpeningCommand command, CancellationToken ct)
    {
        var o = new JobOpening();
        RecruitmentMapping.Fill(o, command.Input);
        openings.Add(o);
        await openings.SaveChangesAsync(ct);
        return JobOpeningDto.From(o, await candidates.ListStagesOfOpeningAsync(o.Id, ct));
    }
}
