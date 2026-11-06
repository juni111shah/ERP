using ERP.Application.Common;
using ERP.Domain.Platform;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Abstractions;

namespace ERP.Application.Platform;

public sealed record PublicFeature(string Icon, string Title, string Text, string TitleAr, string TextAr);
public sealed record PublicFaq(string Q, string A, string QAr, string AAr);
public sealed record PublicContent(IReadOnlyList<PublicFeature> BuiltIn, IReadOnlyList<PublicFeature> Trust, IReadOnlyList<PublicFaq> Faqs, string? About);

/// <summary>What the public pages say about the product. One source, read by the pricing page and by the public assistant, so they cannot disagree.</summary>
public sealed record GetPublicContent : IQuery<PublicContent>;

/// <summary>Each line is a feature that exists in the product today. The Arabic was written alongside the English: have a native speaker review it before launch.</summary>
public static class PublicContentText
{
    public static readonly IReadOnlyList<PublicFeature> BuiltIn =
    [
        new("team", "Roles and permissions", "Give each person access to exactly what their job needs, down to single actions.",
            "الأدوار والصلاحيات", "امنح كل شخص الوصول الذي يحتاجه عمله بالضبط، حتى على مستوى الإجراء الواحد."),
        new("apartment", "Several companies, one sign-in", "Run more than one legal entity and choose who can see which.",
            "عدة شركات بتسجيل دخول واحد", "أدر أكثر من كيان قانوني واختر من يمكنه رؤية كل منها."),
        new("check-square", "Approval rules", "Decide what needs a second pair of eyes before it goes through.",
            "قواعد الموافقة", "حدد ما يحتاج إلى مراجعة شخص آخر قبل اعتماده."),
        new("form", "Your own fields", "Add the details your business tracks to records, with no developer needed.",
            "حقولك الخاصة", "أضف إلى السجلات التفاصيل التي يتابعها عملك، دون الحاجة إلى مطور."),
        new("download", "Bring your data", "Import your customers, suppliers and items from a spreadsheet to get going quickly.",
            "انقل بياناتك", "استورد عملاءك ومورديك وأصنافك من جدول بيانات لتبدأ بسرعة."),
        new("book", "Document vault", "Keep files with the records they belong to, instead of in someone's inbox.",
            "خزنة المستندات", "احتفظ بالملفات مع سجلاتها، بدلاً من بقائها في بريد أحد الموظفين."),
        new("printer", "Print and email documents", "Send invoices and other documents to customers straight from the record.",
            "طباعة المستندات وإرسالها بالبريد", "أرسل الفواتير والمستندات الأخرى إلى العملاء مباشرة من السجل."),
        new("user", "Customer and supplier portals", "Let the people you do business with see what is theirs, without an account on your system.",
            "بوابات العملاء والموردين", "دع من تتعامل معهم يطلعون على ما يخصهم، دون حساب على نظامك."),
    ];

    public static readonly IReadOnlyList<PublicFeature> Trust =
    [
        new("safety", "Two-factor sign-in", "A code from an authenticator app on top of the password.",
            "تسجيل الدخول بخطوتين", "رمز من تطبيق المصادقة إضافة إلى كلمة المرور."),
        new("audit", "A full audit log", "See who changed what, and when.",
            "سجل تدقيق كامل", "اعرف من غيّر ماذا ومتى."),
        new("lock", "Your data kept apart", "Each workspace is walled off from every other.",
            "بياناتك منفصلة", "كل مساحة عمل معزولة عن غيرها."),
        new("history", "Your data stays yours", "Even if a subscription ends, your records are not deleted by it.",
            "بياناتك تبقى لك", "حتى إذا انتهى الاشتراك، لا تُحذف سجلاتك بسبب ذلك."),
    ];

    public static IReadOnlyList<PublicFaq> Faqs(int trialDays, string? supportEmail)
    {
        var contact = supportEmail is { Length: > 0 } ? $"write to {supportEmail}" : "contact our team";
        var contactAr = supportEmail is { Length: > 0 } ? $"راسلنا على {supportEmail}" : "تواصل مع فريقنا";
        return
        [
            new("How does the free trial work?",
                trialDays > 0 ? $"You get up to {trialDays} days to use the system with your own business, depending on the plan. You do not need a card. We remind you before the trial ends." : "Plans start as soon as your workspace is ready, and your first invoice follows.",
                "كيف تعمل الفترة التجريبية المجانية؟",
                trialDays > 0 ? $"تحصل على ما يصل إلى {trialDays} يوماً لاستخدام النظام مع عملك، بحسب الخطة. لا تحتاج إلى بطاقة، وسنذكرك قبل انتهاء الفترة التجريبية." : "تبدأ الخطط بمجرد أن تصبح مساحة عملك جاهزة، ثم تصلك فاتورتك الأولى."),
            new("What happens when the trial ends?", "Your workspace becomes view-only: nothing is lost and you can still see everything. Choose a plan and you can carry on exactly where you stopped.",
                "ماذا يحدث عند انتهاء الفترة التجريبية؟", "تصبح مساحة عملك للعرض فقط: لا يضيع شيء ويمكنك رؤية كل بياناتك. اختر خطة وتابع من حيث توقفت."),
            new("How do I pay?", "We send an invoice for each billing period, and you pay it by bank transfer using the details on the invoice. We do not take card payments online at the moment.",
                "كيف أدفع؟", "نرسل فاتورة لكل فترة فوترة، وتدفعها بتحويل بنكي باستخدام التفاصيل المذكورة فيها. لا نقبل الدفع بالبطاقة عبر الإنترنت حالياً."),
            new("Can I change my plan later?", "Yes. Ask for the plan you want from Subscription and billing inside your workspace, and we confirm the change. If you move to a smaller plan, modules it does not include are switched off, and their data stays.",
                "هل يمكنني تغيير خطتي لاحقاً؟", "نعم. اطلب الخطة التي تريدها من قسم الاشتراك والفوترة داخل مساحة عملك، وسنؤكد التغيير. إذا انتقلت إلى خطة أصغر، تتوقف الوحدات غير المشمولة فيها وتبقى بياناتها محفوظة."),
            new("What is a legal entity?", "A separate company with its own books. If your group has two companies, that is two legal entities in one workspace, and you can decide who sees which.",
                "ما هو الكيان القانوني؟", "شركة مستقلة لها دفاترها الخاصة. إذا كانت مجموعتك تضم شركتين، فهذان كيانان قانونيان في مساحة عمل واحدة، ويمكنك تحديد من يرى كلاً منهما."),
            new("What counts as a user?", "Anyone with an active sign-in to your workspace. Deactivating someone frees their place, and their past work stays on record.",
                "من يُحتسب مستخدماً؟", "كل من لديه تسجيل دخول نشط إلى مساحة عملك. إيقاف حساب شخص ما يحرر مكانه، ويبقى عمله السابق مسجلاً."),
            new("What if a payment is late?", "You keep working during a grace period. If the invoice is still unpaid, the workspace becomes view-only and may later be suspended until it is paid. Paying restores access.",
                "ماذا لو تأخر الدفع؟", "تواصل العمل خلال فترة سماح. إذا بقيت الفاتورة غير مدفوعة، تصبح مساحة العمل للعرض فقط وقد تُعلّق لاحقاً حتى السداد. الدفع يعيد الوصول."),
            new("Is my data safe, and is it private?", "Each workspace is kept apart from every other. Two-factor sign-in, permissions by role and a full audit log are built in. See our Privacy Policy for how information is handled.",
                "هل بياناتي آمنة وخاصة؟", "كل مساحة عمل منفصلة عن غيرها. يتضمن النظام تسجيل الدخول بخطوتين والصلاحيات حسب الدور وسجل تدقيق كاملاً. راجع سياسة الخصوصية لمعرفة كيفية التعامل مع المعلومات."),
            new("Is there a mobile app?", "Yes. Sign in with your usual account to see your figures, invoices and customers, handle approvals and use My HR on your phone. It uses the same workspace as the web.",
                "هل يوجد تطبيق للهاتف؟", "نعم. سجّل الدخول بحسابك المعتاد لترى أرقامك وفواتيرك وعملاءك، وتتعامل مع الموافقات، وتستخدم خدمات الموارد البشرية الخاصة بك من هاتفك. وهو يستخدم مساحة العمل نفسها الموجودة على الويب."),
            new("Can I bring my existing data?", "Yes. You can import customers, suppliers and items from a spreadsheet when you set up.",
                "هل يمكنني نقل بياناتي الحالية؟", "نعم. يمكنك استيراد العملاء والموردين والأصناف من جدول بيانات عند الإعداد."),
            new("How do I cancel, and what happens to my data?", $"To cancel, {contact}. Access stops, but cancelling does not delete your records, so you can come back. Tell us if you want your data deleted.",
                "كيف ألغي الاشتراك وماذا يحدث لبياناتي؟", $"للإلغاء، {contactAr}. يتوقف الوصول، لكن الإلغاء لا يحذف سجلاتك، لذا يمكنك العودة. أخبرنا إذا كنت تريد حذف بياناتك."),
            new("We need more than the plans offer. Can you help?", $"Yes. For more users, a special price or anything else the plans do not cover, {contact}.",
                "نحتاج إلى أكثر مما تقدمه الخطط. هل يمكنكم المساعدة؟", $"نعم. لمزيد من المستخدمين أو لسعر خاص أو أي شيء آخر لا تغطيه الخطط، {contactAr}."),
        ];
    }
}

internal sealed class PublicContentHandler(IReadRepository<SubscriptionPlan> plans, PlatformSettingsStore settingsStore) : IRequestHandler<GetPublicContent, PublicContent>
{
    public async Task<PublicContent> Handle(GetPublicContent q, CancellationToken ct)
    {
        var settings = await settingsStore.GetAsync(ct);
        var trialDays = await plans.Query().Where(p => p.IsActive && p.IsPublic).MaxAsync(p => (int?)p.TrialDays, ct) ?? 0;
        return new(PublicContentText.BuiltIn, PublicContentText.Trust, PublicContentText.Faqs(trialDays, settings.SupportEmail), string.IsNullOrWhiteSpace(settings.AboutText) ? null : settings.AboutText.Trim());
    }
}
