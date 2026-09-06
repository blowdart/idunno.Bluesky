// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Actor.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Find actors (profiles) matching search criteria. Does not require authentication.
    /// </summary>
    /// <param name="q">The search query string. Syntax, phrase, Boolean, and faceting is unspecified, but Lucene query syntax is recommended.</param>
    /// <param name="limit">The number of suggested actors to return. Defaults to 50 if <see langword="null"/>.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="q"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt;=0 or &gt;100.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> SearchActors(
        string q,
        int? limit,
        string? cursor,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(q);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        int limitValue = limit ?? 25;

        ArgumentOutOfRangeException.ThrowIfNegative(limitValue);
        ArgumentOutOfRangeException.ThrowIfZero(limitValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limitValue, 100);

        BlueskyHttpClient<SearchActorsResponse> request = new(AppViewProxy, loggerFactory);
        AtProtoHttpResult<SearchActorsResponse> response = await request.Get(
            service,
            $"/xrpc/app.bsky.actor.searchActors?q={Uri.EscapeDataString(q)}&limit={limit}&cursor={cursor}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                new PagedViewReadOnlyCollection<ProfileView>(new List<ProfileView>(response.Result.Actors).AsReadOnly(), response.Result.Cursor),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                new PagedViewReadOnlyCollection<ProfileView>(),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}
