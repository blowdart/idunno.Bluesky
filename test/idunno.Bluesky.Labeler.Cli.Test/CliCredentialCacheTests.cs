// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace idunno.Bluesky.Labeler.Cli.Test;

public sealed class CliCredentialCacheTests : IDisposable
{
    private static readonly DateTimeOffset s_expiration = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly string _directory = Path.Combine(Environment.CurrentDirectory, ".credential-cache-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DiscoveryDoesNotCreateAMissingCacheDirectory()
    {
        CliCredentialCacheBinding binding = CreateBinding();
        IReadOnlyList<string> accounts = await CliCredentialCache.FindAccountsAsync(
            clientMetadataId: binding.ClientMetadataId,
            requestedScope: binding.RequestedScope,
            directory: _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(accounts);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task ListingAnEmptyCacheDoesNotCreateADirectory()
    {
        using StringWriter output = new();
        int exitCode = await Program.ListCredentialsAsync(
            output: output,
            directory: _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("No saved credentials", output.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task ListingLegacyAdministratorCredentialsIsReadOnlyAndDoesNotRevealSecrets()
    {
        CliCredentialCacheBinding binding = CreateBinding();
        DPoPAccessCredentials credentials = CreateCredentials(binding, s_expiration);
        await using CliCredentialCache cache = CreateCache(binding);
        await cache.SaveAsync(credentials, TestContext.Current.CancellationToken);

        CliCredentialCacheEntry entry = new(
            Version: 1,
            Binding: binding,
            AccessJwt: credentials.AccessJwt,
            RefreshToken: credentials.RefreshToken,
            DPoPProofKey: credentials.DPoPProofKey,
            DPoPNonce: credentials.DPoPNonce,
            ClientSigningKeyId: credentials.ClientSigningKeyId);
        JsonObject legacy = JsonSerializer.SerializeToNode(entry, CliCredentialCacheJsonContext.Default.CliCredentialCacheEntry)!.AsObject();
        Assert.True(legacy.Remove(nameof(CliCredentialCacheEntry.LabelerOrigins)));
        byte[] legacyBytes = Encoding.UTF8.GetBytes(legacy.ToJsonString());
        if (OperatingSystem.IsWindows())
        {
            legacyBytes = CliCredentialProtection.Protect(legacyBytes);
        }

        string path = CredentialPath();
        await File.WriteAllBytesAsync(path, legacyBytes, TestContext.Current.CancellationToken);
        string[] filesBefore = [.. Directory.GetFiles(_directory).Order(StringComparer.Ordinal)];
        using StringWriter output = new();
        int exitCode = await Program.ListCredentialsAsync(
            output: output,
            directory: _directory,
            cancellationToken: TestContext.Current.CancellationToken);
        IReadOnlyList<string> accounts = await CliCredentialCache.FindAccountsAsync(
            clientMetadataId: binding.ClientMetadataId,
            requestedScope: binding.RequestedScope,
            directory: _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        string text = output.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains(binding.Account, text, StringComparison.Ordinal);
        Assert.Contains(binding.Actor, text, StringComparison.Ordinal);
        Assert.Contains("https://labeler.example", text, StringComparison.Ordinal);
        Assert.Contains(binding.RequestedScope, text, StringComparison.Ordinal);
        Assert.DoesNotContain(binding.Service.AbsoluteUri, text, StringComparison.Ordinal);
        Assert.DoesNotContain(credentials.AccessJwt, text, StringComparison.Ordinal);
        Assert.DoesNotContain(credentials.RefreshToken, text, StringComparison.Ordinal);
        Assert.DoesNotContain(credentials.DPoPProofKey, text, StringComparison.Ordinal);
        Assert.DoesNotContain(credentials.DPoPNonce, text, StringComparison.Ordinal);
        Assert.DoesNotContain(credentials.ClientSigningKeyId!, text, StringComparison.Ordinal);
        Assert.Equal(["operator.example"], accounts);
        Assert.Equal(legacyBytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(filesBefore, Directory.GetFiles(_directory).Order(StringComparer.Ordinal));
        Assert.NotNull(await cache.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("atproto identity:*")]
    [InlineData("atproto repo:app.bsky.labeler.service")]
    public async Task ListingLegacyNonAdministratorCredentialsDoesNotGuessAnOrigin(string scope)
    {
        CliCredentialCacheBinding binding = CreateBinding() with
        {
            ClientMetadataId = "https://oauth.example/client-metadata.json",
            RequestedScope = scope
        };
        await using CliCredentialCache cache = CreateCache(binding);
        await cache.SaveAsync(CreateCredentials(binding, s_expiration), TestContext.Current.CancellationToken);

        IReadOnlyList<CliCredentialCacheSummary> summaries = await CliCredentialCache.ListAsync(
            _directory,
            TestContext.Current.CancellationToken);
        using StringWriter output = new();
        await Program.ListCredentialsAsync(
            output: output,
            directory: _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(Assert.Single(summaries).LabelerOrigins);
        Assert.Contains("unknown (not recorded)", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("https://oauth.example", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("https://pds.example", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListingMergesKnownOriginsWithoutReplacingNewerCredentials()
    {
        CliCredentialCacheBinding binding = CreateBinding() with { RequestedScope = "atproto identity:*" };
        DPoPAccessCredentials newer = CreateCredentials(binding, s_expiration);
        newer.DPoPNonce = "newer-nonce";
        await using (CliCredentialCache cache = new(
            binding: binding,
            directory: _directory,
            allowUnencryptedStorage: true,
            labelerOrigin: new Uri("https://first.example")))
        {
            await cache.SaveAsync(newer, TestContext.Current.CancellationToken);
        }

        string originalPath = CredentialPath();
        await using (Program.CliSignInLifetime lifetime = new(_directory))
        {
            CliCredentialCache? reused = await lifetime.GetCacheAsync(
                binding: binding,
                rememberMe: false,
                cancellationToken: TestContext.Current.CancellationToken,
                labelerOrigin: new Uri("https://second.example"));
            Assert.NotNull(reused);
            DPoPAccessCredentials older = CreateCredentials(binding, s_expiration.AddHours(-1));
            Assert.False(await reused.SaveAsync(older, TestContext.Current.CancellationToken));

            CliCredentialCache? reusedAgain = await lifetime.GetCacheAsync(
                binding: binding,
                rememberMe: false,
                cancellationToken: TestContext.Current.CancellationToken,
                labelerOrigin: new Uri("https://third.example"));
            Assert.Same(reused, reusedAgain);
            Assert.False(await reusedAgain!.SaveAsync(older, TestContext.Current.CancellationToken));
        }

        IReadOnlyList<CliCredentialCacheSummary> summaries = await CliCredentialCache.ListAsync(
            _directory,
            TestContext.Current.CancellationToken);
        Assert.Equal(["https://first.example", "https://second.example", "https://third.example"], Assert.Single(summaries).LabelerOrigins);
        Assert.Equal(originalPath, CredentialPath());
        await using CliCredentialCache restored = CreateCache(binding);
        DPoPAccessCredentials? credentials = await restored.LoadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(credentials);
        Assert.Equal(s_expiration, credentials.ExpiresOn);
        Assert.Equal("newer-nonce", credentials.DPoPNonce);
    }

    [Theory]
    [InlineData("https://b\u00fccher.example", "https://xn--bcher-kva.example")]
    [InlineData("https://[::1]:8443", "https://[::1]:8443")]
    public async Task ListingNormalizesEquivalentAdministratorOrigins(string configuredOrigin, string metadataOrigin)
    {
        CliCredentialCacheBinding binding = CreateBinding() with
        {
            ClientMetadataId = $"{metadataOrigin}/client-metadata.json"
        };
        await using CliCredentialCache cache = new(
            binding: binding,
            directory: _directory,
            allowUnencryptedStorage: true,
            labelerOrigin: new Uri(configuredOrigin));
        await cache.SaveAsync(CreateCredentials(binding, s_expiration), TestContext.Current.CancellationToken);

        IReadOnlyList<CliCredentialCacheSummary> summaries = await CliCredentialCache.ListAsync(
            _directory,
            TestContext.Current.CancellationToken);

        Assert.Equal([metadataOrigin], Assert.Single(summaries).LabelerOrigins);
    }

    [Fact]
    public async Task ListingSeparatesAccountsAndScopesAndSanitizesUntrustedText()
    {
        CliCredentialCacheBinding administrator = CreateBinding();
        CliCredentialCacheBinding owner = administrator with
        {
            Account = "owner.example",
            Actor = "did:plc:owner",
            RequestedScope = "atproto identity:*\u001b[31m"
        };
        await using (CliCredentialCache cache = CreateCache(administrator))
        {
            await cache.SaveAsync(CreateCredentials(administrator, s_expiration), TestContext.Current.CancellationToken);
        }

        await using (CliCredentialCache cache = new(
            binding: owner,
            directory: _directory,
            allowUnencryptedStorage: true,
            labelerOrigin: new Uri("https://second.example")))
        {
            await cache.SaveAsync(CreateCredentials(owner, s_expiration), TestContext.Current.CancellationToken);
        }

        using StringWriter output = new();
        await Program.ListCredentialsAsync(
            output: output,
            directory: _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        string text = output.ToString();
        Assert.Contains("operator.example", text, StringComparison.Ordinal);
        Assert.Contains("owner.example", text, StringComparison.Ordinal);
        Assert.Contains("https://labeler.example", text, StringComparison.Ordinal);
        Assert.Contains("https://second.example", text, StringComparison.Ordinal);
        Assert.Contains("identity:* [31m", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', text);
        Assert.Equal(2, (await CliCredentialCache.ListAsync(_directory, TestContext.Current.CancellationToken)).Count);
    }

    [Theory]
    [InlineData("client")]
    [InlineData("method")]
    [InlineData("audience")]
    [InlineData("account")]
    public async Task DiscoveryMatchesTheExactClientMethodAndAudienceWithoutLeasingOtherSessions(string changedField)
    {
        CliCredentialCacheBinding binding = CreateBinding();
        CliCredentialCacheBinding changed = changedField switch
        {
            "client" => binding with { ClientMetadataId = "https://other.example/client-metadata.json" },
            "method" => binding with { RequestedScope = binding.RequestedScope.Replace("getTeam", "getAudit", StringComparison.Ordinal) },
            "audience" => binding with { RequestedScope = binding.RequestedScope.Replace("labeler%23", "other%23", StringComparison.Ordinal) },
            "account" => binding with { Account = "other.example", Actor = "did:plc:other" },
            _ => throw new ArgumentOutOfRangeException(nameof(changedField))
        };
        await using CliCredentialCache saved = CreateCache(binding);
        await saved.SaveAsync(CreateCredentials(binding, s_expiration), TestContext.Current.CancellationToken);
        await using CliCredentialCache other = CreateCache(changed);
        await other.SaveAsync(CreateCredentials(changed, s_expiration), TestContext.Current.CancellationToken);

        IReadOnlyList<string> accounts = await CliCredentialCache.FindAccountsAsync(
            clientMetadataId: binding.ClientMetadataId,
            requestedScope: binding.RequestedScope,
            directory: _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        string[] expected = changedField == "account" ? ["operator.example", "other.example"] : ["operator.example"];
        Assert.Equal(expected, accounts);
    }

    [Fact]
    public async Task DiscoveryRejectsAnEnvelopeCopiedToAnotherFilename()
    {
        CliCredentialCacheBinding binding = CreateBinding();
        await using CliCredentialCache saved = CreateCache(binding);
        await saved.SaveAsync(CreateCredentials(binding, s_expiration), TestContext.Current.CancellationToken);
        File.Move(CredentialPath(), Path.Combine(_directory, "oauth-wrong.credential"));

        await Assert.ThrowsAsync<InvalidDataException>(() => CliCredentialCache.FindAccountsAsync(
            clientMetadataId: binding.ClientMetadataId,
            requestedScope: binding.RequestedScope,
            directory: _directory,
            cancellationToken: TestContext.Current.CancellationToken));
        using StringWriter output = new();
        await Assert.ThrowsAsync<InvalidDataException>(() => Program.ListCredentialsAsync(
            output: output,
            directory: _directory,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(string.Empty, output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingSessionIsReusedAndUpdatedWithoutRememberMe(bool identitySession)
    {
        CliCredentialCacheBinding binding = identitySession
            ? CreateBinding() with { RequestedScope = "atproto identity:*" }
            : CreateBinding();
        Uri? labelerOrigin = identitySession ? new Uri("https://other-labeler.example") : null;
        await using (CliCredentialCache saved = CreateCache(binding))
        {
            await saved.SaveAsync(CreateCredentials(binding, s_expiration), TestContext.Current.CancellationToken);
        }

        await using (Program.CliSignInLifetime lifetime = new(_directory))
        {
            CliCredentialCache? reused = await lifetime.GetCacheAsync(
                binding: binding,
                rememberMe: false,
                cancellationToken: TestContext.Current.CancellationToken,
                labelerOrigin: labelerOrigin);
            Assert.NotNull(reused);
            DPoPAccessCredentials? credentials = await reused.LoadAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(credentials);
            credentials.DPoPNonce = "renewed-nonce";
            using BlueskyAgent agent = new();
            reused.Attach(agent);
            Assert.NotNull(agent.CredentialsUpdatedAsync);
            await agent.CredentialsUpdatedAsync(
                new CredentialsUpdatedEventArgs(new Did(binding.Actor), binding.Service, credentials),
                TestContext.Current.CancellationToken);
            Assert.Same(reused, await lifetime.GetCacheAsync(
                binding: binding,
                rememberMe: false,
                cancellationToken: TestContext.Current.CancellationToken,
                labelerOrigin: labelerOrigin));
        }

        await using CliCredentialCache restored = CreateCache(binding);
        Assert.Equal("renewed-nonce", (await restored.LoadAsync(TestContext.Current.CancellationToken))?.DPoPNonce);
        if (identitySession)
        {
            IReadOnlyList<CliCredentialCacheSummary> summaries = await CliCredentialCache.ListAsync(
                _directory,
                TestContext.Current.CancellationToken);
            Assert.Equal(["https://other-labeler.example"], Assert.Single(summaries).LabelerOrigins);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewSessionIsNotSavedWithoutRememberMe(bool anotherScopeWasSaved)
    {
        CliCredentialCacheBinding binding = CreateBinding();
        if (anotherScopeWasSaved)
        {
            CliCredentialCacheBinding otherBinding = binding with { RequestedScope = "atproto identity:*" };
            await using CliCredentialCache saved = CreateCache(otherBinding);
            await saved.SaveAsync(CreateCredentials(otherBinding, s_expiration), TestContext.Current.CancellationToken);
        }

        await using Program.CliSignInLifetime lifetime = new(_directory);
        Assert.Null(await lifetime.GetCacheAsync(
            binding: binding,
            rememberMe: false,
            cancellationToken: TestContext.Current.CancellationToken));
        if (anotherScopeWasSaved)
        {
            Assert.Single(Directory.GetFiles(_directory, "*.credential"));
        }
        else
        {
            Assert.False(Directory.Exists(_directory));
        }
    }

    [Fact]
    public async Task RoundTripPreservesAllCredentialFieldsAndSigningKey()
    {
        CliCredentialCacheBinding binding = CreateBinding();
        DPoPAccessCredentials original = CreateCredentials(binding, s_expiration);
        await using (CliCredentialCache cache = CreateCache(binding))
        {
            Assert.True(await cache.SaveAsync(original, TestContext.Current.CancellationToken));
        }

        await using CliCredentialCache restoredCache = CreateCache(binding);
        DPoPAccessCredentials? restored = await restoredCache.LoadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(restored);
        Assert.Equal(original.AccessJwt, restored.AccessJwt);
        Assert.Equal(original.RefreshToken, restored.RefreshToken);
        Assert.Equal(original.DPoPProofKey, restored.DPoPProofKey);
        Assert.Equal(original.DPoPNonce, restored.DPoPNonce);
        Assert.Equal(original.ClientSigningKeyId, restored.ClientSigningKeyId);
        Assert.Equal(original.OAuthClientId, restored.OAuthClientId);
        Assert.Equal(original.RequestedScope, restored.RequestedScope);
        Assert.Equal(original.Did, restored.Did);
        Assert.Equal(original.Service, restored.Service);
        Assert.Equal(original.ExpiresOn, restored.ExpiresOn);

        byte[] disk = await File.ReadAllBytesAsync(CredentialPath(), TestContext.Current.CancellationToken);
        if (OperatingSystem.IsWindows())
        {
            Assert.DoesNotContain(original.RefreshToken, Encoding.UTF8.GetString(disk), StringComparison.Ordinal);
            Assert.DoesNotContain(original.DPoPProofKey, Encoding.UTF8.GetString(disk), StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(_directory));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(CredentialPath()));
        }
    }

    [Fact]
    public async Task DeleteAllRemovesSavedCredentialsButKeepsOtherCacheFiles()
    {
        CliCredentialCacheBinding firstBinding = CreateBinding();
        CliCredentialCacheBinding secondBinding = firstBinding with { RequestedScope = "atproto rpc:dev.idunno.labeler.admin.getAudit?aud=did%3Aplc%3Alabeler%23labeler_admin" };
        await using (CliCredentialCache first = CreateCache(firstBinding))
        {
            await first.SaveAsync(CreateCredentials(firstBinding, s_expiration), TestContext.Current.CancellationToken);
        }

        await using (CliCredentialCache second = CreateCache(secondBinding))
        {
            await second.SaveAsync(CreateCredentials(secondBinding, s_expiration), TestContext.Current.CancellationToken);
        }

        string unrelatedFile = Path.Combine(_directory, "notes.txt");
        await File.WriteAllTextAsync(unrelatedFile, "preserve", TestContext.Current.CancellationToken);

        int deleted = await CliCredentialCache.DeleteAllAsync(_directory, TestContext.Current.CancellationToken);

        Assert.Equal(2, deleted);
        Assert.Empty(Directory.GetFiles(_directory, "*.credential"));
        Assert.Equal("preserve", await File.ReadAllTextAsync(unrelatedFile, TestContext.Current.CancellationToken));
        Assert.Equal(2, Directory.GetFiles(_directory, "*.credential.lock").Length);
    }

    [Fact]
    public async Task DeleteAllDoesNotCreateAMissingCacheDirectory()
    {
        int deleted = await CliCredentialCache.DeleteAllAsync(_directory, TestContext.Current.CancellationToken);

        Assert.Equal(0, deleted);
        Assert.False(Directory.Exists(_directory));
    }

    [Theory]
    [InlineData("account")]
    [InlineData("actor")]
    [InlineData("client")]
    [InlineData("scope")]
    [InlineData("service")]
    public async Task BindingSeparatesEntriesAndRejectsCopiedEnvelopes(string changedField)
    {
        CliCredentialCacheBinding binding = CreateBinding();
        CliCredentialCacheBinding changed = changedField switch
        {
            "account" => binding with { Account = "OTHER.example" },
            "actor" => binding with { Actor = "did:plc:other" },
            "client" => binding with { ClientMetadataId = "https://other.example/client.json" },
            "scope" => binding with { RequestedScope = "atproto rpc:dev.idunno.labeler.admin.getAudit?aud=did%3Aplc%3Alabeler%23labeler_admin" },
            "service" => binding with { Service = new Uri("https://other-pds.example") },
            _ => throw new ArgumentOutOfRangeException(nameof(changedField))
        };
        await using (CliCredentialCache cache = CreateCache(binding))
        {
            await cache.SaveAsync(CreateCredentials(binding, s_expiration), TestContext.Current.CancellationToken);
        }

        string originalPath = CredentialPath();
        await using (CliCredentialCache other = CreateCache(changed))
        {
            Assert.Null(await other.LoadAsync(TestContext.Current.CancellationToken));
            await other.SaveAsync(CreateCredentials(changed, s_expiration), TestContext.Current.CancellationToken);
        }

        string otherPath = Directory.GetFiles(_directory, "*.credential").Single(path => path != originalPath);
        File.Copy(originalPath, otherPath, overwrite: true);
        await using CliCredentialCache poisoned = CreateCache(changed);
        await Assert.ThrowsAsync<InvalidDataException>(() => poisoned.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("client")]
    [InlineData("scope")]
    [InlineData("service")]
    public async Task SavingCredentialsForAnotherSessionIsRejected(string changedField)
    {
        CliCredentialCacheBinding binding = CreateBinding();
        CliCredentialCacheBinding changed = changedField switch
        {
            "actor" => binding with { Actor = "did:plc:other" },
            "client" => binding with { ClientMetadataId = "https://other.example/client.json" },
            "scope" => binding with { RequestedScope = "atproto identity:*" },
            "service" => binding with { Service = new Uri("https://other-pds.example") },
            _ => throw new ArgumentOutOfRangeException(nameof(changedField))
        };
        await using CliCredentialCache cache = CreateCache(binding);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.SaveAsync(CreateCredentials(changed, s_expiration), TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task ConcurrentWritesRetainNewestExpirationAndAllowNonceUpdates()
    {
        CliCredentialCacheBinding binding = CreateBinding();
        await using CliCredentialCache cache = CreateCache(binding);
        DPoPAccessCredentials newest = CreateCredentials(binding, s_expiration.AddHours(3));
        Task<bool>[] saves =
        [
            cache.SaveAsync(CreateCredentials(binding, s_expiration.AddHours(1)), TestContext.Current.CancellationToken),
            cache.SaveAsync(newest, TestContext.Current.CancellationToken),
            cache.SaveAsync(CreateCredentials(binding, s_expiration.AddHours(2)), TestContext.Current.CancellationToken)
        ];
        await Task.WhenAll(saves);
        Assert.False(await cache.SaveAsync(CreateCredentials(binding, s_expiration), TestContext.Current.CancellationToken));
        newest.DPoPNonce = "updated-nonce";
        Assert.True(await cache.SaveAsync(newest, TestContext.Current.CancellationToken));
        DPoPAccessCredentials? restored = await cache.LoadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(restored);
        Assert.Equal(newest.AccessJwt, restored.AccessJwt);
        Assert.Equal("updated-nonce", restored.DPoPNonce);
        Assert.Empty(Directory.GetFiles(_directory, "*.pending"));
    }

    [Fact]
    public async Task WorkflowLeaseSurvivesReadsAndWritesAndCanBeCancelled()
    {
        CliCredentialCacheBinding binding = CreateBinding();
        CliCredentialCache owner = CreateCache(binding);
        await using CliCredentialCache contender = CreateCache(binding);
        try
        {
            await owner.AcquireSessionAsync(TestContext.Current.CancellationToken);
            await owner.SaveAsync(CreateCredentials(binding, s_expiration), TestContext.Current.CancellationToken);
            Assert.NotNull(await owner.LoadAsync(TestContext.Current.CancellationToken));
            using CancellationTokenSource cancellation = new();
            Task waiting = contender.AcquireSessionAsync(cancellation.Token);
            Assert.False(waiting.IsCompleted);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        }
        finally
        {
            await owner.DisposeAsync();
        }

        await contender.AcquireSessionAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(await contender.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AwaitedCredentialUpdatesAreDurableBeforeReturningAndKeepExistingHandler()
    {
        CliCredentialCacheBinding binding = CreateBinding();
        await using CliCredentialCache cache = CreateCache(binding);
        using BlueskyAgent agent = new();
        bool previousCalled = false;
        agent.CredentialsUpdatedAsync = (_, _) =>
        {
            previousCalled = true;
            return Task.CompletedTask;
        };
        cache.Attach(agent);
        DPoPAccessCredentials credentials = CreateCredentials(binding, s_expiration);
        Assert.NotNull(agent.CredentialsUpdatedAsync);
        await agent.CredentialsUpdatedAsync(
            new CredentialsUpdatedEventArgs(new Did(binding.Actor), binding.Service, credentials),
            TestContext.Current.CancellationToken);
        Assert.True(previousCalled);
        DPoPAccessCredentials? saved = await cache.LoadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(credentials.RefreshToken, saved.RefreshToken);
    }

    [Fact]
    public async Task RestoredSessionPersistsTheServiceTokenNonceChallengeBeforeRetrying()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2029, 1, 1, 0, 0, 0, TimeSpan.Zero));
        CliCredentialCacheBinding binding = CreateBinding();
        using RSA rsa = RSA.Create(2048);
        JsonWebKey key = JsonWebKeyConverter.ConvertFromSecurityKey(new RsaSecurityKey(rsa));
        key.Alg = "PS256";
        DPoPAccessCredentials credentials = CreateCredentials(
            binding: binding,
            expiration: clock.GetUtcNow().AddHours(1),
            proofKey: JsonSerializer.Serialize(key));
        await using (CliCredentialCache saved = CreateCache(binding))
        {
            await saved.SaveAsync(credentials, TestContext.Current.CancellationToken);
        }

        JsonWebTokenHandler tokenHandler = new() { SetDefaultTimesOnTokenCreation = false };
        using ServiceTokenHandler handler = new(tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Audience = "did:plc:labeler#labeler_admin",
            Expires = clock.GetUtcNow().AddMinutes(15).UtcDateTime
        }));
        ServiceCollection services = new();
        services.AddHttpClient("idunno.atproto").ConfigurePrimaryHttpMessageHandler(() => handler);
        using ServiceProvider provider = services.BuildServiceProvider();
        await using CliCredentialCache cache = CreateCache(binding);
        using AtProtoAgent agent = new(
            service: binding.Service,
            httpClientFactory: provider.GetRequiredService<IHttpClientFactory>(),
            options: new() { TimeProvider = clock, EnableBackgroundTokenRefresh = false });
        cache.Attach(agent);
        DPoPAccessCredentials? restored = await cache.LoadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(restored);
        Assert.True(await agent.Login(restored, TestContext.Current.CancellationToken));
        using CancellationTokenSource watchdog = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        watchdog.CancelAfter(TimeSpan.FromSeconds(30));

        AtProtoHttpResult<ServiceCredential> result = await agent.GetServiceAuth(
            service: binding.Service,
            audience: new AtProtoServiceReference(new Did("did:plc:labeler"), "labeler_admin"),
            lxm: new Nsid("dev.idunno.labeler.admin.getTeam"),
            cancellationToken: watchdog.Token);

        Assert.True(result.Succeeded);
        Assert.Equal(2, handler.Requests);
        Assert.Equal("rotated-service-nonce", (await cache.LoadAsync(TestContext.Current.CancellationToken))?.DPoPNonce);
    }

    [Fact]
    public async Task DisposalWaitsForStartedSaveAndRestoresPreviousHandler()
    {
        CliCredentialCacheBinding binding = CreateBinding();
        CliCredentialCache cache = CreateCache(binding);
        using BlueskyAgent agent = new();
        Func<CredentialsUpdatedEventArgs, CancellationToken, Task> previous = (_, _) => Task.CompletedTask;
        agent.CredentialsUpdatedAsync = previous;
        cache.Attach(agent);
        await cache.AcquireSessionAsync(TestContext.Current.CancellationToken);
        DPoPAccessCredentials credentials = CreateCredentials(binding, s_expiration);
        Task<bool> saving = cache.SaveAsync(credentials, TestContext.Current.CancellationToken);
        Task disposing = cache.DisposeAsync().AsTask();
        Assert.True(await saving);
        await disposing;
        Assert.Same(previous, agent.CredentialsUpdatedAsync);

        await using CliCredentialCache nextWorkflow = CreateCache(binding);
        DPoPAccessCredentials? persisted = await nextWorkflow.LoadAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal(credentials.RefreshToken, persisted.RefreshToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.SaveAsync(credentials, TestContext.Current.CancellationToken));
        await cache.DisposeAsync();
    }

    [Fact]
    public async Task SubscriptionCleanupDoesNotReplaceNewerHandler()
    {
        CliCredentialCacheBinding binding = CreateBinding();
        await using CliCredentialCache cache = CreateCache(binding);
        using BlueskyAgent agent = new();
        using IDisposable subscription = cache.Attach(agent);
        Func<CredentialsUpdatedEventArgs, CancellationToken, Task> replacement = (_, _) => Task.CompletedTask;
        agent.CredentialsUpdatedAsync = replacement;
        subscription.Dispose();
        Assert.Same(replacement, agent.CredentialsUpdatedAsync);
    }

    [Theory]
    [InlineData("invalid-json")]
    [InlineData("version")]
    [InlineData("subject")]
    [InlineData("missing-binding")]
    [InlineData("missing-refresh-token")]
    [InlineData("missing-proof-key")]
    [InlineData("missing-nonce")]
    [InlineData("invalid-origin")]
    [InlineData("null-origin")]
    [InlineData("mismatched-origin")]
    public async Task InvalidEnvelopesAreExplicitErrors(string corruption)
    {
        CliCredentialCacheBinding binding = CreateBinding();
        await using CliCredentialCache cache = CreateCache(binding);
        DPoPAccessCredentials credentials = CreateCredentials(binding, s_expiration);
        await cache.SaveAsync(credentials, TestContext.Current.CancellationToken);
        CliCredentialCacheEntry entry = new(
            Version: 1,
            Binding: binding,
            AccessJwt: credentials.AccessJwt,
            RefreshToken: credentials.RefreshToken,
            DPoPProofKey: credentials.DPoPProofKey,
            DPoPNonce: credentials.DPoPNonce,
            ClientSigningKeyId: credentials.ClientSigningKeyId);
        entry = corruption switch
        {
            "version" => entry with { Version = 2 },
            "subject" => entry with { AccessJwt = CreateJwt("did:plc:other", s_expiration) },
            "invalid-origin" => entry with { LabelerOrigins = ["http://labeler.example"] },
            "mismatched-origin" => entry with { LabelerOrigins = ["https://other.example"] },
            _ => entry
        };
        JsonObject envelope = JsonSerializer.SerializeToNode(entry, CliCredentialCacheJsonContext.Default.CliCredentialCacheEntry)!.AsObject();
        string? missingField = corruption switch
        {
            "missing-binding" => nameof(CliCredentialCacheEntry.Binding),
            "missing-refresh-token" => nameof(CliCredentialCacheEntry.RefreshToken),
            "missing-proof-key" => nameof(CliCredentialCacheEntry.DPoPProofKey),
            "missing-nonce" => nameof(CliCredentialCacheEntry.DPoPNonce),
            _ => null
        };
        if (missingField is not null)
        {
            envelope[missingField] = null;
        }
        if (corruption == "null-origin")
        {
            envelope[nameof(CliCredentialCacheEntry.LabelerOrigins)] = new JsonArray((JsonNode?)null);
        }

        byte[] bytes = corruption == "invalid-json" ? "invalid"u8.ToArray() : Encoding.UTF8.GetBytes(envelope.ToJsonString());
        if (OperatingSystem.IsWindows())
        {
            bytes = CliCredentialProtection.Protect(bytes);
        }

        await File.WriteAllBytesAsync(CredentialPath(), bytes, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => cache.LoadAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(() => CliCredentialCache.FindAccountsAsync(
            clientMetadataId: binding.ClientMetadataId,
            requestedScope: binding.RequestedScope,
            directory: _directory,
            cancellationToken: TestContext.Current.CancellationToken));
        using StringWriter output = new();
        await Assert.ThrowsAsync<InvalidDataException>(() => Program.ListCredentialsAsync(
            output: output,
            directory: _directory,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(string.Empty, output.ToString());
        Assert.Equal("[REDACTED]", entry.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StoragePolicyRequiresAffirmativeConsentOnUnencryptedPlatforms(bool answer)
    {
        using StringWriter warning = new();
        bool asked = false;
        bool allowed = await CliCredentialCache.ConfirmStorageAsync(
            warning: warning,
            confirm: _ =>
            {
                asked = true;
                return Task.FromResult(answer);
            },
            cancellationToken: TestContext.Current.CancellationToken);
        if (OperatingSystem.IsWindows())
        {
            Assert.True(allowed);
            Assert.False(asked);
            Assert.Equal(string.Empty, warning.ToString());
        }
        else
        {
            Assert.Equal(answer, allowed);
            Assert.True(asked);
            Assert.Contains("UNENCRYPTED", warning.ToString(), StringComparison.Ordinal);
            Assert.Contains("~/.xenon", warning.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task UnencryptedWritesWithoutConsentAreRefused()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        CliCredentialCacheBinding binding = CreateBinding();
        await using CliCredentialCache cache = new(binding: binding, directory: _directory, allowUnencryptedStorage: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.SaveAsync(CreateCredentials(binding, s_expiration), TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task UnixCacheRejectsOverlyPermissiveCredentialFiles()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        CliCredentialCacheBinding binding = CreateBinding();
        await using CliCredentialCache cache = CreateCache(binding);
        await cache.SaveAsync(CreateCredentials(binding, s_expiration), TestContext.Current.CancellationToken);
        File.SetUnixFileMode(CredentialPath(), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
        await Assert.ThrowsAsync<InvalidDataException>(() => cache.LoadAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Removes only this test's private directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>Creates a cache whose test has explicitly accepted unencrypted storage.</summary>
    private CliCredentialCache CreateCache(CliCredentialCacheBinding binding) => new(
        binding: binding,
        directory: _directory,
        allowUnencryptedStorage: true);

    /// <summary>Finds the single credential written by tests that use one binding.</summary>
    private string CredentialPath() => Assert.Single(Directory.GetFiles(_directory, "*.credential"));

    /// <summary>Creates a fixed independently resolved account and method-bound permission set.</summary>
    private static CliCredentialCacheBinding CreateBinding() => new(
        Account: "operator.example",
        Actor: "did:plc:operator",
        ClientMetadataId: "https://labeler.example/client-metadata.json",
        RequestedScope: "atproto rpc:dev.idunno.labeler.admin.getTeam?aud=did%3Aplc%3Alabeler%23labeler_admin",
        Service: new Uri("https://pds.example"));

    /// <summary>Creates a complete credential without network calls or clock-dependent data.</summary>
    private static DPoPAccessCredentials CreateCredentials(
        CliCredentialCacheBinding binding,
        DateTimeOffset expiration,
        string? proofKey = null) => new(
        service: binding.Service,
        accessJwt: CreateJwt(binding.Actor, expiration),
        refreshToken: "test-refresh-token",
        dPoPProofKey: proofKey ?? "test-private-proof-key",
        dPoPNonce: "test-nonce")
    {
        OAuthClientId = binding.ClientMetadataId,
        RequestedScope = binding.RequestedScope,
        ClientSigningKeyId = "original-client-signing-key"
    };

    /// <summary>Creates an unsigned inert JWT used only for local serialization tests.</summary>
    private static string CreateJwt(string actor, DateTimeOffset expiration)
    {
        string header = Convert.ToBase64String("{\"alg\":\"none\"}"u8).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"sub\":\"{actor}\",\"exp\":{expiration.ToUnixTimeSeconds()}}}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{header}.{payload}.";
    }

    /// <summary>Challenges one restored service-token request and checks that its retry uses the updated proof nonce.</summary>
    /// <param name="serviceToken">The service credential issued by the test PDS.</param>
    private sealed class ServiceTokenHandler(string serviceToken) : HttpMessageHandler
    {
        /// <summary>Gets the number of service-token requests received.</summary>
        internal int Requests { get; private set; }

        /// <summary>Returns a nonce challenge once, then a successful service token.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Assert.Equal("/xrpc/com.atproto.server.getServiceAuth", request.RequestUri?.AbsolutePath);
            JsonWebToken proof = new(request.Headers.GetValues("DPoP").Single());
            Assert.Equal(Requests == 1 ? "test-nonce" : "rotated-service-nonce", proof.GetClaim("nonce").Value);
            HttpResponseMessage response = new(Requests == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)
            {
                Content = new StringContent(
                    Requests == 1 ? """{"error":"use_dpop_nonce"}""" : $$"""{"token":"{{serviceToken}}"}""",
                    Encoding.UTF8,
                    "application/json")
            };
            response.Headers.Add("DPoP-Nonce", "rotated-service-nonce");
            return Task.FromResult(response);
        }
    }
}
