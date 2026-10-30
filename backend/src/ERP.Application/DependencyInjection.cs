using ERP.Application.Reminders;
using ERP.Application.Organizations;
using ERP.Application.Workflow;
using ERP.Application.Settings;
using ERP.Application.Customization;
using ERP.Application.Abstractions;
using ERP.Application.Common.Services;
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
            cfg.AddOpenBehavior(typeof(ConcurrencyRetryBehavior<,>));
        });
        // One constrained open-generic handler serves every list request (no per-table list handlers).
        services.AddTransient(typeof(IRequestHandler<,>), typeof(ListRequestHandler<,>));
        services.AddScoped<ICustomFieldService, CustomFieldService>();
        // Chart-of-accounts sub-categories: generic default; a module or country pack registered later replaces it.
        services.AddSingleton<ERP.Application.Finance.IAccountSubcategoryRules, ERP.Application.Finance.StandardAccountSubcategoryRules>();
        services.AddScoped<INumberingService, NumberingService>();
        services.AddScoped<IResetOnRetry>(sp => (NumberingService)sp.GetRequiredService<INumberingService>());
        services.AddScoped<ICsvExportService, CsvExportService>();
        services.AddScoped<ERP.Application.Dashboard.IDashboardContributor, ERP.Application.Dashboard.FinanceDashboard>();
        services.AddScoped<ERP.Application.Finance.ICashForecastSource, ERP.Application.Finance.ChequeForecastSource>();
        services.AddScoped<ERP.Application.Finance.ICashForecastSource, ERP.Application.Finance.ReceivablesForecastSource>();
        services.AddScoped<ERP.Application.Finance.ICashForecastSource, ERP.Application.Finance.PayablesForecastSource>();
        services.AddScoped<ERP.Application.Search.ISearchProvider, ERP.Application.Search.CoreSearchProvider>();
        services.AddScoped<ERP.Application.Finance.IPostingService, ERP.Application.Finance.PostingService>();
        services.AddScoped<ERP.Application.Finance.IInvoicingService, ERP.Application.Finance.InvoicingService>();
        services.AddScoped<ERP.Application.Finance.ISettlementService, ERP.Application.Finance.SettlementService>();
        services.AddScoped<ILegalEntityGuard, LegalEntityGuard>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<INotifier, Notifier>();
        services.AddScoped<ERP.Application.Finance.TaxLineReader>();
        services.AddScoped<ERP.Application.Imports.IImportDefinition, ERP.Application.Imports.PartyImport>();
        services.AddScoped<ERP.Application.Imports.IImportDefinition, ERP.Application.Imports.ItemImport>();
        services.AddScoped<ERP.Application.Reminders.ReminderDispatcher>();
        services.AddScoped<ERP.Application.Reminders.PermissionAudience>();
        services.AddScoped<IMessageChannel, InAppChannel>();
        services.AddScoped<IMessageChannel, OutboxEmailChannel>();
        services.AddScoped<IScheduledJob, ReminderJob>();
        services.AddScoped<IScheduledJob, ERP.Application.Common.IdempotencyCleanupJob>();
        services.AddScoped<ERP.Application.Common.IdempotencyService>();
        services.AddScoped<IReminderSource, CoreReminderSource>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ERP.Application.Finance.AccountNumbering>();

        services.AddScoped<IAttachmentOwnerResolver>(sp => new EntityAttachmentOwnerResolver<ERP.Domain.Parties.Party>(sp.GetRequiredService<IEntityStore>(), "party", Security.CorePermissions.PartiesRead, Security.CorePermissions.PartiesWrite));
        services.AddScoped<IAttachmentOwnerResolver>(sp => new EntityAttachmentOwnerResolver<ERP.Domain.Finance.Invoice>(sp.GetRequiredService<IEntityStore>(), "invoice", Security.CorePermissions.InvoicesRead, Security.CorePermissions.InvoicesWrite));
        services.AddScoped<IAttachmentOwnerResolver>(sp => new EntityAttachmentOwnerResolver<ERP.Domain.Finance.Bill>(sp.GetRequiredService<IEntityStore>(), "bill", Security.CorePermissions.BillsRead, Security.CorePermissions.BillsWrite));
        services.AddScoped<IAttachmentOwnerResolver>(sp => new EntityAttachmentOwnerResolver<ERP.Domain.Finance.Settlement>(sp.GetRequiredService<IEntityStore>(), "receipt", Security.CorePermissions.ReceiptsRead, Security.CorePermissions.ReceiptsWrite, s => s.Direction == ERP.Domain.Finance.InstrumentDirection.Received));
        services.AddScoped<IAttachmentOwnerResolver>(sp => new EntityAttachmentOwnerResolver<ERP.Domain.Finance.Settlement>(sp.GetRequiredService<IEntityStore>(), "payment", Security.CorePermissions.PaymentsRead, Security.CorePermissions.PaymentsWrite, s => s.Direction == ERP.Domain.Finance.InstrumentDirection.Issued));
        services.AddScoped<IAttachmentOwnerResolver>(sp => new EntityAttachmentOwnerResolver<ERP.Domain.Finance.JournalEntry>(sp.GetRequiredService<IEntityStore>(), "journal", Security.CorePermissions.JournalsRead, Security.CorePermissions.JournalsWrite));
        services.AddScoped<IAttachmentOwnerResolver>(sp => new EntityAttachmentOwnerResolver<ERP.Domain.Catalog.Item>(sp.GetRequiredService<IEntityStore>(), "item", Security.CorePermissions.ItemsRead, Security.CorePermissions.ItemsWrite));
        services.AddScoped<IAttachmentOwnerResolver>(sp => new EntityAttachmentOwnerResolver<ERP.Domain.Finance.PaymentInstrument>(sp.GetRequiredService<IEntityStore>(), "cheque", Security.CorePermissions.ChequesRead, Security.CorePermissions.ChequesWrite));

        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
        return services;
    }
}
