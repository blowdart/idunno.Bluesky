// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using System.Threading.Channels;

using idunno.AtProto;
using idunno.Bluesky.Labeler;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace idunno.Bluesky.AspNet.Labeler.Integration.Test;

/// <summary>Owns one initialized SQLite file and an actual mapped host, optionally using loopback Kestrel for SDK sockets.</summary>
/// <param name="database">The existing test-owned database to reopen without taking deletion ownership, or null for a new file.</param>
internal sealed class TestServerBuilder(string? database = null) : IAsyncDisposable
{
    internal static readonly Did s_issuer = new("did:plc:label-issuer");
    private readonly string _database = database ?? Path.Combine(Path.GetTempPath(), "label_host_" + Guid.NewGuid().ToString("N") + ".db");
    private readonly bool _ownsDatabase = database is null;
    private WebApplication _app = null!;
    internal ILabelStore Store => _app.Services.GetRequiredService<ILabelStore>();
    internal IServiceProvider Services => _app.Services;
    internal string DatabasePath => _database;
    internal string? OwnedSocketDirectory { get; set; }
    internal LabelerHost Host => _app.Services.GetRequiredService<LabelerHost>();
    internal SignalClock Clock { get; } = new();
    internal HttpClient Client { get; private set; } = null!;
    internal Uri Address { get; private set; } = null!;
    internal Uri SocketAddress => new UriBuilder(Address) { Scheme = "ws", Path = "/xrpc/com.atproto.label.subscribeLabels" }.Uri;

    /// <summary>Creates the application without hiding schema initialization or publication inside a request.</summary>
    internal static async Task<TestServerBuilder> CreateAsync(
        LabelerHostingOptions? options = null,
        bool network = false,
        Func<ILabelStore, Task>? initialize = null,
        string? database = null,
        bool http2 = false,
        Action<IServiceCollection>? configureAdministration = null,
        bool remoteAdministrationEnabled = true,
        bool fallbackAuthorizationRequired = false)
    {
        TestServerBuilder fixture = new(database);
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        if (network)
        {
            builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listen =>
                listen.Protocols = http2 ? HttpProtocols.Http2 : HttpProtocols.Http1));
        }
        else
        {
            builder.WebHost.UseTestServer();
        }

        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        builder.Services.AddSqliteLabelStorage($"Data Source={fixture._database}");
        builder.Services.AddBlueskyLabeler(s_issuer, options);
        if (fallbackAuthorizationRequired)
        {
            builder.Services.AddAuthentication("test")
                .AddScheme<AuthenticationSchemeOptions, RejectingAuthenticationHandler>("test", _ => { });
            builder.Services.AddAuthorization(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        }

        configureAdministration?.Invoke(builder.Services);
        fixture._app = builder.Build();
        if (configureAdministration is not null)
        {
            fixture._app.Use(async (context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Remote-IP", out Microsoft.Extensions.Primitives.StringValues address))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
                }

                await next(context);
            });
            fixture._app.UseLabelerAdministrationForwardedHeaders(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownProxies.Add(IPAddress.Loopback);
                options.KnownProxies.Add(IPAddress.IPv6Loopback);
                options.KnownProxies.Add(IPAddress.Parse("192.0.2.1"));
                options.KnownProxies.Add(IPAddress.Parse("192.0.2.2"));
#if NET10_0_OR_GREATER
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("203.0.113.0/24"));
#else
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("203.0.113.0"), 24));
#endif
                options.ForwardLimit = 2;
            });
        }

        if (fallbackAuthorizationRequired)
        {
            fixture._app.UseAuthentication();
            fixture._app.UseAuthorization();
            fixture._app.MapGet("/test/fallback", () => "fallback");
        }

        fixture._app.UseWebSockets();
        fixture._app.MapBlueskyLabeler();
        if (configureAdministration is not null)
        {
            fixture._app.MapLabelerAdministration(remoteAdministrationEnabled);
        }

        await fixture.Store.InitializeAsync(s_issuer);
        if (initialize is not null)
        {
            await initialize(fixture.Store);
        }

        await fixture._app.StartAsync();
        await fixture.Clock.WaitForTimerAsync(options?.PollInterval ?? TimeSpan.FromSeconds(1));
        if (network)
        {
            fixture.Address = new Uri(fixture._app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            fixture.Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = fixture.Address };
        }
        else
        {
            fixture.Client = fixture._app.GetTestClient();
            fixture.Address = fixture.Client.BaseAddress!;
        }

        return fixture;
    }

    /// <summary>Opens a mapped in-memory socket without a real-time scheduling assumption.</summary>
    internal Task<System.Net.WebSockets.WebSocket> ConnectAsync(string query = "") =>
        _app.GetTestServer().CreateWebSocketClient().ConnectAsync(new Uri(SocketAddress + query), TestContext.Current.CancellationToken);

    /// <summary>Stops publication and active requests without disposing metrics needed for assertions.</summary>
    internal Task StopAsync() => _app.StopAsync();

    /// <summary>Creates a separate provider instance sharing only the durable database with the mapped application.</summary>
    internal ServiceProvider OtherProvider()
    {
        ServiceCollection services = new();
        services.AddSqliteLabelStorage($"Data Source={_database}");
        return services.BuildServiceProvider();
    }

    /// <summary>Simulates an incompatible external schema change without exposing connection details through health output.</summary>
    internal async Task CorruptSchemaAsync()
    {
        await using SqliteConnection connection = new($"Data Source={_database};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE label_schema SET version=999";
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Moves a test database's retained boundary atomically to exercise readers racing explicit retention.</summary>
    internal async Task PruneAsync(long through)
    {
        await using SqliteConnection connection = new($"Data Source={_database};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using SqliteTransaction transaction = connection.BeginTransaction();
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM label_entries WHERE seq <= @through;
            DELETE FROM label_distribution WHERE seq <= @through;
            UPDATE label_state SET replay_floor=@through WHERE id=1;
            """;
        command.Parameters.AddWithValue("@through", through);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Stops hosted workers and deletes only this fixture's known database files.</summary>
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        if (_ownsDatabase)
        {
            foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                File.Delete(_database + suffix);
            }
            if (OwnedSocketDirectory is { } directory)
            {
                Directory.Delete(directory);
            }
        }
    }
}

/// <summary>Rejects browser authentication so fallback-policy tests exercise self-authenticating service routes.</summary>
internal sealed class RejectingAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}

/// <summary>Uses official fake time while signaling timer registration before a test advances deadlines.</summary>
internal sealed class SignalClock : TimeProvider
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(
        year: 2026,
        month: 1,
        day: 2,
        hour: 3,
        minute: 4,
        second: 5,
        offset: TimeSpan.Zero));
    internal Channel<TimeSpan> Registrations { get; } = Channel.CreateUnbounded<TimeSpan>();
    public override DateTimeOffset GetUtcNow() => _clock.GetUtcNow();
    public override long GetTimestamp() => _clock.GetTimestamp();
    public override long TimestampFrequency => _clock.TimestampFrequency;

    /// <summary>Aligns service-token evaluation with the test database's authoritative expiry clock.</summary>
    internal void SetUtcNow(DateTimeOffset value) => _clock.SetUtcNow(value);

    /// <summary>Publishes a phase signal only after the underlying fake timer exists.</summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ITimer timer = _clock.CreateTimer(
            callback: callback,
            state: state,
            dueTime: dueTime,
            period: period);
        Registrations.Writer.TryWrite(dueTime);
        return timer;
    }

    internal void Advance(TimeSpan duration) => _clock.Advance(duration);

    /// <summary>Waits for an actual registered operation timer rather than assuming task scheduling follows fake time.</summary>
    internal async Task WaitForTimerAsync(TimeSpan duration)
    {
        using CancellationTokenSource watchdog = new(TimeSpan.FromSeconds(30));
        while (await Registrations.Reader.ReadAsync(watchdog.Token) != duration)
        {
        }
    }
}
