using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

internal sealed class CreateHolidayHandler(IRepository<PublicHoliday> holidays) : ICommandHandler<CreateHolidayCommand, HolidayDto>
{
    public async Task<HolidayDto> Handle(CreateHolidayCommand command, CancellationToken ct)
    {
        // 1. One holiday per date.
        var date = command.Input.Date;
        if (await holidays.AnyAsync(h => h.Date == date, ct)) throw new ConflictException($"{date:yyyy-MM-dd} is already a public holiday.");

        // 2. Add and save.
        var h = new PublicHoliday { Date = date, Name = command.Input.Name.Trim() };
        holidays.Add(h);
        await holidays.SaveChangesAsync(ct);
        return HolidayDto.From(h);
    }
}
