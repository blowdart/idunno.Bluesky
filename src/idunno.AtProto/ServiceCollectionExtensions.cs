// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

/// <summary>
/// Service Collection extensions for building
/// </summary>
public static class ServiceCollectionExtensions
{
    internal const string RequiresDynamicCodeMessage = "Binding strongly typed objects to configuration values may require generating dynamic code at runtime.";
    internal const string TrimmingRequiredUnreferencedCodeMessage = "AtProtoAgentOptions members may be trimmed. Ensure all required members are preserved.";

    /// <summary>
    /// Binds configuration for <see cref="AtProtoAgent"/> to the specified <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
    /// <param name="configuration">The configuration section to bind to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TrimmingRequiredUnreferencedCodeMessage)]
    public static IServiceCollection AddAtProtoAgentOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AtProtoAgentOptions>(configuration);

        AddLoggerFactory(services);

        return services;
    }

    /// <summary>
    /// Reads configuration for <see cref="AtProtoAgent"/> from a section and adds it to services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
    /// <param name="configSectionPath">The configuration section to load configuration from.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(TrimmingRequiredUnreferencedCodeMessage)]
    public static IServiceCollection AddAtProtoAgentOptions(
        this IServiceCollection services,
        string configSectionPath = AtProtoAgentOptions.AtProtoAgent)
    {
        services.AddOptions<AtProtoAgentOptions>()
            .BindConfiguration(configSectionPath);

        AddLoggerFactory(services);

        return services;
    }

    /// <summary>
    /// Configures options for an AtProto agent.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
    /// <param name="configureOptions">A lambda which yields an instance of <see cref="AtProtoAgentOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddAtProtoAgentOptions(
        this IServiceCollection services,
        Action<AtProtoAgentOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services;
    }

    /// <summary>
    /// Configures options for an AtProto agent.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
    /// <param name="configureOptions">A lambda which yields an instance of <see cref="AtProtoAgentOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddAtProtoAgentOptions(
        this IServiceCollection services,
        AtProtoAgentOptions configureOptions)
    {
        services.AddOptions<AtProtoAgentOptions>().
            Configure(options =>
            {
                options.EnableBackgroundTokenRefresh = configureOptions.EnableBackgroundTokenRefresh;
                options.HttpClientOptions = configureOptions.HttpClientOptions;
                options.HttpJsonOptions = configureOptions.HttpJsonOptions;
                options.LoggerFactory = configureOptions.LoggerFactory;
                options.OAuthOptions = configureOptions.OAuthOptions;
                options.PlcDirectoryServer = configureOptions.PlcDirectoryServer;
            });

        if (configureOptions is not null && configureOptions.LoggerFactory is null)
        {
            AddLoggerFactory(services);
        }

        return services;
    }

    /// <summary>
    /// Registers the named <see cref="HttpClient"/> agents make their requests through.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the client to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddAtProtoHttpClient(this IServiceCollection services)
    {
        return services.AddAtProtoHttpClient(httpClientOptions: null);
    }

    /// <summary>
    /// Registers the named <see cref="HttpClient"/> agents make their requests through.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the client to.</param>
    /// <param name="httpClientOptions">Any <see cref="HttpClientOptions"/> to configure the client with.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    ///   An agent created without an <see cref="IHttpClientFactory"/> builds a service provider of its own to create one
    ///   from, which gives every agent its own connection pool. Long lived applications, in particular web applications
    ///   which create an agent per request, should register the client once with this method and hand the resulting
    ///   <see cref="IHttpClientFactory"/> to the agents they create.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAtProtoHttpClient(
        this IServiceCollection services,
        HttpClientOptions? httpClientOptions)
    {
        return services.AddAtProtoHttpClient(_ => httpClientOptions);
    }

    /// <summary>
    /// Registers the named <see cref="HttpClient"/> agents make their requests through, configured from services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the client to.</param>
    /// <param name="httpClientOptionsProvider">
    ///   A function which returns the <see cref="HttpClientOptions"/> to configure the client with, for applications
    ///   whose options are not known until the service provider has been built.
    /// </param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> or <paramref name="httpClientOptionsProvider"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    ///   The client is created through the same SSRF protected handler an agent builds for itself, so registering it
    ///   cannot weaken the protections an agent relies on when it resolves a service it was told about by a DID
    ///   document.
    /// </para>
    /// <para>
    ///   <paramref name="httpClientOptionsProvider"/> is called when a handler is created rather than on every request,
    ///   so a change to the options takes effect when the handler is next rotated rather than immediately.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAtProtoHttpClient(
        this IServiceCollection services,
        Func<IServiceProvider, HttpClientOptions?> httpClientOptionsProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(httpClientOptionsProvider);

        services
            .AddHttpClient(Agent.HttpClientName)
            .ConfigureHttpClient((provider, client) =>
            {
                HttpClientOptions? httpClientOptions = httpClientOptionsProvider(provider);

                Agent.InternalConfigureHttpClient(client, httpClientOptions?.HttpUserAgent, httpClientOptions?.Timeout);
            })
            .ConfigurePrimaryHttpMessageHandler(provider =>
                Agent.CreateHttpMessageHandler(httpClientOptionsProvider(provider), provider.GetService<ILoggerFactory>()));

        return services;
    }

    private static void AddLoggerFactory(IServiceCollection services)
    {
        services.PostConfigure<AtProtoAgentOptions>(options =>
        {
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            options.LoggerFactory = serviceProvider.GetService<ILoggerFactory>();
        });
    }
}