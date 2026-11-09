using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace ERP.Api.Infrastructure;

/// <summary>
/// MVC set-up for the API controllers (core in ERP.Api, modules in their own assemblies). It keeps the HTTP contract the
/// minimal endpoints had: the same JSON options, the <see cref="ApiResponse{T}"/> envelope, 400 for a missing required
/// query value, and only the controllers of installed modules.
/// </summary>
public static class ControllerSetup
{
    public static IMvcBuilder AddApiControllers(this IServiceCollection services, IReadOnlyList<IModule> modules)
    {
        var installedKeys = modules.Select(m => m.Manifest.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var moduleAssemblies = modules.Select(m => m.GetType().Assembly).Distinct().ToList();

        return services
            .AddControllers(o =>
            {
                o.Filters.Add<ApiEnvelopeResultFilter>();
                // Missing body fields are reported by the FluentValidation validators, not by MVC's implicit [Required].
                o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                o.ModelMetadataDetailsProviders.Add(new RequiredActionParameterMetadataProvider());
            })
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                o.JsonSerializerOptions.MaxDepth = 16;
            })
            .ConfigureApiBehaviorOptions(o =>
            {
                o.InvalidModelStateResponseFactory = InvalidRequest;
                // NotFound()/BadRequest() stay empty-bodied, so the status-code pages write the same error envelope the minimal endpoints got.
                o.SuppressMapClientErrors = true;
            })
            .ConfigureApplicationPartManager(parts =>
            {
                // Module controllers live in the module assemblies. Only installed modules are mounted.
                foreach (var stale in parts.ApplicationParts.OfType<AssemblyPart>().Where(IsModuleAssembly).ToList()) parts.ApplicationParts.Remove(stale);
                foreach (var assembly in moduleAssemblies) parts.ApplicationParts.Add(new AssemblyPart(assembly));
                parts.FeatureProviders.Add(new InstalledModuleControllers(installedKeys));
            });
    }

    private static bool IsModuleAssembly(AssemblyPart part) => part.Name.StartsWith("ERP.Modules.", StringComparison.Ordinal);

    /// <summary>Binding errors (bad JSON, a missing body, an unparsable query value) answer 400 in the standard error envelope.</summary>
    private static IActionResult InvalidRequest(ActionContext context)
    {
        var errors = context.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? e.Exception?.Message ?? "The value is not valid." : e.ErrorMessage).ToArray());
        var body = ApiResponses.Error(StatusCodes.Status400BadRequest, "Bad request", "The request could not be read.", context.HttpContext.TraceIdentifier, errors, null, null);
        return new ObjectResult(body) { StatusCode = StatusCodes.Status400BadRequest, ContentTypes = { ApiEnvelope.ProblemContentType } };
    }

    /// <summary>
    /// A non-nullable action parameter without a default (<c>DateOnly asOf</c>, <c>string ownerType</c>) must be sent, as it had
    /// to be on the minimal endpoints: missing, it is a 400 instead of silently binding default(T).
    /// </summary>
    private sealed class RequiredActionParameterMetadataProvider : IBindingMetadataProvider
    {


        public void CreateBindingMetadata(BindingMetadataProviderContext context)
        {
            if (context.Key.MetadataKind != ModelMetadataKind.Parameter || context.Key.ParameterInfo is not { } p || p.HasDefaultValue) return;
            var type = p.ParameterType;
            var required = type == typeof(string)
                ? new NullabilityInfoContext().Create(p).ReadState == NullabilityState.NotNull
                : type.IsValueType && Nullable.GetUnderlyingType(type) is null && type != typeof(CancellationToken);
            if (required) context.BindingMetadata.IsBindingRequired = true;
        }
    }

    /// <summary>Drops controllers whose <see cref="ModuleGateAttribute"/> names a module this deployment did not install.</summary>
    private sealed class InstalledModuleControllers(IReadOnlySet<string> installedKeys) : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var controller in feature.Controllers.ToList())
                if (controller.GetCustomAttribute<ModuleGateAttribute>() is { } gate && !installedKeys.Contains(gate.ModuleKey))
                    feature.Controllers.Remove(controller);
        }
    }
}
