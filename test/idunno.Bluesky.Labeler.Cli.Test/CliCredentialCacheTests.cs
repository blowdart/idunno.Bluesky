// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Events;

namespace idunno.Bluesky.Labeler.Cli.Test;

public sealed class CliCredentialCacheTests : IDisposable
{
    private static readonly DateTimeOffset s_expiration = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly string _directory = Path.Combine(Environment.CurrentDirectory, ".credential-cache-tests", Guid.NewGuid().ToString("N"));

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
            "missing-binding" => entry with { Binding = null! },
            "missing-refresh-token" => entry with { RefreshToken = null! },
            "missing-proof-key" => entry with { DPoPProofKey = null! },
            "missing-nonce" => entry with { DPoPNonce = null! },
            _ => entry
        };
        byte[] bytes = corruption == "invalid-json" ? "invalid"u8.ToArray() :
            JsonSerializer.SerializeToUtf8Bytes(entry, CliCredentialCacheJsonContext.Default.CliCredentialCacheEntry);
        if (OperatingSystem.IsWindows())
        {
            bytes = CliCredentialProtection.Protect(bytes);
        }

        await File.WriteAllBytesAsync(CredentialPath(), bytes, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => cache.LoadAsync(TestContext.Current.CancellationToken));
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
    private static DPoPAccessCredentials CreateCredentials(CliCredentialCacheBinding binding, DateTimeOffset expiration) => new(
        service: binding.Service,
        accessJwt: CreateJwt(binding.Actor, expiration),
        refreshToken: "test-refresh-token",
        dPoPProofKey: "test-private-proof-key",
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
}
