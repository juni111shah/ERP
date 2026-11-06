using ERP.Application.Platform;
using ERP.Modularity.Features;

namespace ERP.UnitTests;

public class BrandAssistantRulesTests
{
    private static PublicPlan Plan(string name, decimal monthly, int? users, int? entities, params string[] modules)
        => new(Guid.NewGuid(), name.ToLowerInvariant(), name, null, monthly, monthly * 10, "USD", 14, users, entities, modules, [], false);

    private static readonly PublicPlan[] Plans = [Plan("Enterprise", 399, null, null), Plan("Starter", 49, 5, 1, "sales"), Plan("Business", 149, 25, 3, "sales", "hr")];

    [Theory]
    [InlineData(3, 1, "Starter")]
    [InlineData(12, 2, "Business")]
    [InlineData(5, 2, "Business")]
    [InlineData(100, 1, "Enterprise")]
    [InlineData(2, 9, "Enterprise")]
    public void The_cheapest_plan_that_fits_is_recommended(int users, int entities, string expected)
        => Assert.Equal(expected, BrandAssistant.Recommend(Plans, users, entities, [])!.Name);

    [Fact]
    public void Modules_count_and_an_empty_list_means_all_included()
    {
        Assert.Equal("Business", BrandAssistant.Recommend(Plans, 1, 1, ["hr"])!.Name);
        Assert.Equal("Enterprise", BrandAssistant.Recommend(Plans, 1, 1, ["manufacturing"])!.Name);
    }

    [Fact]
    public void No_plan_fits_when_every_plan_has_a_lower_limit()
        => Assert.Null(BrandAssistant.Recommend([Plan("Small", 10, 2, 1)], 50, 1, []));

    [Fact]
    public void A_plan_is_described_in_plain_sentences_with_its_saving()
    {
        var text = BrandAssistant.Describe(Plans[1]);
        Assert.Contains("USD 49 per month", text);
        Assert.Contains("USD 490 per year", text);
        Assert.Contains("saves 17%", text);
        Assert.Contains("up to 5 users", text);
        Assert.Contains("modules: sales", text);
        Assert.Contains("no limit on users", BrandAssistant.Describe(Plans[0]));
        Assert.Contains("all modules included", BrandAssistant.Describe(Plans[0]));
    }

    [Theory]
    [InlineData("How much does it cost?", true)]
    [InlineData("What are your plans?", true)]
    [InlineData("Tell me about pricing", true)]
    [InlineData("كم السعر؟", true)]
    [InlineData("ما هي الخطط المتاحة؟", true)]
    [InlineData("Who are you?", false)]
    [InlineData("Do you have an airplane?", false)]
    [InlineData("Does it support inventory?", false)]
    [InlineData("من أنتم؟", false)]
    public void Price_questions_are_recognised_in_both_languages(string text, bool expected)
        => Assert.Equal(expected, BrandAssistant.AsksAboutPrices(text));

    private static readonly IReadOnlyList<PublicFaq> Faqs = PublicContentText.Faqs(14, "help@example.test");

    [Theory]
    [InlineData("How do I pay?", "How do I pay?")]
    [InlineData("what happens when my trial ends", "What happens when the trial ends?")]
    [InlineData("Is there a mobile app?", "Is there a mobile app?")]
    [InlineData("كيف أدفع؟", "How do I pay?")]
    [InlineData("كيف يمكنني الدفع؟", "How do I pay?")]
    [InlineData("هل يوجد تطبيق للهاتف؟", "Is there a mobile app?")]
    public void The_right_written_answer_is_found_in_either_language(string question, string expected)
        => Assert.Equal(expected, BrandAssistant.FindFaqs(Faqs, question, 3)[0].Q);

    [Fact]
    public void Nothing_is_found_for_words_that_match_nothing()
    {
        Assert.Empty(BrandAssistant.FindFaqs(Faqs, "xyzzy qwerty", 3));
        Assert.Empty(BrandAssistant.FindFaqs(Faqs, "the and how", 3));
    }

    [Fact]
    public void Every_written_answer_has_both_languages_and_uses_the_support_email()
    {
        Assert.All(Faqs, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.A));
            Assert.True(BrandAssistant.IsArabic(f.QAr), f.QAr);
            Assert.True(BrandAssistant.IsArabic(f.AAr), f.AAr);
        });
        Assert.Contains(Faqs, f => f.A.Contains("help@example.test") && f.AAr.Contains("help@example.test"));
    }

    [Theory]
    [InlineData("The Starter plan is USD 49 per month.", false)]
    [InlineData("That is 149 USD a month or USD 1,490 a year.", false)]
    [InlineData("It costs USD 12.34 per month.", true)]
    [InlineData("A special price of 30 USD.", true)]
    [InlineData("Up to 25 users and 3 legal entities.", false)]
    public void An_amount_that_is_not_on_the_price_list_is_noticed(string reply, bool flagged)
        => Assert.Equal(flagged, BrandAssistant.QuotesUnknownPrice(reply, Plans));

    [Theory]
    [InlineData("The Business plan is $149 a month.", false)]
    [InlineData("That is $1,490 a year.", false)]
    [InlineData("A special price of $99.", true)]
    [InlineData("Only €12.50 per month.", true)]
    [InlineData("It comes to 30$ a month.", true)]
    [InlineData("It costs 30 dollars a month.", true)]
    public void Currency_symbols_and_words_count_as_amounts(string reply, bool flagged)
        => Assert.Equal(flagged, BrandAssistant.QuotesUnknownPrice(reply, Plans));

    [Theory]
    [InlineData("سعر الخطة ١٤٩ دولار شهرياً.", false)]
    [InlineData("USD ٤٩ شهرياً", false)]
    [InlineData("USD ١٬٤٩٠ سنوياً", false)]
    [InlineData("سعر خاص ٣٠ دولاراً.", true)]
    [InlineData("USD ٣٠ شهرياً", true)]
    [InlineData("٤٩٫٥٠ درهم شهرياً", true)]
    [InlineData("السعر ٣٠ ريالاً", true)]
    [InlineData("السعر 30 ر.س", true)]
    [InlineData("حتى ٢٥ مستخدماً و٣ كيانات قانونية.", false)]
    public void Arabic_digits_and_currency_words_are_read_as_amounts(string reply, bool flagged)
        => Assert.Equal(flagged, BrandAssistant.QuotesUnknownPrice(reply, Plans));

    private static readonly PlatformModuleView[] Catalog = [new("hr", "Human Resources", "People and payroll", "People", false, []), new("sales", "Sales", "Selling", "Sales", false, [])];

    [Theory]
    [InlineData("Human Resources", "hr")]
    [InlineData("human resources", "hr")]
    [InlineData(" HR ", "hr")]
    [InlineData("SALES", "sales")]
    [InlineData("Teleportation", "Teleportation")]
    public void A_module_is_understood_by_its_name_or_its_key(string asked, string key)
        => Assert.Equal(key, BrandAssistant.ModuleKeys(Catalog, [asked]).Single());

    [Fact]
    public void A_module_name_from_the_model_finds_the_plan_that_has_it()
    {
        Assert.Equal("Enterprise", BrandAssistant.Recommend(Plans, 1, 1, ["Human Resources"])!.Name); // what a name did before it was turned into a key
        Assert.Equal("Business", BrandAssistant.Recommend(Plans, 1, 1, BrandAssistant.ModuleKeys(Catalog, ["Human Resources"]))!.Name);
    }

    [Theory]
    [InlineData("مرحبا، كيف حالك؟", true)]
    [InlineData("Hello there", false)]
    [InlineData("ما هو ERP؟", true)]
    public void Arabic_is_recognised(string text, bool expected) => Assert.Equal(expected, BrandAssistant.IsArabic(text));

    [Fact]
    public void The_fact_sheet_states_the_closed_mode_and_offers_nothing_it_does_not_have()
    {
        var config = new SignupConfig(ERP.Domain.Platform.SignupMode.Closed, false, null, "USD", null, null, []);
        var legal = new LegalDocuments("Acme", null, null, "", "", null);
        var content = new PublicContent(PublicContentText.BuiltIn, PublicContentText.Trust, Faqs, null);
        var sheet = BrandAssistant.FactSheet(config, legal, content, [], [], [], []);
        Assert.Contains("Registration is closed", sheet);
        Assert.Contains("No plans are published", sheet);
        Assert.DoesNotContain("About the company", sheet);
        Assert.Contains("not published", sheet);
    }
}
