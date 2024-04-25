// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

using idunno.AtProto.Json;
using idunno.AtProto.Server;

namespace idunno.AtProto
{
    public class AtProtoRequest<TResult> where TResult : class
    {
        private static readonly object s_syncLock = new ();
        private static HttpClient? s_httpClient;

        /// <summary>
        /// Gets a new instance of an <see cref="HttpClient"/> configured for accepting only json responses
        /// and with a user agent built from the library assembly name and version.
        /// </summary>
        /// <returns>A pre-configured instance of <see cref="HttpClient"/>.</returns>
        private static HttpClient GetHttpClient(HttpClientHandler? httpClientHandler)
        {
            if (s_httpClient is null)
            {
                lock (s_syncLock)
                {
                    if (s_httpClient is null)
                    {
                        s_httpClient = httpClientHandler is null ? new HttpClient() : new HttpClient(httpClientHandler, disposeHandler: false);

                        s_httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
                        s_httpClient.DefaultRequestVersion = new Version(2, 0);
                        s_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("idunno.AtProto/" + typeof(Session).Assembly.GetName().Version);
                        s_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
                    }
                }
            }

            return s_httpClient;
        }

        /// <summary>
        /// Performs a GET request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of class to deserialize the results into.</typeparam>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult"/> wrapping the <typeparamref name="TResult"/>.</returns>
        public async Task<HttpResult<TResult>> Get(Uri service, string endpoint, HttpClientHandler? httpClientHandler, CancellationToken cancellationToken)
        {
            return await Get(service, endpoint, null, httpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a GET request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of class to deserialize the results into.</typeparam>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="accessToken">An option access token to send in the HTTP Authorization header to the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult"/> wrapping the <typeparamref name="TResult"/>.</returns>
        public async Task<HttpResult<TResult>> Get(Uri service, string endpoint, string? accessToken, HttpClientHandler? httpClientHandler, CancellationToken cancellationToken)
        {
            HttpClient httpClient = GetHttpClient(httpClientHandler);

            using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, new Uri(service, endpoint)))
            {
                if (accessToken is not null)
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                }

                using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
                HttpResult<TResult> result = new()
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
                            PreconfiguredSerializerOptions.Options);
                    }
                }
                else
                {
                    string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    result.Error = JsonSerializer.Deserialize<AtErrorDetail>(
                        responseContent,
                        PreconfiguredSerializerOptions.Options);

                    result.Error ??= new AtErrorDetail();

                    result.Error.Instance = requestMessage.RequestUri;
                    result.Error.HttpMethod = requestMessage.Method;
                }
                return result;
            }
        }

        /// <summary>
        /// Performs a POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of class to deserialize the results into.</typeparam>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <returns>An <see cref="HttpResult"/> wrapping the <typeparamref name="TResult"/>.</returns>
        public async Task<HttpResult<TResult>> Post(Uri service, string endpoint, HttpClientHandler? httpClientHandler, CancellationToken cancellationToken)
        {
            return await Post(service, endpoint, null, httpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of class to deserialize the results into.</typeparam>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="requestBody">An optional object to serialize to JSON and send as the request body.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult"/> wrapping the <typeparamref name="TResult"/>.</returns>
        public async Task<HttpResult<TResult>> Post(Uri service, string endpoint, object? requestBody, HttpClientHandler? httpClientHandler, CancellationToken cancellationToken)
        {
            return await Post(service, endpoint, requestBody, null, httpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a POST request against the supplied <paramref name="service"/> and <paramref name="endpoint"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of class to deserialize the results into.</typeparam>
        /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
        /// <param name="endpoint">The endpoint on the <paramref name="service"/> to call.</param>
        /// <param name="requestBody">An optional object to serialize to JSON and send as the request body.</param>
        /// <param name="accessToken">An option access token to send in the HTTP Authorization header to the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult"/> wrapping the <typeparamref name="TResult"/>.</returns>
        public async Task<HttpResult<TResult>> Post(Uri service, string endpoint, object? requestBody, string? accessToken, HttpClientHandler? httpClientHandler, CancellationToken cancellationToken)
        {
            HttpClient httpClient = GetHttpClient(httpClientHandler);

            using (var postMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(service, endpoint)))
            {
                if (accessToken is not null)
                {
                    postMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                }

                if (requestBody is not null)
                {
                    string content = JsonSerializer.Serialize(requestBody, PreconfiguredSerializerOptions.Options);
                    postMessage.Content = new StringContent(content, Encoding.UTF8, MediaTypeNames.Application.Json);
                }

                using (HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(postMessage, cancellationToken).ConfigureAwait(false))
                {
                    HttpResult<TResult> result = new()
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
                                PreconfiguredSerializerOptions.Options);
                        }
                    }
                    else
                    {
                        string responseContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                        result.Error = JsonSerializer.Deserialize<AtErrorDetail>(
                            responseContent,
                            PreconfiguredSerializerOptions.Options);

                        result.Error ??= new AtErrorDetail();

                        result.Error.Instance = postMessage.RequestUri;
                        result.Error.HttpMethod = postMessage.Method;
                    }

                    return result;
                }
            }
        }
    }
}
