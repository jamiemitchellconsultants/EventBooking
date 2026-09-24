using System.Net;
using System.Text;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Admin;

internal static class AdminPageFixture
{
    internal static void Register(IServiceCollection services, params HttpResponseMessage[] responses)
    {
        services.AddSingleton(new AdminClient(new HttpClient(new QueueHandler(responses))
            { BaseAddress = new Uri("https://api.example") }));
        RegisterContext(services, "createLocation", "createAppointmentType", "createAttendeeGroup");
    }

    internal static void RegisterReadOnly(
        IServiceCollection services, params HttpResponseMessage[] responses)
    {
        services.AddSingleton(new AdminClient(new HttpClient(new QueueHandler(responses))
            { BaseAddress = new Uri("https://api.example") }));
        RegisterContext(services);
    }

    internal static void RegisterContext(IServiceCollection services, params string[] relations) =>
        services.AddSingleton<IMeClient>(new FakeMeClient(relations));

    internal static void RegisterStaff(
        IServiceCollection services, params HttpResponseMessage[] responses) =>
        services.AddSingleton(new StaffAccessClient(new HttpClient(new QueueHandler(responses))
            { BaseAddress = new Uri("https://api.example") }));

    internal static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8,
            (int)status >= 400 ? "application/problem+json" : "application/json"),
    };

    private sealed class QueueHandler(IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) => Task.FromResult(_responses.Dequeue());
    }

    private sealed class FakeMeClient(IEnumerable<string> relations) : IMeClient
    {
        private readonly IReadOnlyDictionary<string, ApiLink> _links = relations.ToDictionary(
            relation => relation,
            relation => new ApiLink("/test", "POST", relation));

        public Task<ApiOutcome<MeDto>> GetAsync(CancellationToken ct) => Task.FromResult(
            ApiOutcome<MeDto>.Success(new(
                "Admin User", "A1", ["Admin"], null, null, null,
                ["ManageReferenceData"], null, _links)));
    }
}
