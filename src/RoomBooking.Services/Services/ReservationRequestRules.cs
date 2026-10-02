using RoomBooking.Services.Contracts;

namespace RoomBooking.Services.Services;

internal static class ReservationRequestRules
{
    internal const int MaxLimit = 100;

    public static bool TryNormalizeRoomsRange(
        DateTimeOffset? start,
        DateTimeOffset? end,
        out DateTimeOffset? normalizedStartUtc,
        out DateTimeOffset? normalizedEndUtc,
        out ValidationFailure? failure)
    {
        normalizedStartUtc = start?.ToUniversalTime();
        normalizedEndUtc = end?.ToUniversalTime();

        if (normalizedStartUtc.HasValue != normalizedEndUtc.HasValue)
        {
            failure = CreateValidationFailure(
                "invalid_time_range",
                "Both start and end must be provided together.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["start"] = new[] { "Provide both start and end or neither." },
                    ["end"] = new[] { "Provide both start and end or neither." }
                });

            return false;
        }

        if (normalizedStartUtc.HasValue && normalizedEndUtc.HasValue && normalizedStartUtc.Value >= normalizedEndUtc.Value)
        {
            failure = CreateValidationFailure(
                "invalid_time_range",
                "Start must be before end.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["start"] = new[] { "Start must be before end." },
                    ["end"] = new[] { "End must be after start." }
                });

            return false;
        }

        failure = null;
        return true;
    }

    public static bool TryNormalizeReservationQuery(
        Guid roomId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int limit,
        int offset,
        out ReservationQuery query,
        out ValidationFailure? failure)
    {
        var normalizedStartUtc = start?.ToUniversalTime();
        var normalizedEndUtc = end?.ToUniversalTime();

        if (normalizedStartUtc.HasValue && normalizedEndUtc.HasValue && normalizedStartUtc.Value >= normalizedEndUtc.Value)
        {
            query = default!;
            failure = CreateValidationFailure(
                "invalid_time_range",
                "Start must be before end.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["start"] = new[] { "Start must be before end." },
                    ["end"] = new[] { "End must be after start." }
                });

            return false;
        }

        if (offset < 0)
        {
            query = default!;
            failure = CreateValidationFailure(
                "invalid_pagination",
                "Offset must be zero or greater.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["offset"] = new[] { "Offset must be zero or greater." }
                });

            return false;
        }

        if (limit < 1)
        {
            query = default!;
            failure = CreateValidationFailure(
                "invalid_pagination",
                "Limit must be at least 1.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["limit"] = new[] { "Limit must be at least 1." }
                });

            return false;
        }

        var normalizedLimit = Math.Min(limit, MaxLimit);

        query = new ReservationQuery(
            roomId,
            normalizedStartUtc,
            normalizedEndUtc,
            normalizedLimit,
            offset);

        failure = null;
        return true;
    }

    public static bool TryNormalizeCreateReservation(
        Guid roomId,
        DateTimeOffset start,
        DateTimeOffset end,
        string title,
        DateTimeOffset createdAtUtc,
        out CreateReservationCommand command,
        out ValidationFailure? failure)
    {
        var normalizedStartUtc = start.ToUniversalTime();
        var normalizedEndUtc = end.ToUniversalTime();

        if (normalizedStartUtc >= normalizedEndUtc)
        {
            command = default!;
            failure = CreateValidationFailure(
                "invalid_time_range",
                "Start must be before end.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["start"] = new[] { "Start must be before end." },
                    ["end"] = new[] { "End must be after start." }
                });

            return false;
        }

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length == 0)
        {
            command = default!;
            failure = CreateValidationFailure(
                "title_required",
                "Title is required.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["title"] = new[] { "Title is required." }
                });

            return false;
        }

        if (trimmedTitle.Length > 200)
        {
            command = default!;
            failure = CreateValidationFailure(
                "title_too_long",
                "Title cannot exceed 200 characters.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["title"] = new[] { "Title cannot exceed 200 characters." }
                });

            return false;
        }

        command = new CreateReservationCommand(
            roomId,
            normalizedStartUtc,
            normalizedEndUtc,
            trimmedTitle,
            createdAtUtc.ToUniversalTime());

        failure = null;
        return true;
    }

    private static ValidationFailure CreateValidationFailure(
        string code,
        string message,
        IReadOnlyDictionary<string, IReadOnlyList<string>> errorsByField)
    {
        return new ValidationFailure(code, message, errorsByField);
    }
}