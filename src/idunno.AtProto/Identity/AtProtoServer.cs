// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using DnsClient;
using DnsClient.Protocol;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto
{
    /// <summary>
    /// Represents an atproto server and provides methods to send messages and receive responses from the server.
    /// </summary>
    public static partial class AtProtoServer
    {
        /// <summary>
        /// Resolves a handle (domain name) to a DID.
        /// </summary>
        /// <param name="handle">The handle to resolve.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<Did?> ResolveHandle(
            string handle,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(handle);
            ArgumentNullException.ThrowIfNull(httpClient);

            return await ResolveHandle(new Handle(handle), httpClient, loggerFactory, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves a handle (domain name) to a DID.
        /// </summary>
        /// <param name="handle">The handle to resolve.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<Did?> ResolveHandle(
            Handle handle,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            Did? did = null;

            ArgumentNullException.ThrowIfNull(handle);
            ArgumentNullException.ThrowIfNull(httpClient);

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

                IDnsQueryResponse dnsLookupResult = await lookupClient.QueryAsync(didTxtRecordHost, QueryType.TXT, QueryClass.IN, cancellationToken).ConfigureAwait(false);
                if (!cancellationToken.IsCancellationRequested && !dnsLookupResult.HasError)
                {
                    foreach (TxtRecord? textRecord in dnsLookupResult.Answers.TxtRecords())
                    {
                        foreach (string? text in textRecord.Text.Where(t => t.StartsWith(didTextRecordPrefix, StringComparison.InvariantCulture)))
                        {
                            did = new Did(text.Substring(didTextRecordPrefix.Length));
                            Logger.ResolvedHandleToDidViaDNS(logger, handle, did);
                        }
                    }
                }

                if (!cancellationToken.IsCancellationRequested && did is null)
                {
                    // Fall back to /well-known/did.json
                    Uri didUri = new($"https://{handle}/.well-known/atproto-did");

                    Logger.ResolvingHandleViaHttp(logger, handle, didUri);

                    using (HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, didUri) { Headers = { Accept = { new("text/plain") } } })
                    {
                        using (HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false))
                        {
                            if (httpResponseMessage.IsSuccessStatusCode)
                            {
                                string lookupResult = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

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
                            else
                            {
                                Logger.HttpHandleResolutionRequestFailed(logger, handle, didUri, httpResponseMessage.StatusCode);
                            }
                        }
                    }
                }
            }

            return did;
        }
    }
}
