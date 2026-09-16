// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;

using idunno.AtProto;
using idunno.AtProto.DidPlcDirectory;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.DidPlcDirectory;

/// <summary>
/// Provides a class for sending requests to and receiving responses from an directory service, identified by its service URI.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = "Used in DID resolution.")]
internal static partial class DirectoryServer
{
    private const string LocalHost = "localhost";

    // The maximum length of a DNS name, which a did:web identifier resolves to a request against.
    private const int MaximumHostNameLength = 253;

    // AT Proto did:web identifiers are hostnames, which is the same grammar a handle uses, so the handle
    // validation pattern is reused here. As well as requiring a well formed hostname it requires the right
    // most label to begin with a letter, which is what rules out IPv4 literals such as 169.254.169.254.
    [GeneratedRegex(Handle.ValidationRegex, RegexOptions.None, 5000)]
    private static partial Regex s_validateHostName();

    /// <summary>
    /// Tries to create the <see cref="Uri"/> of the web server a <c>did:web</c> DID document should be resolved from.
    /// </summary>
    /// <param name="identifier">The method specific identifier of the DID, the portion following <c>did:web:</c>.</param>
    /// <param name="service">The <see cref="Uri"/> of the web server, if <paramref name="identifier"/> is supported, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="identifier"/> is a supported <c>did:web</c> identifier, otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    ///   A DID is not necessarily trustworthy, it can be chosen by whoever controls the handle or record it was read from,
    ///   and resolving one causes an outbound request to the host it names. AT Proto narrows the did:web method considerably,
    ///   and applying those restrictions is what stops a DID naming an internal host or a host and port of the caller's choosing.
    /// </para>
    /// <para>
    ///   See <see href="https://atproto.com/specs/did">the AT Proto DID specification</see>. Only hostname level DIDs are
    ///   supported, path based DIDs are not, and a port number is only allowed on <c>localhost</c>, for testing and development.
    /// </para>
    /// </remarks>
    internal static bool TryGetWebDidService(string identifier, [NotNullWhen(true)] out Uri? service)
    {
        service = null;

        if (string.IsNullOrEmpty(identifier))
        {
            return false;
        }

        // In the did:web method a colon separates path segments. AT Proto does not support path based DIDs, so an
        // undecoded colon here means the DID is one this library will not resolve.
        if (identifier.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        // A port number is carried percent encoded, so the identifier has to be decoded before it can be validated.
        string hostAndPort = HttpUtility.UrlDecode(identifier);

        string host = hostAndPort;
        int portSeparatorPosition = hostAndPort.IndexOf(':', StringComparison.Ordinal);

        if (portSeparatorPosition >= 0)
        {
            host = hostAndPort[..portSeparatorPosition];

            // A port number is only allowed on localhost, for testing and development.
            if (!host.Equals(LocalHost, StringComparison.OrdinalIgnoreCase) ||
                !ushort.TryParse(hostAndPort[(portSeparatorPosition + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out ushort port) ||
                port == 0)
            {
                return false;
            }
        }

        if (!host.Equals(LocalHost, StringComparison.OrdinalIgnoreCase) &&
            (host.Length > MaximumHostNameLength || !s_validateHostName().IsMatch(host)))
        {
            return false;
        }

        return Uri.TryCreate($"https://{hostAndPort}", UriKind.Absolute, out service);
    }

    // DirectoryServer's json classes are encapsulated in the default source generation context, we can hard code this.
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = false,
        TypeInfoResolver = SourceGenerationContext.Default,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    // https://web.plc.directory/api/redoc#operation/ResolveDid

    /// <summary>
    /// Resolves the specified <paramref name="did"/> on the specified <paramref name="directory"/>.
    /// </summary>
    /// <param name="did">The DID to resolve.</param>
    /// <param name="directory">The directory server to use to resolve the <paramref name="did"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="directory"/>.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<DidDocument>> ResolveDidDocument(
        Did did,
        Uri directory,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        return await ResolveDidDocument(
            did: did,
            directory: directory,
            httpClient: httpClient,
            loggerFactory: null,
            meterFactory: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the specified <paramref name="did"/> on the specified <paramref name="directory"/>.
    /// </summary>
    /// <param name="did">The DID to resolve.</param>
    /// <param name="directory">The directory server to use to resolve the <paramref name="did"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="directory"/>.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<DidDocument>> ResolveDidDocument(
        Did did,
        Uri directory,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        return await ResolveDidDocument(
            did: did,
            directory: directory,
            httpClient: httpClient,
            loggerFactory: loggerFactory,
            meterFactory: default,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the specified <paramref name="did"/> on the specified <paramref name="directory"/>.
    /// </summary>
    /// <param name="did">The DID to resolve.</param>
    /// <param name="directory">The directory server to use to resolve the <paramref name="did"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="directory"/>.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="meterFactory">An instance of <see cref="IMeterFactory"/> to use to create a meter.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<DidDocument>> ResolveDidDocument(
        Did did,
        Uri directory,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = default,
        IMeterFactory? meterFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        const string plcDidPrefix = "did:plc:"; // https://github.com/did-method-plc/did-method-plc
        const string webDidPrefix = "did:web:"; // https://w3c-ccg.github.io/did-method-web/

        long startTimestamp = Stopwatch.GetTimestamp();

        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger logger = loggerFactory.CreateLogger(typeof(DirectoryServer));

        DirectoryMetrics metrics;

        if (meterFactory is not null)
        {
            metrics = new DirectoryMetrics(meterFactory);
        }
        else
        {
            metrics = new DirectoryMetrics(null);
        }

        try
        {
            using (logger.BeginScope($"Resolving DidDoc for {did}"))
            {
                if (did.ToString().StartsWith(plcDidPrefix, StringComparison.InvariantCulture))
                {
                    Logger.ResolvingPlcDid(logger, did, directory);
                    metrics.TotalRequests.Add(1, new KeyValuePair<string, object?>("did.type", "plc"));

                    AtProtoHttpClient<DidDocument> request = new(serviceProxy: null, loggerFactory: loggerFactory, meterFactory: meterFactory) { MaximumResponseSize = maximumResponseSize };

                    AtProtoHttpResult<DidDocument> result = await request.Get(
                        directory,
                        $"/{did}",
                        httpClient: httpClient,
                        jsonSerializerOptions: s_jsonSerializerOptions,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (result.Succeeded)
                    {
                        metrics.SuccessfulRequests.Add(1, new KeyValuePair<string, object?>("did.type", "plc"));
                    }
                    else
                    {
                        metrics.FailedRequests.Add(
                            1,
                            new KeyValuePair<string, object?>("http_status_code", (int)result.StatusCode),
                            new KeyValuePair<string, object?>("did.type", "plc"));
                    }

                    return result;
                }
                else if (did.ToString().StartsWith(webDidPrefix, StringComparison.InvariantCulture))
                {
                    string webDidIdentifier = did.ToString()[webDidPrefix.Length..];

                    if (!TryGetWebDidService(webDidIdentifier, out Uri? service))
                    {
                        metrics.TotalRequests.Add(1, new KeyValuePair<string, object?>("did.type", "web"));
                        metrics.FailedRequests.Add(1, new KeyValuePair<string, object?>("did.type", "web"));
                        Logger.UnsupportedWebDid(logger, did);

                        return new AtProtoHttpResult<DidDocument>(
                            result: null,
                            statusCode: HttpStatusCode.BadRequest,
                            httpResponseHeaders: null,
                            atErrorDetail: new AtErrorDetail("UnsupportedWebDid", $"{did} is not a supported did:web identifier."),
                            rateLimit: null);
                    }

                    Logger.ResolvingWebDid(logger, did, service);
                    metrics.TotalRequests.Add(1, new KeyValuePair<string, object?>("did.type", "web"));

                    AtProtoHttpClient<DidDocument> request = new(serviceProxy: null, loggerFactory: loggerFactory, meterFactory: meterFactory) { MaximumResponseSize = maximumResponseSize };

                    AtProtoHttpResult<DidDocument> result = await request.Get(
                        service,
                        $"/.well-known/did.json",
                        httpClient: httpClient,
                        jsonSerializerOptions: s_jsonSerializerOptions,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (result.Succeeded)
                    {
                        metrics.SuccessfulRequests.Add(1, new KeyValuePair<string, object?>("did.type", "web"));
                    }
                    else
                    {
                        metrics.FailedRequests.Add(
                            1,
                            new KeyValuePair<string, object?>("http_status_code", ((int)result.StatusCode)),
                            new KeyValuePair<string, object?>("did.type", "web"));
                    }

                    return result;
                }
                else
                {
                    metrics.TotalRequests.Add(1, new KeyValuePair<string, object?>("did.type", "unknown"));
                    metrics.FailedRequests.Add(1, new KeyValuePair<string, object?>("did.type", "unknown"));
                    Logger.UnknownDidType(logger, did);
                    throw new ArgumentException("DID is of an unknown type.", nameof(did));
                }
            }
        }
        finally
        {
            metrics.RequestDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds);
        }
    }
}