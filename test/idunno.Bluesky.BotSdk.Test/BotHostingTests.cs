// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Jetstream;
using idunno.Bluesky.BotSdk.DataProviders;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Verifies that the reusable SDK can register policies, workers, and AOT-safe probes without remote writes.</summary>
public sealed class BotHostingTests
{
    /// <summary>Retains configured instances of one handler type for multiple persisted payload versions.</summary>
    [Fact]
    public void BuilderPreservesConfiguredHandlerVersions()
    {
        ServiceCollection services = new();
        services.AddLogging();
        CustomHandler first = new("application-action", 1);
        CustomHandler second = new("application-action", 2);
        services.AddBlueskyBot<NoReplyPolicy>(TestRecords.Options(), "handler-versions-test/1.0")
            .WithInMemoryStorage()
            .AddActionHandler(first)
            .AddActionHandler(second);
        using ServiceProvider provider = services.BuildServiceProvider();
        ActionHandlerRegistry registry = provider.GetRequiredService<ActionHandlerRegistry>();
        BotAction action = TestRecords.Work() with { ActionKind = first.Kind };
        Assert.Same(first, registry.Get(action));
        Assert.Same(second, registry.Get(action with { PayloadVersion = 2 }));
        Assert.Equal(7, provider.GetServices<IBotActionHandler>().Count());
    }

    /// <summary>Reports kind/version collisions instead of silently dropping a configured instance.</summary>
    [Fact]
    public void BuilderRejectsConfiguredHandlerCollisions()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddBlueskyBot<NoReplyPolicy>(TestRecords.Options(), "handler-collision-test/1.0")
            .WithInMemoryStorage()
            .AddActionHandler(new CustomHandler("application-action", 1))
            .AddActionHandler(new CustomHandler("application-action", 1));
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ActionHandlerRegistry>());
    }

    /// <summary>Resolves an application handler from DI using its exact persisted payload version.</summary>
    [Fact]
    public void RegistrationResolvesCustomHandlerVersion()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSqliteBotStorage("Data Source=unused-custom-handler.db");
        CustomHandler handler = new("application-action", 2);
        services.AddSingleton<IBotActionHandler>(handler);
        services.AddBlueskyBot<NoReplyPolicy>(TestRecords.Options(), "custom-handler-test/1.0");
        using ServiceProvider provider = services.BuildServiceProvider();
        ActionHandlerRegistry registry = provider.GetRequiredService<ActionHandlerRegistry>();
        using JsonDocument payload = JsonDocument.Parse("""{"message":"saved intent"}""");
        BotAction action = BotActions.Create(
            handler.Kind,
            new(TestRecords.Work().SourceUri, new(TestRecords.CidValue)),
            11,
            payload.RootElement,
            BotJsonContext.Default.JsonElement,
            payloadVersion: 2);
        registry.Validate(action);
        Assert.Same(handler, registry.Get(action));
        Assert.Throws<InvalidOperationException>(() => registry.Get(action with { PayloadVersion = 1 }));
        Assert.Equal(6, provider.GetServices<IBotActionHandler>().Count());
    }

    /// <summary>Reports conflicting application formats instead of silently replacing built-in handlers.</summary>
    /// <param name="kind">The conflicting action kind.</param>
    /// <param name="version">The conflicting payload version.</param>
    [Theory]
    [InlineData("reply", 1)]
    [InlineData("application-action", 2)]
    public void RejectsDuplicateHandlerFormats(string kind, int version)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSqliteBotStorage("Data Source=unused-collision-test.db");
        services.AddSingleton<IBotActionHandler>(new CustomHandler(kind, version));
        if (kind != "reply")
        {
            services.AddSingleton<IBotActionHandler>(new CustomHandler(kind, version));
        }
        services.AddBlueskyBot<NoReplyPolicy>(TestRecords.Options(), "custom-handler-test/1.0");
        using ServiceProvider provider = services.BuildServiceProvider();
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<ActionHandlerRegistry>());
        Assert.Contains($"'{kind}' payload version {version}", error.Message);
    }

    /// <summary>Requires an explicit database provider rather than choosing an SDK default.</summary>
    [Fact]
    public void RegistrationRequiresStorage()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddBlueskyBot<NoReplyPolicy>(TestRecords.Options(), "registration-test/1.0");
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IDatabaseProvider>());
    }

    /// <summary>Preserves an application's direct singleton registration without creating a default connection pool.</summary>
    [Fact]
    public void RegistrationPreservesApplicationStorage()
    {
        ServiceCollection services = new();
        services.AddLogging();
        BotOptions options = TestRecords.Options();
        using NpgsqlDataSource source = NpgsqlDataSource.Create("Host=127.0.0.1;Database=unused;Username=unused");
        IDatabaseProvider database = new PostgreSqlDatabaseProvider(
            source, options, TimeProvider.System, NullLogger<PostgreSqlDatabaseProvider>.Instance);
        services.AddSingleton(database);
        services.AddBlueskyBot<NoReplyPolicy>(options, "registration-test/1.0");

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        Assert.Same(database, provider.GetRequiredService<IDatabaseProvider>());
        Assert.Single(services, service => service.ServiceType == typeof(IDatabaseProvider));
        Assert.Null(provider.GetService<NpgsqlDataSource>());
    }

    /// <summary>Resolves the explicitly selected relational storage and rejects conflicting registrations.</summary>
    /// <param name="mysql">A value indicating whether to register MySQL rather than SQLite.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegistrationResolvesSelectedStorage(bool mysql)
    {
        ServiceCollection services = new();
        services.AddLogging();
        if (mysql)
        {
            services.AddMySqlBotStorage("Server=127.0.0.1;Database=unused;User ID=unused");
        }
        else
        {
            services.AddSqliteBotStorage($"Data Source={Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db")}");
        }
        Assert.Throws<InvalidOperationException>(() =>
            services.AddPostgreSqlBotStorage("Host=127.0.0.1;Database=unused;Username=unused"));
        services.AddBlueskyBot<NoReplyPolicy>(TestRecords.Options(), "registration-test/1.0");

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        IDatabaseProvider database = provider.GetRequiredService<IDatabaseProvider>();
        if (mysql)
        {
            Assert.IsType<MySqlDatabaseProvider>(database);
        }
        else
        {
            Assert.IsType<SqliteDatabaseProvider>(database);
        }
        Assert.Null(provider.GetService<NpgsqlDataSource>());
    }

    /// <summary>Checks the public registration API resolves the sample policy and all workers.</summary>
    [Fact]
    public void RegistrationResolvesPolicyAndWorkers()
    {
        ServiceCollection services = new();
        services.AddLogging();
        BotOptions options = TestRecords.Options();
        services.AddSingleton(options);
        services.AddPostgreSqlBotStorage("Host=127.0.0.1;Database=unused;Username=unused");
        services.AddBlueskyBot<NoReplyPolicy>(
            options, "registration-test/1.0");

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        Assert.Same(options, provider.GetRequiredService<idunno.Bluesky.BotSdk.BotOptions>());
        IBotPolicy policy = provider.GetRequiredService<IBotPolicy>();
        Assert.IsType<NoReplyPolicy>(policy);
        Assert.Empty(policy.Evaluate(TestRecords.Event(), TestRecords.Start));
        Assert.IsType<PostgreSqlDatabaseProvider>(provider.GetRequiredService<IDatabaseProvider>());
        Assert.Collection(provider.GetServices<IHostedService>(),
            worker => Assert.IsType<OwnershipWorker>(worker),
            worker => Assert.IsType<ReaderWorker>(worker),
            worker => Assert.IsType<SenderWorker>(worker));
    }

    /// <summary>Checks public health mappings preserve JSON shapes and status codes without opening a database.</summary>
    /// <param name="dependencyRecovery">A value indicating whether the reader is recovering from a dependency outage.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HealthEndpointsPreserveResponseShapes(bool dependencyRecovery)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(BotHostingTests).Assembly.FullName,
            Args = []
        });
        builder.Services.AddPostgreSqlBotStorage("Host=127.0.0.1;Database=unused;Username=unused");
        builder.Services.AddBlueskyBot<NoReplyPolicy>(TestRecords.Options(), "probe-test/1.0");
        await using WebApplication app = builder.Build();
        app.MapBlueskyBotHealthEndpoints();
        WorkerHealth health = app.Services.GetRequiredService<WorkerHealth>();
        if (dependencyRecovery)
        {
            health.Set("reader", "recovery", TimeSpan.FromSeconds(30), "archive");
        }

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()];
        foreach (RouteEndpoint endpoint in endpoints)
        {
            DefaultHttpContext context = new() { RequestServices = app.Services };
            await using MemoryStream body = new();
            context.Response.Body = body;
            await endpoint.RequestDelegate!(context);
            body.Position = 0;
            using JsonDocument json = await JsonDocument.ParseAsync(
                body,
                cancellationToken: TestContext.Current.CancellationToken);
            if (endpoint.RoutePattern.RawText == "/health/live")
            {
                Assert.Equal(200, context.Response.StatusCode);
                Assert.Equal("healthy", json.RootElement.GetProperty("status").GetString());
                Assert.True(json.RootElement.GetProperty("workers").TryGetProperty("reader", out _));
            }
            else
            {
                Assert.Equal("/health/dependencies", endpoint.RoutePattern.RawText);
                Assert.Equal(dependencyRecovery ? 503 : 200, context.Response.StatusCode);
                Assert.Equal(dependencyRecovery ? "archive" : null, json.RootElement.GetProperty("reader").GetString());
            }
        }
        Assert.Equal(2, endpoints.Length);
    }

    /// <summary>Renders durable storage metrics as Prometheus text without changing health probe behavior.</summary>
    [Fact]
    public async Task MetricsEndpointReturnsPrometheusSnapshot()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bot-metrics-{Guid.NewGuid():N}.db");
        try
        {
            SqliteDatabaseProvider database = new(
                $"Data Source={path}",
                TestRecords.Options(),
                TimeProvider.System,
                NullLogger<SqliteDatabaseProvider>.Instance);
            await database.InitializeAsync(CancellationToken.None);

            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(BotHostingTests).Assembly.FullName,
                Args = []
            });
            builder.Services.AddSingleton<IDatabaseProvider>(database);
            await using WebApplication app = builder.Build();
            app.MapBlueskyBotMetricsEndpoint();

            RouteEndpoint endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>());
            DefaultHttpContext context = new() { RequestServices = app.Services };
            await using MemoryStream body = new();
            context.Response.Body = body;
            await endpoint.RequestDelegate!(context);
            body.Position = 0;
            using StreamReader reader = new(body);
            string metrics = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

            Assert.Equal("/metrics", endpoint.RoutePattern.RawText);
            Assert.Equal("text/plain; version=0.0.4; charset=utf-8", context.Response.ContentType);
            Assert.Contains("bluesky_bot_processed_through_sequence 0", metrics, StringComparison.Ordinal);
            Assert.Contains("bluesky_bot_actions{status=\"uncertain\"} 0", metrics, StringComparison.Ordinal);
            Assert.Contains("bluesky_bot_actions{status=\"not-sent\"} 0", metrics, StringComparison.Ordinal);
            Assert.Contains("bluesky_bot_retry_due_actions 0", metrics, StringComparison.Ordinal);
            Assert.DoesNotContain("bluesky_bot_actions{status=\"retry_due\"}", metrics, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }

    /// <summary>Provides an application policy that deliberately creates no replies.</summary>
    public sealed class NoReplyPolicy : IBotPolicy
    {
        /// <inheritdoc/>
        public IReadOnlyList<BotAction> Evaluate(
            JetstreamEvent item,
            DateTimeOffset startedAt) => [];
    }

    /// <summary>Provides a versioned application handler for registration tests without remote effects.</summary>
    /// <param name="kind">The stable custom action kind.</param>
    /// <param name="version">The exact supported payload version.</param>
    private sealed class CustomHandler(
        string kind,
        int version) : IBotActionHandler
    {
        /// <inheritdoc/>
        public string Kind => kind;

        /// <inheritdoc/>
        public int PayloadVersion => version;

        /// <inheritdoc/>
        public void Validate(BotAction action)
        {
            Assert.Equal(Kind, action.ActionKind);
            Assert.Equal(PayloadVersion, action.PayloadVersion);
        }

        /// <inheritdoc/>
        public Task<BotActionResult?> ReconcileAsync(
            BotAction action,
            IBotActionRemote remote,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Registration tests must not contact remote services.");

        /// <inheritdoc/>
        public Task<BotActionResult> ExecuteAsync(
            BotAction action,
            IBotActionRemote remote,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Registration tests must not perform remote actions.");
    }

    /// <summary>Configures the bot runtime fluently using the builder pattern with options action and storage chaining.</summary>
    [Fact]
    public void BuilderChainsOptionsAndStorage()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddBlueskyBot<NoReplyPolicy>(
            options =>
            {
                options.Did = TestRecords.AuthorDid;
                options.CredentialEncryptionKey = "custom-key-0123456789abcdef0123456789";
                options.ArchiveApiKey = "archive-key";
            },
            "builder-test/1.0")
            .WithInMemoryStorage()
            .AddActionHandler<ParameterlessHandler>();

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        BotOptions resolvedOptions = provider.GetRequiredService<BotOptions>();
        Assert.Equal(TestRecords.AuthorDid, resolvedOptions.Did);
        Assert.IsType<InMemoryDatabaseProvider>(provider.GetRequiredService<IDatabaseProvider>());
        Assert.Equal(6, provider.GetServices<IBotActionHandler>().Count());
    }

    /// <summary>Binds runtime options from an IConfiguration section.</summary>
    [Fact]
    public void RegistrationBindsFromConfigurationSection()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{BotOptions.Bot}:Did"] = TestRecords.AuthorDid,
                [$"{BotOptions.Bot}:CredentialEncryptionKey"] = "custom-key-0123456789abcdef0123456789",
                [$"{BotOptions.Bot}:ArchiveApiKey"] = "archive-key"
            })
            .Build();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddBlueskyBot<NoReplyPolicy>(
            configuration.GetSection(BotOptions.Bot),
            "config-test/1.0")
            .WithInMemoryStorage();

        using ServiceProvider provider = services.BuildServiceProvider();
        BotOptions resolvedOptions = provider.GetRequiredService<BotOptions>();
        Assert.Equal(TestRecords.AuthorDid, resolvedOptions.Did);
        Assert.Equal("custom-key-0123456789abcdef0123456789", resolvedOptions.CredentialEncryptionKey);
    }

    private sealed class ParameterlessHandler : IBotActionHandler
    {
        public string Kind => "param-less";
        public int PayloadVersion => 1;
        public void Validate(BotAction action) { }
        public Task<BotActionResult?> ReconcileAsync(BotAction action, IBotActionRemote remote, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<BotActionResult> ExecuteAsync(BotAction action, IBotActionRemote remote, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
