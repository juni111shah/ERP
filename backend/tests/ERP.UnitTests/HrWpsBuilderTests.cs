using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Wps;

namespace ERP.UnitTests;

public class HrWpsBuilderTests
{
    private static (WpsInput Input, Employee Emp) Sample(string? columns, string? header, string currency = "AED")
    {
        var emp = new Employee { FullName = "Amir Khan", Number = "EMP-0001", Iban = "AE07 0331 2345 6789 0123 456", LabourCardNumber = "12345678901234", BankRoutingCode = "123456789" };
        var run = new PayrollRun { Year = 2026, Month = 10 };
        var line = new PayrollLine { EmployeeId = emp.Id, EmployeeNumber = emp.Number, EmployeeName = emp.FullName, Basic = 6000m, Allowances = 3300m, Overtime = 312.5m, Absence = 620m, GrossEarnings = 8992.5m, Deductions = 500m, NetPay = 8492.5m, AbsentDays = 2 };
        var s = new WpsSettings(currency, new DateTime(2026, 11, 2, 9, 30, 0, DateTimeKind.Utc), "1234567890123", "987654321", columns, header);
        return (new WpsInput(run, [line], new Dictionary<Guid, Employee> { [emp.Id] = emp }, s), emp);
    }

    [Fact]
    public void The_UAE_builder_writes_EDR_then_SCR_with_the_entity_currency()
    {
        var (input, _) = Sample(null, null, "USD");
        var lines = new UaeWpsFileBuilder().Build(input).TrimEnd('\n').Split('\n');
        Assert.Equal("EDR,12345678901234,123456789,AE070331234567890123456,2026-10-01,2026-10-31,31,8180.00,312.50,2", lines[0]);
        Assert.Equal("SCR,1234567890123,987654321,2026-11-02,0930,102026,1,8492.50,USD", lines[1]);
    }

    [Fact]
    public void The_UAE_builder_names_what_is_missing()
    {
        var (input, emp) = Sample(null, null);
        emp.Iban = null;
        var ex = Assert.Throws<ConflictException>(() => new UaeWpsFileBuilder().Build(input));
        Assert.Contains("EMP-0001 Amir Khan: IBAN", ex.Message);
    }

    [Fact]
    public void The_Qatar_builder_is_a_stub_until_the_bank_template_exists()
    {
        var (input, _) = Sample(null, null);
        var ex = Assert.Throws<ConflictException>(() => new QatarWpsFileBuilder().Build(input));
        Assert.Equal("The Qatar salary file layout has not been configured. Provide the bank template.", ex.Message);
    }

    [Fact]
    public void The_configurable_CSV_writes_the_chosen_columns_in_order_after_the_header()
    {
        var (input, _) = Sample("employeeNumber, name, iban, netPay, currency", "No,Name,IBAN,Net,Cur");
        var text = new ConfigurableCsvWpsBuilder().Build(input);
        Assert.Equal("No,Name,IBAN,Net,Cur\nEMP-0001,Amir Khan,AE070331234567890123456,8492.50,AED\n", text);
    }

    [Fact]
    public void The_configurable_CSV_quotes_values_and_rejects_unknown_fields()
    {
        var (input, _) = Sample("name,favouriteColour", null);
        Assert.Throws<ConflictException>(() => new ConfigurableCsvWpsBuilder().Build(input));
        var (ok, _) = Sample("name", null);
        ok.Lines[0].EmployeeName = "Khan, Amir";
        Assert.Equal("\"Khan, Amir\"\n", new ConfigurableCsvWpsBuilder().Build(ok));
        Assert.False(ConfigurableCsvWpsBuilder.IsConfigured(Sample(" ", null).Input.Settings));
    }
}
