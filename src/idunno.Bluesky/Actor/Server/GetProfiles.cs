// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
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
    /// Get a detailed profile view of accounts. Does not require authentication,
    /// but if an access token is provided will contain relevant metadata about the requesting
    /// actor's relationship to the requested account.
    /// </summary>
    /// <param name="actors">The handle or DID of the accounts to fetch profiles for.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="actors"/>, <paramref name="service"/> or <paramref name="httpClient" /> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="actors"/> is an empty collection or if it contains &gt;25 handles.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<IReadOnlyCollection<ProfileViewDetailed>>> GetProfiles(
        IEnumerable<AtIdentifier> actors,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        var actorList = new List<AtIdentifier>(actors);

        if (actorList.Count == 0 || actorList.Count > 25)
        {
            ArgumentOutOfRangeException.ThrowIfZero(actorList.Count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(actorList.Count, 25);
        }

        string queryString = string.Join("&", actorList.Select(uri => $"actors={Uri.EscapeDataString(uri.ToString())}"));

        BlueskyHttpClient<GetProfilesResponse> request = new(AppViewProxy, loggerFactory);
        AtProtoHttpResult<GetProfilesResponse> response = await request.Get(
            service,
            $"/xrpc/app.bsky.actor.getProfiles?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<IReadOnlyCollection<ProfileViewDetailed>>(
                new Collection<ProfileViewDetailed>(response.Result.Profiles).AsReadOnly(),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<IReadOnlyCollection<ProfileViewDetailed>>(
                new Collection<ProfileViewDetailed>().AsReadOnly(),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}
