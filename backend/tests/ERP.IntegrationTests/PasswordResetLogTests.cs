using ERP.Application.Abstractions;
using ERP.Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.IntegrationTests;

/// <summary>A reset link is a live credential: with email off it may be logged on a developer's machine and nowhere else.</summary>
public class PasswordResetLogTests
{
    private const string Token = "reset-token-value";

    private sealed class CapturingLogger : ILogger<LoggingPasswordResetSender>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }

    private sealed class EmailOff : IEmailSender
    {
        public bool IsEnabled => false;
        public Task SendAsync(string to, string subject, string bodyText, string? bodyHtml = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<List<string>> SendAsync(string environment)
    {
        var log = new CapturingLogger();
        var services = new ServiceCollection()
            .AddSingleton<ILogger<LoggingPasswordResetSender>>(log)
            .AddSingleton<IEmailSender, EmailOff>()
            .AddSingleton(Options.Create(new EmailOptions { AppBaseUrl = "https://app.example" }))
            .AddSingleton<IHostEnvironment>(new Environment(environment))
            .BuildServiceProvider();
        await ActivatorUtilities.CreateInstance<LoggingPasswordResetSender>(services).SendAsync("someone@demo.local", Token);
        return log.Lines;
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task Outside_development_the_link_is_never_logged(string environment)
        => Assert.DoesNotContain(await SendAsync(environment), line => line.Contains(Token));

    [Fact]
    public async Task In_development_the_link_is_logged()
        => Assert.Contains(await SendAsync("Development"), line => line.Contains(Token));
}
