namespace EventBooking.Api.Contracts;

/// <summary>One candidate affordance and the capability that unlocks it.</summary>
/// <param name="Rel">The stable relation name.</param>
/// <param name="OperationId">The OpenAPI operation id.</param>
/// <param name="Href">The root-relative URI.</param>
/// <param name="RequiredCapability">The capability needed, or null when unconditional.</param>
public sealed record LinkCandidate(
    string Rel, string OperationId, string Href, string? RequiredCapability);

/// <summary>
/// Builds the `_links` map a representation carries. Design 05: each representation carries
/// links to the actions the caller is currently permitted to take, which is what lets the Web
/// front end enable or disable a control without restating the authorization matrix. The
/// capabilities come from the caller's access profile, which the request has already
/// resolved — this function decides nothing, it only filters.
/// </summary>
public static class CallerLinks
{
    /// <summary>Returns the candidates the caller's capabilities permit.</summary>
    /// <param name="capabilities">The capability names the caller currently holds.</param>
    /// <param name="candidates">Every affordance the representation could carry.</param>
    /// <returns>The permitted affordances, keyed by relation name.</returns>
    public static IReadOnlyDictionary<string, ApiLink> For(
        IReadOnlySet<string> capabilities, params LinkCandidate[] candidates)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(candidates);

        var links = new Dictionary<string, ApiLink>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (candidate.RequiredCapability is null ||
                capabilities.Contains(candidate.RequiredCapability))
            {
                var operation = OpenApi.AgentOperationCatalog.Get(candidate.OperationId);
                links[candidate.Rel] = new ApiLink(
                    candidate.Href, operation.Method, candidate.OperationId);
            }
        }

        return links;
    }
}
