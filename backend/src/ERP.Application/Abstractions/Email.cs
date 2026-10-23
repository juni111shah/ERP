namespace ERP.Application.Abstractions;

/// <summary>Outbound email. The SMTP implementation lives in Infrastructure; swap it in DI for a provider API.</summary>
public interface IEmailSender
{
    /// <summary>False when email is switched off or not configured: callers then skip sending (and the message log says so).</summary>
    bool IsEnabled { get; }
    /// <summary>Sends one message; throws when the server refuses it, so callers can record the failure and retry.</summary>
    Task SendAsync(string to, string subject, string bodyText, string? bodyHtml = null, CancellationToken ct = default);
}

/// <summary>Settings of the "Email" configuration section. Real values come from user-secrets or environment variables, never from source control.</summary>
public sealed class EmailOptions
{
    public const string Section = "Email";
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "ERP";
    /// <summary>Where the web app is served; used to build links in emails (password reset).</summary>
    public string AppBaseUrl { get; set; } = "http://localhost:4200";
}
