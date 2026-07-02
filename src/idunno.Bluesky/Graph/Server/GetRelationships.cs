// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Graph;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    /// <summary>
    /// Enumerates public relationships between one account, and a list of other accounts.
    /// </summary>
    /// <param name="actor">Primary account requesting relationships for.</param>
    /// <param name="others">List of 'other' accounts to be related back to the primary.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to get relationships from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="others"/>, <paramref name="service"/>, <paramref name="httpClient"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="others"/> is empty or contains more than 30 identifers.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<RelationshipMap>> GetRelationships(
        AtIdentifier actor,
        ICollection<AtIdentifier> others,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(others);
        if (others.Count == 0)
        {
            throw new ArgumentException("The collection of others must not be empty.", nameof(others));
        }
        else if (others.Count > 30)
        {
            throw new ArgumentException("The collection of others must not contain more than 30 AtIdentifiers.", nameof(others));
        }
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);


        StringBuilder queryStringBuilder = new();
        queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor.ToString())}");

        // The GetRelationships documentation is wrong, and states that others can be an AtIdentifier. However in reality they must be DIDs, so, if needed convert to DIDs.
        // See https://github.com/bluesky-social/atproto/issues/2919
        foreach (AtIdentifier other in others)
        {
            if (other is Handle handle)
            {
                Did? did = await AtProtoServer.ResolveHandle(handle, httpClient, loggerFactory, cancellationToken).ConfigureAwait(false);
                if (did is not null)
                {
                    queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&others={Uri.EscapeDataString(did.ToString())}");
                }
                else
                {
                    // We keep an unmappable handle in the list, as the API will return a relationship for it, but it will be <see cref="ActorNotFound"/>.
                    queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&others={Uri.EscapeDataString(handle.ToString())}");
                }
            }
            else if (other is Did did)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&others={Uri.EscapeDataString(did.ToString())}");
            }
            else
            {
                throw new ArgumentException($"The collection of others must only contain Handles or DIDs. Found: {other.GetType().Name}", nameof(others));
            }
        }

        string queryString = queryStringBuilder.ToString();

        BlueskyHttpClient<RelationshipMap> client = new(AppViewProxy, loggerFactory);

        return await client.Get(
            service: service,
            endpoint: $"/xrpc/app.bsky.graph.getRelationships?{queryString}",
            requestHeaders: null,
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}