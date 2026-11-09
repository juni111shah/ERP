namespace ERP.Application.Finance;

/// <summary>Buckets an amount by days past due (shared by receivables and payables ageing).</summary>
internal static class Ageing
{
    public static AgeingRow Add(AgeingRow r, DateOnly asOf, DateOnly due, decimal amount)
    {
        var late = asOf.DayNumber - due.DayNumber;
        return late switch
        {
            <= 0 => r with { Current = r.Current + amount, Total = r.Total + amount },
            <= 30 => r with { Days1To30 = r.Days1To30 + amount, Total = r.Total + amount },
            <= 60 => r with { Days31To60 = r.Days31To60 + amount, Total = r.Total + amount },
            <= 90 => r with { Days61To90 = r.Days61To90 + amount, Total = r.Total + amount },
            _ => r with { Over90 = r.Over90 + amount, Total = r.Total + amount },
        };
    }
}
