// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Sends information about interactions with feed items back to the feed generator that served them.
    /// </summary>
    /// <param name="interactions">The <see cref="Interaction"/>s to send.</param>
    /// <param name="feed">The optional <see cref="AtUri"/> of the feed generator record for the feed the interactions occurred in.</param>
    /// <param name="feedGenerator">
    /// The optional <see cref="Did"/> of the feed generator service which served the items. When <see langword="null"/>
    /// the interactions are sent to the Bluesky AppView.
    /// </param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="interactions"/> or any interaction is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when an interaction has an <see cref="InteractionEvent.Unknown"/> event or an <see cref="InteractionViewContext.Unknown"/> view context,
    /// or a <see cref="Interaction.FeedContext"/> or <see cref="Interaction.RequestId"/> longer than the lexicon allows.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The request is sent to the agent's PDS, which proxies it to the <c>#bsky_fg</c> service of <paramref name="feedGenerator"/>,
    /// or to the Bluesky AppView when no feed generator is specified.
    /// </para>
    /// </remarks>
    public async Task<AtProtoHttpResult<EmptyResponse>> SendInteractions(
        IEnumerable<Interaction> interactions,
        AtUri? feed = null,
        Did? feedGenerator = null,
        CancellationToken cancellationToken = default)
    {
        List<Interaction> interactionList = BlueskyServer.ValidateInteractions(interactions);

        return await BlueskyServer.SendInteractions(
            interactionList,
            feed,
            service: Service,
            serviceProxy: feedGenerator is null ? BlueskyServer.AppViewProxy : FeedGeneratorServiceProxy(feedGenerator),
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
