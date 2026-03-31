// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using idunno.Security;


namespace idunno.AtProto;

/// <summary>
/// Provides a base class for agent classes that will use HttpClients to connect with a service.
/// </summary>
public abstract class Agent : IDisposable
{
    private const string HttpClientName = "idunno.atproto";

    private volatile bool _disposed;

    private readonly ServiceProvider? _serviceProvider;

    private readonly ILoggerFactory? _loggerFactory;

    internal HttpClientOptions? _httpClientOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="Agent"/> class.
    /// </summary>
    /// <param name="httpClientOptions">Any <see cref="HttpClientOptions"/> for the internal http client used to make HTTP requests.</param>
    /// <param name="jsonOptions">Any <see cref="JsonOptions"/> to use during serialization and deserialization.</param>
    /// <remarks>
    /// <para>
    /// Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
    /// <see langword="false"/> if you are using a debugging proxy which does not support CRLs.
    /// </para>
    /// </remarks>
    protected Agent(HttpClientOptions? httpClientOptions, JsonOptions? jsonOptions): this(httpClientOptions, jsonOptions, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Agent"/> class.
    /// </summary>
    /// <param name="httpClientOptions">Any <see cref="HttpClientOptions"/> for the internal http client used to make HTTP requests.</param>
    /// <param name="jsonOptions">Any <see cref="JsonOptions"/> to use during serialization and deserialization.</param>
    /// <param name="loggerFactory">An optional <see cref="ILoggerFactory"/> to use to create loggers from.</param>
    /// <remarks>
    /// <para>
    /// Setting <see cref="HttpClientOptions.CheckCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
    /// <see langword="false"/> if you are using a debugging proxy which does not support CRLs.
    /// </para>
    /// </remarks>
    protected Agent(HttpClientOptions? httpClientOptions, JsonOptions? jsonOptions, ILoggerFactory? loggerFactory)
    {
        if (jsonOptions is not null)
        {
            JsonOptions = jsonOptions;
        }

        bool checkCrl;
        if (httpClientOptions is null)
        {
            checkCrl = true;
        }
        else
        {
            checkCrl = httpClientOptions.CheckCertificateRevocationList;
        }

        _loggerFactory = loggerFactory;

        IServiceCollection services = new ServiceCollection();
        _httpClientOptions = httpClientOptions;

        IWebProxy? proxy = null;
        SslClientAuthenticationOptions? sslOptions = null;

        if (_httpClientOptions?.ProxyUri is not null)
        {
            proxy = new WebProxy(_httpClientOptions.ProxyUri);
        }

        if (!checkCrl)
        {
            sslOptions = new SslClientAuthenticationOptions
            {
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            };
        }

        services
            .AddHttpClient(HttpClientName, client => InternalConfigureHttpClient(client, _httpClientOptions?.HttpUserAgent, _httpClientOptions?.Timeout))
            .ConfigurePrimaryHttpMessageHandler(() => SsrfSocketsHttpHandlerFactory.Create(
                connectionStrategy: ConnectionStrategy.None,
                additionalUnsafeNetworks: null,
                additionalUnsafeIpAddresses: null,
                connectTimeout: _httpClientOptions?.Timeout,
                allowInsecureProtocols: false,
                failMixedResults: true,
                allowAutoRedirect: false,
                automaticDecompression: DecompressionMethods.All,
                proxy: proxy,
                sslOptions: sslOptions,
                loggerFactory: loggerFactory
                ));

        _serviceProvider = services.BuildServiceProvider();
      
        HttpClientFactory = _serviceProvider.GetService<IHttpClientFactory>()!;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Agent"/> class.
    /// </summary>
    /// <param name="httpClientFactory">An <see cref="IHttpClientFactory"/> to use to create HTTP clients.</param>
    /// <param name="jsonOptions">Any <see cref="JsonOptions"/> to use during serialization and deserialization.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>The agent will request a named HttpClient with a name of "idunno.AtProto".</para>
    /// </remarks>
    protected Agent(IHttpClientFactory httpClientFactory, JsonOptions? jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        if (jsonOptions is not null)
        {
            JsonOptions = jsonOptions;
        }

        HttpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Gets the <see cref="IHttpClientFactory"/> used when creating <see cref="HttpClient"/>s.
    /// </summary>
    protected IHttpClientFactory HttpClientFactory { get; init; }

    /// <summary>
    /// Gets the <see cref="JsonOptions"/> for the agent.
    /// </summary>
    public JsonOptions JsonOptions { get; } = new JsonOptions();

    /// <summary>
    /// Gets an <see cref="HttpClient"/> to use when making requests.
    /// </summary>
    public HttpClient HttpClient => HttpClientFactory.CreateClient(HttpClientName);

    /// <summary>
    /// Gets a value indicating whether the agent has an active session.
    /// </summary>
    public virtual bool IsAuthenticated { get; }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="Agent"/> and optionally disposes of the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to releases only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _serviceProvider?.Dispose();

        _disposed = true;
    }

    /// <summary>
    /// Disposes of any managed and unmanaged resources used by the <see cref="Agent"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Configures an HttpClient with the initialization parameters specified when creating the agent.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> to configure.</param>
    /// <returns>The configured <see cref="HttpClient"/>.</returns>
    protected HttpClient ConfigureHttpClient(HttpClient client)
    {
        InternalConfigureHttpClient(client, _httpClientOptions?.HttpUserAgent, _httpClientOptions?.Timeout);

        return client;
    }

    /// <summary>
    /// Creates a client handler to configure proxy setup with the initialization parameters specified when creating the agent.
    /// </summary>
    /// <returns>An <see cref="SocketsHttpHandler"/> configured to any proxy specified when the agent was created.</returns>
    protected SocketsHttpHandler BuildProxyHttpClientHandler()
    {
        IWebProxy? proxy = null;
        SslClientAuthenticationOptions? sslOptions = null;

        if (_httpClientOptions?.ProxyUri is not null)
        {
            proxy = new WebProxy(_httpClientOptions.ProxyUri);
        }

        if (_httpClientOptions?.CheckCertificateRevocationList == false)
        {
            sslOptions = new SslClientAuthenticationOptions
            {
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            };
        }

        return SsrfSocketsHttpHandlerFactory.Create(
                        connectionStrategy: ConnectionStrategy.None,
                        additionalUnsafeNetworks: null,
                        additionalUnsafeIpAddresses: null,
                        connectTimeout: _httpClientOptions?.Timeout,
                        allowInsecureProtocols: false,
                        failMixedResults: true,
                        allowAutoRedirect: false,
                        automaticDecompression: DecompressionMethods.All,
                        proxy: proxy,
                        sslOptions: sslOptions,
                        loggerFactory: _loggerFactory);
    }

    private static void InternalConfigureHttpClient(HttpClient client, string? httpUserAgent = null, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        client.DefaultRequestVersion = HttpVersion.Version20;

        Assembly assembly = typeof(Agent).Assembly;
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (httpUserAgent is null)
        {
            if (string.IsNullOrEmpty(version))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("idunno.AtProto");
            }
            else
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("idunno.AtProto/" + version);
            }
        }
        else
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(httpUserAgent);
        }

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
    }
}
