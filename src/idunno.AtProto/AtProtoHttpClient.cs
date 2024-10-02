// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto.Json;

namespace idunno.AtProto
{
    /// <summary>
    /// A helper class to perform HTTP requests against an AT Proto service.
    /// </summary>
    /// <typeparam name="TResult">The type of class to use when deserializing results from an AT Proto API call.</typeparam>
    public class AtProtoHttpClient<TResult> where TResult : class
    {
        /// <summary>
        /// Gets the default <see cref="JsonSerializerOptions"/> to use when deserializing JSON.
        /// </summary>
        public static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
        {
            Converters =
            {
                new AtIdentifierConverter(),
                new AtUriConverter(),
                new AtCidConverter(),
                new DidConverter(),
                new HandleConverter(),
                new NsidConverter()
            },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Performs a GET request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Get(Uri service, string endpoint, HttpClient httpClient, CancellationToken cancellationToken = default)
        {
            return await Get(service, endpoint, null, httpClient, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a GET request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="accessToken">An optional access token to send in the HTTP Authorization header to the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Get(
            Uri service,
            string endpoint,
            string? accessToken,
            HttpClient httpClient,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(httpClient);

            jsonSerializerOptions ??= DefaultJsonSerializerOptions;

            using (var getMessage = new HttpRequestMessage(HttpMethod.Get, new Uri(service, endpoint)))
            {
                getMessage.Headers.Accept.Clear();
                getMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

                if (accessToken is not null)
                {
                    getMessage.AddBearerToken(accessToken);
                }

                using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(getMessage, cancellationToken).ConfigureAwait(false);
                AtProtoHttpResult<TResult> result = new()
                {
                    StatusCode = httpResponseMessage.StatusCode
                };

                if (httpResponseMessage.IsSuccessStatusCode)
                {
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
                    string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    AtErrorDetail? atErrorDetail = JsonSerializer.Deserialize<AtErrorDetail>(
                        responseContent,
                        jsonSerializerOptions);

                    if (atErrorDetail is null)
                    {
                        result.AtErrorDetail = new AtErrorDetail();
                    }
                    else
                    {
                        result.AtErrorDetail = atErrorDetail;
                    }

                    result.AtErrorDetail.Instance = getMessage.RequestUri;
                    result.AtErrorDetail.HttpMethod = getMessage.Method;
                }
                return result;
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
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<TResult>> Post(
            Uri service,
            string endpoint,
            object? requestBody,
            string? accessToken,
            HttpClient httpClient,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await Post(service, endpoint, requestBody, null, accessToken, httpClient, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
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
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNullOrEmpty(endpoint);
            ArgumentNullException.ThrowIfNull(httpClient);

            jsonSerializerOptions ??= DefaultJsonSerializerOptions;

            using (var postMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(service, endpoint)))
            {
                postMessage.Headers.Accept.Clear();
                postMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

                if (accessToken is not null)
                {
                    postMessage.AddBearerToken(accessToken);
                }

                if (requestHeaders is not null)
                {
                    foreach (NameValueHeaderValue header in requestHeaders)
                    {
                        postMessage.Headers.TryAddWithoutValidation(header.Name, header.Value);
                    }
                }

                if (requestBody is not null)
                {
                    string content = JsonSerializer.Serialize(requestBody, jsonSerializerOptions);
                    postMessage.Content = new StringContent(content, Encoding.UTF8, MediaTypeNames.Application.Json);
                }

                using (HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(postMessage, cancellationToken).ConfigureAwait(false))
                {
                    AtProtoHttpResult<TResult> result = new()
                    {
                        StatusCode = httpResponseMessage.StatusCode
                    };

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
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
                        string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                        AtErrorDetail? atErrorDetail = JsonSerializer.Deserialize<AtErrorDetail>(
                            responseContent,
                            jsonSerializerOptions);

                        if (atErrorDetail is null)
                        {
                            result.AtErrorDetail = new AtErrorDetail();
                        }
                        else
                        {
                            result.AtErrorDetail = atErrorDetail;
                        }

                        result.AtErrorDetail.Instance = postMessage.RequestUri;
                        result.AtErrorDetail.HttpMethod = postMessage.Method;
                    }

                    return result;
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

            jsonSerializerOptions ??= DefaultJsonSerializerOptions;

            using (var postMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(service, endpoint)))
            {
                postMessage.Headers.Accept.Clear();
                postMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

                if (accessToken is not null)
                {
                    postMessage.AddBearerToken(accessToken);
                }

                postMessage.Content = new ByteArrayContent(blob);

                if (contentHeaders is not null)
                {
                    foreach (NameValueHeaderValue header in contentHeaders)
                    {
                        postMessage.Content.Headers.Add(header.Name, header.Value);
                    }
                }

                using (HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(postMessage, cancellationToken).ConfigureAwait(false))
                {
                    AtProtoHttpResult<TResult> result = new()
                    {
                        StatusCode = httpResponseMessage.StatusCode
                    };

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
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
                        string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                        AtErrorDetail? atErrorDetail = JsonSerializer.Deserialize<AtErrorDetail>(
                            responseContent,
                            jsonSerializerOptions);

                        if (atErrorDetail is null)
                        {
                            result.AtErrorDetail = new AtErrorDetail();
                        }
                        else
                        {
                            result.AtErrorDetail = atErrorDetail;
                        }

                        result.AtErrorDetail.Instance = postMessage.RequestUri;
                        result.AtErrorDetail.HttpMethod = postMessage.Method;
                    }

                    return result;
                }
            }
        }
    }
}
