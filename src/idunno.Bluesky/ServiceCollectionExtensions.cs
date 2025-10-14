// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky
{
    /// <summary>
    /// Service Collection extensions for building
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        internal const string RequiresDynamicCodeMessage = "Binding strongly typed objects to configuration values may require generating dynamic code at runtime.";
        internal const string TrimmingRequiredUnreferencedCodeMessage = "BlueskyAgentOptions instances may their members trimmed. Ensure all required members are preserved.";

        /// <summary>
        /// Binds configuration for <see cref="BlueskyAgent"/> to the specified <paramref name="configuration"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
        /// <param name="configuration">The configuration section to bind to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        [RequiresDynamicCode(RequiresDynamicCodeMessage)]
        [RequiresUnreferencedCode(TrimmingRequiredUnreferencedCodeMessage)]
        public static IServiceCollection AddBlueskyAgentOptions(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<BlueskyAgentOptions>(configuration);

            AddLoggerFactory(services);

            return services;
        }

        /// <summary>
        /// Reads configuration for <see cref="BlueskyAgent"/> from a section and adds it to services.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
        /// <param name="configSectionPath">The configuration section to load configuration from.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        [RequiresDynamicCode(RequiresDynamicCodeMessage)]
        [RequiresUnreferencedCode(TrimmingRequiredUnreferencedCodeMessage)]
        public static IServiceCollection AddBlueskyAgentOptions(
            this IServiceCollection services,
            string configSectionPath = BlueskyAgentOptions.BlueskyAgent)
        {
            services.AddOptions<BlueskyAgentOptions>()
                .BindConfiguration(configSectionPath);

            AddLoggerFactory(services);

            return services;
        }

        /// <summary>
        /// Configures options for a Bluesky agent.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
        /// <param name="configureOptions">A lambda which yields an instance of <see cref="BlueskyAgentOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddBlueskyAgentOptions(
            this IServiceCollection services,
            Action<BlueskyAgentOptions> configureOptions)
        {
            services.Configure(configureOptions);

            return services;
        }

        /// <summary>
        /// Configures options for a Bluesky agent.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
        /// <param name="configureOptions">A lambda which yields an instance of <see cref="BlueskyAgentOptions"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddBlueskyAgentOptions(
            this IServiceCollection services,
            BlueskyAgentOptions configureOptions)
        {
            services.AddOptions<BlueskyAgentOptions>().
                Configure(options =>
                {
                    options.EnableBackgroundTokenRefresh = configureOptions.EnableBackgroundTokenRefresh;
                    options.FacetExtractor = configureOptions.FacetExtractor;
                    options.HttpClientOptions = configureOptions.HttpClientOptions;
                    options.HttpJsonOptions = configureOptions.HttpJsonOptions;
                    options.LoggerFactory = configureOptions.LoggerFactory;
                    options.OAuthOptions = configureOptions.OAuthOptions;
                    options.PlcDirectoryServer = configureOptions.PlcDirectoryServer;
                    options.PublicAppViewUri = configureOptions.PublicAppViewUri;
                });

            if (configureOptions is not null && configureOptions.LoggerFactory is null)
            {
                AddLoggerFactory(services);
            }

            return services;
        }

        private static void AddLoggerFactory(IServiceCollection services)
        {
            services.PostConfigure<BlueskyAgentOptions>(options =>
            {
                IServiceProvider serviceProvider = services.BuildServiceProvider();
                options.LoggerFactory = serviceProvider.GetService<ILoggerFactory>();
            });
        }
    }
}
