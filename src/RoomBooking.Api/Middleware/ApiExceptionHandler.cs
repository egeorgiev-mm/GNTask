using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using RoomBooking.Api.Infrastructure;

namespace RoomBooking.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request aborted by client for {Path}", httpContext.Request.Path);
            return true;
        }

        if (exception is BadHttpRequestException badRequestException)
        {
            logger.LogWarning(badRequestException, "Bad request while processing {Path}", httpContext.Request.Path);

            var parameterName = TryGetParameterName(badRequestException.Message);
            var detail = parameterName is null
                ? "The request could not be parsed."
                : $"The request could not be parsed for parameter '{parameterName}'.";

            var result = TypedResults.Problem(
                detail: detail,
                statusCode: badRequestException.StatusCode,
                title: "Bad Request",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "bad_request"
                });

            await result.ExecuteAsync(httpContext);
            return true;
        }

        logger.LogError(exception, "Unhandled exception while processing request {Path}", httpContext.Request.Path);

        var unexpected = ProblemDetailsMapping.UnexpectedError();
        await unexpected.ExecuteAsync(httpContext);
        return true;
    }

    private static string? TryGetParameterName(string message)
    {
        var match = Regex.Match(
            message,
            "parameter [\"'](?<name>[^\"']+)[\"']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (match.Success)
        {
            var raw = match.Groups["name"].Value;
            var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length == 0 ? raw : tokens[^1];
        }

        return null;
    }
}
