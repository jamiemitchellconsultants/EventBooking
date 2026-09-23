using System.Reflection;
using Xunit;
using System.Xml.Linq;

namespace EventBooking.Api.Tests;

public sealed class PortArchitectureTests
{
    [Fact]
    public void Api_dependency_graph_has_no_retired_provider()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Visit(Assembly.Load("EventBooking.Api"), seen);
        Assert.DoesNotContain(seen, IsRetired);
    }

    [Fact]
    public void Source_projects_have_no_retired_provider_reference()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var references = Directory.EnumerateFiles(Path.Combine(directory.FullName, "src"), "*.csproj", SearchOption.AllDirectories)
            .SelectMany(path => XDocument.Load(path).Descendants())
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty);
        Assert.DoesNotContain(references, IsRetired);
    }

    private static bool IsRetired(string name) =>
        name.Contains("Aws", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Amazon.Lambda", StringComparison.OrdinalIgnoreCase)
        || name.Contains("EntraId", StringComparison.OrdinalIgnoreCase);

    private static void Visit(Assembly assembly, HashSet<string> seen)
    {
        if (!seen.Add(assembly.GetName().Name!)) return;
        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            if (IsRetired(reference.Name!)) seen.Add(reference.Name!);
            else if (reference.Name!.StartsWith("EventBooking", StringComparison.Ordinal))
                Visit(Assembly.Load(reference), seen);
        }
    }
}
