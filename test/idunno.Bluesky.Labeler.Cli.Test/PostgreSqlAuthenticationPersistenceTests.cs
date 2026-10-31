// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Authentication.PostgreSQL;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Npgsql;

using HostProgram = idunno.Bluesky.Labeler.Host.Program;
using Repository = idunno.Bluesky.Labeler.Host.PostgreSqlXmlRepository;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Exercises canonical host authentication across independent lifetimes using a disposable PostgreSQL database.</summary>
public class PostgreSqlAuthenticationPersistenceTests
{
    [Fact]
    public async Task RestartRetainsCookieIdentityLoginStateAndRevocation()
    {
        string? database = Environment.GetEnvironmentVariable("LABELER_AUTH_TEST_POSTGRES");
        Assert.SkipUnless(database is not null, "Set LABELER_AUTH_TEST_POSTGRES to a disposable PostgreSQL database.");
        CancellationToken token = TestContext.Current.CancellationToken;
        Did did = new("did:plc:persistence");
        Guid correlation = Guid.NewGuid();
        string cookie;
        string protectedValue;
        using (ServiceProvider first = CreateServices(database))
        {
            Repository repository = first.GetRequiredService<Repository>();
            await repository.InitializeAsync(token);
            await repository.InitializeAsync(token);
            BlueskyAuthenticationOptions options = Options(first);
            Assert.IsType<PostgreSqlIdentityStore>(options.IdentityStore);
            Assert.IsType<PostgreSqlCorrelationStateCache>(options.CorrelationStateCache);
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
            using NpgsqlConnection connection = new(database);
            await connection.OpenAsync(token);
            using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = """SELECT "Identity" FROM idunno_bluesky_identities""";
            Assert.DoesNotContain("test-refresh-secret", Encoding.UTF8.GetString(Assert.IsType<byte[]>(await command.ExecuteScalarAsync(token))), StringComparison.Ordinal);
            command.CommandText = """SELECT "State" FROM idunno_bluesky_correlation_states""";
            Assert.DoesNotContain("test-pkce-secret", Assert.IsType<string>(await command.ExecuteScalarAsync(token)), StringComparison.Ordinal);
        }

        using (ServiceProvider second = CreateServices(database))
        {
            await second.GetRequiredService<Repository>().InitializeAsync(token);
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
            Repository repository = second.GetRequiredService<Repository>();
            repository.StoreElement(new XElement("test", new XAttribute("id", "one")), "duplicate");
            repository.StoreElement(new XElement("test", new XAttribute("id", "two")), "duplicate");
            XElement returned = repository.GetAllElements().First(element => element.Name == "test");
            returned.SetAttributeValue("id", "changed");
            Assert.Equal(2, repository.GetAllElements().Count(element => element.Name == "test"));
            Assert.Contains(repository.GetAllElements(), element => element.Attribute("id")?.Value == "one");
        }

        using ServiceProvider third = CreateServices(database);
        Assert.Throws<CryptographicException>(() =>
            third.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart").Unprotect(protectedValue));
        Assert.Null(await Options(third).CorrelationStateCache!.TakeOAuthLoginState(correlation, token));
        Assert.Null(await Options(third).IdentityStore!.GetIdentity(did, token));
    }

    /// <summary>Creates independent host services with a stable origin and the same database.</summary>
    private static ServiceProvider CreateServices(string database)
    {
        IServiceCollection services = WebApplication.CreateBuilder().Services;
        Uri origin = new("https://labeler.example");
        HostProgram.AddPersistentAuthentication(services, database, origin);
        HostProgram.AddWebsiteAuthentication(services, origin);
        return services.BuildServiceProvider();
    }

    /// <summary>Resolves the real named authentication scheme after postconfiguration.</summary>
    private static BlueskyAuthenticationOptions Options(ServiceProvider services) =>
        services.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>().Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
}
