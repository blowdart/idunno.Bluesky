// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;
using idunno.Security;

using Microsoft.AspNetCore.HttpOverrides;

namespace idunno.Bluesky.Labeler.Host;

/// <summary>Configures the canonical labeler application and its isolated readiness probe.</summary>
internal static class Program
{
    /// <summary>Runs either the standalone labeler host or an isolated readiness probe.</summary>
    internal static async Task<int> Main(string[] args)
    {
        if (args is ["--health-check", ..])
        {
            if (args.Length != 2 ||
                !Uri.TryCreate(args[1], UriKind.Absolute, out Uri? endpoint) ||
                endpoint.Scheme is not ("http" or "https") ||
                !string.IsNullOrEmpty(endpoint.UserInfo))
            {
                await Console.Error.WriteLineAsync("Usage: --health-check <absolute HTTP(S) readiness URL without credentials>").ConfigureAwait(false);
                return 2;
            }

            return await CheckReadinessAsync(endpoint, CancellationToken.None).ConfigureAwait(false);
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        Did issuer = new(Required(builder.Configuration, "Labeler:IssuerDid"));
        Uri publicEndpoint = new(Required(builder.Configuration, "Labeler:PublicEndpoint"));
        string provider = builder.Configuration["Labeler:DatabaseProvider"] ?? "postgresql";
        string connectionString = Required(builder.Configuration, "Labeler:ConnectionString");
        string localAdministrationEndpoint =
            builder.Configuration["Labeler:LocalAdministrationEndpoint"] ??
            (OperatingSystem.IsWindows() ? "idunno-labeler-admin" : "/run/labeler/admin.sock");
        AtProtoServiceReference audience = ParseAudience(Required(builder.Configuration, "Labeler:AdministrationAudience"));

        using IDisposable? keyRingLifetime = IsKeylessConfiguration(builder.Configuration)
            ? null
            : CreateSigningKeyRing(builder.Configuration);
        ILabelSigningKeyRing? keyRing = (ILabelSigningKeyRing?)keyRingLifetime;
        using HttpClient identityClient = CreateIdentityClient();
        switch (provider)
        {
            case "sqlite":
                builder.Services.AddSqliteLabelStorage(connectionString);
                break;
            case "postgresql":
                builder.Services.AddPostgreSqlLabelStorage(connectionString);
                break;
            case "mysql":
                builder.Services.AddMySqlLabelStorage(connectionString);
                break;
            default:
                throw new InvalidOperationException("Labeler:DatabaseProvider must be sqlite, postgresql, or mysql.");
        }

        bool moderationEnabled = builder.Configuration.GetValue<bool>("LabelerModeration:Enabled");
        bool uiEnabled = builder.Configuration.GetValue<bool>("LabelerUI:Enabled");
        if (uiEnabled && !moderationEnabled)
        {
            throw new InvalidOperationException("Enable LabelerModeration before enabling the Xenon web UI.");
        }

        LabelerModerationOptions moderationOptions = new();
        if (moderationEnabled)
        {
            moderationOptions = builder.Configuration.GetSection("LabelerModeration").Get<LabelerModerationOptions>() ?? new();
            builder.Services.AddLabelerModeration(issuer: issuer, httpClient: identityClient, options: moderationOptions);
        }
        else
        {
            builder.Services.AddSingleton<ILabelIssuancePolicyProvider>(
                new ConfigurationLabelIssuancePolicyProvider(builder.Configuration));
        }

        builder.Services.AddBlueskyLabeler(
            issuer,
            new LabelerHostingOptions { PublicEndpoint = publicEndpoint });
        if (keyRing is null)
        {
            builder.Services.AddKeylessLabelerAdministration(audience, identityClient);
        }
        else
        {
            builder.Services.AddLabelerAdministration(
                audience: audience,
                signingKeyRing: keyRing,
                httpClient: identityClient);
        }
        builder.Services.Configure<LabelerAdministrationOptions>(
            builder.Configuration.GetSection("LabelerAdministration"));
        builder.Services.AddLabelerLocalAdministration(localAdministrationEndpoint);
        if (uiEnabled)
        {
            AddWebsiteAuthentication(builder.Services, publicEndpoint);
            builder.Services.AddLabelerUI();
        }

        WebApplication app = builder.Build();
        await using ConfiguredAsyncDisposable appDisposal = app.ConfigureAwait(false);
        if (keyRing is null)
        {
            await ValidateKeylessHostingAsync(
                issuer: issuer,
                store: app.Services.GetRequiredService<ILabelStore>(),
                maintenance: app.Services.GetRequiredService<ILabelMaintenanceStore>(),
                policies: app.Services.GetService<ILabelPolicyStore>(),
                cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }
        else
        {
            await app.Services.GetRequiredService<ILabelStore>()
                .InitializeAsync(issuer, app.Lifetime.ApplicationStopping)
                .ConfigureAwait(false);
        }
        if (moderationEnabled)
        {
            await app.Services.GetRequiredService<ILabelPolicyStore>().InitializePolicyAsync(
                verificationFreshness: moderationOptions.VerificationFreshness,
                cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }
        if (keyRing is EnvironmentLabelSigningKeyRing environmentKeys)
        {
            await environmentKeys.InitializeAsync(
                selectionStore: app.Services.GetRequiredService<ILabelSigningKeySelectionStore>(),
                maintenance: app.Services.GetRequiredService<ILabelMaintenanceStore>(),
                initialKeyId: builder.Configuration["Labeler:InitialSigningKeyId"],
                cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }
        else if (keyRing is PemDirectoryLabelSigningKeyRing pemKeys)
        {
            await pemKeys.InitializeAsync(
                selectionStore: app.Services.GetRequiredService<ILabelSigningKeySelectionStore>(),
                maintenance: app.Services.GetRequiredService<ILabelMaintenanceStore>(),
                cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }

        string? bootstrapAdministrator = builder.Configuration["bootstrap-admin"];
        if (bootstrapAdministrator is not null)
        {
            string operationId = Required(builder.Configuration, "bootstrap-operation-id");
            LabelServiceResult<LabelAdministrationReceipt> bootstrap = await app.Services
                .BootstrapLocalAdministratorAsync(
                    AtIdentifier.Create(bootstrapAdministrator),
                    operationId,
                    app.Lifetime.ApplicationStopping)
                .ConfigureAwait(false);
            if (!bootstrap.Succeeded)
            {
                await Console.Error.WriteLineAsync($"Administrator bootstrap failed: {bootstrap.Error.Code}").ConfigureAwait(false);
                return 1;
            }

            Console.WriteLine(JsonSerializer.Serialize(bootstrap.Result, HostJsonSerializerContext.Default.LabelAdministrationReceipt));
            return 0;
        }

        ConfigureForwardedHeaders(app);
        app.UseBlueskyOAuthClientMetadata(
            CreateNativeOAuthOptions(publicEndpoint, audience),
            new BlueskyOAuthClientMetadataOptions
            {
                ApplicationType = BlueskyOAuthClientApplicationType.Native
            });
        if (uiEnabled)
        {
            app.UseBlueskyOAuthClientMetadata();
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.UseWebSockets();
        app.MapBlueskyLabeler();
        app.MapLabelerAdministration();
        if (moderationEnabled)
        {
            app.MapLabelerModeration();
        }
        if (uiEnabled)
        {
            app.MapStaticAssets();
            app.MapRazorPages().WithStaticAssets();
        }

        await app.RunAsync().ConfigureAwait(false);
        return 0;
    }

    /// <summary>Probes the running host without starting services, loading secrets, following redirects or using an outbound proxy.</summary>
    internal static async Task<int> CheckReadinessAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        using HttpClientHandler handler = new()
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            CheckCertificateRevocationList = true
        };
        using HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using HttpResponseMessage response = await client.GetAsync(
                endpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return 0;
            }

            await Console.Error.WriteLineAsync($"Readiness probe returned HTTP {(int)response.StatusCode}.").ConfigureAwait(false);
            return 1;
        }
        catch (HttpRequestException)
        {
            await Console.Error.WriteLineAsync("Readiness probe could not reach the host.").ConfigureAwait(false);
            return 1;
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Readiness probe timed out or was cancelled.").ConfigureAwait(false);
            return 1;
        }
    }

    /// <summary>Defaults to environment-backed keys and requires explicit selection of filesystem-backed keys.</summary>
    internal static IDisposable CreateSigningKeyRing(IConfiguration configuration)
    {
        string provider = configuration["Labeler:SigningKeyProvider"] ?? "environment";
        bool environmentKeys = configuration.GetSection("Labeler:SigningKeys").GetChildren().Any();
        if ((provider == "pem" && environmentKeys) ||
            (provider == "environment" &&
                (configuration["Labeler:SigningKeyDirectory"] is not null || configuration["Labeler:ActiveKeyPath"] is not null)))
        {
            throw new InvalidOperationException("Configure only the selected signing key provider.");
        }

        return provider switch
        {
            "pem" => new PemDirectoryLabelSigningKeyRing(
                configuration["Labeler:SigningKeyDirectory"] ?? "/run/labeler-keys",
                configuration["Labeler:ActiveKeyPath"] ?? "/data/active-key"),
            "environment" => new EnvironmentLabelSigningKeyRing(configuration),
            _ => throw new InvalidOperationException("Labeler:SigningKeyProvider must be pem or environment.")
        };
    }

    /// <summary>Requires an explicit keyless role without any simultaneously configured private-key source.</summary>
    internal static bool IsKeylessConfiguration(IConfiguration configuration)
    {
        if (configuration["Labeler:SigningKeyProvider"] != "none")
        {
            return false;
        }

        if (configuration.GetSection("Labeler:SigningKeys").GetChildren().Any() ||
            configuration["Labeler:SigningKeyDirectory"] is not null ||
            configuration["Labeler:ActiveKeyPath"] is not null ||
            configuration["Labeler:InitialSigningKeyId"] is not null)
        {
            throw new InvalidOperationException("Keyless hosting cannot configure private signing keys or key selection.");
        }

        return true;
    }

    /// <summary>Validates existing terminal storage without creating or migrating a keyless database.</summary>
    internal static async Task ValidateKeylessHostingAsync(
        Did issuer,
        ILabelStore store,
        ILabelMaintenanceStore maintenance,
        ILabelPolicyStore? policies,
        CancellationToken cancellationToken)
    {
        LabelStoreState state = await store.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (state.Issuer != issuer.Value ||
            state.Phase is not (LabelerLifecyclePhase.Retired or LabelerLifecyclePhase.Purging or LabelerLifecyclePhase.Purged) ||
            state.RetirementThrough != state.RetirementUpperBound ||
            state.PublishedThrough != state.IssuedThrough ||
            (await maintenance.GetMaintenanceStateAsync(cancellationToken).ConfigureAwait(false)).IsActive)
        {
            throw new InvalidOperationException("Keyless hosting requires an initialized retired store with no pending signing, publication or rotation.");
        }

        if (policies is not null &&
            (await policies.ReadPolicyStateAsync(cancellationToken).ConfigureAwait(false))?.Phase is LabelPolicyPhase.Publishing or LabelPolicyPhase.Withdrawing)
        {
            throw new InvalidOperationException("Recover the pending policy publication before starting a keyless retired host.");
        }
    }

    /// <summary>Requires a nonempty configuration value before starting the host.</summary>
    private static string Required(ConfigurationManager configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!
            : throw new InvalidOperationException($"Configuration value '{key}' is required.");

    /// <summary>Parses an explicit issuer service audience without accepting ambiguous fragments.</summary>
    private static AtProtoServiceReference ParseAudience(string value)
    {
        int separator = value.IndexOf('#', StringComparison.Ordinal);
        if (separator <= 0 || separator == value.Length - 1 || value.IndexOf('#', separator + 1) >= 0)
        {
            throw new InvalidOperationException("Labeler:AdministrationAudience must be a DID followed by one service fragment.");
        }

        return new(new Did(value[..separator]), value[(separator + 1)..]);
    }

    /// <summary>Protects identity requests against private-address resolution at connection time.</summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The returned HttpClient owns the handler; failure before ownership transfer disposes it explicitly.")]
    internal static HttpClient CreateIdentityClient()
    {
        SocketsHttpHandler handler = SsrfSocketsHttpHandlerFactory.Create(new SsrfOptions
        {
            ConnectTimeout = TimeSpan.FromSeconds(10)
        });
        try
        {
            handler.AllowAutoRedirect = false;
            handler.MaxConnectionsPerServer = 16;
            handler.PooledConnectionLifetime = TimeSpan.FromMinutes(5);
            handler.UseCookies = false;
            return new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }

    /// <summary>Declares the CLI's method-specific scope superset and its ephemeral loopback callback.</summary>
    internal static OAuthOptions CreateNativeOAuthOptions(Uri publicEndpoint, AtProtoServiceReference audience)
    {
        string encodedAudience = Uri.EscapeDataString(audience.ToString());
        string[] methods =
        [
            "getTeam", "getAudit", "changeTeam", "enroll", "issueLabel", "negateLabel",
            "getOperation", "getPublicationState", "getMaintenanceState", "authorizeMaintenance",
            "beginRetirement", "advanceRetirement", "beginGracePeriod", "completeRetirement",
            "getRetirementState", "prepareRetirement", "pauseRetirement", "resumeRetirement",
            "extendGracePeriod", "requestKeyRemoval", "attestKeyRemoval", "confirmRetirementHostStopped",
            "purgeRetiredData", "beginRotation", "confirmHostStopped", "captureRotationBoundary",
            "verifyRotationKeyPublication", "resignRotationBatch", "completeRotation",
            "getCase", "getCases", "getCaseHistory", "getCaseOperation", "changeCase", "decideCase", "getCaseDecision",
            "getAppeal", "getAppeals", "getAppealReports", "getCaseQueue",
            "getPolicyVersion", "getPolicyVersions", "getPolicyHistory", "getPolicyOperation",
            "getReport", "getReports", "getPolicy", "savePolicy", "beginPolicyPublication", "abandonPolicyPublication", "verifyPolicy"
        ];
        return new OAuthOptions
        {
            ClientId = new Uri(publicEndpoint, "/native-client-metadata.json").AbsoluteUri,
            ClientName = "Labeler administration",
            ClientUri = publicEndpoint,
            ReturnUri = new UriBuilder(Uri.UriSchemeHttp, IPAddress.Loopback.ToString()).Uri,
            Scopes =
            [
                "atproto", "identity:*",
                "repo:app.bsky.labeler.service?action=create", "repo:app.bsky.labeler.service?action=update",
                .. methods.Select(method => $"rpc:dev.idunno.labeler.admin.{method}?aud={encodedAudience}")
            ]
        };
    }

    /// <summary>Registers website sign-in and metadata with origin-based defaults before binding host OAuth configuration.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="publicEndpoint">The configured public HTTPS origin.</param>
    internal static void AddWebsiteAuthentication(IServiceCollection services, Uri publicEndpoint)
    {
        services.Configure<BlueskyAgentOptions>(options => options.OAuthOptions = new OAuthOptions
        {
            ClientId = new Uri(publicEndpoint, "/oauth-client-metadata.json").AbsoluteUri,
            ClientName = "Xenon labeler administration",
            ClientUri = publicEndpoint,
            ReturnUri = new Uri(publicEndpoint, "/Bluesky/Callback"),
            Scopes = ["atproto", "rpc:app.bsky.actor.getProfile?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview"]
        });
        services
            .AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme)
            .AddBluesky(options =>
            {
                options.LoginPath = BlueskyAuthenticationDefaults.LoginPath;
                options.LogoutPath = BlueskyAuthenticationDefaults.LogoutPath;
                options.AccessDeniedPath = "/Bluesky/Error";
            })
            .AddBlueskyAuthenticationUI();
        services.AddAuthorization();
        services.AddBlueskyClaimsTransformer();
        services.AddBlueskyOAuthClientMetadata();
    }

    /// <summary>Trusts only explicitly installed proxy addresses and never accepts arbitrary forwarded headers.</summary>
    internal static void ConfigureForwardedHeaders(WebApplication app)
    {
        string[] proxies = app.Configuration.GetSection("Labeler:TrustedProxies").Get<string[]>() ?? [];
        if (proxies.Length == 0)
        {
            return;
        }

        int forwardLimit = app.Configuration.GetValue("Labeler:ForwardLimit", 1);
        if (forwardLimit <= 0)
        {
            throw new InvalidOperationException("Labeler:ForwardLimit must be positive.");
        }

        ForwardedHeadersOptions options = new()
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = forwardLimit,
            RequireHeaderSymmetry = true
        };
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (string proxy in proxies)
        {
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        }

        app.UseForwardedHeaders(options);
    }
}
