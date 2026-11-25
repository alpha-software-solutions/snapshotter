public class ConverterTests
{
    [Fact]
    public Task Converter() =>
        Verify(new { Company = new Company("Contoso", ["Ann", "Bob"]) })
            .AddExtraSettings(_ => _.Converters.Add(new CompanyConverter()));

    [Fact]
    public Task ConverterFollowsMemberRules() =>
        Verify(new Company("Contoso", ["Ann"]))
            .AddExtraSettings(_ => _.Converters.Add(new CompanyConverter()))
            .IgnoreMember("Employees");

    [Fact]
    public Task Execute() =>
        VerifyJson(WriteOnlyJsonConverter.Execute<CompanyConverter>(new Company("Contoso", ["Ann", "Bob"])));

    [Fact]
    public Task AppendValues() =>
        Verify("TheValue")
            .AppendValue("theData", "theValue")
            .AppendValue("number", 1);

    [Fact]
    public Task AppendValuesToAnObject() =>
        Verify(new { foo = "bar" })
            .AppendValue("theData", "theValue");

    [Fact]
    public Task OnlyAppendedValues() =>
        Verify()
            .AppendValue("theData", "theValue");

    public record Company(string Name, List<string> Employees);

    class CompanyConverter : WriteOnlyJsonConverter<Company>
    {
        public override void Write(VerifyJsonWriter writer, Company company)
        {
            writer.WriteMember(company, company.Name, "Name");
            writer.WriteMember(company, company.Employees.Count, "EmployeeCount");
            writer.WriteMember(company, company.Employees, "Employees");
        }
    }
}
