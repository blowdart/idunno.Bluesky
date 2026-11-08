// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Signing;

namespace idunno.Bluesky.Labeler.Host;

/// <summary>Composes the provider-neutral canonical application using the existing labeler hosting helpers.</summary>
public static class LabelerApplicationExtensions
{
    /// <summary>Runs a canonical host or its isolated readiness probe with explicit database registration.</summary>
    /// <param name="args">The application arguments.</param>
    /// <param name="configureStorage">The registration of one label store and, when enabled, persistent website authentication.</param>
    /// <returns>The application exit code.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <remarks><para>The application owns its signing-key ring and identity HTTP client for the entire host lifetime.
    /// No provider, database credentials or ephemeral persistence fallback is selected by this method.</para></remarks>
    public static Task<int> RunAsync(string[] args, Action<WebApplicationBuilder> configureStorage)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configureStorage);
        return Program.RunAsync(args, configureStorage);
    }

    /// <summary>Registers canonical distribution, protected administration and explicitly enabled optional features.</summary>
    /// <param name="builder">The application builder with one registered label storage provider.</param>
    /// <param name="identityClient">The caller-owned, SSRF-protected identity HTTP client.</param>
    /// <param name="signingKeyRing">The caller-owned signing-key ring, or <see langword="null"/> for explicit terminal keyless hosting.</param>
    /// <returns>The application builder.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Configuration or provider registration is missing or incompatible.</exception>
    /// <remarks><para>Keep both caller-owned dependencies alive until the application has stopped and been disposed.
    /// This extension builds on AddBlueskyLabeler and the existing administration and moderation registrations.</para></remarks>
    public static WebApplicationBuilder AddCanonicalBlueskyLabeler(
        this WebApplicationBuilder builder,
        HttpClient identityClient,
        ILabelSigningKeyRing? signingKeyRing)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(identityClient);
        Program.AddCanonicalServices(builder, identityClient, signingKeyRing);
        return builder;
    }

    /// <summary>Initializes durable authentication, labels, policy and signing-key selection before serving requests.</summary>
    /// <param name="app">The configured application.</param>
    /// <param name="signingKeyRing">The same signing-key ring supplied during registration.</param>
    /// <param name="cancellationToken">The cancellation token for startup.</param>
    /// <returns>A task representing initialization.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Keyless hosting validates existing terminal storage rather than creating a label database.
    /// Initialization failures use the existing bounded startup retry policy and then fail startup.</para></remarks>
    public static Task InitializeCanonicalBlueskyLabelerAsync(
        this WebApplication app,
        ILabelSigningKeyRing? signingKeyRing,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        return Program.InitializeCanonicalServicesAsync(app, signingKeyRing, cancellationToken);
    }

    /// <summary>Installs the canonical HTTP pipeline and maps the configured public and protected routes.</summary>
    /// <param name="app">The initialized application.</param>
    /// <returns>The application.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Call once after initialization and before starting the application. Public distribution
    /// and readiness are mapped through MapBlueskyLabeler, without duplicating its health endpoints.</para></remarks>
    public static WebApplication MapCanonicalBlueskyLabeler(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        Program.MapCanonicalRoutes(app);
        return app;
    }
}
