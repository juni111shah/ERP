using ERP.Application.Security;

namespace ERP.Application.Assistant;

/// <summary>How-to articles for the screens every workspace has. Each module adds its own pack.</summary>
internal static class CoreAssistantHelp
{
    public static readonly AssistantHelpPack Pack = new(null,
    [
        new("Add a customer or supplier", "For a customer go to Sales > Customers and press New customer. For a supplier go to Purchases > Vendors and press New vendor. Fill in the details and press Save.", "/parties", CorePermissions.PartiesRead),
        new("Create a sales invoice", "Go to Sales > Invoices and press Add invoice. Choose the customer, add lines with Add line, then press Save draft or Save and issue.", "/finance/invoices", CorePermissions.InvoicesRead),
        new("Record a receipt from a customer", "Go to Sales > Receipts, press New receipt, choose the customer and amount, and press Record receipt. From an issued invoice, Record Payment starts the same receipt for you.", "/finance/receipts", CorePermissions.ReceiptsRead),
        new("Enter a supplier bill", "Go to Purchases > Bills and press New bill. Choose the vendor, add lines with Add line, then press Save draft or Save and post.", "/finance/bills", CorePermissions.BillsRead),
        new("Pay a supplier", "Go to Purchases > Payments, press New payment, choose the vendor and amount, and press Record payment. From a bill, Make Payment starts the same payment for you.", "/finance/payments", CorePermissions.PaymentsRead),
        new("Invite a user and give them access", "Go to Setup > Users and press Invite user. Enter their details, choose at least one role and press Save. The Roles button on a user's row changes their roles later.", "/admin/users", CorePermissions.UsersRead),
        new("Create a role and its permissions", "Go to Setup > Roles and press New role. Name it, tick the permissions it should have and press Save.", "/admin/roles", CorePermissions.RolesRead),
        new("Approve or request an approval", "Go to Workspace > Approvals. The Waiting for me tab has Approve and Reject on each request, My requests shows your own, and Request approval starts a new one.", "/approvals"),
        new("Run a report", "Go to Finance & Accounting > Reports and choose a report. Set the filters and press Run; CSV and Print export the result.", "/reports", CorePermissions.ReportsRead),
        new("Turn a module on or off", "Go to Setup > Modules and press Enable module, or Disable on one that is on. Disabling keeps its data.","/admin/modules", CorePermissions.SettingsRead),
        new("Switch legal entity", "Use the selector with the bank icon in the top bar and pick a legal entity, or All legal entities if it is offered. People limited to one legal entity do not see it."),
        new("See your subscription and change plan", "Go to Setup > Subscription and billing to see your plan, how to pay and your invoices. Under Change your plan press Ask for and the plan's name, then Send request; our team confirms it.", "/admin/billing", CorePermissions.BillingRead),
    ]);
}
