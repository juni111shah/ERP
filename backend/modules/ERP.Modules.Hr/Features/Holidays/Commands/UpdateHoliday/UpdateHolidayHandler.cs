using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class UpdateHolidayHandler(IRepository<PublicHoliday> holidays) : ICommandHandler<UpdateHolidayCommand, HolidayDto>
{
    public async Task<HolidayDto> Handle(UpdateHolidayCommand command, CancellationToken ct)
    {
        // 1. Load it; a changed date must still be free.
        var h = await holidays.GetForUpdateAsync(command.Id, ct);
        var date = command.Input.Date;
        if (date != h.Date && await holidays.AnyAsync(x => x.Date == date, ct)) throw new ConflictException($"{date:yyyy-MM-dd} is already a public holiday.");

        // 2. Apply and save.
        h.Date = date; h.Name = command.Input.Name.Trim();
        await holidays.SaveChangesAsync(ct);
        return HolidayDto.From(h);
    }
}
