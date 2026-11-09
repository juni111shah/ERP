using ERP.Application.Security;
using ERP.Domain.Common;
using ERP.Infrastructure.Persistence;
using ERP.Modularity;
using ERP.Modules.Inventory;
using ERP.Modules.RealEstate;
using NetArchTest.Rules;

namespace ERP.UnitTests;

public class ArchitectureTests
{
    private static readonly System.Reflection.Assembly[] CoreAssemblies =
    [
        typeof(Entity).Assembly,                                   // ERP.Domain
        typeof(CorePermissions).Assembly,                          // ERP.Application
        typeof(ModuleRegistry).Assembly,                           // ERP.Modularity
        typeof(AppDbContext).Assembly,                             // ERP.Infrastructure
    ];

    [Fact]
    public void Core_assemblies_never_reference_module_assemblies()
    {
        var result = Types.InAssemblies(CoreAssemblies).ShouldNot().HaveDependencyOnAny("ERP.Modules.RealEstate", "ERP.Modules.Inventory", "ERP.Modules.Hr", "ERP.Modules.Projects", "ERP.CountryPacks").GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
        foreach (var a in CoreAssemblies)
            Assert.DoesNotContain(a.GetReferencedAssemblies(), r => r.Name!.StartsWith("ERP.Modules.", StringComparison.Ordinal) || r.Name.StartsWith("ERP.CountryPacks.", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_depends_on_nothing_else_in_the_solution()
    {
        var result = Types.InAssembly(typeof(Entity).Assembly).ShouldNot()
            .HaveDependencyOnAny("ERP.Application", "ERP.Infrastructure", "ERP.Api", "ERP.Modularity", "Microsoft.EntityFrameworkCore").GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_web()
    {
        var result = Types.InAssembly(typeof(CorePermissions).Assembly).ShouldNot()
            .HaveDependencyOnAny("ERP.Infrastructure", "ERP.Api", "Microsoft.AspNetCore").GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Modules_do_not_depend_on_each_other()
    {
        Assert.True(Types.InAssembly(typeof(RealEstateModule).Assembly).ShouldNot().HaveDependencyOn("ERP.Modules.Inventory").GetResult().IsSuccessful);
        Assert.True(Types.InAssembly(typeof(InventoryModule).Assembly).ShouldNot().HaveDependencyOn("ERP.Modules.RealEstate").GetResult().IsSuccessful);
        Assert.True(Types.InAssembly(typeof(ERP.Modules.Projects.ProjectsModule).Assembly).ShouldNot().HaveDependencyOnAny("ERP.Modules.RealEstate", "ERP.Modules.Inventory", "ERP.Modules.Hr").GetResult().IsSuccessful);
        Assert.True(Types.InAssembly(typeof(ERP.Modules.Sales.SalesModule).Assembly).ShouldNot().HaveDependencyOnAny("ERP.Modules.RealEstate", "ERP.Modules.Inventory", "ERP.Modules.Hr", "ERP.Modules.Projects").GetResult().IsSuccessful);
        Assert.True(Types.InAssembly(typeof(ERP.Modules.Purchasing.PurchasingModule).Assembly).ShouldNot().HaveDependencyOnAny("ERP.Modules.RealEstate", "ERP.Modules.Inventory", "ERP.Modules.Hr", "ERP.Modules.Projects", "ERP.Modules.Sales").GetResult().IsSuccessful);
        Assert.True(Types.InAssembly(typeof(ERP.Modules.Pos.PosModule).Assembly).ShouldNot().HaveDependencyOnAny("ERP.Modules.RealEstate", "ERP.Modules.Inventory", "ERP.Modules.Hr", "ERP.Modules.Projects", "ERP.Modules.Sales", "ERP.Modules.Purchasing", "ERP.Modules.Retail").GetResult().IsSuccessful);
    }

    // ---- Controller -> Command/Query handler -> Repository -> Database ------------------------------------------------

    private static readonly System.Reflection.Assembly ApiAssembly = typeof(ERP.Api.Infrastructure.ControllerSetup).Assembly;

    private static readonly System.Reflection.Assembly[] ModuleAssemblies =
    [
        typeof(InventoryModule).Assembly, typeof(RealEstateModule).Assembly, typeof(ERP.Modules.Hr.HrModule).Assembly, typeof(ERP.Modules.Projects.ProjectsModule).Assembly,
        typeof(ERP.Modules.Sales.SalesModule).Assembly, typeof(ERP.Modules.Purchasing.PurchasingModule).Assembly, typeof(ERP.Modules.Pos.PosModule).Assembly,
        typeof(ERP.Modules.Retail.RetailModule).Assembly, typeof(ERP.Modules.Distribution.DistributionModule).Assembly, typeof(ERP.Modules.Manufacturing.ManufacturingModule).Assembly,
        typeof(ERP.Modules.Crm.CrmModule).Assembly, typeof(ERP.Modules.Services.ServicesModule).Assembly, typeof(ERP.Modules.Assets.AssetsModule).Assembly,
        typeof(ERP.Modules.Assistant.AssistantModule).Assembly, typeof(ERP.Modules.IndustryPacks.Packs.ContractsModule).Assembly,
    ];

    /// <summary>Every assembly that holds commands, queries and their handlers.</summary>
    private static readonly System.Reflection.Assembly[] CqrsAssemblies = [typeof(CorePermissions).Assembly, typeof(ModuleRegistry).Assembly, .. ModuleAssemblies];

    private static IEnumerable<Type> Controllers() => new[] { ApiAssembly }.Concat(ModuleAssemblies).SelectMany(a => a.GetTypes())
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t));

    [Fact]
    public void Controllers_only_talk_to_the_mediator()
    {
        // No repositories, services, DbContext or EF in a controller: it binds the request, sends one command/query and shapes the HTTP result.
        var result = Types.InAssemblies([ApiAssembly, .. ModuleAssemblies]).That().Inherit(typeof(Microsoft.AspNetCore.Mvc.ControllerBase)).ShouldNot()
            .HaveDependencyOnAny("ERP.Infrastructure", "Microsoft.EntityFrameworkCore", "ERP.Application.Abstractions").GetResult();
        Assert.True(result.IsSuccessful, "Controllers must only use MediatR: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Controllers_derive_from_the_api_base_and_live_in_a_controllers_namespace()
    {
        var wrong = Controllers()
            .Where(t => !typeof(ERP.Modularity.Web.ApiControllerBase).IsAssignableFrom(t) || !t.Name.EndsWith("Controller", StringComparison.Ordinal)
                || t.Namespace?.Contains(".Controllers", StringComparison.Ordinal) != true)
            .Select(t => t.FullName);
        Assert.Empty(wrong);
    }

    [Fact]
    public void Module_controllers_are_gated_by_their_module_and_mounted_under_its_prefix()
    {
        foreach (var controller in Controllers().Where(t => ModuleAssemblies.Contains(t.Assembly)))
        {
            var gate = controller.GetCustomAttributes(typeof(ModuleGateAttribute), true).Cast<ModuleGateAttribute>().SingleOrDefault();
            Assert.True(gate is not null, $"{controller.FullName} needs [ModuleGate(\"<module key>\")].");
            var route = controller.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RouteAttribute), true).Cast<Microsoft.AspNetCore.Mvc.RouteAttribute>().Single().Template;
            Assert.True(route == $"api/v1/m/{gate.ModuleKey}" || route.StartsWith($"api/v1/m/{gate.ModuleKey}/", StringComparison.Ordinal),
                $"{controller.FullName}: route '{route}' must start with api/v1/m/{gate.ModuleKey}.");
        }
    }

    [Fact]
    public void Every_request_is_a_command_or_a_query_and_named_so()
    {
        static bool Implements(Type t, Type openGeneric) => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);
        var wrong = CqrsAssemblies.SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } && Implements(t, typeof(MediatR.IRequest<>)))
            .Where(t => !Implements(t, typeof(ERP.Application.Common.IListRequest<>)))
            .Where(t => Implements(t, typeof(ERP.Application.Common.ICommand<>)) ? !t.Name.EndsWith("Command", StringComparison.Ordinal)
                : !Implements(t, typeof(ERP.Application.Common.IQuery<>)) || !t.Name.EndsWith("Query", StringComparison.Ordinal))
            .Select(t => t.FullName);
        Assert.Empty(wrong);
    }

    [Fact]
    public void Every_handler_handles_exactly_one_request_and_is_named_after_it()
    {
        var wrong = CqrsAssemblies.SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Select(t => (Type: t, Requests: t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(MediatR.IRequestHandler<,>)).Select(i => i.GetGenericArguments()[0]).ToList()))
            .Where(h => h.Requests.Count > 0)
            .Where(h => h.Requests.Count != 1 || h.Type.Name != StripSuffix(h.Requests[0].Name) + "Handler")
            .Select(h => $"{h.Type.FullName} handles {string.Join(", ", h.Requests.Select(r => r.Name))}");
        Assert.Empty(wrong);

        static string StripSuffix(string name) => name.EndsWith("Command", StringComparison.Ordinal) ? name[..^"Command".Length]
            : name.EndsWith("Query", StringComparison.Ordinal) ? name[..^"Query".Length] : name;
    }

    [Fact]
    public void The_api_host_never_references_infrastructure_outside_the_composition_root()
    {
        var result = Types.InAssembly(ApiAssembly).That().DoNotHaveNameStartingWith("Program").And().DoNotHaveName("ModuleCatalog").And().DoNotHaveName("DatabaseStartup") // the composition root (Program.cs)
            .ShouldNot().HaveDependencyOn("ERP.Infrastructure").GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_never_sees_the_db_context()
    {
        var result = Types.InAssembly(typeof(CorePermissions).Assembly).ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore.DbContext", "Microsoft.EntityFrameworkCore.DbSet`1", "ERP.Infrastructure").GetResult();
        Assert.True(result.IsSuccessful, "Application code must use repositories: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Module_code_outside_handlers_and_repositories_never_touches_repositories()
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var result = Types.InAssembly(assembly).That().DoNotHaveNameEndingWith("Handler").And().DoNotResideInNamespaceContaining(".Features")
                .And().DoNotResideInNamespaceContaining(".Repositories")
                .ShouldNot().HaveDependencyOnAny("ERP.Application.Abstractions.IRepository`1", "ERP.Application.Abstractions.IReadRepository`1").GetResult();
            Assert.True(result.IsSuccessful, "Module controllers must send MediatR requests: " + string.Join(", ", result.FailingTypeNames ?? []));
        }
    }

    [Fact]
    public void Repository_implementations_live_in_infrastructure_or_a_module_repositories_namespace()
    {
        static bool IsRepository(Type t)
        {
            for (var b = t.BaseType; b is not null; b = b.BaseType)
                if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(ERP.Application.Abstractions.RepositoryBase<>)) return true;
            return false;
        }
        var wrong = new[] { typeof(CorePermissions).Assembly, typeof(AppDbContext).Assembly }.Concat(ModuleAssemblies).SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && IsRepository(t))
            .Where(t => t.Namespace?.Contains(".Repositories", StringComparison.Ordinal) != true || !t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Select(t => t.FullName);
        Assert.Empty(wrong);
    }

    [Fact]
    public void Every_mediator_request_has_a_handler()
    {
        // Catches a request type that was added without a handler (it would only fail at runtime on first use).
        var assemblies = CqrsAssemblies;
        var handled = assemblies.SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetInterfaces()).Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(MediatR.IRequestHandler<,>))
            .Select(i => i.GetGenericArguments()[0]).ToHashSet();
        static bool Implements(Type t, Type openGeneric) => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);
        var requests = assemblies.SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition && Implements(t, typeof(MediatR.IRequest<>)))
            .Where(t => !Implements(t, typeof(ERP.Application.Common.IListRequest<>))); // served by the single open-generic ListRequestHandler
        Assert.Empty(requests.Where(r => !handled.Contains(r)).Select(r => r.FullName));
    }

    [Fact]
    public void Core_permission_keys_follow_module_resource_action()
    {
        foreach (var p in CorePermissions.Definitions)
        {
            Assert.Matches("^[a-z]+\\.[a-z]+\\.[a-z]+$", p.Key);
            Assert.True(p.Module is "core" or "finance", p.Key);
        }
    }
}
