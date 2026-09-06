// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Unspecced.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Get a <see cref="PagedReadOnlyCollection{T}"/> of <see cref="ProfileView"/>s of suggested actors.
    /// </summary>
    /// <param name="category">An optional category of users to get suggestions for.</param>
    /// <param name="limit">The number of topics to return. Must be between 1 and 50.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient" /> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt;50.</exception>
    [UnconditionalSuppressMessage("Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
    public static async Task<AtProtoHttpResult<ICollection<ProfileView>>> GetSuggestedUsers(
        string? category,
        int? limit,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, Maximum.SuggestedUsers);
        }

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        string queryString = string.Empty;

        if (!string.IsNullOrEmpty(category))
        {
            queryString += $"category={Uri.EscapeDataString(category)}";
        }

        if (limit is not null)
        {
            queryString += $"&limit={limit}";
        }

        queryString = queryString.TrimStart('&');

        BlueskyHttpClient<GetSuggestedUsersResponse> request = new(AppViewProxy, loggerFactory);

        AtProtoHttpResult<GetSuggestedUsersResponse> response = await request.Get(
            service,
            $"/xrpc/app.bsky.unspecced.getSuggestedUsers?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<ICollection<ProfileView>>(
                response.Result.Actors,
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<ICollection<ProfileView>>(
                null,
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
    }
}