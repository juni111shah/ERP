using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

internal sealed class GetLeaveDaysHandler(LeaveCalendarProvider calendars) : IQueryHandler<GetLeaveDaysQuery, LeaveDaysDto>
{
    public async Task<LeaveDaysDto> Handle(GetLeaveDaysQuery query, CancellationToken ct)
    {
        var q = query;
        if (q.To < q.From) throw new FieldValidationException("to", "The period must end on or after it starts.");
        if (q.To.DayNumber - q.From.DayNumber > 365) throw new FieldValidationException("to", "At most a year at a time.");
        var calendar = await calendars.LoadAsync(q.From, q.To, ct);
        return new(q.From, q.To, calendar.Count(q.From, q.To));
    }
}
