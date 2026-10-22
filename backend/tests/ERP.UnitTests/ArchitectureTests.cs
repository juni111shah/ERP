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
        var result = Types.InAssemblies(CoreAssemblies).ShouldNot().HaveDependencyOnAny("ERP.Modules.RealEstate", "ERP.Modules.Inventory").GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
        foreach (var a in CoreAssemblies)
            Assert.DoesNotContain(a.GetReferencedAssemblies(), r => r.Name!.StartsWith("ERP.Modules.", StringComparison.Ordinal));
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
