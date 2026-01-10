// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using idunno.DidPlcDirectory;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides access to common resolvers used by the AtProto library.
    /// </summary>
    public sealed class Resolution
    {
        private static readonly HttpClientHandler s_httpClientHandler = new()
        {
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false
        };

        /// <summary>
        /// As we need to use the class only as a static container, prevent instantiation.
        /// We can't use static classes as they prevent the use of CreateLogger{T}.
        /// </summary>
        private Resolution()
        {
        }

        /// <summary>
        /// Resolves a handle (domain name) to a DID.
        /// </summary>
        /// <param name="handle">The handle to resolve.</param>
        /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
        /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> is null.</exception>
        public static async Task<Did?> ResolveHandle(
            string handle,
            ILoggerFactory? loggerFactory = null,
            HttpClient? httpClient = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(handle);

            loggerFactory ??= NullLoggerFactory.Instance;
            ILogger<Resolution> logger = loggerFactory.CreateLogger<Resolution>();

            Logger.ResolveHandleCalled(logger, handle);

            using (HttpClient internalHttpClient = httpClient ?? BuildDefaultHttpClient(timeout))
            {
                Did? result = await AtProtoServer.ResolveHandle(
                    handle,
                    httpClient: httpClient ?? internalHttpClient,
                    loggerFactory: loggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (result is null)
                {
                    Logger.CouldNotResolveHandleToDid(logger, handle);
                }
                else
                {
                    Logger.ResolveHandleToDid(logger, handle, result);
                }

                return result;
            }
        }

        /// <summary>
        /// Resolves a handle (domain name) to a DID.
        /// </summary>
        /// <param name="handle">The handle to resolve.</param>
        /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
        /// <param name="timeout">An optional timeout for HTTP requests.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> is null.</exception>
        public static async Task<Did?> ResolveHandle(
            Handle handle,
            ILoggerFactory? loggerFactory = null,
            HttpClient? httpClient = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);
            return await ResolveHandle(
                handle: handle.ToString(),
                loggerFactory: loggerFactory,
                httpClient: httpClient,
                timeout: timeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves the <see cref="DidDocument"/> for the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> to resolve the <see cref="DidDocument"/> for.</param>
        /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If null the default directory server, https://plc.directory, will be used.</param>
        /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
        /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is null.</exception>
        public static async Task<DidDocument?> ResolveDidDocument(
            Did did,
            Uri? plcDirectory = null,
            ILoggerFactory? loggerFactory = null,
            HttpClient? httpClient = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            plcDirectory ??= DirectoryAgent.s_defaultDirectoryServer;

            loggerFactory ??= NullLoggerFactory.Instance;
            ILogger<Resolution> logger = loggerFactory.CreateLogger<Resolution>();

            Logger.ResolveDidDocumentCalled(logger, did);

            DidDocument? didDocument = null;

            using (HttpClient internalHttpClient = httpClient ?? BuildDefaultHttpClient(timeout))
            {
                AtProtoHttpResult<DidDocument> didDocumentResolutionResult = await
                    DirectoryServer.ResolveDidDocument(
                        did: did,
                        directory: plcDirectory,
                        httpClient: httpClient ?? internalHttpClient,
                        loggerFactory: loggerFactory,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                if (didDocumentResolutionResult.Succeeded)
                {
                    didDocument = didDocumentResolutionResult.Result;
                }
                else
                {
                    Logger.ResolveDidDocumentFailed(logger, did, didDocumentResolutionResult.StatusCode);
                }
            }

            return didDocument;
        }

        /// <summary>
        /// Resolves the <see cref="DidDocument"/> for the specified <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">The <see cref="Handle"/> to resolve the <see cref="DidDocument"/> for.</param>
        /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If null the default directory server, https://plc.directory, will be used.</param>
        /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
        /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> could not be resolved to a <see cref="Did"/>.</exception>
        public static async Task<DidDocument?> ResolveDidDocument(
            Handle handle,
            Uri? plcDirectory = null,
            ILoggerFactory? loggerFactory = null,
            HttpClient? httpClient = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);

            plcDirectory ??= DirectoryAgent.s_defaultDirectoryServer;

            loggerFactory ??= NullLoggerFactory.Instance;
            ILogger<Resolution> logger = loggerFactory.CreateLogger<Resolution>();

            Did? did = await ResolveHandle(
                handle: handle,
                loggerFactory: loggerFactory,
                httpClient: httpClient,
                timeout: timeout,
                cancellationToken: cancellationToken).ConfigureAwait(false) ?? throw new ArgumentException("Could not resolve handle to DID.", nameof(handle));

            Logger.ResolveDidDocumentCalled(logger, did);

            DidDocument? didDocument = null;

            using (HttpClient internalHttpClient = httpClient ?? BuildDefaultHttpClient(timeout))
            {
                AtProtoHttpResult<DidDocument> didDocumentResolutionResult = await
                    DirectoryServer.ResolveDidDocument(
                        did: did,
                        directory: plcDirectory,
                        httpClient: httpClient ?? internalHttpClient,
                        loggerFactory: loggerFactory,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                if (didDocumentResolutionResult.Succeeded)
                {
                    didDocument = didDocumentResolutionResult.Result;
                }
                else
                {
                    Logger.ResolveDidDocumentFailed(logger, did, didDocumentResolutionResult.StatusCode);
                }
            }

            return didDocument;
        }

        /// <summary>
        /// Resolves the <see cref="DidDocument"/> for the specified <paramref name="atIdentifier"/>.
        /// </summary>
        /// <param name="atIdentifier">The AtIdentifier to resolve the <see cref="DidDocument"/> for.</param>
        /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If null the default directory server, https://plc.directory, will be used.</param>
        /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
        /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="atIdentifier"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="atIdentifier"/> could not be resolved to a <see cref="Did"/> or a <see cref="Handle"/>.</exception>
        public static async Task<DidDocument?> ResolveDidDocument(
            string atIdentifier,
            Uri? plcDirectory = null,
            ILoggerFactory? loggerFactory = null,
            HttpClient? httpClient = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(atIdentifier);

            if (AtIdentifier.TryParse(atIdentifier, out AtIdentifier? parsedAtIdentifier) && parsedAtIdentifier is not null)
            {
                if (parsedAtIdentifier is Did did)
                {
                    return await ResolveDidDocument(
                        did: did,
                        plcDirectory: plcDirectory,
                        loggerFactory: loggerFactory,
                        httpClient: httpClient,
                        timeout: timeout,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                else if (parsedAtIdentifier is Handle handle)
                {
                    return await ResolveDidDocument(
                        handle: handle,
                        plcDirectory: plcDirectory,
                        loggerFactory: loggerFactory,
                        httpClient: httpClient,
                        timeout: timeout,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw new ArgumentException("AtIdentifier is of an unknown type.", nameof(atIdentifier));
                }
            }
            else
            {
                throw new ArgumentException("Could not parse AtIdentifier.", nameof(atIdentifier));
            }
        }

        /// <summary>
        /// Resolves the Personal Data Server (PDS) <see cref="Uri"/>for the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> to resolve the PDS <see cref="Uri"/> for.</param>
        /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If null the default directory server, https://plc.directory, will be used.</param>
        /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
        /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is null.</exception>
        public static async Task<Uri?> ResolvePds(
            Did did,
            Uri? plcDirectory = null,
            ILoggerFactory? loggerFactory = null,
            HttpClient? httpClient = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            loggerFactory ??= NullLoggerFactory.Instance;
            ILogger<Resolution> logger = loggerFactory.CreateLogger<Resolution>();

            Logger.ResolvePdsCalled(logger, did);

            Uri? pds = null;

            DidDocument? didDocument = await ResolveDidDocument(
                did,
                plcDirectory: plcDirectory,
                loggerFactory: loggerFactory,
                httpClient: httpClient,
                timeout: timeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (didDocument is not null && didDocument.Services is not null)
            {
                pds = didDocument.Services!.FirstOrDefault(s => s.Id == @"#atproto_pds")!.ServiceEndpoint;
            }

            if (pds is null)
            {
                Logger.ResolvePdsFailed(logger, did);
            }

            return pds;
        }

        /// <summary>
        /// Resolves the Personal Data Server (PDS) <see cref="Uri"/>for the specified <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">The <see cref="Handle"/> to resolve the PDS <see cref="Uri"/> for.</param>
        /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If null the default directory server, https://plc.directory, will be used.</param>
        /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
        /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> could not be resolved to a <see cref="Did"/>.</exception>
        public static async Task<Uri?> ResolvePds(
            Handle handle,
            Uri? plcDirectory = null,
            ILoggerFactory? loggerFactory = null,
            HttpClient? httpClient = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);

            Did? did = await ResolveHandle(
                handle: handle,
                loggerFactory: loggerFactory,
                httpClient: httpClient,
                timeout: timeout,
                cancellationToken: cancellationToken).ConfigureAwait(false) ?? throw new ArgumentException("Could not resolve handle to DID.", nameof(handle));

            return await ResolvePds(
                did,
                plcDirectory: plcDirectory,
                loggerFactory: loggerFactory,
                httpClient: httpClient,
                timeout: timeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves the Personal Data Server (PDS) <see cref="Uri"/>for the specified <paramref name="atIdentifier"/>.
        /// </summary>
        /// <param name="atIdentifier">The <see cref="Handle"/> to resolve the PDS <see cref="Uri"/> for.</param>
        /// <param name="plcDirectory">An optional <see cref="Uri"/> of the PLC directory server to use. If null the default directory server, https://plc.directory, will be used.</param>
        /// <param name="loggerFactory">An optional <see cref="LoggerFactory"/> to use to create a logger.</param>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use for HTTP requests.</param>
        /// <param name="timeout">An optional timeout for HTTP requests. This only takes effect if <paramref name="httpClient"/> is null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="atIdentifier"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="atIdentifier"/> could not be resolved to a <see cref="Did"/> or <see cref="Handle"/>.</exception>
        public static async Task<Uri?> ResolvePds(
            string atIdentifier,
            Uri? plcDirectory = null,
            ILoggerFactory? loggerFactory = null,
            HttpClient? httpClient = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(atIdentifier);

            if (AtIdentifier.TryParse(atIdentifier, out AtIdentifier? parsedAtIdentifier) && parsedAtIdentifier is not null)
            {
                if (parsedAtIdentifier is Did did)
                {
                    return await ResolvePds(
                        did: did,
                        plcDirectory: plcDirectory,
                        loggerFactory: loggerFactory,
                        httpClient: httpClient,
                        timeout: timeout,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                else if (parsedAtIdentifier is Handle handle)
                {
                    return await ResolvePds(
                        handle: handle,
                        plcDirectory: plcDirectory,
                        loggerFactory: loggerFactory,
                        httpClient: httpClient,
                        timeout: timeout,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw new ArgumentException("AtIdentifier is of an unknown type.", nameof(atIdentifier));
                }
            }
            else
            {
                throw new ArgumentException("Could not parse AtIdentifier.", nameof(atIdentifier));
            }
        }

        private static HttpClient BuildDefaultHttpClient(TimeSpan? timeout)
        {
            HttpClient client = new(s_httpClientHandler, disposeHandler: false)
            {
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
                DefaultRequestVersion = HttpVersion.Version20
            };

            Assembly assembly = typeof(Agent).Assembly;
            string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("idunno.AtProto/" + version);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
            if (timeout is null)
            {
                client.Timeout = new(0, 5, 0);
            }
            else
            {
                client.Timeout = (TimeSpan)timeout;
            }

            return client;
        }
    }
}
