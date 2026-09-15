// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using DnsClient;
using DnsClient.Protocol;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto;

/// <summary>
/// Represents an atproto server and provides methods to send messages and receive responses from the server.
/// </summary>
public static partial class AtProtoServer
{
    /// <summary>
    /// The default maximum number of bytes read from a <c>/.well-known/atproto-did</c> response.
    /// </summary>
    public const int DefaultMaximumWellKnownResponseSize = 4096;

    /// <summary>
    /// Resolves a handle (domain name) to a DID.
    /// </summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="maximumWellKnownResponseSize">The maximum number of bytes to read from a <c>/.well-known/atproto-did</c> response.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumWellKnownResponseSize"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    ///   The host a handle is resolved through is chosen by whoever owns the handle, so its response is untrusted and the
    ///   amount read from it is limited to <paramref name="maximumWellKnownResponseSize"/> bytes.
    /// </para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    public static async Task<Did?> ResolveHandle(
        string handle,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = default,
        int maximumWellKnownResponseSize = DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWellKnownResponseSize);

        return await ResolveHandle(new Handle(handle), httpClient, loggerFactory, maximumWellKnownResponseSize, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a handle (domain name) to a DID.
    /// </summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="maximumWellKnownResponseSize">The maximum number of bytes to read from a <c>/.well-known/atproto-did</c> response.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="handle"/> is not a valid DNS name, or when <paramref name="maximumWellKnownResponseSize"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    ///   The host a handle is resolved through is chosen by whoever owns the handle, so its response is untrusted and the
    ///   amount read from it is limited to <paramref name="maximumWellKnownResponseSize"/> bytes.
    /// </para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with different first parameter type for convenience")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catching all for logging purposes.")]
    public static async Task<Did?> ResolveHandle(
        Handle handle,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = default,
        int maximumWellKnownResponseSize = DefaultMaximumWellKnownResponseSize,
        CancellationToken cancellationToken = default)
    {
        Did? did = null;

        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWellKnownResponseSize);

        if (Uri.CheckHostName(handle.Value) != UriHostNameType.Dns)
        {
            throw new ArgumentOutOfRangeException(nameof(handle), "handle is not a valid DNS name.");
        }

        loggerFactory ??= NullLoggerFactory.Instance;
        ILogger logger = loggerFactory.CreateLogger(typeof(AtProtoServer));

        LookupClient lookupClient = new(new LookupClientOptions()
        {
            ContinueOnDnsError = true,
            ContinueOnEmptyResponse = true,
            ThrowDnsErrors = false,
            Timeout = TimeSpan.FromSeconds(15),
            UseCache = true
        });

        using (logger.BeginScope($"Resolving {handle}"))
        {
            // First try DNS lookup
            string didTxtRecordHost = $"_atproto.{handle}";
            const string didTextRecordPrefix = "did=";

            Logger.ResolvingHandleViaDNS(logger, handle, didTxtRecordHost);

            try
            {

                IDnsQueryResponse dnsLookupResult = await lookupClient.QueryAsync(didTxtRecordHost, QueryType.TXT, QueryClass.IN, cancellationToken).ConfigureAwait(false);
                if (!cancellationToken.IsCancellationRequested && !dnsLookupResult.HasError)
                {
                    List<string> didTextRecords =
                        [.. dnsLookupResult.Answers.TxtRecords()
                            .SelectMany(textRecord => textRecord.Text)
                            .Where(text => text.StartsWith(didTextRecordPrefix, StringComparison.InvariantCulture))
                            .Distinct(StringComparer.Ordinal)];

                    if (didTextRecords.Count > 1)
                    {
                        // The specification requires that a handle with more than one did text record is treated as unresolvable,
                        // rather than an arbitrary record being chosen.
                        Logger.MultipleDidTextRecordsFound(logger, handle, didTxtRecordHost, didTextRecords.Count);
                    }
                    else if (didTextRecords.Count == 1)
                    {
                        if (Did.TryParse(didTextRecords[0][didTextRecordPrefix.Length..], out Did? didFromDns))
                        {
                            did = didFromDns;
                            Logger.ResolvedHandleToDidViaDNS(logger, handle, did);
                        }
                        else
                        {
                            Logger.DnsHandleResolutionParseFailed(logger, handle, didTxtRecordHost);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorResolvingHandleViaDNS(logger, handle, ex);
            }

            if (!cancellationToken.IsCancellationRequested && did is null)
            {
                // Fall back to /well-known/did.json
                Uri didUri = new($"https://{handle}/.well-known/atproto-did");

                Logger.ResolvingHandleViaHttp(logger, handle, didUri);

                try
                {
                    using (HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, didUri) { Headers = { Accept = { new("text/plain") } } })
                    {
                        // Read the response headers only, so an over-long body is rejected by the bounded read below
                        // rather than being buffered into memory in its entirety first.
                        using (HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                        {
                            if (httpResponseMessage.IsSuccessStatusCode)
                            {
                                string? lookupResult = await HttpContentReader.ReadAsString(
                                    httpResponseMessage.Content,
                                    maximumWellKnownResponseSize,
                                    cancellationToken).ConfigureAwait(false);

                                if (lookupResult is null)
                                {
                                    Logger.HttpHandleResolutionResponseTooLarge(logger, handle, didUri, maximumWellKnownResponseSize);
                                }
                                else
                                {
                                    lookupResult = lookupResult.Trim();

                                    Logger.HttpHandleResolutionReturned(logger, handle, lookupResult);

                                    if (!string.IsNullOrEmpty(lookupResult))
                                    {
                                        if (Did.TryParse(lookupResult, out did))
                                        {
                                            Logger.ResolvedHandleToDidViaHttp(logger, handle, did);
                                        }
                                        else
                                        {
                                            Logger.HttpHandleResolutionParseFailed(logger, handle, didUri);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Logger.HttpHandleResolutionRequestFailed(logger, handle, didUri, httpResponseMessage.StatusCode);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.ErrorResolvingHandleViaHttp(logger, handle, didUri, ex);
                }
            }
        }

        return did;
    }
}