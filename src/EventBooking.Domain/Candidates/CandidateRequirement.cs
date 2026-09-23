namespace EventBooking.Domain.Candidates;

/// <summary>One appointment type a given candidate has to attend.</summary>
public sealed class CandidateRequirement
{
    private CandidateRequirement()
    {
    }

    /// <summary>Defines candidate id for the current use case.</summary>
    public Guid CandidateId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static CandidateRequirement For(Guid candidateId, Guid appointmentTypeId) =>
        new() { CandidateId = candidateId, AppointmentTypeId = appointmentTypeId };
}
