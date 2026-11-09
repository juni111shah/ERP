using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Printing;

/// <summary>A print document as a PDF. Same permission as the print data it is built from; an unknown id is a 404.</summary>
public sealed record GetPrintPdfQuery(PrintDocKind Kind, Guid Id) : IQuery<PrintPdfFile>, IRequirePermission
{
    public string RequiredPermission => Kind switch
    {
        PrintDocKind.Invoice => CorePermissions.InvoicesRead,
        PrintDocKind.Receipt => CorePermissions.ReceiptsRead,
        PrintDocKind.Bill => CorePermissions.BillsRead,
        PrintDocKind.Payment => CorePermissions.PaymentsRead,
        PrintDocKind.Journal => CorePermissions.JournalsRead,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
    };
}
