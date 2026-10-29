using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Manufacturing;

public sealed class ManufacturingModule : ModuleBase
{
    public const string Key = "manufacturing";

    public override ModuleManifest Manifest { get; } = new(Key, "Manufacturing", "Bills of materials, work orders and costing.", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(Manufacturing.Permissions.BomsRead, "View bills of materials and recipe components"),
        PermissionDefinition.Of(Manufacturing.Permissions.BomsManage, "Create and manage bills of materials"),
        PermissionDefinition.Of(Manufacturing.Permissions.WorkOrdersRead, "View production work orders and operations"),
        PermissionDefinition.Of(Manufacturing.Permissions.WorkOrdersManage, "Create, release and complete production work orders"),
        PermissionDefinition.Of(Manufacturing.Permissions.CostingRead, "View standard costing rollups and production variances"),
        PermissionDefinition.Of(Manufacturing.Permissions.AnalyticsRead, "View manufacturing output and yield analytics"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("manufacturing.boms", "Bills of materials", "/manufacturing/boms", "profile", Manufacturing.Permissions.BomsRead, 10, "manufacturing", "Manufacturing", 350),
        new("manufacturing.workorders", "Work orders", "/manufacturing/workorders", "tool", Manufacturing.Permissions.WorkOrdersRead, 20, "manufacturing", "Manufacturing", 350),
        new("manufacturing.costing", "Product costing", "/manufacturing/costing", "calculator", Manufacturing.Permissions.CostingRead, 30, "manufacturing", "Manufacturing", 350),
        new("manufacturing.analytics", "Manufacturing analytics", "/manufacturing/analytics", "pie-chart", Manufacturing.Permissions.AnalyticsRead, 40, "manufacturing", "Manufacturing", 350),
    ];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("manufacturing.autoConsumeComponents", "Automatically backflush component stock on work order completion", "true", "bool"),
        new("manufacturing.strictCostVarianceAlert", "Alert when actual production unit cost exceeds standard by >10%", "true", "bool"),
    ];

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
    [
        new("BillOfMaterials", "Bill of materials", typeof(BillOfMaterials), Key, Manufacturing.Permissions.BomsRead),
        new("WorkOrder", "Production work order", typeof(WorkOrder), Key, Manufacturing.Permissions.WorkOrdersRead),
    ];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("light_manufacturing", "Light Manufacturing & Assembly", "Assembly recipes, component stock allocation and operational routing.", IndustryAvailability.Available, [Key],
            Roles:
            [
                new("Production Planner", "BOM composition, work order scheduling and operation assignment",
                    [Manufacturing.Permissions.BomsRead, Manufacturing.Permissions.BomsManage, Manufacturing.Permissions.WorkOrdersRead, Manufacturing.Permissions.WorkOrdersManage], Key),
                new("Cost Accountant", "BOM cost rollups, production variance analysis and manufacturing yield reporting",
                    [Manufacturing.Permissions.BomsRead, Manufacturing.Permissions.WorkOrdersRead, Manufacturing.Permissions.CostingRead, Manufacturing.Permissions.AnalyticsRead], Key),
            ],
            Sequences:
            [
                new("WorkOrder", "WO", "{prefix}-{seq:5}", false),
            ],
            SortOrder: 60),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDashboardContributor, ManufacturingDashboard>();
        services.AddScoped<ISearchProvider, ManufacturingSearch>();
    }

    public override void ConfigureModel(ModelBuilder mb)
    {
        mb.Entity<BillOfMaterials>(b =>
        {
            b.ToTable("mfg_Boms");
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.ProducedItemCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.ProducedItemName).HasMaxLength(200).IsRequired();
            b.Property(x => x.OutputQuantity).HasPrecision(18, 4);
            b.Property(x => x.Uom).HasMaxLength(20).IsRequired();
            b.Property(x => x.Version).HasMaxLength(20).IsRequired();
            b.Property(x => x.LaborCostPerUnit).HasPrecision(19, 4);
            b.Property(x => x.OverheadCostPerUnit).HasPrecision(19, 4);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasMany(x => x.Components).WithOne(c => c.Bom).HasForeignKey(c => c.BomId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<BomComponent>(b =>
        {
            b.ToTable("mfg_BomComponents");
            b.Property(x => x.ComponentItemCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.ComponentItemName).HasMaxLength(200).IsRequired();
            b.Property(x => x.QuantityRequired).HasPrecision(18, 4);
            b.Property(x => x.Uom).HasMaxLength(20).IsRequired();
            b.Property(x => x.ScrapPercentage).HasPrecision(18, 4);
            b.Property(x => x.UnitCost).HasPrecision(19, 4);
            b.Property(x => x.Notes).HasMaxLength(500);
            b.HasIndex(x => new { x.OrgId, x.BomId });
        });

        mb.Entity<WorkOrder>(b =>
        {
            b.ToTable("mfg_WorkOrders");
            b.Property(x => x.WorkOrderNumber).HasMaxLength(50).IsRequired();
            b.Property(x => x.BomCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.ProducedItemCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.ProducedItemName).HasMaxLength(200).IsRequired();
            b.Property(x => x.PlannedQuantity).HasPrecision(18, 4);
            b.Property(x => x.CompletedQuantity).HasPrecision(18, 4);
            b.Property(x => x.ScrappedQuantity).HasPrecision(18, 4);
            b.Property(x => x.Uom).HasMaxLength(20).IsRequired();
            b.Property(x => x.EstimatedTotalCost).HasPrecision(19, 4);
            b.Property(x => x.ActualTotalCost).HasPrecision(19, 4);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrgId, x.WorkOrderNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasMany(x => x.Operations).WithOne(o => o.WorkOrder).HasForeignKey(o => o.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<WorkOrderOperation>(b =>
        {
            b.ToTable("mfg_WorkOrderOperations");
            b.Property(x => x.OperationName).HasMaxLength(150).IsRequired();
            b.Property(x => x.WorkCenterName).HasMaxLength(150).IsRequired();
            b.Property(x => x.PlannedHours).HasPrecision(18, 4);
            b.Property(x => x.ActualHours).HasPrecision(18, 4);
            b.Property(x => x.HourlyRate).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.WorkOrderId });
        });

        mb.Entity<ProductCostingRecord>(b =>
        {
            b.ToTable("mfg_ProductCostings");
            b.Property(x => x.ItemCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
            b.Property(x => x.MaterialCost).HasPrecision(19, 4);
            b.Property(x => x.LaborCost).HasPrecision(19, 4);
            b.Property(x => x.OverheadCost).HasPrecision(19, 4);
            b.Property(x => x.StandardTotalUnitCost).HasPrecision(19, 4);
            b.Property(x => x.LatestActualUnitCost).HasPrecision(19, 4);
            b.Property(x => x.CostVariance).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.ItemId });
        });
    }

    public override void MapEndpoints(RouteGroupBuilder group) => ManufacturingEndpoints.Map(group);
}
