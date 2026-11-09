using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using MediatR;

namespace ERP.Modules.Hr.Features;

internal sealed class DeleteHolidayHandler(IRepository<PublicHoliday> holidays) : ICommandHandler<DeleteHolidayCommand>
{
    public async Task<Unit> Handle(DeleteHolidayCommand command, CancellationToken ct)
    {
        holidays.Remove(await holidays.GetForUpdateAsync(command.Id, ct));
        await holidays.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
