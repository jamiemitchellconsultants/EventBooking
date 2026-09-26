using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Markdig;
using System.Security.Claims;

namespace EventBooking.Web.Pages;

public partial class Help
{
    [Inject] private IUserGuideCatalog Catalog { get; set; } = default!;
    [Inject] private AuthenticationStateProvider Auth { get; set; } = default!;
    [Inject] private IMeClient Me { get; set; } = default!;

    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private readonly List<GuideSection> _sections = [];
    private bool _authenticated;
    private bool _loading = true;
    private string? _error;

    private sealed record GuideSection(string AnchorId, string Title, string Html);

    private static string SectionHref(GuideSection section) => $"#{section.AnchorId}";

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;
        _sections.Clear();

        try
        {
            var state = await Auth.GetAuthenticationStateAsync();
            var user = state.User;
            _authenticated = user.Identity?.IsAuthenticated == true;
            var roles = user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();
            if (_authenticated)
            {
                // The identity provider's token carries no role claims; the API's /api/me is the source.
                var me = await Me.GetAsync(CancellationToken.None);
                if (me.IsSuccess && me.Value is not null) roles.AddRange(me.Value.Roles);
            }
            var guides = await Catalog.ForAsync(_authenticated, roles, CancellationToken.None);
            foreach (var guide in guides)
            {
                var markdown = await Catalog.ReadMarkdownAsync(guide, CancellationToken.None);
                _sections.Add(new GuideSection(
                    $"guide-{guide.RoleKey.ToLowerInvariant()}",
                    guide.Title,
                    Markdig.Markdown.ToHtml(markdown, Pipeline)));
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _loading = false;
        }
    }
}
