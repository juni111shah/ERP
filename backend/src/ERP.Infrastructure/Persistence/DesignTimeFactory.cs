using ERP.Modularity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ERP.Infrastructure.Persistence;

/// <summary>Used by `dotnet ef`. Module assemblies are discovered next to the tool's startup project.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ERP_Design;Trusted_Connection=True;TrustServerCertificate=True",
                o => o.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, ModuleAwareModelCacheKeyFactory>()
            .Options;
        return new AppDbContext(options, new ModuleRegistry(ModuleDiscovery.DiscoverFromAppDomain()));
    }
}
