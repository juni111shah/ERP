namespace ERP.Application.Workflow;

public static class ApprovalDocumentTypes
{
    public const string Bill = "bill";
    public const string Journal = "journal";
    public const string Payment = "payment";

    /// <summary>Document types whose posting honours approval policies.</summary>
    public static readonly string[] All = [Bill, Journal, Payment, "purchase_order", "purchase_requisition"];
}
