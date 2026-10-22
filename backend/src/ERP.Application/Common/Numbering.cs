using System.Globalization;
using System.Text.RegularExpressions;

namespace ERP.Application.Common;

public static class NumberFormatter
{
    private static readonly Regex Seq = new(@"\{seq(?::(\d+))?\}", RegexOptions.Compiled);

    /// <summary>Formats a pattern such as "{prefix}-{yyyy}-{seq:6}" → "INV-2026-000001".</summary>
    public static string Format(string pattern, string prefix, DateOnly date, long value)
    {
        var s = pattern.Replace("{prefix}", prefix)
            .Replace("{yyyy}", date.Year.ToString("0000", CultureInfo.InvariantCulture))
            .Replace("{yy}", (date.Year % 100).ToString("00", CultureInfo.InvariantCulture))
            .Replace("{mm}", date.Month.ToString("00", CultureInfo.InvariantCulture));
        return Seq.Replace(s, m => value.ToString(new string('0', m.Groups[1].Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 1), CultureInfo.InvariantCulture));
    }
}
