using System.Net;
using System.Net.Sockets;

namespace ERP.Application.Platform;

/// <summary>Encrypts the keys of AI services for storage (implemented in Infrastructure).</summary>
public interface IAiKeyProtector
{
    string Protect(string apiKey);
    /// <summary>Null when the key can no longer be read, for example after restoring a backup on another machine.</summary>
    string? TryUnprotect(string encrypted);
}

public sealed record AiConnectionResult(bool Ok, string Message);

/// <summary>Makes one small real call to an AI service. The message is safe to store and show: it never holds the key or the service's own reply.</summary>
public interface IAiConnectionTester
{
    Task<AiConnectionResult> TestAsync(string baseUrl, string model, string apiKey, CancellationToken ct = default);
}

/// <summary>Which addresses a key may be sent to: public HTTPS only, so a stored key can never reach a machine on the server's own network.</summary>
public static class AiEndpointRules
{
    public static string Normalize(string? url) => (url ?? "").Trim().TrimEnd('/');

    /// <summary>The problem with this address, or null when it is fine.</summary>
    public static string? Problem(string? url)
    {
        if (!Uri.TryCreate(Normalize(url), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return "Enter a full address that starts with https://.";
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return "Enter only the address, with no sign-in details or query.";
        var host = uri.IdnHost.TrimEnd('.');
        if (!host.Contains('.') || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase))
            return "Use the service's public address.";
        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IsPrivate(ip)) return "Use the service's public address.";
        return null;
    }

    /// <summary>Loopback, private, link-local and similar ranges that a public service never uses.</summary>
    public static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal || ip.IsIPv6Multicast;
        var b = ip.GetAddressBytes();
        return b[0] is 0 or 10 or >= 224 || (b[0] == 100 && b[1] >= 64 && b[1] <= 127) || (b[0] == 169 && b[1] == 254)
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
    }
}
