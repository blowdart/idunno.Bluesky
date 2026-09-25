// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;

namespace idunno.AtProto;

/// <summary>
/// Holds the <see cref="HttpClient"/> an operation should make its requests through.
/// </summary>
/// <remarks>
/// <para>
///   A lease disposes the <see cref="Client"/> only when it created it. A caller supplied <see cref="HttpClient"/> remains
///   the caller's to dispose, as it is typically shared and long lived.
/// </para>
/// </remarks>
internal sealed class HttpClientLease : IDisposable
{
    private static readonly HttpMessageHandler s_httpMessageHandler =
        Agent.CreateHttpMessageHandler(httpClientOptions: null, loggerFactory: null);

    private readonly bool _ownsClient;

    /// <summary>
    /// Creates a new instance of <see cref="HttpClientLease"/>.
    /// </summary>
    /// <param name="httpClient">The caller supplied <see cref="HttpClient"/>, if any.</param>
    /// <param name="timeout">The timeout to apply when a client has to be created. Ignored when <paramref name="httpClient"/> is not <see langword="null"/>.</param>
    internal HttpClientLease(HttpClient? httpClient, TimeSpan? timeout)
    {
        _ownsClient = httpClient is null;
        Client = httpClient ?? BuildDefaultHttpClient(timeout);
    }

    /// <summary>
    /// Gets the <see cref="HttpClient"/> the operation should use.
    /// </summary>
    internal HttpClient Client { get; }

    /// <summary>
    /// Disposes the <see cref="Client"/> if, and only if, this instance created it.
    /// </summary>
    public void Dispose()
    {
        if (_ownsClient)
        {
            Client.Dispose();
        }
    }

    private static HttpClient BuildDefaultHttpClient(TimeSpan? timeout)
    {
        HttpClient client = new(s_httpMessageHandler, disposeHandler: false)
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
            DefaultRequestVersion = HttpVersion.Version20
        };

        Assembly assembly = typeof(Agent).Assembly;
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("idunno.AtProto/" + version);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

        client.Timeout = timeout ?? new TimeSpan(0, 5, 0);

        return client;
    }
}
