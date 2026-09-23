using System.Reflection;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Attendees;

/// <summary>Prevents public factories from reintroducing unsourced Attendee requirements.</summary>
public sealed class RequirementOverrideSurfaceTests
{
    /// <summary>Attendee has no public factory accepting raw requirement identifiers.</summary>
    [Fact]
    public void AttendeeHasNoRawRequirementFactory()
    {
        var raw = typeof(Attendee).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "Create")
            .SelectMany(method => method.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(IEnumerable<Guid>));

        Assert.False(raw);
    }

    /// <summary>Invite has only named initial and recovery factories.</summary>
    [Fact]
    public void InviteHasNoUnsnapshottedCreateFactory()
    {
        Assert.DoesNotContain(
            typeof(Invite).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == "Create");
        Assert.Contains(typeof(Invite).GetMethods(), method => method.Name == "CreateInitial");
        Assert.Contains(typeof(Invite).GetMethods(), method => method.Name == "CreateRecovery");
    }
}
