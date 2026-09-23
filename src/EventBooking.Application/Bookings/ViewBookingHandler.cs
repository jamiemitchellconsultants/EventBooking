using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Bookings;

/// <summary>Defines booking view for the current use case.</summary>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Display">The display.</param>
/// <param name="CandidateName">The candidate name.</param>
public sealed record BookingView(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display,
    string CandidateName);

/// <summary>Defines view booking query for the current use case.</summary>
/// <param name="ManageToken">The manage token.</param>
public sealed record ViewBookingQuery(string? ManageToken);

/// <summary>Defines view booking handler for the current use case.</summary>
/// <param name="bookings">The bookings.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="slots">The slots.</param>
/// <param name="tokens">The tokens.</param>
public sealed class ViewBookingHandler(
    IBookingRepository bookings,
    ICandidateRepository candidates,
    IConfirmedSlotRepository slots,
    ITokenService tokens)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<BookingView>> HandleAsync(
        ViewBookingQuery query,
        CancellationToken cancellationToken)
    {
        if (query.ManageToken is null || !tokens.TryRead(query.ManageToken, out _))
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var booking = await bookings.GetByManageTokenHashAsync(
            tokens.Hash(query.ManageToken), cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Active)
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        var slot = await slots.GetAsync(booking.ConfirmedSlotId, cancellationToken);
        var candidate = await candidates.GetAsync(booking.CandidateId, cancellationToken);

        if (slot is null || candidate is null)
        {
            return Result<BookingView>.Failure(Error.NotFound(ViewInviteHandler.InvalidLinkMessage));
        }

        return Result<BookingView>.Success(new BookingView(
            slot.Window.Date,
            slot.Window.StartTime,
            slot.Window.EndTime,
            CandidateEmailComposer.FormatWindow(slot.Window),
            candidate.Name));
    }
}
