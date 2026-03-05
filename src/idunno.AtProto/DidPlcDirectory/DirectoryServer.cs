// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using idunno.AtProto;
using idunno.AtProto.DidPlcDirectory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.DidPlcDirectory
{
    /// <summary>
    /// Provides a class for sending requests to and receiving responses from an directory service, identified by its service URI.
    /// </summary>
    [SuppressMessage("Performance", "CA1812", Justification = "Used in DID resolution.")]
    internal static class DirectoryServer
    {
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

                        AtProtoHttpClient<DidDocument> request = new(serviceProxy: null, loggerFactory: loggerFactory, meterFactory: meterFactory);

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
                        string webDidDomainNameAndPath = HttpUtility.UrlDecode(did.ToString().Substring(webDidPrefix.Length).Replace(':', '/'));

                        Uri service = new($"https://{webDidDomainNameAndPath}");

                        Logger.ResolvingWebDid(logger, did, service);
                        metrics.TotalRequests.Add(1, new KeyValuePair<string, object?>("did.type", "web"));

                        AtProtoHttpClient<DidDocument> request = new(serviceProxy: null, loggerFactory: loggerFactory, meterFactory: meterFactory);

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
}
