// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto.Authentication;

namespace idunno.AtProto
{
    /// <summary>
    /// A helper class to perform HTTP requests against an AT Proto service.
    /// </summary>
    /// <remarks>
    /// <para>Creates a new instance of <see cref="AtProtoHttpClient"/></para>
    /// </remarks>
    /// <param name="serviceProxy">Any service a PDS should proxy the request to.</param>
    /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
    /// <param name="meterFactory">An optional meter factory to create meters from.</param>
    public class AtProtoHttpClient(string? serviceProxy = null, ILoggerFactory? loggerFactory = null, IMeterFactory? meterFactory = null)
    {
        static readonly HttpClientHandler s_defaultClientHandler = new()
        {
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false
        };

        private readonly AtProtoHttpClient<string> _internalClient = new(
                serviceProxy: serviceProxy,
                requestHeaders: null,
                loggerFactory: loggerFactory,
                meterFactory: meterFactory);

        /// <summary>
        /// Gets or sets a function called when a request is about to be sent.
        /// </summary>
        public Func<HttpRequestMessage, CancellationToken, Task> OnSendingRequest => _internalClient.OnSendingRequest;

        /// <summary>
        /// Gets or sets a function called when a response has been received.
        /// </summary>
        public Func<HttpResponseMessage, CancellationToken, Task> OnResponseReceived => _internalClient.OnResponseReceived;

        /// <summary>
        /// Gets the result of an AT Proto GET request, returning the raw response wrapped in an <see cref="AtProtoHttpResult{TResult}"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of service to send the request to.</param>
        /// <param name="endpoint">The endpoint on the service to send the request to.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to authenticate with, if any.</param>
        /// <param name="httpClient">The <see cref="HttpClient"/> to use, in any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="requestHeaders">A collection of HTTP headers to send with the request.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="endpoint"/> is <see langword="null"/> or empty.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "Using a return type of string avoids json serialization and deserialization in the typed client.")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "Using a return type of string avoids json serialization and deserialization in the typed client.")]
        public async Task<AtProtoHttpResult<string>> Get(
            Uri service,
            string endpoint,
            AtProtoCredential? credentials = null,
            HttpClient? httpClient = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ICollection<NameValueHeaderValue>? requestHeaders = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrEmpty(endpoint);

            using (HttpClient internalClient = new(
                handler : s_defaultClientHandler,
                disposeHandler: false)
            {
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            })
            {
                return await _internalClient.Get(
                    service: service,
                    endpoint: endpoint,
                    credentials: credentials,
                    httpClient: httpClient ?? internalClient,
                    onCredentialsUpdated: onCredentialsUpdated,
                    requestHeaders: requestHeaders,
                    subscribedLabelers: subscribedLabelers,
                    jsonSerializerOptions: AtProtoServer.AtProtoJsonSerializerOptions,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets the result of an AT Proto GET request, returning the raw response wrapped in an <see cref="AtProtoHttpResult{TResult}"/>.
        /// </summary>
        /// <param name="service">The location of service to send the request to. This must be a uri string.</param>
        /// <param name="endpoint">The endpoint on the service to send the request to.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to authenticate with, if any.</param>
        /// <param name="httpClient">The <see cref="HttpClient"/> to use, in any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="requestHeaders">A collection of HTTP headers to send with the request.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="service"/> or <paramref name="endpoint"/> is <see langword="null"/> or empty.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "Using a return type of string avoids json serialization and deserialization in the typed client.")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "Using a return type of string avoids json serialization and deserialization in the typed client.")]
        public async Task<AtProtoHttpResult<string>> Get(
            string service,
            string endpoint,
            AtProtoCredential? credentials = null,
            HttpClient? httpClient = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ICollection<NameValueHeaderValue>? requestHeaders = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(service);
            ArgumentException.ThrowIfNullOrEmpty(endpoint);

            return await Get(
                service: new Uri(service),
                endpoint: endpoint,
                credentials: credentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                requestHeaders: requestHeaders,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Posts the specified <paramref name="body"/> to the <paramref name="service"/> <paramref name="endpoint"/> and returns the result.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of service to send the request to.</param>
        /// <param name="endpoint">The endpoint on the service to send the request to.</param>
        /// <param name="body">The body of the request to send.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to authenticate with, if any.</param>
        /// <param name="httpClient">The <see cref="HttpClient"/> to use, in any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="requestHeaders">A collection of HTTP headers to send with the request.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="endpoint"/> is <see langword="null"/> or empty.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "Using a return type of string avoids json serialization and deserialization in the typed client.")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "Using a return type of string avoids json serialization and deserialization in the typed client.")]
        public async Task<AtProtoHttpResult<string>> Post(
            Uri service,
            string endpoint,
            string? body,
            AtProtoCredential? credentials = null,
            HttpClient? httpClient = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ICollection<NameValueHeaderValue>? requestHeaders = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrEmpty(endpoint);

            using (HttpClient internalClient = new(
                handler: s_defaultClientHandler,
                disposeHandler: false)
            {
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            })
            {
                return await _internalClient.Post(
                    service: service,
                    endpoint: endpoint,
                    record: body,
                    credentials: credentials,
                    httpClient: httpClient ?? internalClient,
                    onCredentialsUpdated: onCredentialsUpdated,
                    requestHeaders: requestHeaders,
                    subscribedLabelers: subscribedLabelers,
                    jsonSerializerOptions: AtProtoServer.AtProtoJsonSerializerOptions,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Posts the specified <paramref name="body"/> to the <paramref name="service"/> <paramref name="endpoint"/> and returns the result.
        /// </summary>
        /// <param name="service">The location of service to send the request to. This must be a uri string.</param>
        /// <param name="endpoint">The endpoint on the service to send the request to.</param>
        /// <param name="body">The body of the request to send.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to authenticate with, if any.</param>
        /// <param name="httpClient">The <see cref="HttpClient"/> to use, in any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="requestHeaders">A collection of HTTP headers to send with the request.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="endpoint"/> is <see langword="null"/> or empty.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "Using a return type of string avoids json serialization and deserialization in the typed client.")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "Using a return type of string avoids json serialization and deserialization in the typed client.")]
        public async Task<AtProtoHttpResult<string>> Post(
            string service,
            string endpoint,
            string? body,
            AtProtoCredential? credentials = null,
            HttpClient? httpClient = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ICollection<NameValueHeaderValue>? requestHeaders = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            return await Post(
                service: new Uri(service),
                endpoint: endpoint,
                credentials: credentials,
                body: body,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                requestHeaders: requestHeaders,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A helper class to perform HTTP requests against an AT Proto service.
    /// </summary>
    /// <typeparam name="TResult">The type of class to use when deserializing results from an AT Proto API call.</typeparam>
    public class AtProtoHttpClient<TResult> where TResult : class
    {
        // Each public method must have an overload which takes a required JsonSerializerOptions parameter, and which which does not have the parameter.
        // The overload without the parameter must be marked with [RequiresDynamicCode()] to enable AOT compilation.

        const string DPoPNonceRetryError = "use_dpop_nonce";

        private readonly ILogger<AtProtoHttpClient<TResult>> _logger;

        private readonly AtProtoHttpClientMetrics _metrics;

        private readonly ICollection<NameValueHeaderValue>? _extraRequestHeaders;

        private readonly bool _suppressProxyHeaderCheck;

        private readonly JsonSerializerOptions _jsonSerializationOptionsDefault = new(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IgnoreReadOnlyFields = false,
            IgnoreReadOnlyProperties = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
        };

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        internal AtProtoHttpClient() : this(loggerFactory: null)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
        internal AtProtoHttpClient(ILoggerFactory? loggerFactory) : this(
            serviceProxy: null,
            requestHeaders: null,
            loggerFactory: loggerFactory,
            meterFactory: null)
        {
        }


        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceProxy"/> is <see langword="null"/> or white space.</exception>
        /// <remarks>
        ///<para>Passing <see langword="null"/> as the <paramref name="serviceProxy"/> value will suppress the checks for the presence of the atproto-proxy header on requests by this instance.</para>
        /// </remarks>
        public AtProtoHttpClient(string? serviceProxy) : this(
            serviceProxy: serviceProxy,
            requestHeaders: null,
            loggerFactory: null,
            meterFactory: null)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
        /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceProxy"/> is <see langword="null"/> or white space.</exception>
        /// <remarks>
        ///<para>Passing <see langword="null"/> as the <paramref name="serviceProxy"/> value will suppress the checks for the presence of the atproto-proxy header on requests by this instance.</para>
        /// </remarks>
        public AtProtoHttpClient(string? serviceProxy, ILoggerFactory? loggerFactory) :this(
            serviceProxy: serviceProxy,
            requestHeaders: null,
            loggerFactory: loggerFactory,
            meterFactory: null)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
        /// <param name="requestHeader">An header to add to the requests this instance makes.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestHeader"/> is <see langword="null"/>.</exception>
        public AtProtoHttpClient(string serviceProxy, NameValueHeaderValue requestHeader) : this(
                serviceProxy: serviceProxy,
                requestHeaders: [requestHeader],
                loggerFactory: null,
                meterFactory: null)
        {
            ArgumentNullException.ThrowIfNull(requestHeader);
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
        /// <param name="requestHeader">An header to add to the requests this instance makes.</param>
        /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestHeader"/> is <see langword="null"/>.</exception>
        public AtProtoHttpClient(string serviceProxy, NameValueHeaderValue requestHeader, ILoggerFactory? loggerFactory) : this(
                serviceProxy : serviceProxy,
                requestHeaders: [requestHeader],
                loggerFactory: loggerFactory,
                meterFactory: null)
        {
            ArgumentNullException.ThrowIfNull(requestHeader);
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
        /// <param name="requestHeader">An header to add to the requests this instance makes.</param>
        /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
        /// <param name="meterFactory">An optional meter factory to create meters from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestHeader"/> is <see langword="null"/>.</exception>
        public AtProtoHttpClient(string serviceProxy, NameValueHeaderValue requestHeader, ILoggerFactory? loggerFactory, IMeterFactory? meterFactory) : this(
                serviceProxy: serviceProxy,
                requestHeaders: [requestHeader],
                loggerFactory: loggerFactory,
                meterFactory: meterFactory)
        {
            ArgumentNullException.ThrowIfNull(requestHeader);
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
        /// <param name="requestHeaders">Optional headers to add to the requests this instance makes.</param>
        public AtProtoHttpClient(string serviceProxy, ICollection<NameValueHeaderValue>? requestHeaders) : this(
                serviceProxy: serviceProxy,
                requestHeaders: requestHeaders,
                loggerFactory: null,
                meterFactory: null)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
        /// <param name="requestHeaders">Optional headers to add to the requests this instance makes.</param>
        /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
        public AtProtoHttpClient(string serviceProxy,
            ICollection<NameValueHeaderValue>? requestHeaders,
            ILoggerFactory? loggerFactory) : this(
                serviceProxy: serviceProxy,
                requestHeaders: requestHeaders,
                loggerFactory: loggerFactory,
                meterFactory: null)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
        /// <param name="loggerFactory">An optional logger factory to create loggers from.</param>
        /// <param name="meterFactory">An optional meter factory to create meters from.</param>
        public AtProtoHttpClient(
            string? serviceProxy,
            ILoggerFactory? loggerFactory,
            IMeterFactory? meterFactory) : this(
                serviceProxy: serviceProxy,
                requestHeaders: null,
                loggerFactory: loggerFactory,
                meterFactory: meterFactory)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="serviceProxy">The service a PDS should proxy the request to.</param>
        /// <param name="requestHeaders">Optional headers to add to the requests this instance makes.</param>
        /// <param name="loggerFactory">An optional logger factory to create loggers from.</param>
        /// <param name="meterFactory">An optional meter factory to create meters from.</param>
        public AtProtoHttpClient(
            string? serviceProxy,
            ICollection<NameValueHeaderValue>? requestHeaders,
            ILoggerFactory? loggerFactory,
            IMeterFactory? meterFactory) 
        {
            requestHeaders ??= [];

            if (serviceProxy is not null)
            {
                if (_extraRequestHeaders == null)
                {
                    _extraRequestHeaders = [new("atproto-proxy", serviceProxy)];
                }
                else
                {
                    _extraRequestHeaders.Add(new("atproto-proxy", serviceProxy));
                }
            }
            else
            {
                _suppressProxyHeaderCheck = true;
            }

            if (_extraRequestHeaders is null)
            {
                _extraRequestHeaders = [.. requestHeaders];
            }
            else
            {
                foreach (NameValueHeaderValue header in requestHeaders)
                {
                    _extraRequestHeaders.Add(header);
                }
            }

            loggerFactory ??= NullLoggerFactory.Instance;
            _logger = loggerFactory.CreateLogger<AtProtoHttpClient<TResult>>();

            _metrics = new AtProtoHttpClientMetrics(meterFactory);
        }

        /// <summary>
        /// Gets or sets a function called when a request is about to be sent.
        /// </summary>
        public Func<HttpRequestMessage, CancellationToken, Task> OnSendingRequest { get; set; } = (requestMessage, cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Gets or sets a function called when a response has been received.
        /// </summary>
        public Func<HttpResponseMessage, CancellationToken, Task> OnResponseReceived { get; set; } = (responseMessage, cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Performs an unauthenticated GET request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        [RequiresDynamicCode("Use a Get overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a Get overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<TResult>> Get(
            Uri service,
            string endpoint,
            HttpClient httpClient,
            CancellationToken cancellationToken = default)
        {
            return await Get(
                service: service,
                endpoint: endpoint,
                credentials: null,
                onCredentialsUpdated: null,
                httpClient: httpClient,
                subscribedLabelers: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs an unauthenticated GET request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresDynamicCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<TResult>> Get(
            Uri service,
            string endpoint,
            HttpClient httpClient,
            JsonSerializerOptions jsonSerializerOptions,
            CancellationToken cancellationToken = default)
        {
            return await Get(
                service: service,
                endpoint: endpoint,
                credentials: null,
                onCredentialsUpdated: null,
                httpClient: httpClient,
                subscribedLabelers: null,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a GET request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to use when calling <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="requestHeaders">A collection of HTTP headers to send with the request.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient"/>is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="endpoint"/> is <see langword="null"/> or empty.</exception>
        [RequiresDynamicCode("Use a Get overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a Get overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<TResult>> Get(
            Uri service,
            string endpoint,
            AtProtoCredential? credentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ICollection<NameValueHeaderValue>? requestHeaders = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrEmpty(endpoint);
            ArgumentNullException.ThrowIfNull(httpClient);

            _metrics.GetRequests.Add(1);
            return await MakeRequest<EmptyRequestBody>(
                service: service,
                endpoint: endpoint,
                record: null,
                httpMethod: HttpMethod.Get,
                requestHeaders: MergeRequestHeaders(requestHeaders),
                contentHeaders: null,
                credentials: credentials,
                httpClient: httpClient,
                retry: true,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                jsonSerializerOptions: _jsonSerializationOptionsDefault,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a GET request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to use when calling <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="requestHeaders">A collection of HTTP headers to send with the request.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="httpClient"/> or <paramref name="jsonSerializerOptions"/>is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="endpoint"/> is <see langword="null"/> or empty.</exception>
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresDynamicCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<TResult>> Get(
            Uri service,
            string endpoint,
            AtProtoCredential? credentials,
            HttpClient httpClient,
            JsonSerializerOptions jsonSerializerOptions,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ICollection<NameValueHeaderValue>? requestHeaders = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrEmpty(endpoint);
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(jsonSerializerOptions);

            _metrics.GetRequests.Add(1);
            return await MakeRequest<EmptyRequestBody>(
                service: service,
                endpoint: endpoint,
                record: null,
                httpMethod: HttpMethod.Get,
                requestHeaders: MergeRequestHeaders(requestHeaders),
                contentHeaders: null,
                credentials: credentials,
                httpClient: httpClient,
                retry: true,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/> with no request body.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to use when calling <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the provided have been updated during the HTTP POST.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        [RequiresDynamicCode("Use a Post overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a Post overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<TResult>> Post(
            Uri service,
            string endpoint,
            AtProtoCredential? credentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            return await Post<EmptyRequestBody>(
                service: service,
                endpoint: endpoint,
                record: null,
                requestHeaders: _extraRequestHeaders,
                credentials: credentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/> with no request body.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to use when calling <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the provided have been updated during the HTTP POST.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresDynamicCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<TResult>> Post(
            Uri service,
            string endpoint,
            AtProtoCredential? credentials,
            HttpClient httpClient,
            JsonSerializerOptions jsonSerializerOptions,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            return await Post<EmptyRequestBody>(
                service: service,
                endpoint: endpoint,
                record: null,
                requestHeaders: _extraRequestHeaders,
                credentials: credentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs an anonymous POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <typeparam name="TRecord">The type of the record to post.</typeparam>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="record">An optional object to serialize to JSON and send as the request body.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        [RequiresDynamicCode("Use a Post overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a the Post overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<TResult>> Post<TRecord>(
            Uri service,
            string endpoint,
            TRecord? record,
            HttpClient httpClient,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            return await Post(
                service,
                endpoint,
                record,
                requestHeaders: null,
                credentials: null,
                httpClient: httpClient,
                onCredentialsUpdated: null,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs an anonymous POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <typeparam name="TRecord">The type of the record to post.</typeparam>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="record">An optional object to serialize to JSON and send as the request body.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresDynamicCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<TResult>> Post<TRecord>(
            Uri service,
            string endpoint,
            TRecord? record,
            HttpClient httpClient,
            JsonSerializerOptions jsonSerializerOptions,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            return await Post(
                service,
                endpoint,
                record,
                requestHeaders: null,
                credentials: null,
                httpClient: httpClient,
                onCredentialsUpdated: null,
                subscribedLabelers: subscribedLabelers,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <typeparam name="TRecord">The type of the record to post.</typeparam>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="record">An optional object to serialize to JSON and send as the request body.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to use when calling <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the provided have been updated during the HTTP POST.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        [RequiresDynamicCode("Use a Post overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a Post overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<TResult>> Post<TRecord>(
            Uri service,
            string endpoint,
            TRecord? record,
            AtProtoCredential credentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            return await Post(
                service,
                endpoint,
                record,
                requestHeaders: null,
                credentials: credentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <typeparam name="TRecord">The type of the record to post.</typeparam>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="record">An optional object to serialize to JSON and send as the request body.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to use when calling <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the provided have been updated during the HTTP POST.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresDynamicCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<TResult>> Post<TRecord>(
            Uri service,
            string endpoint,
            TRecord? record,
            AtProtoCredential credentials,
            HttpClient httpClient,
            JsonSerializerOptions jsonSerializerOptions,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            return await Post(
                service,
                endpoint,
                record,
                requestHeaders: null,
                credentials: credentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <typeparam name="TRecord">The type of the class to post.</typeparam>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="record">An optional record to serialize to JSON and send as the request body.</param>
        /// <param name="requestHeaders">A collection of HTTP headers to send with the request.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to use when calling <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the provided have been updated during the HTTP POST.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="endpoint"/> is <see langword="null"/> or empty.</exception>
        [RequiresDynamicCode("Use a Post overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a Post overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<TResult>> Post<TRecord>(
            Uri service,
            string endpoint,
            TRecord? record,
            ICollection<NameValueHeaderValue>? requestHeaders,
            AtProtoCredential? credentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrEmpty(endpoint);
            ArgumentNullException.ThrowIfNull(httpClient);

            _metrics.PostRequests.Add(1);
            return await MakeRequest(
                service: service,
                endpoint: endpoint,
                record: record,
                httpMethod: HttpMethod.Post,
                requestHeaders: MergeRequestHeaders(requestHeaders),
                contentHeaders: null,
                credentials: credentials,
                httpClient: httpClient,
                retry: true,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                jsonSerializerOptions: _jsonSerializationOptionsDefault,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <typeparam name="TRecord">The type of the class to post.</typeparam>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="record">An optional record to serialize to JSON and send as the request body.</param>
        /// <param name="requestHeaders">A collection of HTTP headers to send with the request.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to use when calling <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the provided have been updated during the HTTP POST.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="httpClient"/> or <paramref name="jsonSerializerOptions"/>is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="endpoint"/> is <see langword="null"/> or empty.</exception>
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresDynamicCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<TResult>> Post<TRecord>(
            Uri service,
            string endpoint,
            TRecord? record,
            ICollection<NameValueHeaderValue>? requestHeaders,
            AtProtoCredential? credentials,
            HttpClient httpClient,
            JsonSerializerOptions jsonSerializerOptions,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrEmpty(endpoint);
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(jsonSerializerOptions);

            _metrics.PostRequests.Add(1);
            return await MakeRequest(
                service: service,
                endpoint: endpoint,
                record: record,
                httpMethod: HttpMethod.Post,
                requestHeaders: MergeRequestHeaders(requestHeaders),
                contentHeaders: null,
                credentials: credentials,
                httpClient: httpClient,
                retry: true,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a blob record on the supplied <paramref name="service"/> against the specified <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="blob">The blob to send as the request body.</param>
        /// <param name="requestHeaders">A collection of HTTP headers to send with the request.</param>
        /// <param name="contentHeaders">A collection of HTTP content headers to send with the request content.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to use when calling <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="blob"/> is an empty array.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="credentials"/> is <see langword="null"/>.</exception>
        [RequiresDynamicCode("Use a PostBlob overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a PostBlob overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<TResult>> PostBlob(
            Uri service,
            string endpoint,
            byte[] blob,
            ICollection<NameValueHeaderValue>? requestHeaders,
            ICollection<NameValueHeaderValue>? contentHeaders,
            AtProtoCredential credentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrEmpty(endpoint);

            ArgumentNullException.ThrowIfNull(blob);
            if (blob.Length == 0)
            {
                throw new ArgumentException("Blob cannot be empty.", nameof(blob));
            }

            ArgumentNullException.ThrowIfNull(credentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (credentials is not IAccessCredential)
            {
                throw new ArgumentException("credentials must have access credential", nameof(credentials));
            }

            _metrics.PostRequests.Add(1);
            _metrics.CreateBlob.Add(1);

            return await MakeRequest(
                service: service,
                endpoint: endpoint,
                record: blob,
                httpMethod: HttpMethod.Post,
                requestHeaders: MergeRequestHeaders(requestHeaders),
                contentHeaders: contentHeaders,
                credentials: credentials,
                httpClient: httpClient,
                retry: true,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: null,
                jsonSerializerOptions: _jsonSerializationOptionsDefault,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a blob record on the supplied <paramref name="service"/> against the specified <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="blob">The blob to send as the request body.</param>
        /// <param name="requestHeaders">A collection of HTTP headers to send with the request.</param>
        /// <param name="contentHeaders">A collection of HTTP content headers to send with the request content.</param>
        /// <param name="credentials">The <see cref="AtProtoCredential"/> to use when calling <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="blob"/> is an empty array.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="credentials"/> is <see langword="null"/>.</exception>
        [RequiresUnreferencedCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresDynamicCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<TResult>> PostBlob(
            Uri service,
            string endpoint,
            byte[] blob,
            ICollection<NameValueHeaderValue>? requestHeaders,
            ICollection<NameValueHeaderValue>? contentHeaders,
            AtProtoCredential credentials,
            HttpClient httpClient,
            JsonSerializerOptions jsonSerializerOptions,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrEmpty(endpoint);

            ArgumentNullException.ThrowIfNull(blob);
            if (blob.Length == 0)
            {
                throw new ArgumentException("Blob cannot be empty.", nameof(blob));
            }

            ArgumentNullException.ThrowIfNull(credentials);
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(jsonSerializerOptions);

            if (credentials is not IAccessCredential)
            {
                throw new ArgumentException("credentials must have access credential", nameof(credentials));
            }

            _metrics.PostRequests.Add(1);
            _metrics.CreateBlob.Add(1);

            return await MakeRequest(
                service: service,
                endpoint: endpoint,
                record: blob,
                httpMethod: HttpMethod.Post,
                requestHeaders: MergeRequestHeaders(requestHeaders),
                contentHeaders: contentHeaders,
                credentials: credentials,
                httpClient: httpClient,
                retry: true,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: null,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        [SuppressMessage("Major Code Smell", "S108:Nested blocks of code should not be left empty", Justification = "Catching unexpected exceptions in error handling, so as to return as much as can be returned.")]
        private static async Task<AtErrorDetail> ExtractErrorDetailFromResponse(
            HttpRequestMessage request,
            HttpResponseMessage responseMessage,
            CancellationToken cancellationToken = default)
        {
            AtErrorDetail? errorDetail = new()
            {
                Instance = request.RequestUri,
                HttpMethod = request.Method
            };

            string responseContent = await responseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            errorDetail.RawContent = responseContent;

            if (responseMessage.Content.Headers.ContentType is not null &&
                responseMessage.Content.Headers.ContentType.MediaType is not null &&
                responseMessage.Content.Headers.ContentType.MediaType.Equals(MediaTypeNames.Application.Json, StringComparison.OrdinalIgnoreCase) &&
                responseContent is not null &&
                responseContent.Length > 0)
            {
                try
                {
                    AtErrorDetail? responseAtErrorDetail = JsonSerializer.Deserialize<AtErrorDetail>(
                        responseContent,
                        SourceGenerationContext.Default.AtErrorDetail);

                    if (responseAtErrorDetail is not null)
                    {
                        errorDetail.Error = responseAtErrorDetail.Error;
                        errorDetail.Message = responseAtErrorDetail.Message;
                        errorDetail.ExtensionData = responseAtErrorDetail.ExtensionData;
                    }
                }
                catch (NotSupportedException) { }
                catch (JsonException) { }
                catch (ArgumentNullException) { }
            }

            return errorDetail;
        }

        private static RateLimit? ExtractRateLimitFromResponse(HttpResponseHeaders responseHeaders)
        {
            if (!responseHeaders.TryGetValues("ratelimit-limit", out IEnumerable<string>? limitHeaderValues) ||
                !responseHeaders.TryGetValues("ratelimit-remaining", out IEnumerable<string>? remainingValues) ||
                !responseHeaders.TryGetValues("ratelimit-reset", out IEnumerable<string>? resetValues) ||
                !responseHeaders.TryGetValues("ratelimit-policy", out IEnumerable<string>? limitPolicyValues))
            {
                return null;
            }

            string? limitHeaderValue = limitHeaderValues.FirstOrDefault();
            string? remainingHeaderValue = remainingValues.FirstOrDefault();
            string? resetHeaderValue = resetValues.FirstOrDefault();
            string? policyHeaderValue = limitPolicyValues.FirstOrDefault();

            if (limitHeaderValue is null ||
                remainingHeaderValue is null ||
                resetHeaderValue is null ||
                string.IsNullOrEmpty(policyHeaderValue))
            {
                return null;
            }

            if (!int.TryParse(limitHeaderValue, out int limit) ||
               !int.TryParse(remainingHeaderValue, out int remaining) ||
               !long.TryParse(resetHeaderValue, out long reset))
            {
                return null;
            }

            if (!policyHeaderValue.Contains(";w=", StringComparison.Ordinal))
            {
                return null;
            }

            string[] policyParts = policyHeaderValue.Split(";w=");

            if (policyParts.Length != 2 ||
                !int.TryParse(policyParts[0], out int readLimit) ||
                !int.TryParse(policyParts[1], out int writeLimit))
            {
                return null;
            }

            return new RateLimit(limit, remaining, reset, readLimit, writeLimit);
        }


        private static void SetRequestHeaders(
            HttpRequestMessage httpRequestMessage,
            HttpClient httpClient,
            IEnumerable<Did>? subscribedLabelers = null,
            ICollection<NameValueHeaderValue>? headerValues = null)
        {
            // Because we're using HttpRequestMessage.SendAsync none of the useful default configuration on the httpClient comes through.
            // So copy the ones we care most about into the request message.
            httpRequestMessage.Version = httpClient.DefaultRequestVersion;
            httpRequestMessage.VersionPolicy = httpClient.DefaultVersionPolicy;

            foreach (KeyValuePair<string, IEnumerable<string>> header in httpClient.DefaultRequestHeaders)
            {
                httpRequestMessage.Headers.Add(header.Key, header.Value);
            }

            // Force the response to be json.
            httpRequestMessage.Headers.Accept.Clear();
            httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

            if (subscribedLabelers is not null)
            {
                List<string> labelerIdentifiers = [.. subscribedLabelers];

                if (labelerIdentifiers.Count != 0)
                {
                    httpRequestMessage.Headers.Add("atproto-accept-labelers", labelerIdentifiers);
                }
            }

            if (headerValues is not null)
            {
                foreach (NameValueHeaderValue headerValue in headerValues)
                {
                    httpRequestMessage.Headers.TryAddWithoutValidation(headerValue.Name, headerValue.Value);
                }
            }
        }

        private static void SetContentHeaders(
            HttpRequestMessage httpRequestMessage,
            ICollection<NameValueHeaderValue>? headerValues = null)
        {
            if (headerValues is not null && httpRequestMessage.Content is not null)
            {
                foreach (NameValueHeaderValue headerValue in headerValues)
                {
                    httpRequestMessage.Content.Headers.TryAddWithoutValidation(headerValue.Name, headerValue.Value);
                }
            }
        }

        private void RaiseCredentialsUpdatedOnDPoPNonceChange(
            AtProtoCredential? credentials,
            HttpRequestMessage httpRequestMessage,
            HttpResponseMessage httpResponseMessage,
            Action<AtProtoCredential>? credentialsUpdated)
        {
            if (credentials is null || credentialsUpdated is null || (credentials is not IAccessCredential && credentials is not DPoPRevokeCredentials))
            {
                return;
            }

            if (credentials is IDPoPBoundCredential dPoPBoundCredential &&
                httpResponseMessage.Headers.ContainsDPoPNonce())
            {
                string? returnedDPoPNonce = httpResponseMessage.Headers.DPoPNonce();

                if (returnedDPoPNonce is not null &&
                    !string.Equals(dPoPBoundCredential.DPoPNonce, returnedDPoPNonce, StringComparison.Ordinal))
                {
                    Logger.AtProtoClientDetectedDPoPNonceChanged(_logger, httpRequestMessage.RequestUri!, httpRequestMessage.Method);

                    dPoPBoundCredential.DPoPNonce = returnedDPoPNonce;

                    credentialsUpdated(credentials);
                }
            }
        }

        private ICollection<NameValueHeaderValue>? MergeRequestHeaders(ICollection<NameValueHeaderValue>? requestHeaders)
        {
            if (requestHeaders is null && _extraRequestHeaders is null)
            {
                return null;
            }

            if (requestHeaders is null && _extraRequestHeaders is not null)
            {
                return _extraRequestHeaders;
            }

            if (requestHeaders is not null && _extraRequestHeaders is null)
            {
                return requestHeaders;
            }

            if (requestHeaders is not null && _extraRequestHeaders is not null)
            {
                foreach (NameValueHeaderValue header in _extraRequestHeaders!)
                {
                    if (requestHeaders.Any(h => h.Name.Equals(header.Name, StringComparison.OrdinalIgnoreCase)) && header.Value != null)
                    {
                        continue;
                    }

                    if (requestHeaders.Any(h => h.Name.Equals(header.Name, StringComparison.OrdinalIgnoreCase) && h.Value == header.Value))
                    {
                        continue;
                    }

                    requestHeaders.Add(header);
                }

                return requestHeaders;
            }

            return requestHeaders;
        }

        [RequiresUnreferencedCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresDynamicCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        private async Task<AtProtoHttpResult<TResult>> MakeRequest<TRecord>(
            Uri service,
            string endpoint,
            TRecord? record,
            JsonSerializerOptions jsonSerializerOptions,
            HttpMethod httpMethod,
            ICollection<NameValueHeaderValue>? requestHeaders,
            ICollection<NameValueHeaderValue>? contentHeaders,
            AtProtoCredential? credentials,
            HttpClient httpClient,
            bool retry = false,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            long startTimestamp = Stopwatch.GetTimestamp();

            try
            {
                // The Bluesky 2025 Protocol roadmap announced that the default PDS implementation would stop forwarding app.bsky.* endpoints to the the Bluesky API server
                // at some future point, so log a warning if a request is made to any API endpoint not that is not a PDS endpoint (com.atproto.*).
                // https://docs.bsky.app/blog/2025-protocol-roadmap-spring
                if (!_suppressProxyHeaderCheck &&
                    !endpoint.StartsWith("/xrpc/com.atproto", StringComparison.Ordinal) &&
                    !endpoint.StartsWith("/oauth/", StringComparison.Ordinal) &&
                    (requestHeaders is null ||
                    !requestHeaders.Any(nameValueHeaderValue => nameValueHeaderValue.Name.Equals("atproto-proxy", StringComparison.Ordinal))))
                {
                    Logger.AtProtoHttpClientMakingCallToNoneComAtProtoEndpointWithoutProxyHeader(_logger, endpoint);
                }

                using (var httpRequestMessage = new HttpRequestMessage(httpMethod, new Uri(service, endpoint)))
                {
                    SetRequestHeaders(httpRequestMessage, httpClient, subscribedLabelers, _extraRequestHeaders);

                    // Add authentication headers
                    credentials?.SetAuthenticationHeaders(httpRequestMessage);

                    // Request bodies are, in theory, allowed for all methods except TRACE, however they are not commonly used except in PUT, POST and PATCH,
                    // so limit bodies to those methods. AtProto only accepts, for now, GET and POST methods anyway.
                    if (record is not null &&
                        record is not EmptyRequestBody &&
                        (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Patch || httpMethod == HttpMethod.Put))
                    {
                        switch (record)
                        {
                            case HttpContent httpContent:
                                httpRequestMessage.Content = httpContent;
                                break;

                            case byte[] blob:
                                httpRequestMessage.Content = new ByteArrayContent(blob);
                                break;

                            case string stringContent:
                                httpRequestMessage.Content = new StringContent(stringContent, Encoding.UTF8, MediaTypeNames.Application.Json);
                                break;

                            default:
                                string content = JsonSerializer.Serialize(record, jsonSerializerOptions);

                                httpRequestMessage.Content = new StringContent(content, Encoding.UTF8, MediaTypeNames.Application.Json);
                                break;
                        }

                        if (contentHeaders is not null)
                        {
                            SetContentHeaders(httpRequestMessage, contentHeaders);
                        }
                    }

                    if (OnSendingRequest is not null)
                    {
                        await OnSendingRequest(httpRequestMessage, cancellationToken).ConfigureAwait(false);
                    }

                    try
                    {
                        _metrics.RequestsSent.Add(1);
                        using (HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false))
                        {
                            _metrics.ResponsesReceived.Add(1);

                            if (OnResponseReceived is not null)
                            {
                                await OnResponseReceived(httpResponseMessage, cancellationToken).ConfigureAwait(false);
                            }

                            AtProtoHttpResult<TResult> result = new()
                            {
                                StatusCode = httpResponseMessage.StatusCode,
                                RateLimit = ExtractRateLimitFromResponse(httpResponseMessage.Headers)
                            };

                            RaiseCredentialsUpdatedOnDPoPNonceChange(credentials, httpRequestMessage, httpResponseMessage, onCredentialsUpdated);

                            if (httpResponseMessage.IsSuccessStatusCode)
                            {
                                _metrics.SuccessfulRequests.Add(1);
                                Logger.AtProtoClientRequestSucceeded
                                    (_logger, httpRequestMessage.RequestUri!, httpRequestMessage.Method);

                                if (typeof(TResult) == typeof(EmptyResponse))
                                {
                                    result.Result = new EmptyResponse() as TResult;
                                }
                                else if (typeof(TResult) == typeof(string))
                                {
                                    string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                                    result.Result = responseContent as TResult;
                                }
                                else if (typeof(TResult) == typeof(JsonNode))
                                {
                                    string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                                    result.Result = JsonNode.Parse(responseContent) as TResult;
                                }
                                else if (typeof(TResult) == typeof(JsonObject))
                                {
                                    string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                                    result.Result = JsonObject.Parse(responseContent) as TResult;
                                }
                                else if (typeof(TResult) == typeof(JsonDocument))
                                {
                                    string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                                    result.Result = JsonDocument.Parse(responseContent) as TResult;
                                }
                                else
                                {
                                    string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                                    try
                                    {
                                        result.Result = JsonSerializer.Deserialize<TResult>(
                                            responseContent,
                                            jsonSerializerOptions);
                                    }
                                    catch (JsonException ex)
                                    {
                                        _metrics.DeserializationFailures.Add(1, new KeyValuePair<string, object?>("type", typeof(TResult).FullName));
                                        Logger.AtProtoClientResponseDeserializationThrew(_logger, httpRequestMessage.RequestUri!, httpRequestMessage.Method, ex);
                                    }
                                }
                            }
                            else
                            {
                                _metrics.FailedRequests.Add(1, new KeyValuePair<string, object?>("http_status_code", (int)httpResponseMessage.StatusCode));
                                AtErrorDetail atErrorDetail = await ExtractErrorDetailFromResponse(httpRequestMessage, httpResponseMessage, cancellationToken).ConfigureAwait(false);

                                // Retry if the error returned is there has been a DPoP nonce change and we're sending a DPoP authenticated request.
                                // BadRequest comes from an authorization server, Unauthorized comes from a resource server (the PDS).

                                bool containsDPoPHeader = httpResponseMessage.Headers.ContainsDPoPNonce();

                                if (credentials is IDPoPBoundCredential _ &&
                                    retry &&
                                    containsDPoPHeader)
                                {
                                    bool isAuthorizationServerNonceError = httpResponseMessage.StatusCode == HttpStatusCode.BadRequest &&
                                        string.Equals(DPoPNonceRetryError, atErrorDetail.Error, StringComparison.OrdinalIgnoreCase);

                                    bool isAuthorizationServerNonceErrorInHeader = httpResponseMessage.StatusCode == HttpStatusCode.BadRequest &&
                                        httpResponseMessage.Headers.WwwAuthenticate is not null &&
                                        httpResponseMessage.Headers.WwwAuthenticate.Count == 1 &&
                                        httpResponseMessage.Headers.WwwAuthenticate.First().Parameter is not null &&
                                        httpResponseMessage.Headers.WwwAuthenticate.First().Scheme == "DPoP" &&
                                        httpResponseMessage.Headers.WwwAuthenticate.First().Parameter!.StartsWith("error=\"use_dpop_nonce\"", StringComparison.OrdinalIgnoreCase);

                                    bool isResourceServerNonceErrorInBody = httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized &&
                                        string.Equals(DPoPNonceRetryError, atErrorDetail.Error, StringComparison.OrdinalIgnoreCase);

                                    bool isResourceServerNonceErrorInHeader = httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized &&
                                        httpResponseMessage.Headers.WwwAuthenticate is not null &&
                                        httpResponseMessage.Headers.WwwAuthenticate.Count == 1 &&
                                        httpResponseMessage.Headers.WwwAuthenticate.First().Parameter is not null &&
                                        httpResponseMessage.Headers.WwwAuthenticate.First().Scheme == "DPoP" &&
                                        httpResponseMessage.Headers.WwwAuthenticate.First().Parameter!.StartsWith("error=\"use_dpop_nonce\"", StringComparison.OrdinalIgnoreCase);

                                    if (isAuthorizationServerNonceError ||
                                        isAuthorizationServerNonceErrorInHeader ||
                                        isResourceServerNonceErrorInBody ||
                                        isResourceServerNonceErrorInHeader)
                                    {
                                        // We have an error indicating that the DPoP nonce is invalid, and we have a new nonce in the header,
                                        // so update the nonce in the credentials and retry the request with the new nonce.

                                        string? updatedDPoPNonce = httpResponseMessage.GetDPoPNonce();

                                        if (!string.IsNullOrEmpty(updatedDPoPNonce))
                                        {
                                            // dPoP nonce was already updated in RaiseCredentialsUpdatedOnDPoPNonceChange,
                                            // but raise the event again to ensure that any credential update logic that needs to run on a nonce change runs before the retry.
                                            RaiseCredentialsUpdatedOnDPoPNonceChange(credentials, httpRequestMessage, httpResponseMessage, onCredentialsUpdated);

                                            _metrics.DPoPRetries.Add(1);

                                            // Retry
                                            return await MakeRequest(
                                                service: service,
                                                endpoint: endpoint,
                                                record: record,
                                                httpMethod: httpMethod,
                                                requestHeaders: requestHeaders,
                                                contentHeaders: contentHeaders,
                                                credentials: credentials,
                                                httpClient: httpClient,
                                                retry: false,
                                                onCredentialsUpdated: onCredentialsUpdated,
                                                subscribedLabelers: subscribedLabelers,
                                                jsonSerializerOptions: jsonSerializerOptions,
                                                cancellationToken: cancellationToken).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            Logger.AtProtoClientEncounteredDPoPNonceErrorWithoutANonceHeader(_logger, service, httpMethod);
                                        }
                                    }
                                }

                                result.AtErrorDetail = atErrorDetail;

                                if (typeof(TResult) == typeof(string))
                                {
                                    result.Result = atErrorDetail.RawContent as TResult;
                                }

                                Logger.AtProtoClientRequestFailed
                                    (_logger, httpRequestMessage.RequestUri!, httpRequestMessage.Method, httpResponseMessage.StatusCode, result.AtErrorDetail.Error, result.AtErrorDetail.Message);

                            }

                            result.HttpResponseHeaders = httpResponseMessage.Headers;

                            return result;
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        Logger.AtProtoClientRequestCancelled
                            (_logger, httpRequestMessage.RequestUri!, httpRequestMessage.Method);

                        return new AtProtoHttpResult<TResult>(null, System.Net.HttpStatusCode.OK, null);
                    }
                }
            }
            finally
            {
                _metrics.RequestDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds);
            }
        }

#pragma warning disable S2094 // Classes should not be empty
        private sealed record EmptyRequestBody
#pragma warning restore S2094 // Classes should not be empty
        {
        }
    }
}
