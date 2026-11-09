namespace ERP.Modules.Hr.Features;

/// <summary>Folds one punch into the day's existing times; no I/O.</summary>
public static class PunchMath
{
    public static (TimeOnly CheckIn, TimeOnly? CheckOut) Apply(TimeOnly? checkIn, TimeOnly? checkOut, TimeOnly at)
    {
        if (checkIn is not { } ci) return (at, checkOut is { } o && o > at ? o : null);
        if (at < ci) return (at, checkOut ?? ci);   // an earlier punch arrives late: it becomes the check-in, the old one the check-out
        if (at == ci) return (ci, checkOut);
        return (ci, checkOut is { } co && co >= at ? co : at);
    }
}
