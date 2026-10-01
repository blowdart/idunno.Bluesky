// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Configures an instance of <see cref="AtProtoFirehose"/>.
/// </summary>
/// <remarks>
/// <para>Sequence numbers are scoped to the host which issued them, so a cursor saved from one host is meaningless on
/// another. The hosts are fixed when the options are created, so every cursor an <see cref="AtProtoFirehose"/> issues
/// belongs to its configured <see cref="RelayUri"/> or <see cref="LabelerUri"/>.</para>
/// </remarks>
public sealed record FirehoseOptions
{
    /// <summary>
    /// The relay used for <c>com.atproto.sync.subscribeRepos</c> when none is configured.
    /// </summary>
    internal static readonly Uri s_defaultRelayUri = new("wss://bsky.network");

    /// <summary>
    /// The labeler used for <c>com.atproto.label.subscribeLabels</c> when none is configured.
    /// </summary>
    internal static readonly Uri s_defaultLabelerUri = new("wss://mod.bsky.app");

    /// <summary>
    /// Gets the <see cref="ILoggerFactory"/>, if any, to use when creating loggers.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>
    /// Gets the <see cref="IMeterFactory"/>, if any, to use when creating meters.
    /// </summary>
    public IMeterFactory? MeterFactory { get; init; }

    /// <summary>
    /// Gets the host to subscribe to repository events from.
    /// </summary>
    /// <value>The relay or PDS host. The default is <c>wss://bsky.network</c>.</value>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The value is not an absolute <c>ws</c>, <c>wss</c>, <c>http</c> or <c>https</c> uri.</exception>
    /// <remarks>
    /// <para>Only the scheme, host and port are used; the path is replaced with the XRPC endpoint.
    /// Do not connect to untrusted hosts.</para>
    /// </remarks>
    public Uri RelayUri
    {
        get;
        init => field = ValidateHost(value);
    } = s_defaultRelayUri;

    /// <summary>
    /// Gets the host to subscribe to label events from.
    /// </summary>
    /// <value>The labeler host. The default is <c>wss://mod.bsky.app</c>.</value>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The value is not an absolute <c>ws</c>, <c>wss</c>, <c>http</c> or <c>https</c> uri.</exception>
    /// <remarks>
    /// <para>Only the scheme, host and port are used; the path is replaced with the XRPC endpoint.</para>
    /// </remarks>
    public Uri LabelerUri
    {
        get;
        init => field = ValidateHost(value);
    } = s_defaultLabelerUri;

    /// <summary>
    /// Gets the size, in bytes, of each block read from the web socket.
    /// </summary>
    /// <value>The read buffer size. The default is 8096 bytes.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>This is the size of a single read, not a limit. The limit on a message is <see cref="MaxMessageSize"/>.</para>
    /// </remarks>
    public int BufferSize
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 8096;

    /// <summary>
    /// Gets the maximum size, in bytes, of a single reassembled frame.
    /// </summary>
    /// <value>The maximum frame size. The default is 5 MiB.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>The limit is enforced whilst a frame is being reassembled, so an oversized frame is abandoned before it is
    /// buffered. An oversized frame ends the enumeration, as the server would send the same frame again on reconnection.</para>
    /// <para>A commit carries up to 2,000,000 bytes of CAR data plus its operations, so values below about 2.1 MB will reject
    /// legitimate commits.</para>
    /// </remarks>
    public int MaxMessageSize
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 5 * 1024 * 1024;

    /// <summary>
    /// Gets the maximum number of labels accepted in a single <c>#labels</c> message.
    /// </summary>
    /// <value>The maximum label count. The default is 10,000.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>The lexicon places no limit on the number of labels in a message. A message over this limit is surfaced as a
    /// <see cref="FirehoseInvalidEvent"/>.</para>
    /// </remarks>
    public int MaximumLabelsPerMessage
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 10_000;

    /// <summary>
    /// Gets the maximum number of distinct label sources whose signing keys are resolved for a single <c>#labels</c> message.
    /// </summary>
    /// <value>The maximum number of distinct sources. The default is 10.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>This limit only applies when <see cref="VerifySignatures"/> is <see langword="true"/>. A labeler normally signs every
    /// label it emits with its own key, so a message usually has a single source. Each distinct source needs its DID document
    /// resolved before the message can be delivered, so without a limit a labeler could stall the stream, and make requests
    /// to hosts of its choosing, by sending a message whose labels claim many different sources.</para>
    /// <para>A message with labels from more distinct sources than this is surfaced as a <see cref="FirehoseInvalidEvent"/>
    /// without any of its sources being resolved.</para>
    /// </remarks>
    public int MaximumLabelSourcesPerMessage
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 10;

    /// <summary>
    /// Gets the maximum number of blocks accepted in the CAR data of a single event.
    /// </summary>
    /// <value>The maximum block count. The default is 10,000.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero.</exception>
    public int MaximumCarBlocks
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 10_000;

    /// <summary>
    /// Gets the maximum size, in bytes, of a single block in the CAR data of an event.
    /// </summary>
    /// <value>The maximum block size. The default is 1 MiB.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero.</exception>
    public int MaximumCarBlockSize
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 1024 * 1024;

    /// <summary>
    /// Gets how long a connection may go without receiving a frame before it is treated as stalled and reconnected.
    /// </summary>
    /// <value>The idle timeout. The default is 300 seconds.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero, or exceeds the timer's maximum duration.</exception>
    public TimeSpan IdleTimeout
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TimeSpan.FromMilliseconds(uint.MaxValue - 1));
            field = value;
        }
    } = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Gets how long to wait for a server to answer a close handshake before the connection is aborted instead.
    /// </summary>
    /// <value>The close timeout. The default is 30 seconds.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero, or exceeds the timer's maximum duration.</exception>
    public TimeSpan CloseTimeout
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TimeSpan.FromMilliseconds(uint.MaxValue - 1));
            field = value;
        }
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets a value that indicates whether repository commit and label signatures are verified.
    /// </summary>
    /// <value><see langword="true"/> to verify signatures; otherwise, <see langword="false"/>. The default is <see langword="false"/>.</value>
    /// <remarks>
    /// <para>Verification needs the signing key of every repository and label source, which is taken from its DID document, resolved
    /// with <see cref="DidDocumentResolver"/>. Keys are cached, see <see cref="CacheSigningKeys"/>, but each resolution is made inline,
    /// one at a time, so every event from a DID which is not cached delays the stream, and a relay drops a consumer which falls too far behind.</para>
    /// <para>Verification suits labelers, a single PDS, or other low-volume streams. It is not suitable for the full relay, which carries events
    /// from far more repositories than can be resolved inline, so cache misses continue for as long as the stream runs and the reader falls
    /// further behind until the relay disconnects it with <c>ConsumerTooSlow</c>.</para>
    /// <para>Commit and sync signatures are checked against the repository's <c>#atproto</c> key, label signatures against the
    /// label source's <c>#atproto_label</c> key. An event which fails verification, or whose key cannot be resolved,
    /// is surfaced as a <see cref="FirehoseInvalidEvent"/>.</para>
    /// <para>DID documents are only resolved when verification is on, so a server cannot trigger resolutions, or thrash the signing
    /// key cache, while it is off. See <see cref="CacheSigningKeys"/> for how a malicious server can defeat the cache.</para>
    /// </remarks>
    public bool VerifySignatures { get; init; }

    /// <summary>
    /// Gets the function used to resolve DID documents when <see cref="VerifySignatures"/> is <see langword="true"/>.
    /// </summary>
    /// <value>A DID document resolver, or <see langword="null"/> to resolve each DID with <see cref="Resolution"/>.</value>
    public Func<Did, CancellationToken, Task<DidDocument?>>? DidDocumentResolver { get; init; }

    /// <summary>
    /// Gets the <see cref="IDidHandleResolver"/>, if any, whose cached handle for a DID is invalidated when an <c>#identity</c> event is received.
    /// </summary>
    /// <value>A DID handle resolver, or <see langword="null"/> to not invalidate any handles. The default is <see langword="null"/>.</value>
    /// <remarks>
    /// <para>The resolver is not disposed by the firehose.</para>
    /// <para>A malicious server can send an <c>#identity</c> event before each event for a DID so its handle is never cached.</para>
    /// </remarks>
    public IDidHandleResolver? DidHandleResolver { get; init; }

    /// <summary>
    /// Gets a value that indicates whether signing keys resolved when <see cref="VerifySignatures"/> is <see langword="true"/> are cached.
    /// </summary>
    /// <value><see langword="true"/> to cache signing keys; otherwise, <see langword="false"/>. The default is <see langword="true"/>.</value>
    /// <remarks>
    /// <para>Keys are cached for <see cref="SigningKeyCacheDuration"/>, and at most <see cref="SigningKeyCacheSize"/> are held.
    /// A failure to resolve a usable key is also cached, for a minute, so a DID which cannot be resolved is not resolved for every event.</para>
    /// <para>An <c>#identity</c> event removes the cached keys of its DID. A signature which fails to verify against a cached key causes
    /// the key to be resolved again, at most once every five minutes for each DID, in case the key has been rotated. If that resolution fails
    /// the cached key is kept until it expires.</para>
    /// <para>Disable the cache when <see cref="DidDocumentResolver"/> does its own caching. Without a cache every commit, sync event
    /// and label source resolves a DID document.</para>
    /// <para>The cache only helps against well behaved servers. A malicious server can send malicious data which defeats it, forcing a
    /// DID resolution for most events, either by sending events from more distinct DIDs than <see cref="SigningKeyCacheSize"/> so keys are
    /// evicted before they are reused, or by sending an <c>#identity</c> event before each event for a DID so its key is removed each time.
    /// Each resolution delays the stream and, for the default resolver, makes a request to <c>plc.directory</c> or, for <c>did:web</c>,
    /// to a host the DID names. This only applies when <see cref="VerifySignatures"/> is <see langword="true"/>, which is not the default,
    /// and the cache is on by default when it is.</para>
    /// </remarks>
    public bool CacheSigningKeys { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of signing keys held in the signing key cache.
    /// </summary>
    /// <value>The maximum number of cached signing keys. The default is 100,000.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>When the cache is full a new key is not cached until the cache has been compacted, which happens in the background.</para>
    /// <para>The size bounds the cache's memory, not the number of resolutions. A malicious server sending events from more distinct DIDs
    /// than this can keep evicting keys, so most events need a resolution. This only applies when <see cref="VerifySignatures"/> is
    /// <see langword="true"/>, which is not the default, and <see cref="CacheSigningKeys"/> is <see langword="true"/>, which is the default.</para>
    /// </remarks>
    public int SigningKeyCacheSize
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 100_000;

    /// <summary>
    /// Gets how long a resolved signing key is cached for.
    /// </summary>
    /// <value>The signing key cache duration. The default is one hour.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero, or exceeds the timer's maximum duration.</exception>
    /// <remarks>
    /// <para>A longer duration means fewer resolutions, but a rotated key which is not announced with an <c>#identity</c> event
    /// is trusted for longer.</para>
    /// </remarks>
    public TimeSpan SigningKeyCacheDuration
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TimeSpan.FromMilliseconds(uint.MaxValue - 1));
            field = value;
        }
    } = TimeSpan.FromHours(1);

    private static Uri ValidateHost(Uri value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!value.IsAbsoluteUri ||
            !(value.Scheme.Equals(Uri.UriSchemeWss, StringComparison.OrdinalIgnoreCase) ||
              value.Scheme.Equals(Uri.UriSchemeWs, StringComparison.OrdinalIgnoreCase) ||
              value.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
              value.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("A firehose host must be an absolute ws, wss, http or https uri.", nameof(value));
        }

        // Uri refuses to parse an http, https, ws or wss uri without a host, so this is defence in depth.
        if (value.Host.Length == 0)
        {
            throw new ArgumentException("A firehose host must include a host name.", nameof(value));
        }

        if (value.UserInfo.Length > 0)
        {
            throw new ArgumentException("A firehose host must not contain user information.", nameof(value));
        }

        return value;
    }
}
