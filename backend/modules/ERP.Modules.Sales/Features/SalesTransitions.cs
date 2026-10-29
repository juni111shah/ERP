using ERP.Application.Common;
using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Features;

public static class SalesTransitions
{
    public static readonly StatusTransitions<SalesQuoteStatus> Quote = new(
        new Dictionary<SalesQuoteStatus, IEnumerable<SalesQuoteStatus>>
        {
            [SalesQuoteStatus.Draft] = [SalesQuoteStatus.Sent, SalesQuoteStatus.Accepted, SalesQuoteStatus.Declined],
            [SalesQuoteStatus.Sent] = [SalesQuoteStatus.Accepted, SalesQuoteStatus.Declined, SalesQuoteStatus.Expired],
            [SalesQuoteStatus.Accepted] = [SalesQuoteStatus.Converted],
            [SalesQuoteStatus.Declined] = [SalesQuoteStatus.Draft],
            [SalesQuoteStatus.Expired] = [SalesQuoteStatus.Draft],
            [SalesQuoteStatus.Converted] = [],
        },
        systemOnly: [SalesQuoteStatus.Converted]);

    public static readonly StatusTransitions<SalesOrderStatus> Order = new(
        new Dictionary<SalesOrderStatus, IEnumerable<SalesOrderStatus>>
        {
            [SalesOrderStatus.Draft] = [SalesOrderStatus.Confirmed, SalesOrderStatus.Cancelled],
            [SalesOrderStatus.Confirmed] = [SalesOrderStatus.Processing, SalesOrderStatus.Delivered, SalesOrderStatus.Cancelled, SalesOrderStatus.Invoiced],
            [SalesOrderStatus.Processing] = [SalesOrderStatus.Delivered, SalesOrderStatus.Cancelled, SalesOrderStatus.Invoiced],
            [SalesOrderStatus.Delivered] = [SalesOrderStatus.Invoiced],
            [SalesOrderStatus.Invoiced] = [],
            [SalesOrderStatus.Cancelled] = [],
        },
        systemOnly: [SalesOrderStatus.Invoiced]);
}
