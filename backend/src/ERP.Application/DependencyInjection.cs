using ERP.Application.Organizations;
using ERP.Application.Workflow;
using ERP.Application.Settings;
using ERP.Application.Customization;
using ERP.Application.Abstractions;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Behaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, params System.Reflection.Assembly[] moduleAssemblies)
    {
        var assemblies = new[] { typeof(DependencyInjection).Assembly }.Concat(moduleAssemblies).Distinct().ToArray();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(assemblies);
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        // One constrained open-generic handler serves every list request (no per-table list handlers).
        services.AddTransient(typeof(IRequestHandler<,>), typeof(ListRequestHandler<,>));
        services.AddScoped<ICustomFieldService, CustomFieldService>();
        services.AddScoped<INumberingService, NumberingService>();
        services.AddScoped<ILegalEntityGuard, LegalEntityGuard>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<INotifier, Notifier>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
        return services;
    }
}
