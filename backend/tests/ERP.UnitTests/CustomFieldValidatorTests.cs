using System.Text.Json.Nodes;
using ERP.Application.Customization;
using ERP.Domain.Customization;

namespace ERP.UnitTests;

public class CustomFieldValidatorTests
{
    private static CustomFieldDefinition Def(string key, CustomFieldDataType type, bool required = false, string? options = null, bool active = true)
        => new() { Key = key, Label = key, EntityType = "party", DataType = type, Required = required, Options = options, IsActive = active };

    private static readonly CustomFieldDefinition[] Defs =
    [
        Def("cf_ref", CustomFieldDataType.Text, options: """{"minLength":3,"maxLength":20}"""),
        Def("cf_floors", CustomFieldDataType.Number, options: """{"min":0,"max":200}"""),
        Def("cf_fee", CustomFieldDataType.Decimal, options: """{"scale":2,"min":0}"""),
        Def("cf_since", CustomFieldDataType.Date),
        Def("cf_vip", CustomFieldDataType.Bool),
        Def("cf_tier", CustomFieldDataType.Select, options: """{"choices":[{"value":"gold"},{"value":"silver"},{"value":"bronze","isActive":false}]}"""),
        Def("cf_tags", CustomFieldDataType.MultiSelect, options: """{"choices":[{"value":"pool"},{"value":"gym"},{"value":"spa"}],"maxSelections":2}"""),
        Def("cf_owner", CustomFieldDataType.Lookup, options: """{"target":"party"}"""),
        Def("cf_code", CustomFieldDataType.Text, required: true),
        Def("cf_old", CustomFieldDataType.Text, active: false),
    ];

    private static CustomFieldValidator.Result Run(string json, string? existing = null, bool create = false, Func<string, Guid, bool>? lookup = null)
    {
        var incoming = (JsonObject)JsonNode.Parse(json)!;
        if (create && !incoming.ContainsKey("cf_code")) incoming["cf_code"] = "X1";
        return CustomFieldValidator.Validate(Defs, incoming, existing, create, lookup);
    }

    [Theory]
    [InlineData("""{"cf_ref":"abc"}""", true)]
    [InlineData("""{"cf_ref":"ab"}""", false)]
    [InlineData("""{"cf_ref":"123456789012345678901"}""", false)]
    [InlineData("""{"cf_ref":12}""", false)]
    [InlineData("""{"cf_floors":0}""", true)]
    [InlineData("""{"cf_floors":200}""", true)]
    [InlineData("""{"cf_floors":201}""", false)]
    [InlineData("""{"cf_floors":-1}""", false)]
    [InlineData("""{"cf_floors":1.5}""", false)]
    [InlineData("""{"cf_floors":"12"}""", false)]
    [InlineData("""{"cf_floors":9223372036854775808}""", false)]
    [InlineData("""{"cf_fee":10.5}""", true)]
    [InlineData("""{"cf_fee":10.555}""", false)]
    [InlineData("""{"cf_fee":-0.01}""", false)]
    [InlineData("""{"cf_since":"2026-10-22"}""", true)]
    [InlineData("""{"cf_since":"2026-02-30"}""", false)]
    [InlineData("""{"cf_since":"22/10/2026"}""", false)]
    [InlineData("""{"cf_vip":true}""", true)]
    [InlineData("""{"cf_vip":"true"}""", false)]
    [InlineData("""{"cf_vip":1}""", false)]
    [InlineData("""{"cf_tier":"gold"}""", true)]
    [InlineData("""{"cf_tier":"GOLD"}""", false)]
    [InlineData("""{"cf_tier":"platinum"}""", false)]
    [InlineData("""{"cf_tier":"bronze"}""", false)]
    [InlineData("""{"cf_tier":["gold"]}""", false)]
    [InlineData("""{"cf_tags":["pool","gym"]}""", true)]
    [InlineData("""{"cf_tags":["pool","gym","spa"]}""", false)]
    [InlineData("""{"cf_tags":["pool","pool"]}""", false)]
    [InlineData("""{"cf_tags":"pool"}""", false)]
    [InlineData("""{"cf_unknown":1}""", false)]
    [InlineData("""{"cf_old":"x"}""", false)]
    [InlineData("""{"cf_floors":{"$gt":0}}""", false)]
    public void Values_are_validated_strictly_by_type(string json, bool valid)
        => Assert.Equal(valid, Run(json).IsValid);

    [Fact]
    public void Required_field_is_enforced_on_create_and_reported_by_key()
    {
        var r = CustomFieldValidator.Validate(Defs, new JsonObject(), null, isCreate: true);
        Assert.False(r.IsValid);
        Assert.Contains("customFields.cf_code", r.Errors.Keys);
    }

    [Fact]
    public void Update_merges_and_keeps_inactive_and_untouched_values()
    {
        var existing = """{"cf_code":"A1","cf_old":"legacy","cf_ref":"abcd"}""";
        var r = Run("""{"cf_ref":"wxyz"}""", existing);
        Assert.True(r.IsValid);
        var json = JsonNode.Parse(r.Json!)!.AsObject();
        Assert.Equal("legacy", (string?)json["cf_old"]);
        Assert.Equal("A1", (string?)json["cf_code"]);
        Assert.Equal("wxyz", (string?)json["cf_ref"]);
    }

    [Fact]
    public void Null_clears_an_optional_value()
    {
        var r = Run("""{"cf_ref":null}""", """{"cf_code":"A1","cf_ref":"abcd"}""");
        Assert.True(r.IsValid);
        Assert.False(JsonNode.Parse(r.Json!)!.AsObject().ContainsKey("cf_ref"));
    }

    [Fact]
    public void Lookup_must_resolve_inside_the_workspace()
    {
        var known = Guid.NewGuid();
        Assert.True(Run($$"""{"cf_owner":"{{known}}"}""", lookup: (_, id) => id == known).IsValid);
        Assert.False(Run($$"""{"cf_owner":"{{Guid.NewGuid()}}"}""", lookup: (_, id) => id == known).IsValid);
    }

    [Theory]
    [InlineData("cf_vip_tier", true)]
    [InlineData("cf_1st", true)]
    [InlineData("Floors", false)]
    [InlineData("floors", false)]
    [InlineData("cf_a b", false)]
    [InlineData("cf_x'--", false)]
    [InlineData("cf_", false)]
    public void Definition_keys_follow_the_cf_snake_case_rule(string key, bool valid)
    {
        var errors = CustomFieldValidator.ValidateDefinition(Def(key, CustomFieldDataType.Text));
        Assert.Equal(valid, !errors.ContainsKey("key"));
    }

    [Fact]
    public void Select_definition_needs_choices_and_lookup_needs_target()
    {
        Assert.Contains("options.choices", CustomFieldValidator.ValidateDefinition(Def("cf_s", CustomFieldDataType.Select)).Keys);
        Assert.Contains("options.target", CustomFieldValidator.ValidateDefinition(Def("cf_l", CustomFieldDataType.Lookup)).Keys);
    }

    [Fact]
    public void Catastrophic_regex_is_rejected_or_times_out_safely()
    {
        var def = Def("cf_rx", CustomFieldDataType.Text, options: """{"pattern":"(a+)+$"}""");
        var errors = CustomFieldValidator.ValidateDefinition(def);
        if (errors.Count == 0)
        {
            var err = CustomFieldValidator.ValidateValue(def, JsonValue.Create(new string('a', 40) + "!"), out _);
            Assert.NotNull(err);
        }
    }

    [Fact]
    public void Project_hides_inactive_fields()
    {
        var shown = CustomFieldValidator.Project("""{"cf_ref":"abcd","cf_old":"x"}""", Defs);
        Assert.True(shown.ContainsKey("cf_ref"));
        Assert.False(shown.ContainsKey("cf_old"));
    }
}
