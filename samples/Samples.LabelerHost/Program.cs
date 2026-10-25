// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Labels;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Labeler;
using idunno.Bluesky.Labeler;
using idunno.Security;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Samples.LabelerHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        Did issuer = new(Required(builder.Configuration, "Labeler:IssuerDid"));
        Uri publicEndpoint = new(Required(builder.Configuration, "Labeler:PublicEndpoint"));
        string provider = Required(builder.Configuration, "Labeler:DatabaseProvider");
        string connectionString = Required(builder.Configuration, "Labeler:ConnectionString");
        string keyDirectory = Required(builder.Configuration, "Labeler:SigningKeyDirectory");
        string activeKeyPath = Required(builder.Configuration, "Labeler:ActiveKeyPath");
        string localAdministrationEndpoint =
            builder.Configuration["Labeler:LocalAdministrationEndpoint"] ??
            (OperatingSystem.IsWindows() ? "idunno-labeler-admin" : "/run/labeler/admin.sock");
        AtProtoServiceReference audience = ParseAudience(Required(builder.Configuration, "Labeler:AdministrationAudience"));

        PemDirectoryLabelSigningKeyRing keyRing = new(keyDirectory, activeKeyPath);
        using HttpClient identityClient = CreateIdentityClient();
        builder.Services.AddSingleton<ILabelIssuancePolicyProvider>(
            new ConfigurationLabelIssuancePolicyProvider(builder.Configuration));
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

        builder.Services.AddBlueskyLabeler(
            issuer,
            new LabelerHostingOptions { PublicEndpoint = publicEndpoint });
        builder.Services.AddLabelerAdministration(
            audience: audience,
            signingKeyRing: keyRing,
            httpClient: identityClient);
        builder.Services.Configure<LabelerAdministrationOptions>(
            builder.Configuration.GetSection("LabelerAdministration"));
        builder.Services.AddLabelerLocalAdministration(localAdministrationEndpoint);

        await using WebApplication app = builder.Build();
        await app.Services.GetRequiredService<ILabelStore>()
            .InitializeAsync(issuer, app.Lifetime.ApplicationStopping)
            .ConfigureAwait(false);

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

            Console.WriteLine(JsonSerializer.Serialize(bootstrap.Result));
            return 0;
        }

        ConfigureForwardedHeaders(app);
        app.UseBlueskyOAuthClientMetadata(
            CreateNativeOAuthOptions(publicEndpoint, audience),
            new BlueskyOAuthClientMetadataOptions
            {
                ApplicationType = BlueskyOAuthClientApplicationType.Native
            });
        app.UseWebSockets();
        app.MapBlueskyLabeler();
        app.MapLabelerAdministration();
        await app.RunAsync().ConfigureAwait(false);
        return 0;
    }

    private static string Required(ConfigurationManager configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!
            : throw new InvalidOperationException($"Configuration value '{key}' is required.");

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
    internal static HttpClient CreateIdentityClient()
    {
        SocketsHttpHandler handler = SsrfSocketsHttpHandlerFactory.Create(new SsrfOptions
        {
            ConnectTimeout = TimeSpan.FromSeconds(10)
        });
        handler.AllowAutoRedirect = false;
        handler.MaxConnectionsPerServer = 16;
        handler.PooledConnectionLifetime = TimeSpan.FromMinutes(5);
        handler.UseCookies = false;
        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
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
            "purgeRetiredData", "beginRotation", "confirmHostStopped", "captureRotationBoundary",
            "verifyRotationKeyPublication", "resignRotationBatch", "completeRotation"
        ];
        return new OAuthOptions
        {
            ClientId = new Uri(publicEndpoint, "/native-client-metadata.json").AbsoluteUri,
            ClientName = "Labeler administration",
            ClientUri = publicEndpoint,
            ReturnUri = new Uri("http://127.0.0.1/"),
            Scopes = ["atproto", .. methods.Select(method => $"rpc:dev.idunno.labeler.admin.{method}?aud={encodedAudience}")]
        };
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

internal sealed class ConfigurationLabelIssuancePolicyProvider : ILabelIssuancePolicyProvider
{
    private readonly LabelIssuancePolicy _policy;

    internal ConfigurationLabelIssuancePolicyProvider(IConfiguration configuration)
    {
        string[] values = configuration["Labeler:DeclaredLabels"]?
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (values.Length == 0)
        {
            throw new InvalidOperationException("Configure at least one value in Labeler:DeclaredLabels.");
        }

        _policy = new(
            version: "configuration-v1",
            declaredValues: values,
            allowAccounts: configuration.GetValue<bool>("Labeler:AllowAccountLabels"),
            allowRecords: configuration.GetValue<bool>("Labeler:AllowRecordLabels"));
    }

    public Task<LabelServiceResult<LabelIssuancePolicy>> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LabelServiceResult<LabelIssuancePolicy>.Success(_policy));
    }
}
