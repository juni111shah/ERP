using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Purchasing;

public sealed class PurchasingModule : ModuleBase
{
    public const string Key = "purchasing";

    public override ModuleManifest Manifest { get; } = new(Key, "Purchasing", "Purchase orders, vendor bills and payments.", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(P.OrdersRead, "View purchase orders"),
        PermissionDefinition.Of(P.OrdersManage, "Create, edit, receive, and bill purchase orders"),
        PermissionDefinition.Of(P.RequisitionsRead, "View purchase requisitions"),
        PermissionDefinition.Of(P.RequisitionsManage, "Create, update, and convert purchase requisitions"),
        PermissionDefinition.Of(P.ReceiptsRead, "View goods receipt notes"),
        PermissionDefinition.Of(P.ReceiptsManage, "Record and manage goods receipts"),
        PermissionDefinition.Of(P.AnalyticsRead, "View procurement reporting and spend analytics"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("purchasing.orders", "Purchase orders", "/purchasing/orders", "shopping", P.OrdersRead, 10, "purchasing", "Purchasing", 320),
        new("purchasing.requisitions", "Requisitions", "/purchasing/requisitions", "file-add", P.RequisitionsRead, 20, "purchasing", "Purchasing", 320),
        new("purchasing.receipts", "Goods receipts", "/purchasing/receipts", "download", P.ReceiptsRead, 30, "purchasing", "Purchasing", 320),
        new("purchasing.analytics", "Purchasing analytics", "/purchasing/analytics", "bar-chart", P.AnalyticsRead, 40, "purchasing", "Purchasing", 320),
    ];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("purchasing.requireRequisitionApproval", "Require management approval for requisitions", "false", "bool"),
        new("purchasing.autoApproveOrdersBelow", "Threshold below which POs are auto-approved", "5000", "number"),
    ];

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
    [
        new(Docs.PurchaseOrder, "Purchase order", typeof(PurchaseOrder), Key, P.OrdersRead),
        new(Docs.PurchaseRequisition, "Purchase requisition", typeof(PurchaseRequisition), Key, P.RequisitionsRead),
        new(Docs.GoodsReceipt, "Goods receipt", typeof(GoodsReceipt), Key, P.ReceiptsRead),
    ];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("procurement", "Procurement & Supply", "Requisitions, purchase orders, receiving and vendor bills.", IndustryAvailability.Available, [Key],
            Roles:
            [
                new("Procurement officer", "Purchase orders, goods receipt and requisitions",
                    [P.OrdersRead, P.OrdersManage, P.RequisitionsRead, P.RequisitionsManage, P.ReceiptsRead, P.ReceiptsManage, P.AnalyticsRead, CorePermissions.BillsRead, CorePermissions.BillsWrite], Key),
            ],
            Sequences:
            [
                new(Docs.PurchaseOrder, "PO", "{prefix}-{seq:4}", false),
                new(Docs.PurchaseRequisition, "PR", "{prefix}-{seq:4}", false),
                new(Docs.GoodsReceipt, "GRN", "{prefix}-{seq:4}", false),
            ],
            SortOrder: 35),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDashboardContributor, PurchasingDashboard>();
        services.AddScoped<ISearchProvider, PurchasingSearch>();
        services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp =>
            new ERP.Application.Workflow.EntityAttachmentOwnerResolver<PurchaseOrder>(
                sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), "purchaseorder", P.OrdersRead, P.OrdersManage));
        services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp =>
            new ERP.Application.Workflow.EntityAttachmentOwnerResolver<PurchaseOrder>(
                sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), "purchase_order", P.OrdersRead, P.OrdersManage));
        services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp =>
            new ERP.Application.Workflow.EntityAttachmentOwnerResolver<PurchaseRequisition>(
                sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), "requisition", P.RequisitionsRead, P.RequisitionsManage));
    }

    public override void ConfigureModel(ModelBuilder mb)
    {
        mb.Entity<PurchaseOrder>(b =>
        {
            b.ToTable("pur_Orders");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.VendorName).HasMaxLength(200).IsRequired();
            b.Property(x => x.VendorContact).HasMaxLength(150);
            b.Property(x => x.VendorReference).HasMaxLength(100);
            b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            b.Property(x => x.SubTotal).HasPrecision(19, 4);
            b.Property(x => x.TaxTotal).HasPrecision(19, 4);
            b.Property(x => x.DiscountTotal).HasPrecision(19, 4);
            b.Property(x => x.TotalAmount).HasPrecision(19, 4);
            b.Property(x => x.PaymentTerms).HasMaxLength(200);
            b.Property(x => x.DeliveryAddress).HasMaxLength(500);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasIndex(x => new { x.OrgId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Status });
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<PurchaseOrderLine>(b =>
        {
            b.ToTable("pur_OrderLines");
            b.Property(x => x.ItemCode).HasMaxLength(50);
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.Property(x => x.Quantity).HasPrecision(18, 4);
            b.Property(x => x.ReceivedQuantity).HasPrecision(18, 4);
            b.Property(x => x.BilledQuantity).HasPrecision(18, 4);
            b.Property(x => x.UnitOfMeasure).HasMaxLength(20);
            b.Property(x => x.UnitPrice).HasPrecision(19, 4);
            b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            b.Property(x => x.TaxRate).HasPrecision(5, 2);
            b.Property(x => x.LineTotal).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.OrderId });
        });

        mb.Entity<PurchaseRequisition>(b =>
        {
            b.ToTable("pur_Requisitions");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.RequesterName).HasMaxLength(150).IsRequired();
            b.Property(x => x.Department).HasMaxLength(100);
            b.Property(x => x.EstimatedTotal).HasPrecision(19, 4);
            b.Property(x => x.Reason).HasMaxLength(2000);
            b.HasIndex(x => new { x.OrgId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Status });
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.RequisitionId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<PurchaseRequisitionLine>(b =>
        {
            b.ToTable("pur_RequisitionLines");
            b.Property(x => x.ItemCode).HasMaxLength(50);
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.Property(x => x.Quantity).HasPrecision(18, 4);
            b.Property(x => x.UnitOfMeasure).HasMaxLength(20);
            b.Property(x => x.EstimatedUnitPrice).HasPrecision(19, 4);
            b.Property(x => x.LineTotal).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.RequisitionId });
        });

        mb.Entity<GoodsReceipt>(b =>
        {
            b.ToTable("pur_GoodsReceipts");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.PurchaseOrderNumber).HasMaxLength(30).IsRequired();
            b.Property(x => x.VendorName).HasMaxLength(200).IsRequired();
            b.Property(x => x.ReceivedBy).HasMaxLength(150).IsRequired();
            b.Property(x => x.DeliveryNoteNumber).HasMaxLength(100);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasIndex(x => new { x.OrgId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.PurchaseOrderId });
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.GoodsReceiptId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<GoodsReceiptLine>(b =>
        {
            b.ToTable("pur_GoodsReceiptLines");
            b.Property(x => x.ItemCode).HasMaxLength(50);
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.Property(x => x.QuantityReceived).HasPrecision(18, 4);
            b.Property(x => x.UnitOfMeasure).HasMaxLength(20);
            b.HasIndex(x => new { x.OrgId, x.GoodsReceiptId });
        });
    }

    public override void MapEndpoints(RouteGroupBuilder group) => PurchasingEndpoints.Map(group);
}
