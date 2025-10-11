// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto
{
    /// <summary>
    /// Service Collection extensions for building
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Binds configuration for <see cref="AtProtoAgent"/> to the specified <paramref name="configuration"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add the configuration to.</param>
        /// <param name="configuration">The configuration section to bind to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved.")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved.")]
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
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved.")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved.")]
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

        private static void AddLoggerFactory(IServiceCollection services)
        {
            services.PostConfigure<AtProtoAgentOptions>(options =>
            {
                IServiceProvider serviceProvider = services.BuildServiceProvider();
                options.LoggerFactory = serviceProvider.GetService<ILoggerFactory>();
            });
        }
    }
}
