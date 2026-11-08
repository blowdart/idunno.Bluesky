// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Authentication.MySQL;
using idunno.Bluesky.AspNet.Authentication.PostgreSQL;
using idunno.Bluesky.Labeler.Host;
using idunno.Bluesky.Labeler.Host.MySQL;
using idunno.Bluesky.Labeler.Host.PostgreSQL;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using MySqlConnector;

using Npgsql;

using HostProgram = idunno.Bluesky.Labeler.Host.Program;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Exercises both canonical providers across independent lifetimes using disposable external databases.</summary>
public class RelationalHostAuthenticationPersistenceTests
{
    [Theory]
    [InlineData("postgresql", 0)]
    [InlineData("postgresql", 1)]
    [InlineData("mysql", 0)]
    [InlineData("mysql", 1)]
    public async Task RestartRetainsCookieIdentityLoginStateAndRevocation(string engine, int invocation)
    {
        string variable = engine == "mysql" ? "LABELER_TEST_MYSQL_CONNECTION" : "LABELER_TEST_POSTGRESQL_CONNECTION";
        string administrativeConnection = Environment.GetEnvironmentVariable(variable)
            ?? throw new InvalidOperationException($"Set {variable} to an administrative connection on a disposable database server. Authentication conformance never skips.");
        administrativeConnection = engine == "mysql"
            ? new MySqlConnectionStringBuilder(administrativeConnection)
            {
                Pooling = true,
                MinimumPoolSize = 0,
                MaximumPoolSize = 4,
                ConnectionTimeout = 15,
                DefaultCommandTimeout = 15
            }.ConnectionString
            : new NpgsqlConnectionStringBuilder(administrativeConnection)
            {
                Pooling = true,
                MinPoolSize = 0,
                MaxPoolSize = 4,
                Timeout = 15,
                CommandTimeout = 15
            }.ConnectionString;
        string name = $"labeler_auth_{invocation}_{Guid.NewGuid():N}";
        string database = engine == "mysql"
            ? new MySqlConnectionStringBuilder(administrativeConnection) { Database = name }.ConnectionString
            : new NpgsqlConnectionStringBuilder(administrativeConnection) { Database = name }.ConnectionString;
        await using DbConnection administration = engine == "mysql"
            ? new MySqlConnection(administrativeConnection)
            : new NpgsqlConnection(administrativeConnection);
        await administration.OpenAsync(TestContext.Current.CancellationToken);
        await using DbCommand create = administration.CreateCommand();
#pragma warning disable CA2100 // The identifier contains only a fixed prefix, an integer and generated hexadecimal digits.
        create.CommandText = $"CREATE DATABASE {name}";
#pragma warning restore CA2100
        await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        try
        {
            await VerifyPersistenceAsync(engine, database);
        }
        finally
        {
            await using (DbConnection pooled = engine == "mysql" ? new MySqlConnection(database) : new NpgsqlConnection(database))
            {
                if (pooled is MySqlConnection mysql)
                {
                    await MySqlConnection.ClearPoolAsync(mysql, CancellationToken.None);
                }
                else
                {
                    NpgsqlConnection.ClearPool((NpgsqlConnection)pooled);
                }
            }
            await using DbCommand drop = administration.CreateCommand();
#pragma warning disable CA2100 // The identifier is the same generated fixture-owned database name used above.
            drop.CommandText = engine == "mysql" ? $"DROP DATABASE {name}" : $"DROP DATABASE {name} WITH (FORCE)";
#pragma warning restore CA2100
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    /// <summary>Verifies authentication persistence and revocation across independent service lifetimes in an isolated database.</summary>
    private static async Task VerifyPersistenceAsync(string engine, string database)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        Did did = new("did:plc:persistence");
        Guid correlation = Guid.NewGuid();
        string cookie;
        string protectedValue;
        using (ServiceProvider first = CreateServices(engine, database))
        {
            ILabelerHostAuthenticationPersistence persistence = first.GetRequiredService<ILabelerHostAuthenticationPersistence>();
            await persistence.InitializeAsync(token);
            await persistence.InitializeAsync(token);
            BlueskyAuthenticationOptions options = Options(first);
            Assert.Equal(engine == "mysql" ? typeof(MySqlIdentityStore) : typeof(PostgreSqlIdentityStore), options.IdentityStore!.GetType());
            Assert.Equal(engine == "mysql" ? typeof(MySqlCorrelationStateCache) : typeof(PostgreSqlCorrelationStateCache), options.CorrelationStateCache!.GetType());
            ClaimsIdentity identity = new(
                [
                    new Claim(AtProtoClaims.Did, did.Value),
                    new Claim(AtProtoClaims.RefreshToken, "test-refresh-secret")
                ],
                BlueskyAuthenticationDefaults.AuthenticationScheme);
            await options.IdentityStore.Add(identity, token);
            await options.CorrelationStateCache.AddOAuthLoginState(correlation, new OAuthLoginState(
                startUrl: "https://bsky.social/oauth/authorize",
                state: "test-state",
                codeVerifier: "test-pkce-secret",
                redirectUri: "https://labeler.example/Bluesky/Callback",
                error: string.Empty,
                errorDescription: string.Empty,
                expectedAuthority: "https://bsky.social",
                expectedService: "https://bsky.social",
                proofKey: "test-proof-secret",
                correlationId: correlation), token);
            cookie = options.TicketDataFormat.Protect(new AuthenticationTicket(
                principal: new ClaimsPrincipal(identity),
                properties: new AuthenticationProperties(),
                authenticationScheme: BlueskyAuthenticationDefaults.AuthenticationScheme));
            protectedValue = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart").Protect("retained");
            using DbConnection connection = engine == "mysql" ? new MySqlConnection(database) : new NpgsqlConnection(database);
            await connection.OpenAsync(token);
            using DbCommand command = connection.CreateCommand();
            command.CommandText = engine == "mysql"
                ? "SELECT `Identity` FROM idunno_bluesky_identities"
                : """SELECT "Identity" FROM idunno_bluesky_identities""";
            Assert.DoesNotContain("test-refresh-secret", Encoding.UTF8.GetString(Assert.IsType<byte[]>(await command.ExecuteScalarAsync(token))), StringComparison.Ordinal);
            command.CommandText = engine == "mysql"
                ? "SELECT `State` FROM idunno_bluesky_correlation_states"
                : """SELECT "State" FROM idunno_bluesky_correlation_states""";
            Assert.DoesNotContain("test-pkce-secret", Assert.IsType<string>(await command.ExecuteScalarAsync(token)), StringComparison.Ordinal);
        }

        using (ServiceProvider otherOrigin = CreateServices(engine, database, new Uri("https://another-labeler.example")))
        {
            Assert.Throws<CryptographicException>(() =>
                otherOrigin.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart").Unprotect(protectedValue));
        }

        using (ServiceProvider second = CreateServices(engine, database))
        {
            await second.GetRequiredService<ILabelerHostAuthenticationPersistence>().InitializeAsync(token);
            BlueskyAuthenticationOptions options = Options(second);
            AuthenticationTicket ticket = Assert.IsType<AuthenticationTicket>(options.TicketDataFormat.Unprotect(cookie));
            Assert.Equal(did.Value, ticket.Principal.FindFirst(AtProtoClaims.Did)?.Value);
            ClaimsIdentity retained = Assert.IsType<ClaimsIdentity>(await options.IdentityStore!.GetIdentity(did, token));
            Assert.Equal("test-refresh-secret", retained.FindFirst(AtProtoClaims.RefreshToken)?.Value);
            OAuthLoginState login = Assert.IsType<OAuthLoginState>(await options.CorrelationStateCache!.TakeOAuthLoginState(correlation, token));
            Assert.Equal("test-pkce-secret", login.CodeVerifier);
            Assert.Equal("retained", second.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart").Unprotect(protectedValue));
            second.GetRequiredService<IKeyManager>().RevokeAllKeys(DateTimeOffset.MaxValue, "Test revocation");
            await options.IdentityStore.Remove(did, token);
            IXmlRepository repository = second.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository!;
            repository.StoreElement(new XElement("test", new XAttribute("id", "one")), "duplicate");
            repository.StoreElement(new XElement("test", new XAttribute("id", "two")), "duplicate");
            XElement returned = repository.GetAllElements().First(element => element.Name == "test");
            returned.SetAttributeValue("id", "changed");
            Assert.Equal(2, repository.GetAllElements().Count(element => element.Name == "test"));
            Assert.Contains(repository.GetAllElements(), element => element.Attribute("id")?.Value == "one");
        }

        using ServiceProvider third = CreateServices(engine, database);
        Assert.Throws<CryptographicException>(() =>
            third.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart").Unprotect(protectedValue));
        Assert.Null(await Options(third).CorrelationStateCache!.TakeOAuthLoginState(correlation, token));
        Assert.Null(await Options(third).IdentityStore!.GetIdentity(did, token));
    }

    /// <summary>Creates independent host services with a stable origin and the same database.</summary>
    private static ServiceProvider CreateServices(string engine, string database, Uri? origin = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        Uri endpoint = origin ?? new Uri("https://labeler.example");
        builder.Configuration["Labeler:ConnectionString"] = database;
        builder.Configuration["Labeler:PublicEndpoint"] = endpoint.AbsoluteUri;
        builder.Configuration["LabelerUI:Enabled"] = "true";
        if (engine == "mysql")
        {
            builder.AddMySqlCanonicalLabelerStorage();
        }
        else
        {
            builder.AddPostgreSqlCanonicalLabelerStorage();
        }
        HostProgram.AddWebsiteAuthentication(builder.Services, endpoint);
        return builder.Services.BuildServiceProvider();
    }

    /// <summary>Resolves the real named authentication scheme after postconfiguration.</summary>
    private static BlueskyAuthenticationOptions Options(ServiceProvider services) =>
        services.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>().Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
}
