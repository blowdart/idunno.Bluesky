// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Text.Json;

using idunno.Bluesky.BotSdk.DataProviders;

namespace idunno.Bluesky.BotSdk;

// Apply limits at the HTTP boundary rather than counting successful replies. SDK helpers can
// make several requests (discovery, reads, authentication, or refresh) for one logical operation.
/// <summary>Reserves durable HTTP budgets and persists server cooldowns before permitting further requests.</summary>
/// <param name="database">The provider storing rolling budgets and cooldowns.</param>
/// <param name="options">The runtime mode controlling whether mutations are permitted.</param>
/// <param name="time">The clock used for pacing deadlines.</param>
/// <param name="health">The deadline tracker for actual outbound waits.</param>
/// <param name="logger">The HTTP outcome logger, which never logs request credentials.</param>
internal sealed class OutboundHandler(
    IDatabaseProvider database,
    BotOptions options,
    TimeProvider time,
    WorkerHealth health,
    ILogger<OutboundHandler> logger) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string operation = request.RequestUri?.AbsolutePath.Split('/')[^1]
            ?? throw new InvalidOperationException("An outbound request has no URI.");

        if (options.DeveloperMode && request.Method != HttpMethod.Get)
        {
            throw new InvalidOperationException("Developer mode prohibits outbound mutation requests.");
        }

        int writePoints = await WritePointsAsync(operation, request, cancellationToken).ConfigureAwait(false);
        while (await database.ReserveRequestAsync(
            writePoints,
            operation == "com.atproto.server.createSession",
            cancellationToken).ConfigureAwait(false) is DateTimeOffset until)
        {
            TimeSpan delay = until - time.GetUtcNow();

            if (delay > TimeSpan.FromSeconds(10))
            {
                // Long waits belong to the sender's durable scheduler, not a request's HTTP timeout.
                throw new RateWaitException(until);
            }

            health.Set("sender", "outbound pacing", TimeSpan.FromSeconds(45));
            await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }

        // Reserve before touching the network: timeouts and failed writes still consume the durable budget.
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        DateTimeOffset? cooldown = Cooldown(response, time.GetUtcNow());

        try
        {
            if (cooldown is DateTimeOffset reset)
            {
                await database.CooldownAsync(reset, cancellationToken).ConfigureAwait(false);
            }

            BotLog.HttpResult(logger, operation, (int)response.StatusCode, cooldown);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RateWaitException(cooldown ?? time.GetUtcNow().AddMinutes(5));
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                throw new HttpRequestException($"Transient HTTP {(int)response.StatusCode} from {operation}.", null, response.StatusCode);
            }

            return response;
        }
        catch
        {
            // We own this response until it is returned. Release it on every exceptional path
            // while preserving the original error for the worker's recovery decision.
            response.Dispose();
            throw;
        }
    }

    /// <summary>Counts repository write costs before admitting a request to the network.</summary>
    /// <param name="operation">The XRPC endpoint name.</param>
    /// <param name="request">The request containing any batch operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The conservative write-point cost.</returns>
    private static async Task<int> WritePointsAsync(
        string operation,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (operation != "com.atproto.repo.applyWrites")
        {
            return operation switch
            {
                // putRecord is an upsert: reserve the create cost even when updating.
                "com.atproto.repo.createRecord" or "com.atproto.repo.putRecord" => 3,
                "com.atproto.repo.deleteRecord" => 1,
                _ => 0
            };
        }

        HttpContent content = request.Content
            ?? throw new InvalidOperationException("applyWrites has no request body.");
        // Buffer through HttpContent so inspecting the body does not consume the outgoing stream.
        using JsonDocument body = JsonDocument.Parse(
            await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        int points = 0;
        foreach (JsonElement write in body.RootElement.GetProperty("writes").EnumerateArray())
        {
            points = checked(points + (write.GetProperty("$type").GetString() switch
            {
                "com.atproto.repo.applyWrites#create" => 3,
                "com.atproto.repo.applyWrites#update" => 2,
                "com.atproto.repo.applyWrites#delete" => 1,
                _ => throw new InvalidOperationException("Cannot account for an unknown applyWrites operation.")
            }));
        }

        return points;
    }

    /// <summary>Combines Retry-After and server reset advice without shortening an existing wait.</summary>
    /// <param name="response">The server response containing rate-limit headers.</param>
    /// <param name="now">The current UTC time.</param>
    /// <returns>The future cooldown deadline, or <see langword="null"/> when no wait is indicated.</returns>
    internal static DateTimeOffset? Cooldown(HttpResponseMessage response, DateTimeOffset now)
    {
        DateTimeOffset? cooldown = response.Headers.RetryAfter?.Date;

        if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
        {
            cooldown = now + delta;
        }

        // Honour server advice even before hitting 429, leaving a small safety margin.
        // Different requests may suggest different deadlines; the later one wins.
        if (response.Headers.TryGetValues("ratelimit-remaining", out IEnumerable<string>? remainingValues) &&
            int.TryParse(remainingValues.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int remaining) &&
            remaining < 10 && response.Headers.TryGetValues("ratelimit-reset", out IEnumerable<string>? resetValues) &&
            long.TryParse(resetValues.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds) &&
            seconds is >= 0 and <= 253402300799)
        {
            DateTimeOffset reset = DateTimeOffset.FromUnixTimeSeconds(seconds);
            cooldown = cooldown is null || reset > cooldown ? reset : cooldown;
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests && (cooldown is null || cooldown <= now))
        {
            // A 429 without a usable deadline still needs backoff; do not hammer the server.
            cooldown = now.AddMinutes(5);
        }

        return cooldown > now ? cooldown : null;
    }
}
