using EventBooking.Application.Common;

namespace EventBooking.Api.Endpoints;

/// <summary>One wire contract for one failure: its slug, status, title and data member.</summary>
/// <param name="Type">The stable slug from design 05's error catalogue.</param>
/// <param name="Status">The HTTP status design 05 gives the slug.</param>
/// <param name="Title">The short, caller-safe title.</param>
/// <param name="DataMember">The extension carrying the error's numeric map, if any.</param>
/// <param name="ScalarKey">The single key to lift out where the design gives a bare number.</param>
/// <param name="ObjectMember">A second extension carrying a renamed subset of the map, if any.</param>
/// <param name="ObjectKeys">The wire-to-data key map for the object member.</param>
public sealed record ProblemShape(
    string Type, int Status, string Title, string? DataMember = null, string? ScalarKey = null,
    string? ObjectMember = null, IReadOnlyDictionary<string, string>? ObjectKeys = null);

/// <summary>
/// Every failure the API can return, keyed by the application error code that produces it.
/// Design 05's table supplies seventeen slugs; four more close failures the table does not
/// name and the application already produces. A code with no row here is a programming
/// error — <see cref="For"/> throws rather than letting an expected failure become a 500.
/// </summary>
public static class ProblemCatalogue
{
    /// <summary>The code the rate limiter reports under; no application error produces it.</summary>
    public const string RateLimitedCode = "rate-limited";

    /// <summary>The code the authentication challenge reports under.</summary>
    public const string UnauthenticatedCode = "unauthenticated";

    /// <summary>The code the cursor and limit binders report under.</summary>
    public const string ValidationCode = "validation";

    /// <summary>Gets every catalogued failure, keyed by application error code.</summary>
    public static IReadOnlyDictionary<string, ProblemShape> ByErrorCode { get; } =
        new Dictionary<string, ProblemShape>(StringComparer.Ordinal)
        {
            // 422 — a field or row the caller can correct.
            [ValidationCode] = Validation,
            ["attendee_group_required"] = Validation,
            ["attendee_group_unknown"] = Validation,
            ["attendee_group_inactive"] = Validation,
            ["attendee_group_unmapped"] = Validation,

            // 409 — the request was well formed and the state refused it.
            [Error.VersionConflictCode] = new(
                "version-conflict", StatusCodes.Status409Conflict,
                "Someone else changed this first.", "current"),
            [Error.AppointmentVersionConflictCode] = new(
                "version-conflict", StatusCodes.Status409Conflict,
                "Someone else changed this first.", "current"),
            [Error.ConfirmationRequiredCode] = new(
                "confirmation-required", StatusCodes.Status409Conflict,
                "This action needs confirming.", "consequence"),
            [Error.CapacityExhaustedCode] = new(
                "capacity-exhausted", StatusCodes.Status409Conflict,
                "This time is no longer available."),
            [Error.CapacityBelowBookingsCode] = new(
                "capacity-below-bookings", StatusCodes.Status409Conflict,
                "That total is below the places already booked.", "minimum", "minimum",
                "current", new Dictionary<string, string>
                {
                    ["totalHeadcount"] = "currentTotal",
                    ["remainingCapacity"] = "currentRemaining",
                }),
            [Error.ProposalNotOpenCode] = new(
                "proposal-not-open", StatusCodes.Status409Conflict,
                "This proposal is no longer open."),
            [Error.WindowStartedCode] = new(
                "window-started", StatusCodes.Status409Conflict,
                "This event has already started."),
            [Error.ReferenceDataInUseCode] = new(
                "in-use", StatusCodes.Status409Conflict,
                "This is still in use.", "blocking"),
            [Error.RequirementsLockedCode] = new(
                "requirements-locked", StatusCodes.Status409Conflict,
                "Requirements cannot change while bookings are active.", "blocking"),
            [Error.AttendeeGroupActiveBookingConflictCode] = new(
                "requirements-locked", StatusCodes.Status409Conflict,
                "Requirements cannot change while bookings are active.", "blocking"),
            [Error.InsufficientEventsCode] = new(
                "insufficient-events", StatusCodes.Status409Conflict,
                "There are not enough events to offer."),
            [Error.RecoveryActiveCode] = new(
                "recovery-active", StatusCodes.Status409Conflict,
                "A recovery is already under way."),
            [Error.RecoveryAlreadyPendingCode] = new(
                "recovery-active", StatusCodes.Status409Conflict,
                "A recovery is already under way."),
            [Error.RecoveryStateChangedCode] = new(
                "recovery-active", StatusCodes.Status409Conflict,
                "A recovery is already under way."),
            [Error.LastAdminCode] = new(
                "last-admin", StatusCodes.Status409Conflict,
                "That change would leave no administrator."),

            // 409 — the four slugs design 05's table does not name (see the settlements above).
            [Error.RequirementMismatchCode] = new(
                "requirement-mismatch", StatusCodes.Status409Conflict,
                "This attendee's requirements have changed."),
            [Error.RecoveryNotAvailableCode] = new(
                "requirement-mismatch", StatusCodes.Status409Conflict,
                "This attendee's requirements have changed."),
            [Error.AttendeeRequirementSnapshotMismatchCode] = new(
                "requirement-mismatch", StatusCodes.Status409Conflict,
                "This attendee's requirements have changed."),
            [Error.AlreadyConfirmedCode] = new(
                "already-confirmed", StatusCodes.Status409Conflict,
                "This invitation has already been used."),
            ["conflict"] = new(
                "conflict", StatusCodes.Status409Conflict, "That is not possible right now."),

            // 404, 410 — the attendee token pair. Unknown, forged, superseded and cancelled
            // are one answer; only a lapsed expiry is distinguishable (settled with the user).
            [Error.TokenInvalidCode] = new(
                "token-invalid", StatusCodes.Status404NotFound, "This link is not valid."),
            ["not_found"] = new(
                "not-found", StatusCodes.Status404NotFound, "That was not found."),
            [Error.TokenExpiredCode] = new(
                "token-expired", StatusCodes.Status410Gone, "This link has expired."),

            // 403, 401, 429 — the boundary. A missing staff number is forbidden, not
            // unauthenticated: contradiction #3, settled in Phase 3.
            ["forbidden"] = new(
                "forbidden", StatusCodes.Status403Forbidden, "You cannot do that."),
            [UnauthenticatedCode] = new(
                "unauthenticated", StatusCodes.Status401Unauthorized, "Please sign in."),
            [RateLimitedCode] = new(
                "rate-limited", StatusCodes.Status429TooManyRequests, "Too many requests."),
        };

    /// <summary>Returns the catalogued shape for an application error code.</summary>
    /// <param name="errorCode">The application error code.</param>
    /// <returns>The shape to render.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the code has no row.</exception>
    public static ProblemShape For(string errorCode) =>
        ByErrorCode.TryGetValue(errorCode, out var shape)
            ? shape
            : throw new InvalidOperationException(
                $"Application error code '{errorCode}' has no problem-catalogue row. " +
                "Add one rather than letting an expected failure reach a caller as a 500.");

    private static ProblemShape Validation => new(
        "validation-failed", StatusCodes.Status422UnprocessableEntity,
        "The request could not be accepted.");
}
