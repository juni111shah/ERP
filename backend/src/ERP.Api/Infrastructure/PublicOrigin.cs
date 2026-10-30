namespace ERP.Api.Infrastructure;

/// <summary>
/// The public origin an environment is reached at (e.g. a Cloudflare named-tunnel hostname), from <c>Hosting:PublicUrl</c>.
/// Set it once per environment (deploy scripts pass <c>ERP_Hosting__PublicUrl</c>). When set, it is added to the CORS
/// origins and used as <c>Email:AppBaseUrl</c> for links in emails. When empty, nothing changes (localhost and quick-tunnel
/// behaviour stay as they are).
/// </summary>
public static class PublicOrigin
{
    public const string Key = "Hosting:PublicUrl";

    /// <summary>Returns <c>scheme://host[:port]</c> without a trailing slash, or null when unset. Throws on an invalid value.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || (uri.AbsolutePath != "/" && uri.AbsolutePath.Length > 0) || !string.IsNullOrEmpty(uri.Query))
            throw new InvalidOperationException($"{Key} must be an absolute http(s) origin such as https://erp.example.com (no path or query).");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    /// <summary>The configured CORS origins plus the public origin (if any), de-duplicated.</summary>
    public static string[] CorsOrigins(IEnumerable<string>? configured, string? publicOrigin)
    {
        var list = new List<string>();
        foreach (var o in configured ?? []) if (!string.IsNullOrWhiteSpace(o)) list.Add(o.Trim().TrimEnd('/'));
        if (publicOrigin is not null) list.Add(publicOrigin);
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
