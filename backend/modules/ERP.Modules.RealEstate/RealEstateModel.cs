using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate;

/// <summary>EF Core mapping of the module's tables (prefix "re_"). Tenant / entity / soft-delete filters, rowversion and the custom-fields JSON check are applied centrally.</summary>
internal static class RealEstateModel
{
    private const string Live = "[IsDeleted] = 0";

    public static void Configure(ModelBuilder mb)
    {
        mb.Entity<UnitType>(b =>
        {
            b.ToTable("re_UnitTypes");
            b.Property(x => x.Code).HasMaxLength(30).IsRequired();
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter(Live);
        });

        mb.Entity<Property>(b =>
        {
            b.ToTable("re_Properties");
            b.Property(x => x.Code).HasMaxLength(20).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Region).HasMaxLength(100);
            b.Property(x => x.Community).HasMaxLength(100);
            b.Property(x => x.PlotNumber).HasMaxLength(50);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.IndicativeValue).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter(Live);
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.IsActive });
            b.HasIndex(x => new { x.OrgId, x.OwnerPartyId });
        });

        mb.Entity<Unit>(b =>
        {
            b.ToTable("re_Units");
            b.Property(x => x.UnitNumber).HasMaxLength(30).IsRequired();
            b.Property(x => x.Floor).HasMaxLength(10);
            b.Property(x => x.AreaSqft).HasPrecision(10, 2);
            b.Property(x => x.AskingRent).HasPrecision(19, 4);
            b.Property(x => x.UnavailableReason).HasMaxLength(200);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UnitType>().WithMany().HasForeignKey(x => x.UnitTypeId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.PropertyId, x.UnitNumber }).IsUnique().HasFilter(Live);
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Status, x.PropertyId });
        });

        mb.Entity<UnitRentRate>(b =>
        {
            b.ToTable("re_UnitRentRates");
            b.Property(x => x.AnnualRent).HasPrecision(19, 4);
            b.Property(x => x.Reason).HasMaxLength(200);
            b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.UnitId, x.EffectiveFrom }).IsUnique().HasFilter(Live);
        });

        mb.Entity<Lease>(b =>
        {
            b.ToTable("re_Leases", t =>
            {
                t.HasCheckConstraint("CK_re_Leases_Dates", "[EndDate] > [StartDate]");
                t.HasCheckConstraint("CK_re_Leases_Amounts", "[AnnualRent] > 0 AND [SecurityDepositAmount] >= 0");
                t.HasCheckConstraint("CK_re_Leases_PaymentFrequency", "[PaymentFrequency] IN (1, 2, 4, 6, 12)");
            });
            b.Property(x => x.Number).HasMaxLength(30);
            b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
            b.Property(x => x.AnnualRent).HasPrecision(19, 4);
            b.Property(x => x.SecurityDepositAmount).HasPrecision(19, 4);
            b.Property(x => x.TerminationReason).HasMaxLength(500);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Lease>().WithMany().HasForeignKey(x => x.RenewalOfLeaseId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0 AND [Number] IS NOT NULL");
            // Statuses 1 (Active) and 5 (HoldingOver): at most one occupying lease per unit, even under concurrent activation.
            b.HasIndex(x => new { x.OrgId, x.UnitId }).IsUnique().HasFilter("[IsDeleted] = 0 AND [Status] IN (1, 5)").HasDatabaseName("UX_re_Leases_OneOccupyingPerUnit");
            b.HasIndex(x => new { x.OrgId, x.RenewalOfLeaseId }).IsUnique().HasFilter("[IsDeleted] = 0 AND [RenewalOfLeaseId] IS NOT NULL AND [Status] <> 6")
                .HasDatabaseName("UX_re_Leases_OneRenewal");
            b.HasIndex(x => new { x.OrgId, x.LesseePartyId });
            b.HasIndex(x => new { x.OrgId, x.PropertyId });
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Status, x.EndDate });
        });

        mb.Entity<RentInstalment>(b =>
        {
            b.ToTable("re_RentInstalments", t => t.HasCheckConstraint("CK_re_RentInstalments_Period", "[PeriodTo] >= [PeriodFrom] AND [Amount] >= 0"));
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.HasOne<Lease>().WithMany().HasForeignKey(x => x.LeaseId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.LeaseId, x.Sequence }).IsUnique().HasFilter(Live);
            b.HasIndex(x => new { x.OrgId, x.DueDate }).HasFilter("[IsDeleted] = 0 AND [Status] = 0"); // billing run: due, un-invoiced
        });

        mb.Entity<WorkOrder>(b =>
        {
            b.ToTable("re_WorkOrders", t => t.HasCheckConstraint("CK_re_WorkOrders_Costs", "[EstimatedCost] >= 0 AND ([ActualCost] IS NULL OR [ActualCost] >= 0)"));
            b.Property(x => x.Number).HasMaxLength(30);
            b.Property(x => x.Issue).HasMaxLength(300).IsRequired();
            b.Property(x => x.Category).HasMaxLength(50);
            b.Property(x => x.ReportedBy).HasMaxLength(100);
            b.Property(x => x.Technician).HasMaxLength(100);
            b.Property(x => x.Details).HasMaxLength(4000);
            b.Property(x => x.ResolutionNotes).HasMaxLength(4000);
            b.Property(x => x.EstimatedCost).HasPrecision(19, 4);
            b.Property(x => x.ActualCost).HasPrecision(19, 4);
            b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Lease>().WithMany().HasForeignKey(x => x.LeaseId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0 AND [Number] IS NOT NULL");
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Status, x.Priority });
            b.HasIndex(x => new { x.OrgId, x.PropertyId, x.ReportedOn });
            b.HasIndex(x => new { x.OrgId, x.UnitId });
        });

        mb.Entity<PmSchedule>(b =>
        {
            b.ToTable("re_PmSchedules");
            b.Property(x => x.Title).HasMaxLength(300).IsRequired();
            b.Property(x => x.Category).HasMaxLength(50);
            b.Property(x => x.Notes).HasMaxLength(4000);
            b.Property(x => x.EstimatedCost).HasPrecision(19, 4);
            b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.IsActive, x.NextDue });
        });

        mb.Entity<Acquisition>(b =>
        {
            b.ToTable("re_Acquisitions");
            b.Property(x => x.Number).HasMaxLength(30);
            b.Property(x => x.SellerName).HasMaxLength(200).IsRequired();
            b.Property(x => x.ContractReference).HasMaxLength(100);
            b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
            b.Property(x => x.Notes).HasMaxLength(1000);
            foreach (var m in new[] { nameof(Acquisition.PurchasePrice), nameof(Acquisition.DownPayment), nameof(Acquisition.TransferFees), nameof(Acquisition.OtherCosts) }) b.Property(m).HasPrecision(19, 4);
            b.Ignore(x => x.TotalCost); b.Ignore(x => x.CashAtCompletion);
            b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0 AND [Number] IS NOT NULL");
            b.HasIndex(x => new { x.OrgId, x.PropertyId });
        });

        mb.Entity<FundingSource>(b =>
        {
            b.ToTable("re_FundingSources");
            b.Property(x => x.Lender).HasMaxLength(200).IsRequired();
            b.Property(x => x.Principal).HasPrecision(19, 4);
            b.Property(x => x.MonthlyPayment).HasPrecision(19, 4);
            b.Property(x => x.AnnualRatePercent).HasPrecision(9, 4);
            b.HasOne<Acquisition>().WithMany().HasForeignKey(x => x.AcquisitionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.AcquisitionId });
        });

        mb.Entity<FundingInstalment>(b =>
        {
            b.ToTable("re_FundingInstalments");
            foreach (var m in new[] { nameof(FundingInstalment.Principal), nameof(FundingInstalment.Interest), nameof(FundingInstalment.Total), nameof(FundingInstalment.BalanceAfter) }) b.Property(m).HasPrecision(19, 4);
            b.HasOne<FundingSource>().WithMany().HasForeignKey(x => x.FundingSourceId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.FundingSourceId, x.Sequence }).IsUnique().HasFilter(Live);
            b.HasIndex(x => new { x.OrgId, x.Status, x.DueDate });
        });

        mb.Entity<DepositTransaction>(b =>
        {
            b.ToTable("re_DepositTransactions");
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.Property(x => x.Description).HasMaxLength(300);
            b.HasOne<Lease>().WithMany().HasForeignKey(x => x.LeaseId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.LeaseId, x.Date });
        });

        mb.Entity<BrokerageDeal>(b =>
        {
            b.ToTable("re_BrokerageDeals"); // percentage rules are enforced by DealInputValidator (SQLite compares decimals as text, so no CHECK)
            b.Property(x => x.Number).HasMaxLength(30);
            b.Property(x => x.UnitReference).HasMaxLength(100);
            b.Property(x => x.ClientName).HasMaxLength(200).IsRequired();
            b.Property(x => x.ExternalAgentName).HasMaxLength(200);
            b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
            b.Property(x => x.LostReason).HasMaxLength(500);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.DealValue).HasPrecision(19, 4);
            b.Property(x => x.GrossCommission).HasPrecision(19, 4);
            b.Property(x => x.CommissionRatePercent).HasPrecision(9, 4);
            b.Property(x => x.AgentSharePercent).HasPrecision(9, 4);
            b.Property(x => x.TeamLeadSharePercent).HasPrecision(9, 4);
            b.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0 AND [Number] IS NOT NULL");
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Stage, x.DealDate });
            b.HasIndex(x => new { x.OrgId, x.AgentPartyId });
        });
    }
}
