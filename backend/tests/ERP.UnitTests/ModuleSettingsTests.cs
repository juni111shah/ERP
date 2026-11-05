using System.Text.Json;
using ERP.Modularity;
using Xunit;

namespace ERP.UnitTests;

public class ModuleSettingsTests
{
    public static TheoryData<IModule> Modules =>
    [
        new ERP.Modules.Crm.CrmModule(),
        new ERP.Modules.Distribution.DistributionModule(),
        new ERP.Modules.Hr.HrModule(),
        new ERP.Modules.Inventory.InventoryModule(),
        new ERP.Modules.Manufacturing.ManufacturingModule(),
        new ERP.Modules.Pos.PosModule(),
        new ERP.Modules.Projects.ProjectsModule(),
        new ERP.Modules.Purchasing.PurchasingModule(),
        new ERP.Modules.RealEstate.RealEstateModule(),
        new ERP.Modules.Retail.RetailModule(),
        new ERP.Modules.Sales.SalesModule(),
        new ERP.Modules.Services.ServicesModule(),
    ];

    /// <summary>The settings list parses every default, so one bad value makes the whole Settings page fail with 400.</summary>
    [Theory]
    [MemberData(nameof(Modules))]
    public void Every_setting_default_is_valid_json(IModule module)
    {
        foreach (var setting in module.Settings)
        {
            var parsed = JsonDocument.Parse(setting.DefaultJson);
            Assert.NotNull(parsed);
        }
    }
}
