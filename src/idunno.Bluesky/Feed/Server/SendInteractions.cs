// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Sends information about interactions with feed items back to the feed generator that served them.
    /// </summary>
    /// <param name="interactions">The <see cref="Interaction"/>s to send.</param>
    /// <param name="feed">The optional <see cref="AtUri"/> of the feed generator record for the feed the interactions occurred in.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to send the interactions to.</param>
    /// <param name="serviceProxy">
    /// The service the PDS should proxy the request to, typically <c>{feed generator DID}#bsky_fg</c> for the feed generator which served the items.
    /// </param>
    /// <param name="accessCredentials">Optional <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Func{T1, T2, TResult}" /> to await if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="interactions"/>, any interaction, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when an interaction has an <see cref="InteractionEvent.Unknown"/> event or an <see cref="InteractionViewContext.Unknown"/> view context,
    /// or a <see cref="Interaction.FeedContext"/> or <see cref="Interaction.RequestId"/> longer than the lexicon allows.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This overload uses the caller-supplied <see cref="HttpClient"/> to contact <paramref name="service"/>. If that URI can be
    /// influenced by untrusted input, the client must enforce SSRF protection at connection time: resolve the destination and block
    /// loopback, private, link-local and other non-public IPv4 and IPv6 addresses, and disable redirects or apply the same checks to
    /// every redirect target. URI-only validation does not prevent DNS rebinding.
    /// </para>
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<EmptyResponse>> SendInteractions(
        IEnumerable<Interaction> interactions,
        AtUri? feed,
        Uri service,
        string serviceProxy,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        List<Interaction> interactionList = ValidateInteractions(interactions);

        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrEmpty(serviceProxy);
        ArgumentNullException.ThrowIfNull(httpClient);

        BlueskyHttpClient<EmptyResponse> client = new(serviceProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };

        return await client.Post(
            service,
            "/xrpc/app.bsky.feed.sendInteractions",
            new SendInteractionsRequest(interactionList, feed),
            requestHeaders: null,
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates interactions before they are sent, so that a value which cannot be serialized, or which the lexicon forbids,
    /// is reported as an argument error rather than a serialization or service failure.
    /// </summary>
    /// <param name="interactions">The interactions to validate.</param>
    /// <returns>A materialized list of <paramref name="interactions"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="interactions"/> or any interaction is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an interaction contains an unknown enumeration value or an over-length string.</exception>
    internal static List<Interaction> ValidateInteractions(IEnumerable<Interaction> interactions)
    {
        ArgumentNullException.ThrowIfNull(interactions);

        List<Interaction> interactionList = [.. interactions];

        foreach (Interaction interaction in interactionList)
        {
            if (interaction is null)
            {
                throw new ArgumentNullException(nameof(interactions), "An interaction cannot be null.");
            }

            if (interaction.Event == InteractionEvent.Unknown)
            {
                throw new ArgumentException($"An interaction with an event of {nameof(InteractionEvent)}.{nameof(InteractionEvent.Unknown)} cannot be sent.", nameof(interactions));
            }

            if (interaction.ViewContext == InteractionViewContext.Unknown)
            {
                throw new ArgumentException($"An interaction with a view context of {nameof(InteractionViewContext)}.{nameof(InteractionViewContext.Unknown)} cannot be sent.", nameof(interactions));
            }

            if (interaction.FeedContext is not null && Encoding.UTF8.GetByteCount(interaction.FeedContext) > Interaction.MaximumFeedContextLength)
            {
                throw new ArgumentException($"An interaction's {nameof(Interaction.FeedContext)} cannot be longer than {Interaction.MaximumFeedContextLength} UTF-8 bytes.", nameof(interactions));
            }

            if (interaction.RequestId is not null && Encoding.UTF8.GetByteCount(interaction.RequestId) > Interaction.MaximumRequestIdLength)
            {
                throw new ArgumentException($"An interaction's {nameof(Interaction.RequestId)} cannot be longer than {Interaction.MaximumRequestIdLength} UTF-8 bytes.", nameof(interactions));
            }
        }

        return interactionList;
    }
}
