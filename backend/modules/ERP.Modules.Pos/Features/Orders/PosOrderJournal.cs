namespace ERP.Modules.Pos.Features;

/// <summary>The journal source of counter sales, used when a sale posts and when its void reverses that entry.</summary>
internal static class PosOrderJournal
{
    public const string Source = "pos";
}
