// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.Bluesky.BotSdk.DataProviders;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.BotSdk;

/// <summary>Registers and hosts a durable database-backed watcher-and-action bot.</summary>
public static class BotHosting
{
    /// <summary>Registers replay, ownership, authentication, rate accounting, and built-in action delivery services using an existing options instance.</summary>
    /// <typeparam name="TPolicy">The application's event policy implementation.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="options">The validated runtime options, including any application-specific derived options.</param>
    /// <param name="httpUserAgent">The application's HTTP identity, not its account handle.</param>
    /// <returns>A bot builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="httpUserAgent"/> is <see langword="null"/> or whitespace.</exception>
    /// <remarks>
    /// <para>Register dependencies of <typeparamref name="TPolicy"/> before building the host. The host owns registered
    /// storage services and workers; callers must not separately register another set of workers.
    /// The generic parameter annotation preserves public policy constructors for DI under trimming and Native AOT.</para>
    /// <para>Register a singleton <see cref="IDatabaseProvider"/> before or after this method using a storage registration extension
    /// on the returned builder or your own implementation. This method does not choose a database.</para>
    /// <para>Remote writes cannot share a database transaction. Fixed keys and reconciliation reduce duplicates
    /// but do not provide an absolute exactly-once guarantee.</para>
    /// </remarks>
    public static BlueskyBotBuilder AddBlueskyBot<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(
            this IServiceCollection services,
            BotOptions options,
            string httpUserAgent)
        where TPolicy : class, IBotPolicy =>
        AddBlueskyBot<TPolicy>(services, options, httpUserAgent, lookbackCutoff: null);

    /// <summary>Registers replay, ownership, authentication, rate accounting, and built-in action delivery services using an existing options instance and lookback cutoff.</summary>
    /// <typeparam name="TPolicy">The application's event policy implementation.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="options">The validated runtime options, including any application-specific derived options.</param>
    /// <param name="httpUserAgent">The application's HTTP identity, not its account handle.</param>
    /// <param name="lookbackCutoff">The optional fixed cutoff for an explicit forward-only history skip.</param>
    /// <returns>A bot builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="httpUserAgent"/> is <see langword="null"/> or whitespace.</exception>
    /// <remarks>
    /// <para>Register dependencies of <typeparamref name="TPolicy"/> before building the host. The host owns registered
    /// storage services and workers; callers must not separately register another set of workers.
    /// The generic parameter annotation preserves public policy constructors for DI under trimming and Native AOT.</para>
    /// <para>Register a singleton <see cref="IDatabaseProvider"/> before or after this method using a storage registration extension
    /// on the returned builder or your own implementation. This method does not choose a database.</para>
    /// <para>Remote writes cannot share a database transaction. Fixed keys and reconciliation reduce duplicates
    /// but do not provide an absolute exactly-once guarantee.</para>
    /// </remarks>
    public static BlueskyBotBuilder AddBlueskyBot<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(
            this IServiceCollection services,
            BotOptions options,
            string httpUserAgent,
            DateTimeOffset? lookbackCutoff)
        where TPolicy : class, IBotPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpUserAgent);
        options.Validate();

        services.AddSingleton(Options.Create(options));
        services.AddSingleton(options);

        return AddBlueskyBotCore<TPolicy>(services, httpUserAgent, lookbackCutoff);
    }

    /// <summary>Registers replay, ownership, authentication, rate accounting, and built-in action delivery services using an options action.</summary>
    /// <typeparam name="TPolicy">The application's event policy implementation.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="configureOptions">A delegate to configure runtime options.</param>
    /// <param name="httpUserAgent">The application's HTTP identity, not its account handle.</param>
    /// <returns>A bot builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configureOptions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="httpUserAgent"/> is <see langword="null"/> or whitespace.</exception>
    /// <remarks>
    /// <para>Register dependencies of <typeparamref name="TPolicy"/> before building the host. The host owns registered
    /// storage services and workers; callers must not separately register another set of workers.
    /// The generic parameter annotation preserves public policy constructors for DI under trimming and Native AOT.</para>
    /// <para>Register a singleton <see cref="IDatabaseProvider"/> before or after this method using a storage registration extension
    /// on the returned builder or your own implementation. This method does not choose a database.</para>
    /// <para>Remote writes cannot share a database transaction. Fixed keys and reconciliation reduce duplicates
    /// but do not provide an absolute exactly-once guarantee.</para>
    /// </remarks>
    public static BlueskyBotBuilder AddBlueskyBot<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(
            this IServiceCollection services,
            Action<BotOptions> configureOptions,
            string httpUserAgent)
        where TPolicy : class, IBotPolicy =>
        AddBlueskyBot<TPolicy>(services, configureOptions, httpUserAgent, lookbackCutoff: null);

    /// <summary>Registers replay, ownership, authentication, rate accounting, and built-in action delivery services using an options action and lookback cutoff.</summary>
    /// <typeparam name="TPolicy">The application's event policy implementation.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="configureOptions">A delegate to configure runtime options.</param>
    /// <param name="httpUserAgent">The application's HTTP identity, not its account handle.</param>
    /// <param name="lookbackCutoff">The optional fixed cutoff for an explicit forward-only history skip.</param>
    /// <returns>A bot builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configureOptions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="httpUserAgent"/> is <see langword="null"/> or whitespace.</exception>
    /// <remarks>
    /// <para>Register dependencies of <typeparamref name="TPolicy"/> before building the host. The host owns registered
    /// storage services and workers; callers must not separately register another set of workers.
    /// The generic parameter annotation preserves public policy constructors for DI under trimming and Native AOT.</para>
    /// <para>Register a singleton <see cref="IDatabaseProvider"/> before or after this method using a storage registration extension
    /// on the returned builder or your own implementation. This method does not choose a database.</para>
    /// <para>Remote writes cannot share a database transaction. Fixed keys and reconciliation reduce duplicates
    /// but do not provide an absolute exactly-once guarantee.</para>
    /// </remarks>
    public static BlueskyBotBuilder AddBlueskyBot<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(
            this IServiceCollection services,
            Action<BotOptions> configureOptions,
            string httpUserAgent,
            DateTimeOffset? lookbackCutoff)
        where TPolicy : class, IBotPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpUserAgent);

        services.Configure(configureOptions);
        services.AddOptions<BotOptions>()
            .Validate(options =>
            {
                options.Validate();
                return true;
            })
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<BotOptions>>().Value);

        return AddBlueskyBotCore<TPolicy>(services, httpUserAgent, lookbackCutoff);
    }

    /// <summary>Registers replay, ownership, authentication, rate accounting, and built-in action delivery services using a configuration section.</summary>
    /// <typeparam name="TPolicy">The application's event policy implementation.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="configuration">The configuration section to bind.</param>
    /// <param name="httpUserAgent">The application's HTTP identity, not its account handle.</param>
    /// <returns>A bot builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="httpUserAgent"/> is <see langword="null"/> or whitespace.</exception>
    /// <remarks>
    /// <para>Register dependencies of <typeparamref name="TPolicy"/> before building the host. The host owns registered
    /// storage services and workers; callers must not separately register another set of workers.
    /// The generic parameter annotation preserves public policy constructors for DI under trimming and Native AOT.</para>
    /// <para>Register a singleton <see cref="IDatabaseProvider"/> before or after this method using a storage registration extension
    /// on the returned builder or your own implementation. This method does not choose a database.</para>
    /// <para>Remote writes cannot share a database transaction. Fixed keys and reconciliation reduce duplicates
    /// but do not provide an absolute exactly-once guarantee.</para>
    /// </remarks>
    public static BlueskyBotBuilder AddBlueskyBot<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(
            this IServiceCollection services,
            IConfiguration configuration,
            string httpUserAgent)
        where TPolicy : class, IBotPolicy =>
        AddBlueskyBot<TPolicy>(services, configuration, httpUserAgent, lookbackCutoff: null);

    /// <summary>Registers replay, ownership, authentication, rate accounting, and built-in action delivery services using a configuration section and lookback cutoff.</summary>
    /// <typeparam name="TPolicy">The application's event policy implementation.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="configuration">The configuration section to bind.</param>
    /// <param name="httpUserAgent">The application's HTTP identity, not its account handle.</param>
    /// <param name="lookbackCutoff">The optional fixed cutoff for an explicit forward-only history skip.</param>
    /// <returns>A bot builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="httpUserAgent"/> is <see langword="null"/> or whitespace.</exception>
    /// <remarks>
    /// <para>Register dependencies of <typeparamref name="TPolicy"/> before building the host. The host owns registered
    /// storage services and workers; callers must not separately register another set of workers.
    /// The generic parameter annotation preserves public policy constructors for DI under trimming and Native AOT.</para>
    /// <para>Register a singleton <see cref="IDatabaseProvider"/> before or after this method using a storage registration extension
    /// on the returned builder or your own implementation. This method does not choose a database.</para>
    /// <para>Remote writes cannot share a database transaction. Fixed keys and reconciliation reduce duplicates
    /// but do not provide an absolute exactly-once guarantee.</para>
    /// </remarks>
    public static BlueskyBotBuilder AddBlueskyBot<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(
            this IServiceCollection services,
            IConfiguration configuration,
            string httpUserAgent,
            DateTimeOffset? lookbackCutoff)
        where TPolicy : class, IBotPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpUserAgent);

        services.Configure<BotOptions>(configuration);
        services.AddOptions<BotOptions>()
            .Validate(options =>
            {
                options.Validate();
                return true;
            })
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<BotOptions>>().Value);

        return AddBlueskyBotCore<TPolicy>(services, httpUserAgent, lookbackCutoff);
    }

    /// <summary>Registers replay, ownership, authentication, rate accounting, and built-in action delivery services binding from default configuration.</summary>
    /// <typeparam name="TPolicy">The application's event policy implementation.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="httpUserAgent">The application's HTTP identity, not its account handle.</param>
    /// <returns>A bot builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="httpUserAgent"/> is <see langword="null"/> or whitespace.</exception>
    /// <remarks>
    /// <para>This method binds <see cref="BotOptions"/> from the configuration section named <see cref="BotOptions.Bot"/>.</para>
    /// <para>Register dependencies of <typeparamref name="TPolicy"/> before building the host. The host owns registered
    /// storage services and workers; callers must not separately register another set of workers.
    /// The generic parameter annotation preserves public policy constructors for DI under trimming and Native AOT.</para>
    /// <para>Register a singleton <see cref="IDatabaseProvider"/> before or after this method using a storage registration extension
    /// on the returned builder or your own implementation. This method does not choose a database.</para>
    /// <para>Remote writes cannot share a database transaction. Fixed keys and reconciliation reduce duplicates
    /// but do not provide an absolute exactly-once guarantee.</para>
    /// </remarks>
    public static BlueskyBotBuilder AddBlueskyBot<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(
            this IServiceCollection services,
            string httpUserAgent)
        where TPolicy : class, IBotPolicy =>
        AddBlueskyBot<TPolicy>(services, httpUserAgent, lookbackCutoff: null);

    /// <summary>Registers replay, ownership, authentication, rate accounting, and built-in action delivery services binding from default configuration and lookback cutoff.</summary>
    /// <typeparam name="TPolicy">The application's event policy implementation.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="httpUserAgent">The application's HTTP identity, not its account handle.</param>
    /// <param name="lookbackCutoff">The optional fixed cutoff for an explicit forward-only history skip.</param>
    /// <returns>A bot builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="httpUserAgent"/> is <see langword="null"/> or whitespace.</exception>
    /// <remarks>
    /// <para>This method binds <see cref="BotOptions"/> from the configuration section named <see cref="BotOptions.Bot"/>.</para>
    /// <para>Register dependencies of <typeparamref name="TPolicy"/> before building the host. The host owns registered
    /// storage services and workers; callers must not separately register another set of workers.
    /// The generic parameter annotation preserves public policy constructors for DI under trimming and Native AOT.</para>
    /// <para>Register a singleton <see cref="IDatabaseProvider"/> before or after this method using a storage registration extension
    /// on the returned builder or your own implementation. This method does not choose a database.</para>
    /// <para>Remote writes cannot share a database transaction. Fixed keys and reconciliation reduce duplicates
    /// but do not provide an absolute exactly-once guarantee.</para>
    /// </remarks>
    public static BlueskyBotBuilder AddBlueskyBot<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(
            this IServiceCollection services,
            string httpUserAgent,
            DateTimeOffset? lookbackCutoff)
        where TPolicy : class, IBotPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpUserAgent);

        services.AddOptions<BotOptions>()
            .BindConfiguration(BotOptions.Bot)
            .Validate(options =>
            {
                options.Validate();
                return true;
            })
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<BotOptions>>().Value);

        return AddBlueskyBotCore<TPolicy>(services, httpUserAgent, lookbackCutoff);
    }

    private static BlueskyBotBuilder AddBlueskyBotCore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(
            IServiceCollection services,
            string httpUserAgent,
            DateTimeOffset? lookbackCutoff)
        where TPolicy : class, IBotPolicy
    {
        services.AddSingleton<IBotPolicy, TPolicy>();
        services.AddSingleton(new LookbackOverride(lookbackCutoff));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => new ActionStatistics(provider.GetRequiredService<TimeProvider>().GetUtcNow()));
        services.AddSingleton<Ownership>();
        services.AddSingleton<WorkerHealth>();
        services.AddSingleton<CredentialVault>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotActionHandler, ReplyActionHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotActionHandler, LikeActionHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotActionHandler, RepostActionHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotActionHandler, BookmarkActionHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotActionHandler, ListItemActionHandler>());
        services.AddSingleton<ActionHandlerRegistry>();
        services.AddSingleton<ActionDispatcher>();
        services.AddTransient<OutboundHandler>();

        // Reuse the SDK's protected transport without changing clients used by other agents.
        HttpClientOptions httpOptions = new(httpUserAgent: httpUserAgent, timeout: TimeSpan.FromSeconds(30));
        services.AddHttpClient(BotHttpClientFactory.ClientName)
            .ConfigureHttpClient(client => Agent.InternalConfigureHttpClient(client, httpOptions.HttpUserAgent, httpOptions.Timeout))
            .ConfigurePrimaryHttpMessageHandler(provider => Agent.CreateHttpMessageHandler(httpOptions, provider.GetService<ILoggerFactory>()))
            .AddHttpMessageHandler<OutboundHandler>();
        services.AddSingleton<BotHttpClientFactory>();
        services.Configure<HostOptions>(host =>
        {
            host.ShutdownTimeout = TimeSpan.FromSeconds(45);
            host.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
        });

        // Initialize and renew the lease first; ingestion and sending wait for ownership.
        services.AddHostedService<OwnershipWorker>();
        services.AddHostedService<ReaderWorker>();
        services.AddHostedService<SenderWorker>();

        return new BlueskyBotBuilder(services);
    }

    /// <summary>Maps memory-only liveness and dependency endpoints without contacting remote services.</summary>
    /// <param name="endpoints">The host endpoint builder.</param>
    /// <returns>The endpoint builder for further mapping.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    public static IEndpointRouteBuilder MapBlueskyBotHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
            "/health/live",
            async context =>
        {
            WorkerHealth health = context.RequestServices.GetRequiredService<WorkerHealth>();
            (bool healthy, Dictionary<string, HealthState> workers) = health.Snapshot();

            await Results.Json(
                new LivenessResponse(healthy ? "healthy" : "unhealthy", workers),
                BotJsonContext.Default.LivenessResponse,
                statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context).ConfigureAwait(false);
        });
        endpoints.MapGet(
            "/health/dependencies",
            async context =>
        {
            WorkerHealth health = context.RequestServices.GetRequiredService<WorkerHealth>();
            (_, Dictionary<string, HealthState> Workers) = health.Snapshot();

            await Results.Json(
                Workers.ToDictionary(pair => pair.Key, pair => pair.Value.Dependency),
                BotJsonContext.Default.DictionaryStringString,
                statusCode: Workers.Values.Any(state => state.Dependency is not null) ? 503 : 200).ExecuteAsync(context).ConfigureAwait(false);
        });

        return endpoints;
    }

    /// <summary>Maps a Prometheus text endpoint backed by a read-only durable-state snapshot.</summary>
    /// <param name="endpoints">The host endpoint builder.</param>
    /// <returns>The endpoint builder for further mapping.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This endpoint queries the database when scraped. Restrict access at the network boundary if operational
    /// queue counts or replay progress should not be public. It is independent of memory-only liveness probes.</para>
    /// </remarks>
    public static IEndpointRouteBuilder MapBlueskyBotMetricsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
            "/metrics",
            async context =>
            {
                IDatabaseProvider database = context.RequestServices.GetRequiredService<IDatabaseProvider>();
                if (database is not IBotDatabaseDiagnostics diagnostics)
                {
                    await Results.Text(
                        "The registered database provider does not support operational metrics.",
                        statusCode: StatusCodes.Status501NotImplemented).ExecuteAsync(context).ConfigureAwait(false);

                    return;
                }
                BotOperationalSnapshot snapshot = await diagnostics.GetOperationalSnapshotAsync(context.RequestAborted).ConfigureAwait(false);
                StringBuilder content = new();
                content.AppendLine("# HELP bluesky_bot_processed_through_sequence Highest sequence durably handled; not a timestamp or event count.");
                content.AppendLine("# TYPE bluesky_bot_processed_through_sequence gauge");
                content.Append("bluesky_bot_processed_through_sequence ")
                    .AppendLine(snapshot.ProcessedThrough.ToString(CultureInfo.InvariantCulture));
                content.AppendLine("# HELP bluesky_bot_actions Durable action rows by status, including terminal rows retained for pruning.");
                content.AppendLine("# TYPE bluesky_bot_actions gauge");
                AppendMetric(content, "pending", snapshot.PendingActions);
                AppendMetric(content, "uncertain", snapshot.UncertainActions);
                AppendMetric(content, "sent", snapshot.SentActions);
                AppendMetric(content, "not-sent", snapshot.RejectedActions);
                AppendMetric(content, "simulated", snapshot.SimulatedActions);
                content.AppendLine("# HELP bluesky_bot_retry_due_actions Pending or uncertain actions whose saved retry time has arrived.");
                content.AppendLine("# TYPE bluesky_bot_retry_due_actions gauge");
                content.Append("bluesky_bot_retry_due_actions ")
                    .AppendLine(snapshot.RetryDueActions.ToString(CultureInfo.InvariantCulture));

                await Results.Text(
                    content.ToString(),
                    contentType: "text/plain; version=0.0.4; charset=utf-8").ExecuteAsync(context).ConfigureAwait(false);
            });

        return endpoints;
    }

    /// <summary>Appends one static-label action metric using invariant numeric formatting.</summary>
    /// <param name="content">The metrics response being assembled.</param>
    /// <param name="status">The fixed action status label.</param>
    /// <param name="value">The durable number of actions.</param>
    private static void AppendMetric(StringBuilder content, string status, long value) =>
        content.Append("bluesky_bot_actions{status=\"")
            .Append(status)
            .Append("\"} ")
            .AppendLine(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Configures timestamped console diagnostics for the bot runtime.</summary>
    /// <param name="logging">The host logging builder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logging"/> is <see langword="null"/>.</exception>
    public static void ConfigureConsoleLogging(ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);
        BotLog.ConfigureConsole(logging);
    }

    /// <summary>Parses an explicit history-skip duration with a whole-number count and s, m, h, or d suffix.</summary>
    /// <param name="value">The duration text, such as <c>30m</c>.</param>
    /// <returns>The duration to subtract from invocation time.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The duration syntax is invalid or the duration exceeds the supported range.</exception>
    public static TimeSpan ParseLookback(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return LookbackOverride.Parse(value);
    }
}
