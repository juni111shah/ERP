using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateReviewCycleHandler(IReviewCycleRepository cycles) : ICommandHandler<UpdateReviewCycleCommand, ReviewCycleDto>
{
    public async Task<ReviewCycleDto> Handle(UpdateReviewCycleCommand command, CancellationToken ct)
    {
        var cycle = await cycles.GetForUpdateAsync(command.Id, ct);
        var name = command.Input.Name.Trim();
        if (name != cycle.Name && await cycles.AnyAsync(c => c.Name == name && c.Id != cycle.Id, ct)) throw new ConflictException($"A review cycle named '{name}' already exists.");
        cycle.Name = name; cycle.PeriodStart = command.Input.PeriodStart; cycle.PeriodEnd = command.Input.PeriodEnd; cycle.Status = command.Input.Status;
        await cycles.SaveChangesAsync(ct);
        return ReviewCycleDto.From(cycle);
    }
}
