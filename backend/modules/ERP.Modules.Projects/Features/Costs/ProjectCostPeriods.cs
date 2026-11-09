namespace ERP.Modules.Projects.Features;

/// <summary>Grouping and dating of cost journals; no I/O.</summary>
public static class ProjectCostPeriods
{
    /// <summary>The date of a month's journal: the last day worked, but never in the future and never in a locked period (the books would refuse it).</summary>
    public static DateOnly JournalDate(DateOnly lastWorkDate, DateOnly today, DateOnly? booksLockedTo)
    {
        var date = lastWorkDate > today ? today : lastWorkDate;
        return booksLockedTo is { } locked && date <= locked ? locked.AddDays(1) : date;
    }
}
