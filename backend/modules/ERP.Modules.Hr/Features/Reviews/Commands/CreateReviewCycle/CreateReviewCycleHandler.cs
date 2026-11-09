using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateReviewCycleHandler(IReviewCycleRepository cycles) : ICommandHandler<CreateReviewCycleCommand, ReviewCycleDto>
{
    public async Task<ReviewCycleDto> Handle(CreateReviewCycleCommand command, CancellationToken ct)
    {
        var name = command.Input.Name.Trim();
        if (await cycles.AnyAsync(c => c.Name == name, ct)) throw new ConflictException($"A review cycle named '{name}' already exists.");
        var cycle = new ReviewCycle { Name = name, PeriodStart = command.Input.PeriodStart, PeriodEnd = command.Input.PeriodEnd, Status = command.Input.Status };
        cycles.Add(cycle);
        await cycles.SaveChangesAsync(ct);
        return ReviewCycleDto.From(cycle);
    }
}
