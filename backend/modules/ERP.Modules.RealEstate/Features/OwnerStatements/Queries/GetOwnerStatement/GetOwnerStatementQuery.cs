using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>
/// What a property owner is due for a period: rent actually collected, net of tax (receipts allocated to the rent invoices of their properties, by receipt date),
/// less the management fee on it and the owner's share of the maintenance cost of work completed in the period (cost from the linked vendor bill when there is one, otherwise the actual or estimated cost; each job can be charged to the owner, the company, the tenant or shared). Cash basis.
/// </summary>
public sealed record GetOwnerStatementQuery(Guid OwnerPartyId, DateOnly From, DateOnly To) : IQuery<OwnerStatementDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }
