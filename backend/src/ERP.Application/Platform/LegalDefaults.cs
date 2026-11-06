namespace ERP.Application.Platform;

/// <summary>
/// Starter wording for the two public documents, describing how this product actually behaves. The founder edits them in
/// Settings and should have them reviewed by a lawyer before selling. {company} and {email} are filled in when shown.
/// </summary>
public static class LegalDefaults
{
    public const string Terms = """
        These terms are an agreement between you, the business that registers a workspace, and {company}, who provides this software. By registering or using a workspace, you accept them.

        ## The service
        You get access to the ERP software through a workspace for your business. The modules, number of users and number of legal entities depend on your plan.

        ## Your account
        You are responsible for the people you give access to and for what they do in your workspace. Keep passwords and sign-in codes private, and tell us at {email} if you think an account has been misused. The information you give when registering must be correct.

        ## Free trial
        A plan may include a free trial. No payment details are needed to start one. When a trial ends without a plan being activated, your workspace becomes view-only: you can see your data but not change it. If it stays that way, it may then be suspended.

        ## Prices and payment
        Prices are shown on our pricing page and may change. A change applies from your next billing period. Plans are billed monthly or annually, in advance, by invoice. Invoices are paid as described on the invoice. Taxes are added where they apply.

        ## Late payment
        If an invoice is overdue, you keep normal use for a grace period. After that your workspace becomes view-only, and it may later be suspended until the invoice is paid.

        ## Changing plan or cancelling
        Ask for a plan change from Subscription and billing in your workspace. We confirm it. Moving to a plan with fewer modules switches off the others. Their data stays and returns if you move back. To cancel, write to {email}. Amounts already invoiced for a period that has started are not refunded unless the law requires it or we agree otherwise.

        ## Your data
        The data you enter stays yours. We use it only to provide the service to you. When a subscription is suspended or cancelled, access stops but the data is not deleted by that action. Write to {email} about getting a copy of your data or having it deleted.

        ## Acceptable use
        Do not use the service for anything unlawful, try to reach another customer's data, disrupt the service, or resell it without our written agreement. We may suspend a workspace that does so.

        ## Availability and liability
        We work to keep the service available and your data safe, but the software is provided as it is and we cannot promise it will never be interrupted or free of errors. You remain responsible for checking your own records, including tax and accounting figures. To the extent the law allows, our total liability for any claim is limited to the amount you paid us in the 12 months before it.

        ## Changes to these terms
        We may update these terms. The date of the current version is shown on this page. If you keep using the service after a change, the new terms apply.

        ## Contact
        Questions about these terms: {email}.
        """;

    public const string Privacy = """
        This policy explains what {company} does with personal information when you use this software.

        ## What we collect
        When you register: your name, business name, work email, phone number if you give one, and the internet address the registration came from. When you use the service: the people you add as users, sign-in times, and a record of important actions for your audit log. We also hold the business data you and your team enter.

        ## How we use it
        To create and run your workspace, keep it secure, send invoices and notices about your subscription, and answer your questions. We do not sell personal information.

        ## Cookies
        We use one essential cookie to keep you signed in, including across browser tabs. It cannot be read by page scripts. We do not use advertising cookies.

        ## Who can see it
        People you give access to in your workspace, and our own team when running the service or helping you. Service providers that help us run it, such as hosting and email, handle data only for that purpose. We share information if the law requires it.

        ## AI assistant
        Our website and the software can include an AI assistant. What you type to it is sent to an AI service provider, which may process it outside your country, so that it can reply. Please do not enter passwords, payment details or other sensitive information. Chats on our public website are kept for 30 days to improve our answers. If you ask us to contact you, we keep the details you give until we have dealt with your request. Inside a workspace, the assistant is off until the workspace's administrator turns it on, and it can only use what the person asking is allowed to see.

        ## How long we keep it
        We keep your data while you have a workspace. If you cancel, your data is kept so that you can come back. Write to us if you want it deleted, and we will do so unless the law requires us to keep it. Registrations that are never completed are closed, and their stored passwords are removed.

        ## Security
        Passwords are stored only as secure hashes. Two-factor sign-in, role-based permissions and an audit log are built in. Each workspace's data is kept apart from every other's.

        ## Your rights
        You can ask to see, correct or delete your personal information. Depending on where you live, you may have further rights. Write to {email}.

        ## Changes to this policy
        We may update this policy. The date of the current version is shown on this page.

        ## Contact
        Questions about your information: {email}.
        """;

    public static string Fill(string text, string? company, string? email) => text
        .Replace("{company}", string.IsNullOrWhiteSpace(company) ? "the provider of this service" : company.Trim())
        .Replace("{email}", string.IsNullOrWhiteSpace(email) ? "our support team" : email.Trim());
}
