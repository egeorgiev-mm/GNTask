namespace RoomBooking.Api.Infrastructure;

public static class WeakETag
{
    public static string Format(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return $"W/\"{token}\"";
    }

    public static bool Matches(string? ifNoneMatchHeader, string token)
    {
        if (string.IsNullOrWhiteSpace(ifNoneMatchHeader))
        {
            return false;
        }

        foreach (var rawItem in ifNoneMatchHeader.Split(','))
        {
            var item = rawItem.Trim();
            if (item.Length == 0)
            {
                continue;
            }

            if (item == "*")
            {
                return true;
            }

            if (!TryExtractOpaqueTag(item, out var candidateToken))
            {
                continue;
            }

            if (string.Equals(candidateToken, token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractOpaqueTag(string value, out string token)
    {
        token = string.Empty;
        var span = value.AsSpan().Trim();

        if (span.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            span = span[2..].TrimStart();
        }

        if (span.Length < 2 || span[0] != '"' || span[^1] != '"')
        {
            return false;
        }

        var inner = span[1..^1];
        if (inner.Contains('"'))
        {
            return false;
        }

        token = inner.ToString();
        return token.Length > 0;
    }
}
