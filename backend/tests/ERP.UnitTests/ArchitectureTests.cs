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
        var result = Types.InAssemblies(CoreAssemblies).ShouldNot().HaveDependencyOnAny("ERP.Modules.RealEstate", "ERP.Modules.Inventory", "ERP.CountryPacks.AE").GetResult();
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
    }

    // ---- Endpoint -> MediatR (CQRS) -> Repository -> DB --------------------------------------------------------------

    private static readonly System.Reflection.Assembly ApiAssembly = typeof(ERP.Api.Endpoints.CoreEndpoints).Assembly;

    [Fact]
    public void Endpoints_only_talk_to_the_mediator()
    {
        // No repositories, services, DbContext or EF in an endpoint: it binds the request, sends one command/query and shapes the HTTP result.
        var result = Types.InAssembly(ApiAssembly).That().ResideInNamespace("ERP.Api.Endpoints").ShouldNot()
            .HaveDependencyOnAny("ERP.Infrastructure", "Microsoft.EntityFrameworkCore", "ERP.Application.Abstractions").GetResult();
        Assert.True(result.IsSuccessful, "Endpoints must only use MediatR: " + string.Join(", ", result.FailingTypeNames ?? []));
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
    public void Module_code_outside_handlers_never_touches_repositories()
    {
        foreach (var assembly in new[] { typeof(InventoryModule).Assembly, typeof(RealEstateModule).Assembly })
        {
            var result = Types.InAssembly(assembly).That().DoNotHaveNameEndingWith("Handlers")
                .ShouldNot().HaveDependencyOnAny("ERP.Application.Abstractions.IRepository`1", "ERP.Application.Abstractions.IReadRepository`1").GetResult();
            Assert.True(result.IsSuccessful, "Module endpoints must send MediatR requests: " + string.Join(", ", result.FailingTypeNames ?? []));
        }
    }

    [Fact]
    public void Every_mediator_request_has_a_handler()
    {
        // Catches a request type that was added without a handler (it would only fail at runtime on first use).
        var assemblies = new[] { typeof(CorePermissions).Assembly, typeof(ModuleRegistry).Assembly, typeof(InventoryModule).Assembly };
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
