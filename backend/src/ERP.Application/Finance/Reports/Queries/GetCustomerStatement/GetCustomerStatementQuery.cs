using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>A customer's open documents as of a date, with its unallocated receipts and the balance.</summary>
public sealed record GetCustomerStatementQuery(Guid PartyId, DateOnly AsOf, Guid? LegalEntityId) : IQuery<CustomerStatementDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
