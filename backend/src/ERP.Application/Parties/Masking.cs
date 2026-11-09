namespace ERP.Application.Parties;

public static class Masking
{
    /// <summary>Keeps the last 4 characters: "AE070331234567890123456" → "*******************3456".</summary>
    public static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var keep = Math.Min(4, value.Length / 3);
        return new string('*', value.Length - keep) + value[^keep..];
    }
}
