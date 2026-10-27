// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.AccessControl;
using System.Security.Principal;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Authentication.SQLite;
using idunno.Bluesky.AspNet.Labeler;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Data.Sqlite;

using HostProgram = idunno.Bluesky.Labeler.Host.Program;

namespace Xenon.TestHost;

/// <summary>Runs a separate manual-test environment without registering or distributing a public labeler.</summary>
internal static class Program
{
    /// <summary>Starts the loopback host, optionally performing explicit first-time setup and seeding.</summary>
    private static async Task<int> Main(string[] args)
    {
        bool seed = args.Contains("--seed", StringComparer.Ordinal);
#if !DEBUG
        if (seed)
        {
            await Console.Error.WriteLineAsync("--seed is available only in Debug builds.");
            return 2;
        }
#endif
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args.Where(argument => argument != "--seed").ToArray());
        builder.WebHost.UseStaticWebAssets();
        Uri origin = ReadPublicOrigin(builder.Configuration["TestHost:PublicOrigin"]);
        string directory = Path.GetFullPath(builder.Configuration["TestHost:DataDirectory"] ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "idunno.Bluesky", "Xenon.TestHost"));
        ProtectDirectory(directory);
        string database = Path.Combine(directory, "xenon.db");
        string keyPath = Path.Combine(directory, "label-key.pem");
        if (!File.Exists(keyPath) && File.Exists(database))
        {
            throw new InvalidOperationException("The retained database has no test signing key. Restore its key or select a new test data directory.");
        }

        using LabelSigningKey key = LoadKey(keyPath);
        using HttpClient network = HostProgram.CreateIdentityClient();
        using HttpClient fixtures = new(new FixtureHandler(key.ExportPublicKeyMultibase(), network));
        string endpoint = builder.Configuration["TestHost:LocalEndpoint"] ??
            (OperatingSystem.IsWindows() ? "xenon-test-admin" : Path.Combine(directory, "admin.sock"));
        int port = builder.Configuration.GetValue("TestHost:Port", 5187);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        builder.WebHost.ConfigureKestrel(server => server.Listen(System.Net.IPAddress.Loopback, port));
        AddHostFiltering(builder.Services, origin);
        AddPersistentAuthentication(builder.Services, database);
        builder.Services.AddSqliteLabelStorage(new SqliteConnectionStringBuilder { DataSource = database }.ToString());
        builder.Services.AddLabelerModeration(FixtureHandler.Issuer, fixtures);
        builder.Services.AddLabelerAdministration(
            audience: new(FixtureHandler.Issuer, "labeler_admin"),
            signingKey: key,
            httpClient: fixtures);
        builder.Services.AddLabelerLocalAdministration(endpoint);
        HostProgram.AddWebsiteAuthentication(builder.Services, origin);
        builder.Services.AddSingleton<IDidHandleResolver>(serviceProvider => new DidHandleCache(new DidHandleCacheOptions
        {
            HttpClient = fixtures,
            LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>(),
            Size = 10_000,
            ResolutionTimeout = TimeSpan.FromSeconds(5)
        }));
        builder.Services.AddLabelerUI();
        builder.Services.AddHttpClient("Xenon.PublicPreviews")
            .ConfigurePrimaryHttpMessageHandler(() => new FixtureHandler(key.ExportPublicKeyMultibase(), network));
        builder.Services.AddRazorPages(options => options.Conventions.ConfigureFilter(new TestEnvironmentFilter()));

        await using WebApplication app = builder.Build();
        CancellationToken token = app.Lifetime.ApplicationStopping;
        await InitializeAuthenticationAsync(database, token);
        app.Services.GetRequiredService<SqliteXmlRepository>().Initialize(
            directory,
            app.Services.GetRequiredService<ILoggerFactory>());
        await app.Services.GetRequiredService<ILabelStore>().InitializeAsync(FixtureHandler.Issuer, token);
        await app.Services.GetRequiredService<idunno.Bluesky.Labeler.Moderation.ILabelPolicyStore>().InitializePolicyAsync(
            verificationFreshness: TimeSpan.FromMinutes(1),
            cancellationToken: token);
        if (seed)
        {
            Did administrator = new(builder.Configuration["TestHost:AdministratorDid"] ??
                throw new InvalidOperationException("Seeding requires TestHost:AdministratorDid: the real DID you will use to sign in."));
            await SeedData.InitializeAsync(
                services: app.Services,
                administrator: administrator,
                identity: fixtures,
                key: key,
                cancellationToken: token);
            await SeedData.InitializeRealPostsAsync(
                services: app.Services,
                administrator: administrator,
                identity: fixtures,
                key: key,
                cancellationToken: token);
        }

        ConfigurePipeline(app, origin);
        Console.WriteLine($"LOCAL TEST ENVIRONMENT. Public OAuth origin: {origin}. Local CLI endpoint: {endpoint}.");
        Console.WriteLine($"Test data: {directory}. No public distribution or report-intake routes are mapped.");
        await app.RunAsync();
        return 0;
    }

    /// <summary>Keeps encrypted OAuth identities and in-flight sign-in state alongside the harness's retained moderation data.</summary>
    internal static void AddPersistentAuthentication(IServiceCollection services, string database)
    {
        IDataProtectionBuilder protection = services.AddDataProtection().SetApplicationName("Xenon.TestHost");
        if (OperatingSystem.IsWindows())
        {
            protection.ProtectKeysWithDpapi();
        }

        services.AddSingleton(new SqliteXmlRepository(database));
        services.AddOptions<KeyManagementOptions>()
            .Configure<SqliteXmlRepository>((options, repository) => options.XmlRepository = repository);
        string connectionString = new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString();
        services.AddOptions<BlueskyAuthenticationOptions>(BlueskyAuthenticationDefaults.AuthenticationScheme)
            .Configure<ILoggerFactory>((options, loggerFactory) =>
            {
                options.IdentityStore = SqliteIdentityStore.CreateWithTimeProvider(
                    connectionString: connectionString,
                    timeProvider: options.TimeProvider ?? TimeProvider.System,
                    entryTimeToLive: options.IdentityStoreEntryTimeToLive ?? options.ExpireTimeSpan,
                    refreshLockLength: options.RefreshLockLength,
                    loggerFactory: loggerFactory);
                options.CorrelationStateCache = SqliteCorrelationStateCache.CreateWithTimeProvider(
                    connectionString: connectionString,
                    timeProvider: options.TimeProvider ?? TimeProvider.System,
                    loggerFactory: loggerFactory);
            });
    }

    /// <summary>Applies the authentication package's idempotent schema without duplicating or resetting existing tables.</summary>
    internal static async Task InitializeAuthenticationAsync(string database, CancellationToken cancellationToken)
    {
        using Stream schema = typeof(Program).Assembly.GetManifestResourceStream("Xenon.AuthenticationSchema.sql")
            ?? throw new InvalidOperationException("The SQLite authentication schema is missing.");
        using StreamReader reader = new(schema);
        string sql = await reader.ReadToEndAsync(cancellationToken);
        using SqliteConnection connection = new(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString());
        await connection.OpenAsync(cancellationToken);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Allows the configured tunnel host and explicit local browser addresses, never wildcard hosts.</summary>
    internal static void AddHostFiltering(IServiceCollection services, Uri origin)
    {
        services.PostConfigure<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>(options =>
        {
            options.AllowedHosts = [origin.IdnHost, "localhost", "127.0.0.1"];
            options.AllowEmptyHosts = false;
            options.IncludeFailureMessage = false;
        });
        services.AddHostFiltering(_ => { });
    }

    /// <summary>Installs proxy and hostname validation before public metadata, real authentication and protected administration.</summary>
    internal static void ConfigurePipeline(WebApplication app, Uri origin)
    {
        // Tunnel traffic must come from an explicitly trusted proxy; request headers never choose OAuth origins.
        HostProgram.ConfigureForwardedHeaders(app);
        app.UseHostFiltering();
        app.UseBlueskyOAuthClientMetadata(
            HostProgram.CreateNativeOAuthOptions(origin, new(FixtureHandler.Issuer, "labeler_admin")),
            new idunno.Bluesky.AspNet.Authentication.BlueskyOAuthClientMetadataOptions
            {
                ApplicationType = idunno.Bluesky.AspNet.Authentication.BlueskyOAuthClientApplicationType.Native
            });
        app.UseBlueskyOAuthClientMetadata();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapLabelerAdministration(remoteEnabled: true);
        app.MapStaticAssets();
        app.MapRazorPages().WithStaticAssets();
        app.MapGet("/", () => Results.Redirect("/Xenon"));
    }

    /// <summary>Validates an explicit HTTPS origin independently of forwarded or host headers.</summary>
    internal static Uri ReadPublicOrigin(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? origin) ||
            origin.Scheme != Uri.UriSchemeHttps || origin.HostNameType != UriHostNameType.Dns ||
            !string.IsNullOrEmpty(origin.UserInfo) || !string.IsNullOrEmpty(origin.Query) ||
            !string.IsNullOrEmpty(origin.Fragment) || origin.AbsolutePath != "/" || origin.Authority.Contains(':'))
        {
            throw new InvalidOperationException("Set TestHost:PublicOrigin to an HTTPS hostname without a nondefault port, path, credentials, query or fragment.");
        }

        return origin;
    }

    /// <summary>Retains the development-only signing identity without replacing missing or invalid keys.</summary>
    private static LabelSigningKey LoadKey(string path)
    {
        if (File.Exists(path))
        {
            return LabelSigningKey.ImportFromPem(File.ReadAllText(path));
        }

        using LabelSigningKey generated = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using (FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (StreamWriter writer = new(stream, new System.Text.UTF8Encoding(false)))
        {
            writer.Write(generated.ExportPkcs8PrivateKeyPem());
        }

        return LabelSigningKey.ImportFromPem(File.ReadAllText(path));
    }

    /// <summary>Restricts the harness database, disposable keys and authentication state to the current OS user.</summary>
    private static void ProtectDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            DirectorySecurity security = new();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                identity: identity.User!,
                fileSystemRights: FileSystemRights.FullControl,
                inheritanceFlags: InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                propagationFlags: PropagationFlags.None,
                type: AccessControlType.Allow));
            DirectoryInfo directory = Directory.CreateDirectory(path);
            directory.SetAccessControl(security);
        }
        else
        {
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
