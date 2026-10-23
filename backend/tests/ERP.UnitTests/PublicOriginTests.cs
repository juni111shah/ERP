using ERP.Api.Infrastructure;

namespace ERP.UnitTests;

public class PublicOriginTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("https://erp.example.com", "https://erp.example.com")]
    [InlineData("https://erp.example.com/", "https://erp.example.com")]
    [InlineData(" https://ERP-staging.example.com ", "https://erp-staging.example.com")]
    [InlineData("http://localhost:5180", "http://localhost:5180")]
    public void Normalize_returns_origin_or_null(string? input, string? expected) => Assert.Equal(expected, PublicOrigin.Normalize(input));

    [Theory]
    [InlineData("erp.example.com")]
    [InlineData("ftp://erp.example.com")]
    [InlineData("https://erp.example.com/app")]
    [InlineData("https://erp.example.com/?x=1")]
    public void Normalize_rejects_non_origins(string input) => Assert.Throws<InvalidOperationException>(() => PublicOrigin.Normalize(input));

    [Fact]
    public void CorsOrigins_appends_public_origin_once()
    {
        Assert.Equal(["http://localhost:5180", "https://erp-staging.example.com"],
            PublicOrigin.CorsOrigins(["http://localhost:5180", "https://erp-staging.example.com/"], "https://erp-staging.example.com"));
        Assert.Equal(["http://localhost:4200"], PublicOrigin.CorsOrigins(["http://localhost:4200"], null));
    }
}
