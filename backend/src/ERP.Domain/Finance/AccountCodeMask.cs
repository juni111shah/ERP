namespace ERP.Domain.Finance;

/// <summary>
/// How account codes are numbered in a workspace. <c>free</c> keeps today's behaviour (any text up to 20 characters).
/// <c>hierarchical</c> builds codes level by level: a root code is Segments[0] digits, a child is its parent's code, the separator and
/// the next Segments[level] digits (1000 -> 1000-01 -> 1000-01-01). Pure rules, no data access.
/// </summary>
public sealed record AccountCodeMask(string Mode, IReadOnlyList<int> Segments, string Separator)
{
    public const string Free = "free";
    public const string Hierarchical = "hierarchical";
    public const int MaxCodeLength = 20;
    public static readonly string[] Separators = ["", "-", ".", "/"];

    public static AccountCodeMask Default { get; } = new(Free, [4, 2, 2], "-");

    public bool IsHierarchical => Mode == Hierarchical;

    /// <summary>Null when the mask is usable, otherwise what is wrong with it (shown to the user).</summary>
    public string? Validate()
    {
        if (Mode is not (Free or Hierarchical)) return "Mode must be free or hierarchical.";
        if (Segments is null || Segments.Count is < 1 or > 6) return "Use between 1 and 6 segments.";
        if (Segments.Any(s => s is < 1 or > 6)) return "Each segment must be 1 to 6 digits long.";
        if (!Separators.Contains(Separator ?? "")) return "The separator must be empty, -, . or /.";
        if (MaxLength > MaxCodeLength) return $"The full code would be {MaxLength} characters; the maximum is {MaxCodeLength}.";
        return null;
    }

    /// <summary>Length of a code at the deepest level.</summary>
    public int MaxLength => Segments.Sum() + (Segments.Count - 1) * (Separator ?? "").Length;

    /// <summary>Human hint such as "0000-00-00".</summary>
    public string Pattern => string.Join(Separator ?? "", Segments.Select(s => new string('0', s)));

    /// <summary>The level (1-based) a code sits at, or 0 when it does not follow the mask.</summary>
    public int LevelOf(string code)
    {
        var sep = Separator ?? "";
        var pos = 0;
        for (var level = 1; level <= Segments.Count; level++)
        {
            if (level > 1)
            {
                if (pos == code.Length) return level - 1;
                if (pos + sep.Length > code.Length || string.CompareOrdinal(code, pos, sep, 0, sep.Length) != 0) return 0;
                pos += sep.Length;
            }
            var len = Segments[level - 1];
            if (pos + len > code.Length) return 0;
            for (var i = 0; i < len; i++) if (!char.IsAsciiDigit(code[pos + i])) return 0;
            pos += len;
        }
        return pos == code.Length ? Segments.Count : 0;
    }

    /// <summary>The code of the parent level, or null for a root or a code that does not follow the mask.</summary>
    public string? ParentCodeOf(string code)
    {
        var level = LevelOf(code);
        if (level <= 1) return null;
        var sep = Separator ?? "";
        var cut = Segments.Take(level - 1).Sum() + (level - 2) * sep.Length;
        return code[..cut];
    }

    /// <summary>The numeric value of the last segment of a code at its level (for picking the next number); -1 when the code does not follow the mask.</summary>
    public int LastSegment(string code)
    {
        var level = LevelOf(code);
        if (level == 0) return -1;
        return int.Parse(code[^Segments[level - 1]..]);
    }

    /// <summary>Formats the child of <paramref name="parentCode"/> (null = a root) with a segment number.</summary>
    public string Format(string? parentCode, int number)
    {
        var level = parentCode is null ? 1 : LevelOf(parentCode) + 1;
        var len = Segments[level - 1];
        var text = number.ToString().PadLeft(len, '0');
        if (text.Length > len) throw new InvalidOperationException("Segment overflow.");
        return parentCode is null ? text : parentCode + (Separator ?? "") + text;
    }

    /// <summary>Largest number a segment at <paramref name="level"/> can hold (99 for two digits).</summary>
    public int MaxSegment(int level) => (int)Math.Pow(10, Segments[level - 1]) - 1;
}
