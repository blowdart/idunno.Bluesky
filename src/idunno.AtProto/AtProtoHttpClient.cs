// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using IdentityModel.OidcClient.DPoP;

using idunno.AtProto.Authentication;

namespace idunno.AtProto
{
    /// <summary>
    /// A helper class to perform HTTP requests against an AT Proto service.
    /// </summary>
    /// <typeparam name="TResult">The type of class to use when deserializing results from an AT Proto API call.</typeparam>
    public class AtProtoHttpClient<TResult> where TResult : class
    {
        const string DPoPNonceRetryError = "use_dpop_nonce";

        private readonly ILogger<AtProtoHttpClient<TResult>> _logger;

        private readonly ICollection<NameValueHeaderValue>? _extraHeaders;

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
        public AtProtoHttpClient(ILoggerFactory? loggerFactory = null)
        {
            loggerFactory ??= NullLoggerFactory.Instance;
            _logger = loggerFactory.CreateLogger<AtProtoHttpClient<TResult>>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="serviceProxy">An optional headers to add to the requests this instance makes.</param>
        /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceProxy"/> is null or white space./</exception>
        public AtProtoHttpClient(string serviceProxy, ILoggerFactory? loggerFactory = null) : this(loggerFactory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceProxy);

            _extraHeaders = [new("atproto-proxy", serviceProxy)];
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="header">An header to add to the requests this instance makes.</param>
        /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="header"/> is null/</exception>
        public AtProtoHttpClient(NameValueHeaderValue header, ILoggerFactory? loggerFactory = null) : this(loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(header);

            _extraHeaders = [header];
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoHttpClient{TResult}"/>
        /// </summary>
        /// <param name="headers">Headers to add to the requests this instance makes.</param>
        /// <param name="loggerFactory">An optional logger factory to create loggers from/</param>
        public AtProtoHttpClient(ICollection<NameValueHeaderValue> headers, ILoggerFactory? loggerFactory = null) : this(loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(headers);
            ArgumentOutOfRangeException.ThrowIfZero(headers.Count);

            _extraHeaders = [.. headers];
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
                jsonSerializerOptions: null,
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
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Get(
            Uri service,
            string endpoint,
            AtProtoCredential? credentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ICollection<NameValueHeaderValue>? requestHeaders = null,
            IEnumerable<Did>? subscribedLabelers = null,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrEmpty(endpoint);
            ArgumentNullException.ThrowIfNull(httpClient);

            return await MakeRequest<EmptyRequestBody>(
                service: service,
                endpoint: endpoint,
                record: null,
                httpMethod: HttpMethod.Get,
                requestHeaders: requestHeaders,
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
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Post(
            Uri service,
            string endpoint,
            AtProtoCredential? credentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            IEnumerable<Did>? subscribedLabelers = null,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await Post<EmptyRequestBody>(
                service: service,
                endpoint: endpoint,
                record: null,
                requestHeaders: null,
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
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Post<TRecord>(
            Uri service,
            string endpoint,
            TRecord? record,
            HttpClient httpClient,
            IEnumerable<Did>? subscribedLabelers = null,
            JsonSerializerOptions? jsonSerializerOptions = null,
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
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Post<TRecord>(
            Uri service,
            string endpoint,
            TRecord? record,
            AtProtoCredential credentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            IEnumerable<Did>? subscribedLabelers = null,
            JsonSerializerOptions? jsonSerializerOptions = null,
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
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Post<TRecord>(
            Uri service,
            string endpoint,
            TRecord? record,
            ICollection<NameValueHeaderValue>? requestHeaders,
            AtProtoCredential? credentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            IEnumerable<Did>? subscribedLabelers = null,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrEmpty(endpoint);
            ArgumentNullException.ThrowIfNull(httpClient);

            return await MakeRequest(
                service: service,
                endpoint: endpoint,
                record: record,
                httpMethod: HttpMethod.Post,
                requestHeaders: requestHeaders,
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
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="blob"/> is an empty array.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="credentials"/> is null.</exception>
        public async Task<AtProtoHttpResult<TResult>> PostBlob(
            Uri service,
            string endpoint,
            byte[] blob,
            ICollection<NameValueHeaderValue>? requestHeaders,
            ICollection<NameValueHeaderValue>? contentHeaders,
            AtProtoCredential credentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            JsonSerializerOptions? jsonSerializerOptions = null,
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

            return await MakeRequest(
                service: service,
                endpoint: endpoint,
                record: blob,
                httpMethod: HttpMethod.Post,
                requestHeaders: requestHeaders,
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
        private static async Task<AtErrorDetail> BuildErrorDetail(
            HttpRequestMessage request,
            HttpResponseMessage responseMessage,
            JsonSerializerOptions jsonSerializerOptions,
            CancellationToken cancellationToken = default)
        {
            AtErrorDetail? errorDetail = new()
            {
                Instance = request.RequestUri,
                HttpMethod = request.Method
            };

            if (responseMessage.Content.Headers.ContentType is not null &&
                responseMessage.Content.Headers.ContentType.MediaType is not null &&
                responseMessage.Content.Headers.ContentType.MediaType.Equals(MediaTypeNames.Application.Json, StringComparison.OrdinalIgnoreCase))
            {
                string responseContent = await responseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (responseContent is not null && responseContent.Length > 0)
                {
                    try
                    {
                        AtErrorDetail? responseAtErrorDetail = JsonSerializer.Deserialize<AtErrorDetail>(
                            responseContent,
                            jsonSerializerOptions);

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
            }

            return errorDetail;
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

        private static RateLimit? ExtractRateLimit(HttpResponseHeaders responseHeaders)
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

        private void NotifyOnDPoPNonceChange(
            AtProtoCredential? credentials,
            HttpRequestMessage httpRequestMessage,
            HttpResponseMessage httpResponseMessage,
            Action<AtProtoCredential>? credentialsUpdated)
        {
            if (credentials is null || credentialsUpdated is null || credentials is not IAccessCredential)
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

        private async Task<AtProtoHttpResult<TResult>> MakeRequest<TRecord>(
            Uri service,
            string endpoint,
            TRecord? record,
            HttpMethod httpMethod,
            ICollection<NameValueHeaderValue>? requestHeaders,
            ICollection<NameValueHeaderValue>? contentHeaders,
            AtProtoCredential? credentials,
            HttpClient httpClient,
            bool retry = false,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            IEnumerable<Did>? subscribedLabelers = null,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            jsonSerializerOptions ??= JsonSerializationDefaults.DefaultJsonSerializerOptions;

            using (var httpRequestMessage = new HttpRequestMessage(httpMethod, new Uri(service, endpoint)))
            {
                SetRequestHeaders(httpRequestMessage, httpClient, subscribedLabelers, _extraHeaders);

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
                    using (HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false))
                    {
                        if (OnResponseReceived is not null)
                        {
                            await OnResponseReceived(httpResponseMessage, cancellationToken).ConfigureAwait(false);
                        }

                        AtProtoHttpResult<TResult> result = new()
                        {
                            StatusCode = httpResponseMessage.StatusCode,
                            RateLimit = ExtractRateLimit(httpResponseMessage.Headers)
                        };

                        NotifyOnDPoPNonceChange(credentials, httpRequestMessage, httpResponseMessage, onCredentialsUpdated);

                        if (httpResponseMessage.IsSuccessStatusCode)
                        {
                            Logger.AtProtoClientRequestSucceeded
                                (_logger, httpRequestMessage.RequestUri!, httpRequestMessage.Method);

                            if (typeof(TResult) != typeof(EmptyResponse))
                            {
                                string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                                result.Result = JsonSerializer.Deserialize<TResult>(
                                    responseContent,
                                    jsonSerializerOptions);
                            }
                            else
                            {
                                result.Result = new EmptyResponse() as TResult;
                            }
                        }
                        else
                        {
                            AtErrorDetail atErrorDetail = await BuildErrorDetail(httpRequestMessage, httpResponseMessage, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);

                            // Retry if the error returned is there has been a DPoP nonce change and we're sending a DPoP authenticated request.
                            if (credentials is IDPoPBoundCredential dPoPBoundCredential &&
                                string.Equals(DPoPNonceRetryError, atErrorDetail.Error, StringComparison.OrdinalIgnoreCase) &&
                                retry)
                            {
                                // Update nonce

                                string? updatedDPoPNonce = httpResponseMessage.GetDPoPNonce();

                                if (!string.IsNullOrEmpty(updatedDPoPNonce))
                                {
                                    dPoPBoundCredential.DPoPNonce = updatedDPoPNonce;

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

                            }

                            result.AtErrorDetail = atErrorDetail;

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

#pragma warning disable S2094 // Classes should not be empty
        private sealed record EmptyRequestBody
#pragma warning restore S2094 // Classes should not be empty
        {
        }
    }
}
