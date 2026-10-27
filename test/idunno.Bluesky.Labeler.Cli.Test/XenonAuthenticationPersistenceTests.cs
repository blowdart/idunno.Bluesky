// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Authentication.SQLite;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xenon.TestHost;

using TestProgram = Xenon.TestHost.Program;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Exercises the test host's actual authentication registrations across independent service-provider lifetimes.</summary>
public class XenonAuthenticationPersistenceTests
{
    [Fact]
    public async Task RestartRetainsCookieIdentityAndSingleUseLoginState()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string directory = Path.Combine(Path.GetTempPath(), "xenon_auth_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "xenon.db");
        try
        {
            await TestProgram.InitializeAuthenticationAsync(database, token);
            Did did = new("did:plc:persistence");
            Guid correlationId = Guid.NewGuid();
            string accessToken = "eyJhbGciOiJub25lIn0." +
                Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"iss":"https://bsky.social","sub":"did:plc:persistence","exp":1767232800}"""))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".dGVzdA";
            OAuthLoginState login = new(
                startUrl: "https://bsky.social/oauth/authorize",
                state: "retained-state",
                codeVerifier: "retained-verifier",
                redirectUri: "https://xenon-test.example/Bluesky/Callback",
                error: string.Empty,
                errorDescription: string.Empty,
                expectedAuthority: "https://bsky.social",
                expectedService: "https://bsky.social",
                proofKey: "retained-proof-key",
                correlationId: correlationId);
            string cookie;
            using (ServiceProvider first = CreateServices(database, directory))
            {
                BlueskyAuthenticationOptions options = Options(first);
                Assert.IsType<SqliteIdentityStore>(options.IdentityStore);
                Assert.IsType<SqliteCorrelationStateCache>(options.CorrelationStateCache);
                ClaimsIdentity identity = new(
                    [
                        new Claim(type: AtProtoClaims.Did, value: did.Value, valueType: ClaimValueTypes.String, issuer: "https://bsky.social"),
                        new Claim(type: AtProtoClaims.AccessToken, value: accessToken, valueType: ClaimValueTypes.String, issuer: "https://bsky.social"),
                        new Claim(type: AtProtoClaims.RefreshToken, value: "retained-refresh-token", valueType: ClaimValueTypes.String, issuer: "https://bsky.social"),
                        new Claim(type: AtProtoClaims.DPoPProof, value: "retained-proof-key", valueType: ClaimValueTypes.String, issuer: "https://bsky.social"),
                        new Claim(type: AtProtoClaims.DPoPNonce, value: "retained-nonce", valueType: ClaimValueTypes.String, issuer: "https://bsky.social")
                    ],
                    BlueskyAuthenticationDefaults.AuthenticationScheme);
                await options.IdentityStore.Add(identity, token);
                await options.CorrelationStateCache.AddOAuthLoginState(correlationId, login, token);
                cookie = options.TicketDataFormat.Protect(new AuthenticationTicket(
                    principal: new ClaimsPrincipal(new ClaimsIdentity([new Claim(AtProtoClaims.Did, did.Value)])),
                    properties: new AuthenticationProperties
                    {
                        IssuedUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                        ExpiresUtc = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
                    },
                    authenticationScheme: BlueskyAuthenticationDefaults.AuthenticationScheme));
                using SqliteConnection connection = new($"Data Source={database};Pooling=False");
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "SELECT CAST(Identity AS TEXT) FROM idunno_bluesky_identities";
                Assert.DoesNotContain(accessToken, Assert.IsType<string>(command.ExecuteScalar()), StringComparison.Ordinal);
                command.CommandText = "SELECT State FROM idunno_bluesky_correlation_states";
                Assert.DoesNotContain("retained-verifier", Assert.IsType<string>(command.ExecuteScalar()), StringComparison.Ordinal);
            }

            await TestProgram.InitializeAuthenticationAsync(database, token);
            using (ServiceProvider second = CreateServices(database, directory))
            {
                BlueskyAuthenticationOptions options = Options(second);
                AuthenticationTicket ticket = Assert.IsType<AuthenticationTicket>(options.TicketDataFormat.Unprotect(cookie));
                Assert.Equal(did.Value, ticket.Principal.FindFirst(AtProtoClaims.Did)?.Value);
                ClaimsIdentity identity = Assert.IsType<ClaimsIdentity>(await options.IdentityStore!.GetIdentity(did, token));
                Assert.Equal(accessToken, identity.FindFirst(AtProtoClaims.AccessToken)?.Value);
                using IServiceScope requestScope = second.CreateScope();
                DefaultHttpContext request = new() { RequestServices = requestScope.ServiceProvider };
                request.Request.Scheme = "https";
                request.Request.Headers.Cookie = $"{options.Cookie.Name}={cookie}";
                AuthenticateResult result = await request.AuthenticateAsync(BlueskyAuthenticationDefaults.AuthenticationScheme);
                Assert.True(result.Succeeded, result.Failure?.Message);
                Assert.Equal(accessToken, result.Principal?.FindFirst(AtProtoClaims.AccessToken)?.Value);
                OAuthLoginState retained = Assert.IsType<OAuthLoginState>(
                    await options.CorrelationStateCache!.TakeOAuthLoginState(correlationId, token));
                Assert.Equal(login.CodeVerifier, retained.CodeVerifier);
                Assert.Equal(login.ProofKey, retained.ProofKey);
                await options.IdentityStore.Remove(did, token);
            }

            using ServiceProvider third = CreateServices(database, directory);
            Assert.Null(await Options(third).CorrelationStateCache!.TakeOAuthLoginState(correlationId, token));
            Assert.Null(await Options(third).IdentityStore!.GetIdentity(did, token));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExistingFileKeysMigrateOnceAndRevocationSurvivesRestart()
    {
        string directory = Path.Combine(Path.GetTempPath(), "xenon_keys_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, "xenon.db");
        try
        {
            string protectedValue;
            ServiceCollection legacyServices = new();
            legacyServices.AddDataProtection()
                .SetApplicationName("Xenon.TestHost")
                .PersistKeysToFileSystem(new DirectoryInfo(directory));
            using (ServiceProvider legacy = legacyServices.BuildServiceProvider())
            {
                protectedValue = legacy.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart").Protect("retained");
            }

            using (ServiceProvider first = CreateServices(database, directory))
            {
                Assert.Equal("retained", first.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart").Unprotect(protectedValue));
                SqliteXmlRepository repository = first.GetRequiredService<SqliteXmlRepository>();
                int count = repository.GetAllElements().Count;
                repository.Initialize(directory, NullLoggerFactory.Instance);
                Assert.Equal(count, repository.GetAllElements().Count);
                first.GetRequiredService<IKeyManager>().RevokeAllKeys(DateTimeOffset.MaxValue, "Test revocation");
            }

            using ServiceProvider second = CreateServices(database, directory);
            IDataProtector protector = second.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart");
            Assert.Throws<CryptographicException>(() => protector.Unprotect(protectedValue));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RepositoryAppendsElementsWithDuplicateNamesAndReturnsIndependentXml()
    {
        string directory = Path.Combine(Path.GetTempPath(), "xenon_xml_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            SqliteXmlRepository repository = new(Path.Combine(directory, "xenon.db"));
            repository.Initialize(directory, NullLoggerFactory.Instance);
            repository.StoreElement(new XElement("key", new XAttribute("id", "one")), "same-name");
            repository.StoreElement(new XElement("revocation", new XAttribute("id", "two")), "same-name");
            IReadOnlyCollection<XElement> original = repository.GetAllElements();
            Assert.Equal(2, original.Count);
            original.First().SetAttributeValue("id", "changed");
            SqliteXmlRepository restarted = new(Path.Combine(directory, "xenon.db"));
            Assert.Equal("one", restarted.GetAllElements().First().Attribute("id")?.Value);
            Assert.Equal("revocation", restarted.GetAllElements().Last().Name.LocalName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Builds a fresh application container with the same named authentication configuration as the host.</summary>
    private static ServiceProvider CreateServices(string database, string directory)
    {
        IServiceCollection services = WebApplication.CreateBuilder().Services;
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        services.AddOptions<BlueskyAuthenticationOptions>(BlueskyAuthenticationDefaults.AuthenticationScheme)
            .Configure(options => options.TimeProvider = clock);
        TestProgram.AddPersistentAuthentication(services, database);
        idunno.Bluesky.Labeler.Host.Program.AddWebsiteAuthentication(services, new Uri("https://xenon-test.example"));
        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<SqliteXmlRepository>().Initialize(directory, NullLoggerFactory.Instance);
        return provider;
    }

    /// <summary>Resolves the scheme-specific postconfigured stores and ticket protector.</summary>
    private static BlueskyAuthenticationOptions Options(ServiceProvider provider) =>
        provider.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>().Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
}
