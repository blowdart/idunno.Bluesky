// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Graph.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Gets a basic view of the specified starter packs.
    /// </summary>
    /// <param name="uris">A collection of <see cref="AtUri"/>s for the starter packs to view.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the starter packs from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uris"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when number <paramref name="uris"/> is &lt; 1 or &gt; 25.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<IReadOnlyList<StarterPackViewBasic>>> GetStarterPacks(
        ICollection<AtUri> uris,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uris);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        ArgumentOutOfRangeException.ThrowIfLessThan(uris.Count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(uris.Count, 25);

        StringBuilder queryStringBuilder = new();
        foreach (AtUri uri in uris)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"uris={Uri.EscapeDataString(uri.ToString())}&");
        }
        queryStringBuilder.Length--;

        string queryString = queryStringBuilder.ToString();

        BlueskyHttpClient<GetStarterPacksResponse> client = new(AppViewProxy, loggerFactory);
        AtProtoHttpResult<GetStarterPacksResponse> response = await client.Get(
            service,
            $"/xrpc/app.bsky.graph.getStarterPacks?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<IReadOnlyList<StarterPackViewBasic>>(
                new List<StarterPackViewBasic>(response.Result.StarterPacks).AsReadOnly(),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<IReadOnlyList<StarterPackViewBasic>>(
                new List<StarterPackViewBasic>().AsReadOnly(),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}
