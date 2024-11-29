// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto
{
    /// <summary>
    /// A helper class to perform HTTP requests against an AT Proto service.
    /// </summary>
    /// <typeparam name="TResult">The type of class to use when deserializing results from an AT Proto API call.</typeparam>
    public class AtProtoHttpClient<TResult> where TResult : class
    {
        private readonly ILogger<AtProtoHttpClient<TResult>> _logger;

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
        /// Performs an unauthenticated GET request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Get(Uri service, string endpoint, HttpClient httpClient, CancellationToken cancellationToken = default)
        {
            return await Get(
                service: service,
                endpoint: endpoint,
                accessToken: null,
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
        /// <param name="accessToken">An optional access token to send in the HTTP Authorization header to the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Get(
            Uri service,
            string endpoint,
            string? accessToken,
            HttpClient httpClient,
            IEnumerable<Did>? subscribedLabelers = null,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (jsonSerializerOptions is null)
            {
                jsonSerializerOptions = JsonSerializationDefaults.DefaultJsonSerializerOptions;
            }
            else
            {
                if (!jsonSerializerOptions.AllowOutOfOrderMetadataProperties)
                {
                    jsonSerializerOptions = new JsonSerializerOptions(jsonSerializerOptions)
                    {
                        AllowOutOfOrderMetadataProperties = true
                    };
                }
            }

            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, new Uri(service, endpoint)))
            {
                ConfigureRequest(httpRequestMessage, httpClient, accessToken, subscribedLabelers);

                try
                {
                    using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false);
                    AtProtoHttpResult<TResult> result = new()
                    {
                        StatusCode = httpResponseMessage.StatusCode,
                        RateLimit = ExtractRateLimit(httpResponseMessage.Headers)
                    };

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        Logger.AtProtoClientRequestSucceeded(_logger, httpRequestMessage.RequestUri!, httpRequestMessage.Method);

                        if (typeof(TResult) != typeof(EmptyResponse))
                        {
                            string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                            result.Result = JsonSerializer.Deserialize<TResult>(
                                responseContent,
                                jsonSerializerOptions);
                        }
                    }
                    else
                    {
                        result.AtErrorDetail = await BuildErrorDetail(httpRequestMessage, httpResponseMessage, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);

                        Logger.AtProtoClientRequestFailed
                                (_logger, httpRequestMessage.RequestUri!, httpRequestMessage.Method, httpResponseMessage.StatusCode, result.AtErrorDetail.Error, result.AtErrorDetail.Message);
                    }

                    return result;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Logger.AtProtoClientRequestCancelled
                        (_logger, httpRequestMessage.RequestUri!, httpRequestMessage.Method);

                    return new AtProtoHttpResult<TResult>(null, System.Net.HttpStatusCode.OK, null);
                }
            }
        }

        /// <summary>
        /// Performs a POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="requestBody">An optional object to serialize to JSON and send as the request body.</param>
        /// <param name="accessToken">The access token to send in the HTTP Authorization header to the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Post(
            Uri service,
            string endpoint,
            object? requestBody,
            string? accessToken,
            HttpClient httpClient,
            IEnumerable<Did>? subscribedLabelers = null,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await Post(service, endpoint, requestBody, null, accessToken, httpClient, subscribedLabelers, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="requestBody">An optional object to serialize to JSON and send as the request body.</param>
        /// <param name="accessToken">The access token to send in the HTTP Authorization header to the <paramref name="service"/>.</param>
        /// <param name="requestHeaders">A collection of HTTP headers to send with the request.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Post(
            Uri service,
            string endpoint,
            object? requestBody,
            IReadOnlyCollection<NameValueHeaderValue>? requestHeaders,
            string? accessToken,
            HttpClient httpClient,
            IEnumerable<Did>? subscribedLabelers = null,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNullOrEmpty(endpoint);
            ArgumentNullException.ThrowIfNull(httpClient);

            jsonSerializerOptions ??= JsonSerializationDefaults.DefaultJsonSerializerOptions;

            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(service, endpoint)))
            {
                ConfigureRequest(httpRequestMessage, httpClient, accessToken, subscribedLabelers);

                if (requestHeaders is not null)
                {
                    foreach (NameValueHeaderValue header in requestHeaders)
                    {
                        httpRequestMessage.Headers.TryAddWithoutValidation(header.Name, header.Value);
                    }
                }

                if (requestBody is not null)
                {
                    string content = JsonSerializer.Serialize(requestBody, jsonSerializerOptions);
                    httpRequestMessage.Content = new StringContent(content, Encoding.UTF8, MediaTypeNames.Application.Json);
                }

                try
                {
                    using (HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false))
                    {
                        AtProtoHttpResult<TResult> result = new()
                        {
                            StatusCode = httpResponseMessage.StatusCode,
                            RateLimit = ExtractRateLimit(httpResponseMessage.Headers)
                        };

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
                        }
                        else
                        {
                            result.AtErrorDetail = await BuildErrorDetail(httpRequestMessage, httpResponseMessage, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);

                            Logger.AtProtoClientRequestFailed
                                (_logger, httpRequestMessage.RequestUri!, httpRequestMessage.Method, httpResponseMessage.StatusCode, result.AtErrorDetail.Error, result.AtErrorDetail.Message);

                        }

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

        /// <summary>
        /// Creates a blob record on the supplied <paramref name="service"/> against the specified <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="blob">The blob to send as the request body.</param>
        /// <param name="contentHeaders">A collection of HTTP content headers to send with the request.</param>
        /// <param name="accessToken">The access token to send in the HTTP Authorization header to the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> PostBlob(
            Uri service,
            string endpoint,
            byte[] blob,
            IReadOnlyCollection<NameValueHeaderValue>? contentHeaders,
            string accessToken,
            HttpClient httpClient,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNullOrEmpty(endpoint);

            ArgumentNullException.ThrowIfNull(blob);
            if (blob.Length == 0)
            {
                throw new ArgumentException("Blob cannot be empty.", nameof(blob));
            }

            ArgumentNullException.ThrowIfNullOrEmpty(accessToken);
            ArgumentNullException.ThrowIfNull(httpClient);

            jsonSerializerOptions ??= JsonSerializationDefaults.DefaultJsonSerializerOptions;

            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(service, endpoint)))
            {
                ConfigureRequest(httpRequestMessage, httpClient, accessToken);

                httpRequestMessage.Content = new ByteArrayContent(blob);

                if (contentHeaders is not null)
                {
                    foreach (NameValueHeaderValue header in contentHeaders)
                    {
                        httpRequestMessage.Content.Headers.Add(header.Name, header.Value);
                    }
                }

                try
                {
                    using (HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false))
                    {
                        AtProtoHttpResult<TResult> result = new()
                        {
                            StatusCode = httpResponseMessage.StatusCode,
                            RateLimit = ExtractRateLimit(httpResponseMessage.Headers)
                        };

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
                        }
                        else
                        {
                            result.AtErrorDetail = await BuildErrorDetail(httpRequestMessage, httpResponseMessage, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);

                            Logger.AtProtoClientRequestFailed
                                (_logger, httpRequestMessage.RequestUri!, httpRequestMessage.Method, httpResponseMessage.StatusCode, result.AtErrorDetail.Error, result.AtErrorDetail.Message);
                        }

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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S108:Nested blocks of code should not be left empty", Justification = "Catching unexpected exceptions in error handling, so as to return as much as can be returned.")]
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
                        }
                    }
                    catch (NotSupportedException) { }
                    catch (JsonException) { }
                    catch (ArgumentNullException) { }
                }
            }

            return errorDetail;
        }

        private static void ConfigureRequest(HttpRequestMessage httpRequestMessage, HttpClient httpClient, string? accessToken, IEnumerable<Did>? subscribedLabelers = null)
        {
            // Because we're using HttpRequestMessage.SendAsync none of the useful default configuration on the httpClient comes through.
            // So copy the ones we care most about into the request message.

            httpRequestMessage.Version = httpClient.DefaultRequestVersion;
            httpRequestMessage.VersionPolicy = httpClient.DefaultVersionPolicy;

            foreach (KeyValuePair<string, IEnumerable<string>> header in httpClient.DefaultRequestHeaders)
            {
                httpRequestMessage.Headers.Add(header.Key, header.Value);
            }

            // Force the request to be json.
            httpRequestMessage.Headers.Accept.Clear();
            httpRequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

            if (accessToken is not null)
            {
                httpRequestMessage.AddBearerToken(accessToken);
            }

            if (subscribedLabelers is not null)
            {
                List<string> labelerIdentifiers = new();
                foreach (Did did in subscribedLabelers)
                {
                    labelerIdentifiers.Add(did);
                }

                if (labelerIdentifiers.Count != 0)
                {
                    httpRequestMessage.Headers.Add("atproto-accept-labelers", labelerIdentifiers);
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

            if(!int.TryParse(limitHeaderValue, out int limit) || 
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
    }
}
