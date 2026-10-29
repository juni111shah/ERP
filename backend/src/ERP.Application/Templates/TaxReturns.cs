using ERP.Domain.Finance;

namespace ERP.Application.Templates;

public enum TaxSide { Sales, Purchases }
public enum TaxMeasure { Net, Tax }

/// <summary>
/// One aggregated line of the period's tax: everything issued (sales, credit notes negative) or posted (purchases) under one tax code.
/// Zero-rated / exempt / reverse-charge are classified from the tax code itself, so no schema is needed.
/// </summary>
public sealed record TaxLine(TaxSide Side, string Code, TaxKind Kind, decimal Rate, bool Recoverable, decimal Net, decimal Tax)
{
    /// <summary>Rate 0 and not recoverable (the template "Exempt" code).</summary>
    public bool IsExempt => Rate == 0 && !Recoverable;
    /// <summary>Rate 0 but recoverable: a taxable supply at zero percent.</summary>
    public bool IsZeroRated => Rate == 0 && Recoverable;
    /// <summary>Reverse charge: a tax code whose code starts with "RC" (convention; there is no flag on the tax code).</summary>
    public bool IsReverseCharge => Code.StartsWith("RC", StringComparison.OrdinalIgnoreCase);
    /// <summary>A positive-rate code that is not reverse charge.</summary>
    public bool IsStandard => Rate > 0 && !IsReverseCharge;
}

/// <summary>
/// A box on a statutory return. Either it sums <see cref="Measure"/> over the lines that satisfy <see cref="Match"/> (times <see cref="Sign"/>),
/// or it is computed from earlier boxes by <see cref="Formula"/> (looked up by key).
/// </summary>
public sealed record TaxReturnBox(string Key, string Label, string LabelAr, Func<TaxLine, bool>? Match = null, TaxMeasure Measure = TaxMeasure.Net, decimal Sign = 1m,
    Func<IReadOnlyDictionary<string, decimal>, decimal>? Formula = null, bool IsTotal = false);

/// <summary>A country's periodic tax return: boxes in display order, which box is the net amount payable, and an optional generator of extra rows from the data (for example one row per rate).</summary>
public sealed record TaxReturnDefinition(string Code, string Title, string TitleAr, IReadOnlyList<TaxReturnBox> Boxes, string NetPayableKey,
    Func<IReadOnlyList<TaxLine>, IReadOnlyList<TaxReturnBox>>? Dynamic = null, string? Notes = null);

public sealed record TaxReturnRow(string Key, string Label, string LabelAr, decimal Amount, bool IsTotal);

/// <summary>Fills a return definition from the period's tax lines. Pure function: easy to test per country with fabricated lines.</summary>
public static class TaxReturnCalculator
{
    public static (IReadOnlyList<TaxReturnRow> Rows, decimal NetPayable) Compute(TaxReturnDefinition def, IReadOnlyList<TaxLine> lines)
    {
        var values = new Dictionary<string, decimal>();
        var rows = new List<TaxReturnRow>();
        var boxes = (def.Dynamic?.Invoke(lines) ?? []).Concat(def.Boxes);
        foreach (var box in boxes)
        {
            var amount = box.Formula is { } f
                ? f(values)
                : lines.Where(l => box.Match?.Invoke(l) == true).Sum(l => (box.Measure == TaxMeasure.Net ? l.Net : l.Tax)) * box.Sign;
            amount = Math.Round(amount, 2);
            values[box.Key] = amount;
            rows.Add(new(box.Key, box.Label, box.LabelAr, amount, box.IsTotal));
        }
        return (rows, values.GetValueOrDefault(def.NetPayableKey));
    }
}

/// <summary>What a country expects on a tax invoice. Missing items are surfaced as warnings when an invoice is issued, never as a hard block.</summary>
public enum InvoiceField { SellerTaxNumber, CustomerTaxNumber }

/// <summary>Short-hand builders so each country pack declares its return as data.</summary>
public static class TaxBoxes
{
    public static TaxReturnBox Sum(string key, string label, string labelAr, Func<TaxLine, bool> match, TaxMeasure measure = TaxMeasure.Net, decimal sign = 1m)
        => new(key, label, labelAr, match, measure, sign);

    public static TaxReturnBox Calc(string key, string label, string labelAr, Func<IReadOnlyDictionary<string, decimal>, decimal> formula, bool total = false)
        => new(key, label, labelAr, Formula: formula, IsTotal: total);

    public static decimal V(this IReadOnlyDictionary<string, decimal> v, string key) => v.GetValueOrDefault(key);
}
