using ERP.Application.Templates;
using ERP.Application;
using ERP.Application.Abstractions;
using ERP.Application.Auth;
using ERP.Application.Platform;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Infrastructure.Auth;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using ERP.Modularity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers persistence, identity and core services. <paramref name="modules"/> are the installed modules.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, IReadOnlyList<IModule> modules)
    {
        var registry = new ModuleRegistry(modules);
        services.AddSingleton(registry);
        services.AddSingleton<CountryPackRegistry>(); // packs (ICountryPack) are registered by the host
        foreach (var m in modules) m.ConfigureServices(services, config);
        services.AddApplication(modules.Select(m => m.GetType().Assembly).Append(typeof(ModuleRegistry).Assembly).ToArray());

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IOrgContext, OrgContext>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        var provider = config["Database:Provider"] ?? "SqlServer";
        var cs = config.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");
        services.AddDbContext<AppDbContext>((sp, o) =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var conn = sp.GetService<Microsoft.Data.Sqlite.SqliteConnection>();
                if (conn is not null) o.UseSqlite(conn); else o.UseSqlite(cs);
            }
            else o.UseSqlServer(cs, s => s.EnableRetryOnFailure(3));
            o.ReplaceService<IModelCacheKeyFactory, ModuleAwareModelCacheKeyFactory>();
            o.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IEntityStore, EfEntityStore>();

        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 10;
                o.Password.RequireUppercase = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireDigit = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = config.GetValue("Auth:MaxFailedAttempts", 5);
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(config.GetValue("Auth:LockoutMinutes", 15));
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddPasswordValidator<PasswordNotEmailValidator>();

        services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));
        services.AddDataProtection();
        services.AddScoped<AuthService>();
        services.AddScoped<ERP.Application.Portal.IPortalAuth, PortalAuth>();
        services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());
        services.AddScoped<OnboardingService>();
        services.AddScoped<IOnboardingService>(sp => sp.GetRequiredService<OnboardingService>());
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddSingleton<IPasswordResetSender, LoggingPasswordResetSender>();

        services.AddSingleton<IPermissionCatalog, PermissionCatalog>();
        services.AddSingleton<ICustomizableEntityRegistry, CustomizableEntityRegistry>();
        services.AddScoped<IModuleAccess, ModuleAccess>();
        services.AddSingleton<ERP.Application.Common.ISequenceDefaults, ModuleSequenceDefaults>();
        services.AddSingleton<ISettingsCatalog, SettingsCatalog>();
        services.AddSingleton<INotificationPusher, NullNotificationPusher>();
        services.AddSingleton<IFileStore, LocalFileStore>();
        return services;
    }
}
