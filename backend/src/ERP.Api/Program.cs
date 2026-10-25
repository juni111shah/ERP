using ERP.Application.Abstractions;
using System.Text;
using System.Threading.RateLimiting;
using ERP.Api.Endpoints;
using ERP.Api.Infrastructure;
using ERP.Infrastructure;
using ERP.Application.Auth;
using ERP.Infrastructure.Auth;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using ERP.Modularity;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.CountryPacks.AE;
using ERP.CountryPacks.GB;
using ERP.CountryPacks.IN;
using ERP.CountryPacks.PK;
using ERP.CountryPacks.SA;
using ERP.CountryPacks.US;
using ERP.Modules.Inventory;
using ERP.Modules.RealEstate;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// When hosted as a Windows service (staging/production on the deploy PC) the content root is the install folder.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null,
});
builder.Services.AddWindowsService(); // no-op unless the process was started by the Windows service control manager

// ERP_-prefixed environment variables (e.g. ERP_Hosting__PublicUrl) override everything else; deploy scripts and the
// named-tunnel setup use them so a machine-specific value never has to be committed.
builder.Configuration.AddEnvironmentVariables(prefix: "ERP_");
// Public origin (named-tunnel hostname). Empty by default: localhost and quick-tunnel behaviour unchanged.
var publicOrigin = PublicOrigin.Normalize(builder.Configuration[PublicOrigin.Key]);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteTo.Console());

// ---- Module composition (the host is the only place that references module assemblies) ----
var modules = ModuleCatalog.Select(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, modules);
// ---- Country packs (the host is the only place that references pack assemblies) ----
foreach (var pack in CountryPackCatalog.All()) builder.Services.AddSingleton(pack);

// Background housekeeping (lease expiry and similar). Always resolvable so it can be triggered; only started by the host when enabled.
builder.Services.AddSingleton(sp => new ERP.Infrastructure.Services.ScheduledJobRunner(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<ILogger<ERP.Infrastructure.Services.ScheduledJobRunner>>(),
    TimeSpan.FromMinutes(builder.Configuration.GetValue("Jobs:IntervalMinutes", 360)), TimeSpan.FromSeconds(30)));
if (builder.Configuration.GetValue("Jobs:Enabled", true)) builder.Services.AddHostedService(sp => sp.GetRequiredService<ERP.Infrastructure.Services.ScheduledJobRunner>());
builder.Services.AddSingleton<INotificationPusher, SignalRNotificationPusher>();

// ---- Auth ----
var auth = builder.Configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
if (Encoding.UTF8.GetByteCount(auth.SigningKey) < 32)
    throw new InvalidOperationException("Auth:SigningKey must be at least 32 bytes. Set it via environment variable Auth__SigningKey or user secrets.");

builder.Services.AddDistributedMemoryCache(); // swap for Redis (AddStackExchangeRedisCache) when scaled out
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = auth.Issuer, ValidAudience = auth.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "name",
    };
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            // Query-string tokens only for the SignalR hub.
            if (ctx.HttpContext.Request.Path.StartsWithSegments("/hubs") && ctx.Request.Query.TryGetValue("access_token", out var t)) ctx.Token = t;
            return Task.CompletedTask;
        },
        OnTokenValidated = async ctx =>
        {
            var jti = ctx.Principal?.FindFirst("jti")?.Value;
            if (string.IsNullOrEmpty(jti) || ctx.Principal?.FindFirst(ErpClaims.Org) is null) { ctx.Fail("Missing claims."); return; }
            var cache = ctx.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
            if (await cache.GetStringAsync(AuthService.DenyKey(jti)) is not null) ctx.Fail("Token revoked.");
        },
    };
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization(o =>
{
    var staff = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireAssertion(ctx => !ctx.User.HasClaim(c => c.Type == "portal_party"))
        .Build();
    o.DefaultPolicy = staff;
    o.FallbackPolicy = staff;
    o.AddPolicy("portal", p => p.RequireAuthenticatedUser().RequireClaim("portal_party"));
    o.AddPolicy("portal.customer", p => p.RequireAuthenticatedUser().RequireClaim("portal_party").RequireClaim("portal_kind", "customer"));
    o.AddPolicy("portal.vendor", p => p.RequireAuthenticatedUser().RequireClaim("portal_party").RequireClaim("portal_kind", "vendor"));
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var permitLimit = builder.Configuration.GetValue("RateLimiting:LoginPerMinute", 10);
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) }));
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        await ApiEnvelope.WriteErrorAsync(ctx.HttpContext, StatusCodes.Status429TooManyRequests, "Too many attempts",
            "Too many attempts. Please wait a minute and try again.", extensions: new Dictionary<string, object?> { ["retryAfterSeconds"] = 60 });
    };
});

// ---- Cross-cutting ----
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = builder.Configuration["Product:Name"] ?? "ERP", Version = "v1" });
    c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
    c.AddSecurityDefinition("Bearer", new() { Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    c.AddSecurityRequirement(new() { { new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }, [] } });
});
// Behind a reverse proxy / tunnel (cloudflared on the same machine): trust X-Forwarded-For/Proto from loopback only,
// so rate limiting sees the real client IP and Request.IsHttps reflects the public https URL.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 2;
});
if (publicOrigin is not null) builder.Services.PostConfigure<EmailOptions>(o => o.AppBaseUrl = publicOrigin); // links in emails
builder.Services.AddCors(o => o.AddPolicy("web", p => p
    .WithOrigins(PublicOrigin.CorsOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"], publicOrigin))
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    o.SerializerOptions.MaxDepth = 16;
});

var app = builder.Build();

// A published build can carry the compiled Angular app in wwwroot (deploy packages do); then the API serves it as
// one origin. In development there is no wwwroot, ng serve hosts the UI and / keeps redirecting to Swagger.
var serveSpa = app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists;

app.UseForwardedHeaders();
app.UseExceptionHandler();
// Empty-bodied API errors (401 challenge, 403 forbid, 404 no route, 405) get the same error envelope.
app.UseStatusCodePages(async ctx =>
{
    var http = ctx.HttpContext;
    if (http.Request.Path.StartsWithSegments("/api") && !http.Response.HasStarted && http.Response.ContentLength is null && string.IsNullOrEmpty(http.Response.ContentType))
        await ApiEnvelope.WriteErrorAsync(http, http.Response.StatusCode, null, null);
});
app.UseSerilogRequestLogging();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers.XFrameOptions = "DENY";
    await next();
});
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) app.UseHsts();
var swaggerEnabled = app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (serveSpa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}
app.UseCors("web");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ModuleGateMiddleware>();
app.UseAuthorization();
app.UseMiddleware<LegalEntityScopeMiddleware>();

// Every /api/v1 endpoint (core and modules) returns the standard ApiResponse envelope.
var api = app.MapGroup("/api/v1");
api.AddEndpointFilter<ApiEnvelopeFilter>();
api.MapAuth();
api.MapCore();

// Module endpoints: /api/v1/m/{key}/..., authenticated and gated per workspace (403 module-disabled).
var registry = app.Services.GetRequiredService<ModuleRegistry>();
foreach (var module in registry.Modules)
{
    var group = api.MapGroup($"/m/{module.Manifest.Key}")
        .WithMetadata(new ModuleGateMetadata(module.Manifest.Key))
        .WithTags($"Module: {module.Manifest.Name}");
    module.MapEndpoints(group);
}

app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapHealthChecks("/health").AllowAnonymous();
if (serveSpa)
{
    // Client-side routes (e.g. /auth/login) fall back to index.html; unknown /api, /hubs and /swagger paths still 404.
    app.MapFallbackToFile("{*path:regex(^(?!api/|hubs/|swagger/|health$).*$)}", "index.html").AllowAnonymous();
}
else if (swaggerEnabled)
{
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription().AllowAnonymous();
}

await DatabaseStartup.InitializeAsync(app.Services, app.Configuration);
app.Run();

/// <summary>Installed country packs (the host is the only place that references pack assemblies).</summary>
public static class CountryPackCatalog
{
    public static IReadOnlyList<ICountryPack> All() =>
        [new UaeCountryPack(), new SaudiCountryPack(), new PakistanCountryPack(), new IndiaCountryPack(), new UnitedKingdomCountryPack(), new UnitedStatesCountryPack()];
}

/// <summary>Installed modules. ERP_MODULES / Modules:Installed (comma list) narrows them; an empty value runs the core alone.</summary>
public static class ModuleCatalog
{
    public static IReadOnlyList<IModule> All() => [new RealEstateModule(), new InventoryModule(), new ERP.Modules.Hr.HrModule(), new ERP.Modules.Projects.ProjectsModule(), new ERP.Modules.Sales.SalesModule(), new ERP.Modules.Purchasing.PurchasingModule(), new ERP.Modules.Pos.PosModule(), new ERP.Modules.Retail.RetailModule(), new ERP.Modules.Distribution.DistributionModule(), new ERP.Modules.Manufacturing.ManufacturingModule(), new ERP.Modules.Crm.CrmModule(), new ERP.Modules.Services.ServicesModule()];

    public static IReadOnlyList<IModule> Select(IConfiguration config)
    {
        var raw = Environment.GetEnvironmentVariable("ERP_MODULES") ?? config["Modules:Installed"];
        if (raw is null) return All();
        var keys = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return All().Where(m => keys.Contains(m.Manifest.Key)).ToList();
    }
}

public static class DatabaseStartup
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (config.GetValue<bool>("Database:AutoMigrate"))
        {
            await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(db);
        }
        await DevSeeder.SeedAsync(scope.ServiceProvider);
        await ProgramSeeder.SeedModulesAsync(scope.ServiceProvider);
    }
}

public partial class Program;
