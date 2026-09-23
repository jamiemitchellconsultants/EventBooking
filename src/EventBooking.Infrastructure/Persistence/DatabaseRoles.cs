using System.Reflection;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// The role script that has to run before migrations, because the initial migration grants to a
/// role it does not create. Shipped as an embedded resource so a deployment cannot apply a copy
/// that has drifted from the schema it guards.
/// </summary>
public static class DatabaseRoles
{
    private const string ResourceName = "EventBooking.Infrastructure.Persistence.Sql.roles.sql";

    private static readonly Lazy<string> Contents = new(() =>
    {
        using var stream = typeof(DatabaseRoles).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <summary>Gets the role script, verbatim.</summary>
    public static string Script => Contents.Value;
}
