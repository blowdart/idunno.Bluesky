// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Requests that a relay persistently crawls the repositories hosted by the specified host.
    /// </summary>
    /// <param name="hostname">The hostname of the service, such as a personal data server, that is requesting to be crawled.</param>
    /// <param name="service">The relay to send the crawl request to. Defaults to the agent's <see cref="Service"/> if <see langword="null"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hostname"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hostname"/> is empty or white space.</exception>
    /// <remarks>
    /// <para>
    /// This endpoint is only implemented by relays. Personal data servers do not implement it and will return an error,
    /// so <paramref name="service"/> should be a relay.
    /// </para>
    /// <para>The service may return a <c>HostBanned</c> error.</para>
    /// <para>If the agent is authenticated and <paramref name="service"/> is the service the agent is authenticated to, the agent's access credentials are sent with the request.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<EmptyResponse>> RequestCrawl(
        string hostname,
        Uri? service = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        service ??= Service;

        return await AtProtoServer.RequestCrawl(
            hostname,
            service,
            HttpClient,
            GetSyncCredentials(service),
            InternalOnCredentialsUpdatedCallBack,
            LoggerFactory,
            MaximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
