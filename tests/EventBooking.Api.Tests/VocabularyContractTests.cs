using System.Reflection;

namespace EventBooking.Api.Tests;

public sealed class VocabularyContractTests
{
    [Fact]
    public void Public_domain_and_application_contracts_use_canonical_names()
    {
        string[] forbidden = ["Candi" + "date", "Slo" + "t", "Employee" + "Group", "Head" + "Office"];
        var names = new[] { "EventBooking.Domain", "EventBooking.Application" }
            .Select(Assembly.Load)
            .SelectMany(assembly => assembly.GetExportedTypes())
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(member => type.FullName + "." + member.Name).Append(type.FullName!));
        Assert.DoesNotContain(names, name => forbidden.Any(term => name.Contains(term, StringComparison.Ordinal)));
    }
}
