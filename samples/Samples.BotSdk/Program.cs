// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.BotSdk;
using idunno.Bluesky.BotSdk.DataProviders;

using Samples.BotSdk;

// -----------------------------------------------------------------------------------------
// Console Setup
// -----------------------------------------------------------------------------------------
// Set the console output encoding to UTF-8 so emojis like "🫘" and non-ASCII text render
// properly in console logs.
Console.OutputEncoding = System.Text.Encoding.UTF8;

// -----------------------------------------------------------------------------------------
// Application Host Builder
// -----------------------------------------------------------------------------------------
// The durable Bot SDK uses the ASP.NET Core web host (WebApplicationBuilder).
// This allows the bot to run its long-lived background workers (ReaderWorker for streaming
// and SenderWorker for dispatching) while simultaneously serving operational HTTP endpoints
// for container health checks and Prometheus metrics.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------------------
// Register the Durable Bluesky Bot
// -----------------------------------------------------------------------------------------
// We register the bot using AddBlueskyBot<TPolicy> and chain our storage provider.
//
// 1. Policy:
//    BeansBotPolicy implements IBotPolicy. It inspects incoming posts from the live Jetstream
//    and returns a reply action with "🫘" whenever someone posts "#beans".
//
// 2. Options Configuration:
//    We configure BotOptions to set up credentials, replay behavior, and operational mode.
builder.Services.AddBlueskyBot<BeansBotPolicy>(
    options =>
    {
        // Bind the "Bot" section from appsettings.json or user secrets:
        builder.Configuration.GetSection(BotOptions.Bot).Bind(options);

        // DEVELOPER MODE CONFIGURATION:
        // BotOptions and this sample's appsettings.json default DeveloperMode to true.
        // The bot still listens to live Jetstream events, but logs and simulates replies
        // without publishing them to Bluesky. We keep the bound value so you can explicitly
        // set Bot:DeveloperMode to false in configuration when ready to publish real replies.

        // Credential Fallbacks:
        // If credentials are not provided in appsettings.json or user secrets, we check
        // standard environment variables. This makes it convenient to run across different
        // environments without hardcoding secrets:
        // - _BlueskyHandle                 : Your bot's Bluesky handle (e.g. mybot.bsky.social)
        // - _BlueskyPassword               : Your bot's Bluesky App Password (from account settings)
        // - _BlueskyDid                    : Your bot's DID (e.g. did:plc:...)
        // - _BlueskyArchiveApiKey          : Your Jetstream archive API key (from bsky.network)
        // - _BlueskyCredentialEncryptionKey: A base64-encoded 32-byte key used to encrypt cached tokens
        if (string.IsNullOrWhiteSpace(options.Handle))
        {
            options.Handle = Environment.GetEnvironmentVariable("_BlueskyHandle") ?? "";
        }

        if (string.IsNullOrWhiteSpace(options.AppPassword))
        {
            options.AppPassword = Environment.GetEnvironmentVariable("_BlueskyPassword") ?? "";
        }

        if (string.IsNullOrWhiteSpace(options.Did))
        {
            options.Did = Environment.GetEnvironmentVariable("_BlueskyDid") ?? "";
        }

        if (string.IsNullOrWhiteSpace(options.ArchiveApiKey))
        {
            options.ArchiveApiKey = Environment.GetEnvironmentVariable("_BlueskyArchiveApiKey") ?? "";
        }

        if (string.IsNullOrWhiteSpace(options.CredentialEncryptionKey))
        {
            options.CredentialEncryptionKey = Environment.GetEnvironmentVariable("_BlueskyCredentialEncryptionKey") ?? "";
        }
    },
    // The HTTP User-Agent header sent with outbound requests to identify the bot client.
    httpUserAgent: "Sample.BotSdk/1.0")
    // -------------------------------------------------------------------------------------
    // In-Memory Storage Provider
    // -------------------------------------------------------------------------------------
    // The .WithInMemoryStorage() extension registers the in-memory database provider.
    // This keeps all checkpoints, action outbox queues, rate limits, and ownership leases in RAM.
    //
    // WARNING: The in-memory provider is designed for development, samples, and testing.
    // It logs a warning at startup (Event ID 420) reminding you that all state is lost
    // when the application stops. For production bots, use a persistent provider:
    // - .WithSqliteStorage(connectionString)     (single-instance file database)
    // - .WithPostgreSqlStorage(connectionString) (multi-instance PostgreSQL database)
    // - .WithMySqlStorage(connectionString)      (multi-instance MySQL database)
    .WithInMemoryStorage();

WebApplication app = builder.Build();

// -----------------------------------------------------------------------------------------
// Map Operational Endpoints
// -----------------------------------------------------------------------------------------
// The SDK provides built-in endpoints for monitoring:
// - /health/live    : Process and worker liveness (returns HTTP 200 when background workers are running).
// - /health/dependencies: Reports tracked dependency waits (HTTP 503 while a dependency is flagged).
// These memory-only probes do not actively test the database or remote services.
app.MapBlueskyBotHealthEndpoints();

// Exposes Prometheus gauges at /metrics for the durable processed sequence, action counts
// by status, and actions whose retry time has arrived. These are not throughput or latency counters.
app.MapBlueskyBotMetricsEndpoint();

// -----------------------------------------------------------------------------------------
// Run the Application
// -----------------------------------------------------------------------------------------
// Starts the web host and begins background processing.
// The reader worker connects to the live Jetstream stream and evaluates events using BeansBotPolicy.
app.Run();
