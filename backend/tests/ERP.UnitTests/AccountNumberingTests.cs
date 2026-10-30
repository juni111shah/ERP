using ERP.Domain.Finance;

namespace ERP.UnitTests;

/// <summary>The pure rules of the hierarchical chart-of-accounts numbering mask.</summary>
public class AccountNumberingTests
{
    private static AccountCodeMask Mask(string sep = "-", params int[] segments) => new(AccountCodeMask.Hierarchical, segments.Length == 0 ? [4, 2, 2] : segments, sep);

    [Fact]
    public void Default_is_free_and_leaves_existing_four_digit_codes_alone()
    {
        Assert.False(AccountCodeMask.Default.IsHierarchical);
        Assert.Null(AccountCodeMask.Default.Validate());
    }

    [Theory]
    [InlineData("1000", 1)]
    [InlineData("1000-01", 2)]
    [InlineData("1000-01-01", 3)]
    [InlineData("100", 0)]
    [InlineData("1000-1", 0)]
    [InlineData("1000_01", 0)]
    [InlineData("1000-01-01-01", 0)]
    [InlineData("10A0", 0)]
    [InlineData("", 0)]
    public void Level_of_a_code_follows_the_segments(string code, int level) => Assert.Equal(level, Mask().LevelOf(code));

    [Fact]
    public void Parent_code_and_last_segment_are_read_from_the_code()
    {
        var m = Mask();
        Assert.Null(m.ParentCodeOf("1000"));
        Assert.Equal("1000", m.ParentCodeOf("1000-01"));
        Assert.Equal("1000-01", m.ParentCodeOf("1000-01-02"));
        Assert.Equal(2, m.LastSegment("1000-01-02"));
        Assert.Equal(1000, m.LastSegment("1000"));
        Assert.Equal(-1, m.LastSegment("abc"));
    }

    [Fact]
    public void Children_are_formatted_zero_padded_and_overflow_is_reported()
    {
        var m = Mask();
        Assert.Equal("1000-01", m.Format("1000", 1));
        Assert.Equal("1000-01-01", m.Format("1000-01", 1));
        Assert.Equal("1000-99", m.Format("1000", 99));
        Assert.Throws<InvalidOperationException>(() => m.Format("1000", 100));
        Assert.Equal(99, m.MaxSegment(2));
        Assert.Equal("0000-00-00", m.Pattern);
    }

    [Fact]
    public void An_empty_separator_works_by_fixed_lengths()
    {
        var m = Mask("", 4, 2, 2);
        Assert.Equal(3, m.LevelOf("10000101"));
        Assert.Equal("100001", m.ParentCodeOf("10000101"));
        Assert.Equal("100001", m.Format("1000", 1));
    }

    [Theory]
    [InlineData("free", new[] { 4, 2, 2 }, "-", true)]
    [InlineData("hierarchical", new[] { 4 }, "", true)]
    [InlineData("hierarchical", new[] { 6, 6, 6 }, "-", true)] // exactly 20 characters
    [InlineData("hierarchical", new[] { 6, 6, 6, 6 }, "-", false)] // 27 characters
    [InlineData("hierarchical", new int[0], "-", false)]
    [InlineData("hierarchical", new[] { 1, 1, 1, 1, 1, 1, 1 }, "-", false)]
    [InlineData("hierarchical", new[] { 7 }, "-", false)]
    [InlineData("hierarchical", new[] { 4, 2 }, "#", false)]
    [InlineData("other", new[] { 4 }, "-", false)]
    public void Masks_are_validated(string mode, int[] segments, string separator, bool valid)
        => Assert.Equal(valid, new AccountCodeMask(mode, segments, separator).Validate() is null);
}
