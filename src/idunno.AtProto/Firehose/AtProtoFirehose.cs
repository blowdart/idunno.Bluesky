// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Reads the AT Protocol event streams, <c>com.atproto.sync.subscribeRepos</c> from a relay or PDS and
/// <c>com.atproto.label.subscribeLabels</c> from a labeler.
/// </summary>
/// <remarks>
/// <para>Each stream is read with <c>await foreach</c>. Frames are decoded directly from DAG-CBOR into typed events, and the
/// reader is hardened against a malicious server: frame, CAR, operation and label counts are capped, content identifiers are
/// recomputed, sequence numbers must strictly increase, and a stalled connection is dropped after <see cref="FirehoseOptions.IdleTimeout"/>.</para>
/// <para>Invalid framing, invalid DAG-CBOR and sequence violations end the enumeration with an <see cref="InvalidDataException"/>.
/// Events which are well formed but fail validation are surfaced as <see cref="FirehoseInvalidEvent"/> and the stream continues.</para>
/// <para>See https://atproto.com/specs/event-stream and https://atproto.com/specs/sync.</para>
/// </remarks>
public sealed class AtProtoFirehose : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// The number of consecutive reconnection attempts made before an enumeration gives up, unless another is specified.
    /// </summary>
    public const int DefaultMaximumReconnectAttempts = 10;

    private readonly ServiceProvider? _serviceProvider;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly HttpMessageInvoker _invoker;
    private readonly FirehoseSignatureVerifier? _verifier;
    private readonly CancellationTokenSource _disposalTokenSource = new();

    private volatile bool _disposed;

    /// <summary>
    /// Creates a new instance of <see cref="AtProtoFirehose"/>.
    /// </summary>
    /// <param name="options">Any options to configure this instance.</param>
    /// <param name="webSocketOptions">Any <see cref="AtProto.WebSocketOptions"/> to set on the underlying web sockets.</param>
    /// <param name="httpClientOptions">Any <see cref="HttpClientOptions"/> for the internal HTTP client the web sockets connect through.</param>
    /// <exception cref="ArgumentException"><paramref name="webSocketOptions"/> sets <see cref="WebSocketOptions.Proxy"/>, which the firehose does not support.</exception>
    /// <remarks>
    /// <para>To connect through a proxy set <see cref="HttpClientOptions.ProxyUri"/>.</para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overloaded to allow an application to supply its own IHttpClientFactory.")]
    public AtProtoFirehose(
        FirehoseOptions? options = null,
        WebSocketOptions? webSocketOptions = null,
        HttpClientOptions? httpClientOptions = null) : this(null, null, httpClientOptions, options, webSocketOptions)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="AtProtoFirehose"/> which connects through <see cref="HttpClient"/>s created by
    /// the specified <paramref name="httpClientFactory"/>.
    /// </summary>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.</param>
    /// <param name="options">Any options to configure this instance.</param>
    /// <param name="webSocketOptions">Any <see cref="AtProto.WebSocketOptions"/> to set on the underlying web sockets.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClientFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="webSocketOptions"/> sets <see cref="WebSocketOptions.Proxy"/>, which the firehose does not support.</exception>
    /// <remarks>
    /// <para>The <see cref="HttpClient"/> is taken from <paramref name="httpClientFactory"/> as it is, so the factory has to have been configured with
    /// <see cref="ServiceCollectionExtensions.AddAtProtoHttpClient(IServiceCollection)"/>. Any other registration produces a client without
    /// the SSRF protections the firehose would otherwise apply for itself.</para>
    /// <para>Warning: without those protections the firehose can be made to connect to loopback, link local or private network
    /// addresses, such as a cloud metadata service, through <see cref="FirehoseOptions.RelayUri"/>, <see cref="FirehoseOptions.LabelerUri"/>,
    /// a host name which resolves to one of them, or a redirect.</para>
    /// <para>Warning: disable automatic redirects on the handler of a custom registration. The firehose rejects a redirected
    /// connection, and ends the enumeration with a <see cref="FirehoseConnectionException"/>, but a handler which follows redirects
    /// has already sent the request to the redirected host before the firehose can see it.</para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overloaded to allow an application to supply its own IHttpClientFactory.")]
    public AtProtoFirehose(
        IHttpClientFactory httpClientFactory,
        FirehoseOptions? options = null,
        WebSocketOptions? webSocketOptions = null) : this(
            null,
            httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory)),
            null,
            options,
            webSocketOptions)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="AtProtoFirehose"/> which connects through the specified <paramref name="httpClient"/>.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> to connect the web sockets through.</param>
    /// <param name="options">Any options to configure this instance.</param>
    /// <param name="webSocketOptions">Any <see cref="AtProto.WebSocketOptions"/> to set on the underlying web sockets.</param>
    /// <exception cref="ArgumentNullException"><paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="webSocketOptions"/> sets <see cref="WebSocketOptions.Proxy"/>, which the firehose does not support.</exception>
    /// <remarks>
    /// <para>The firehose does not dispose <paramref name="httpClient"/>. It belongs to the caller, who has to keep it alive for as long as the firehose is used.</para>
    /// <para>Warning: <paramref name="httpClient"/> is used as it is, so it has none of the SSRF protections the firehose would otherwise
    /// apply for itself, unless it was created by an <see cref="IHttpClientFactory"/> configured with
    /// <see cref="ServiceCollectionExtensions.AddAtProtoHttpClient(IServiceCollection)"/>. Without them the firehose can be made to connect
    /// to loopback, link local or private network addresses, such as a cloud metadata service, through <see cref="FirehoseOptions.RelayUri"/>,
    /// <see cref="FirehoseOptions.LabelerUri"/>, a host name which resolves to one of them, or a redirect.</para>
    /// <para>Warning: disable automatic redirects on the handler <paramref name="httpClient"/> was created with. An <see cref="HttpClient"/>
    /// created without a handler follows redirects. The firehose rejects a redirected connection, and ends the enumeration with a
    /// <see cref="FirehoseConnectionException"/>, but a client which follows redirects has already sent the request to the redirected
    /// host before the firehose can see it.</para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overloaded to allow an application to supply its own HttpClient.")]
    public AtProtoFirehose(
        HttpClient httpClient,
        FirehoseOptions? options = null,
        WebSocketOptions? webSocketOptions = null) : this(
            httpClient ?? throw new ArgumentNullException(nameof(httpClient)),
            null,
            null,
            options,
            webSocketOptions)
    {
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The handler is owned, and disposed, by the invoker it is given to.")]
    private AtProtoFirehose(
        HttpClient? httpClient,
        IHttpClientFactory? httpClientFactory,
        HttpClientOptions? httpClientOptions,
        FirehoseOptions? options,
        WebSocketOptions? webSocketOptions)
    {
#pragma warning disable CS0618 // Proxy is obsolete because it is rejected here.
        if (webSocketOptions?.Proxy is not null)
        {
            // A web socket which connects through an HttpMessageInvoker cannot have its own proxy, and the firehose always connects through one.
            throw new ArgumentException(
                $"{nameof(AtProto.WebSocketOptions.Proxy)} is not supported by the firehose. Use {nameof(HttpClientOptions)}.{nameof(HttpClientOptions.ProxyUri)}, or configure the proxy on the handler of the HttpClient you supply.",
                nameof(webSocketOptions));
        }
#pragma warning restore CS0618

        Options = options ?? new FirehoseOptions();
        WebSocketOptions = webSocketOptions;

        ILoggerFactory loggerFactory = Options.LoggerFactory ?? NullLoggerFactory.Instance;
        ILogger logger = loggerFactory.CreateLogger<AtProtoFirehose>();
        Metrics = new FirehoseMetrics(Options.MeterFactory);

        if (httpClient is null)
        {
            if (httpClientFactory is null)
            {
                // Without a factory of its own an application has nowhere to configure this client, so the firehose builds
                // the same SSRF protected client an agent builds for itself rather than a second definition of one.
                IServiceCollection services = new ServiceCollection();

                services
                    .AddHttpClient(Agent.HttpClientName, client => Agent.InternalConfigureHttpClient(client, httpClientOptions?.HttpUserAgent, httpClientOptions?.Timeout))
                    .ConfigurePrimaryHttpMessageHandler(() => Agent.CreateHttpMessageHandler(httpClientOptions, loggerFactory));

                _serviceProvider = services.BuildServiceProvider();
                httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
            }

            httpClient = httpClientFactory.CreateClient(Agent.HttpClientName);
            _ownsHttpClient = true;
        }

        _httpClient = httpClient;

        // Every client goes through this, including the firehose's own, so a redirected connection is refused however the
        // client was configured.
        _invoker = new HttpMessageInvoker(new RedirectRejectingHandler(_httpClient), disposeHandler: true);

        FirehoseSignatureVerifier? verifier = null;

        if (Options.VerifySignatures)
        {
            Func<Did, CancellationToken, Task<DidDocument?>> resolver = Options.DidDocumentResolver ??
                ((did, token) => IdentityResolution.ResolveDidDocumentAsync(did, loggerFactory: loggerFactory, cancellationToken: token));

            verifier = new FirehoseSignatureVerifier(resolver, Options, Metrics, Options.TimeProvider);
        }

        _verifier = verifier;

        RepoReader = new EventStreamReader(
            Options.RelayUri,
            Options,
            webSocketOptions,
            _invoker,
            new RepoEventDecoder(Options, verifier),
            logger,
            Metrics);

        LabelReader = new EventStreamReader(
            Options.LabelerUri,
            Options,
            webSocketOptions,
            _invoker,
            new LabelEventDecoder(Options, verifier),
            logger,
            Metrics);
    }

    /// <summary>
    /// Gets the options this instance was configured with.
    /// </summary>
    public FirehoseOptions Options { get; }

    /// <summary>
    /// Gets the web socket options this instance was configured with, if any.
    /// </summary>
    public WebSocketOptions? WebSocketOptions { get; }

    /// <summary>
    /// Gets the metrics published by this instance.
    /// </summary>
    public FirehoseMetrics Metrics { get; }

    /// <summary>
    /// Gets the cursor to resume <see cref="SubscribeReposAsync(long?, int?, CancellationToken)"/> from.
    /// </summary>
    /// <value>
    /// The sequence number of the last sequenced repository event yielded, or the cursor the current enumeration started from if
    /// none has been yet, or <see langword="null"/> if there is neither.
    /// </value>
    /// <remarks>
    /// <para>The value belongs to <see cref="FirehoseOptions.RelayUri"/>, and is meaningless to any other host.</para>
    /// </remarks>
    public long? LastRepoSequence => RepoReader.LastSequence;

    /// <summary>
    /// Gets the cursor to resume <see cref="SubscribeLabelsAsync(long?, int?, CancellationToken)"/> from.
    /// </summary>
    /// <value>
    /// The sequence number of the last labels event yielded, or the cursor the current enumeration started from if none has been yet,
    /// or <see langword="null"/> if there is neither.
    /// </value>
    /// <remarks>
    /// <para>The value belongs to <see cref="FirehoseOptions.LabelerUri"/>, and is meaningless to any other host.</para>
    /// </remarks>
    public long? LastLabelSequence => LabelReader.LastSequence;

    /// <summary>
    /// Gets the reader for <c>com.atproto.sync.subscribeRepos</c>, so tests can shorten its reconnection delays.
    /// </summary>
    internal EventStreamReader RepoReader { get; }

    /// <summary>
    /// Gets the reader for <c>com.atproto.label.subscribeLabels</c>, so tests can shorten its reconnection delays.
    /// </summary>
    internal EventStreamReader LabelReader { get; }

    /// <summary>
    /// Streams repository events from <c>com.atproto.sync.subscribeRepos</c> on <see cref="FirehoseOptions.RelayUri"/>.
    /// </summary>
    /// <param name="cursor">
    /// The sequence number of the last event already processed, <c>0</c> to replay every event the server retains, or
    /// <see langword="null"/> to start from the live tip.
    /// </param>
    /// <param name="maximumReconnectAttempts">
    /// The maximum consecutive reconnection attempts, or <see langword="null"/> for unlimited. A delivered event with a sequence number resets the count.
    /// </param>
    /// <param name="cancellationToken">A token which stops the connection and enumeration.</param>
    /// <returns>An ordered sequence of <see cref="FirehoseCommitEvent"/>, <see cref="FirehoseSyncEvent"/>, <see cref="FirehoseIdentityEvent"/>,
    /// <see cref="FirehoseAccountEvent"/>, <see cref="FirehoseInfoEvent"/>, <see cref="FirehoseUnknownEvent"/> and <see cref="FirehoseInvalidEvent"/>s.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cursor"/> is negative or greater than 2^53 - 1, or <paramref name="maximumReconnectAttempts"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">The firehose has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Another enumeration of repository events is active.</exception>
    /// <exception cref="FirehoseConnectionException">The server refuses the connection with a status which is not retried, or sends an error such as <c>FutureCursor</c>.</exception>
    /// <exception cref="InvalidDataException">The server sends an invalid frame, invalid DAG-CBOR, an oversized frame, or a duplicate or out of order sequence number.</exception>
    /// <exception cref="IOException">The connection could not be re-established within <paramref name="maximumReconnectAttempts"/>.</exception>
    /// <remarks>
    /// <para>The enumeration reconnects, with jittered exponential backoff, when the connection drops, stalls, is refused with a status
    /// which can be retried, or the server reports <c>ConsumerTooSlow</c>. It resumes from the last event yielded, and an event at or
    /// below that cursor is never yielded twice. Persist <see cref="FirehoseEvent.Sequence"/> after processing an event.</para>
    /// <para>If the cursor is older than the server retains, a <see cref="FirehoseInfoEvent"/> named <see cref="FirehoseInfoEvent.OutdatedCursor"/>
    /// is yielded and the stream continues from the oldest event available.</para>
    /// <para>Events are yielded in strictly increasing sequence order. Commits are not checked against the previous commit for the same
    /// repository, see <see cref="FirehoseCommitEvent"/>.</para>
    /// </remarks>
    public IAsyncEnumerable<FirehoseEvent> SubscribeReposAsync(
        long? cursor = null,
        int? maximumReconnectAttempts = DefaultMaximumReconnectAttempts,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(cursor, maximumReconnectAttempts);
        return SubscribeCoreAsync(RepoReader, cursor, maximumReconnectAttempts, cancellationToken);
    }

    /// <summary>
    /// Streams label events from <c>com.atproto.label.subscribeLabels</c> on <see cref="FirehoseOptions.LabelerUri"/>.
    /// </summary>
    /// <param name="cursor">
    /// The sequence number of the last event already processed, <c>0</c> to replay every event the server retains, or
    /// <see langword="null"/> to start from the live tip.
    /// </param>
    /// <param name="maximumReconnectAttempts">
    /// The maximum consecutive reconnection attempts, or <see langword="null"/> for unlimited. A delivered event with a sequence number resets the count.
    /// </param>
    /// <param name="cancellationToken">A token which stops the connection and enumeration.</param>
    /// <returns>An ordered sequence of <see cref="FirehoseLabelsEvent"/>, <see cref="FirehoseInfoEvent"/>, <see cref="FirehoseUnknownEvent"/> and <see cref="FirehoseInvalidEvent"/>s.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cursor"/> is negative or greater than 2^53 - 1, or <paramref name="maximumReconnectAttempts"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">The firehose has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Another enumeration of label events is active.</exception>
    /// <exception cref="FirehoseConnectionException">The server refuses the connection with a status which is not retried, or sends an error such as <c>FutureCursor</c>.</exception>
    /// <exception cref="InvalidDataException">The server sends an invalid frame, invalid DAG-CBOR, an oversized frame, or a duplicate or out of order sequence number.</exception>
    /// <exception cref="IOException">The connection could not be re-established within <paramref name="maximumReconnectAttempts"/>.</exception>
    /// <remarks>
    /// <para>Reconnection and cursor handling are the same as <see cref="SubscribeReposAsync(long?, int?, CancellationToken)"/>. A message with more
    /// than <see cref="FirehoseOptions.MaximumLabelsPerMessage"/> labels, or, when verifying signatures, with labels from more than
    /// <see cref="FirehoseOptions.MaximumLabelSourcesPerMessage"/> distinct sources, is surfaced as a <see cref="FirehoseInvalidEvent"/>.</para>
    /// </remarks>
    public IAsyncEnumerable<FirehoseEvent> SubscribeLabelsAsync(
        long? cursor = null,
        int? maximumReconnectAttempts = DefaultMaximumReconnectAttempts,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(cursor, maximumReconnectAttempts);
        return SubscribeCoreAsync(LabelReader, cursor, maximumReconnectAttempts, cancellationToken);
    }

    /// <summary>
    /// Stops any active enumerations and releases the resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposalTokenSource.Cancel();
        _disposalTokenSource.Dispose();
        _invoker.Dispose();
        _verifier?.Dispose();

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _serviceProvider?.Dispose();
    }

    /// <summary>
    /// Stops any active enumerations and releases the resources used by this instance.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _disposalTokenSource.CancelAsync().ConfigureAwait(false);
        _disposalTokenSource.Dispose();
        _invoker.Dispose();
        _verifier?.Dispose();

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void ValidateArguments(long? cursor, int? maximumReconnectAttempts)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (cursor is < 0 or > EventStreamReader.MaximumSequence)
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), cursor, "The cursor must be between 0 and 2^53 - 1.");
        }

        if (maximumReconnectAttempts is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumReconnectAttempts), maximumReconnectAttempts, "The maximum reconnection attempts cannot be negative.");
        }
    }

    private async IAsyncEnumerable<FirehoseEvent> SubscribeCoreAsync(
        EventStreamReader reader,
        long? cursor,
        int? maximumReconnectAttempts,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposalTokenSource.Token);

        IAsyncEnumerator<FirehoseEvent> events = reader.ReadAsync(cursor, maximumReconnectAttempts, linked.Token).GetAsyncEnumerator(linked.Token);

        await using (events.ConfigureAwait(false))
        {
            while (true)
            {
                bool moved;

                try
                {
                    moved = await events.MoveNextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException exception) when (_disposed)
                {
                    // Disposal cancels the enumeration before it releases the HTTP client and signing key cache, but work which
                    // was already in flight can still reach them, so it is reported as the cancellation it is.
                    throw new OperationCanceledException("The firehose was disposed.", exception, linked.Token);
                }

                if (!moved)
                {
                    yield break;
                }

                yield return events.Current;
            }
        }
    }
}
