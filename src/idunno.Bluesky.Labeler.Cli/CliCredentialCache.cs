// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky.Labeler.Cli;

/// <summary>Identifies one resolved account, client, permission set and personal data server.</summary>
/// <param name="Account">The exact account argument supplied by the operator.</param>
/// <param name="Actor">The independently resolved account DID.</param>
/// <param name="ClientMetadataId">The effective OAuth client metadata identifier.</param>
/// <param name="RequestedScope">The exact space-separated requested scopes.</param>
/// <param name="Service">The independently resolved personal data server.</param>
internal sealed record CliCredentialCacheBinding(
    string Account,
    string Actor,
    string ClientMetadataId,
    string RequestedScope,
    Uri Service);

/// <summary>Persists opt-in OAuth credentials without widening their account or permission binding.</summary>
/// <remarks>
/// <para>Construct this only for an explicit remember-me request. Resolve the actor and PDS independently before constructing the binding.</para>
/// <para>Login with a loaded credential installs local state, not server authentication. Verify the authenticated account through the server before performing the requested operation.</para>
/// </remarks>
internal sealed class CliCredentialCache : IAsyncDisposable
{
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode FileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const int MaximumFileLength = 1024 * 1024;
    private readonly CliCredentialCacheBinding _binding;
    private readonly string _directory;
    private readonly string _path;
    private readonly bool _allowUnencryptedStorage;
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Retains the gate to reject late refresh callbacks safely and allow idempotent cache disposal.")]
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private FileStream? _sessionLease;
    private readonly List<IDisposable> _attachments = [];
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="CliCredentialCache"/> class.</summary>
    /// <param name="binding">The independently resolved session binding.</param>
    /// <param name="directory">An optional cache directory, defaulting to the user's <c>.xenon</c> directory.</param>
    /// <param name="allowUnencryptedStorage"><see langword="true"/> after explicit informed consent to unencrypted storage; otherwise, <see langword="false"/>.</param>
    internal CliCredentialCache(
        CliCredentialCacheBinding binding,
        string? directory = null,
        bool allowUnencryptedStorage = false)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.Account);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.ClientMetadataId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.RequestedScope);
        _ = new Did(binding.Actor);
        if (!binding.Service.IsAbsoluteUri ||
            binding.Service.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(binding.Service.UserInfo) ||
            !string.IsNullOrEmpty(binding.Service.Fragment))
        {
            throw new ArgumentException("The resolved personal data server must use HTTPS.", nameof(binding));
        }

        _binding = binding;
        _directory = Path.GetFullPath(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".xenon"));
        byte[] identity = JsonSerializer.SerializeToUtf8Bytes(binding, CliCredentialCacheJsonContext.Default.CliCredentialCacheBinding);
        _path = Path.Combine(_directory, $"oauth-{Convert.ToHexString(SHA256.HashData(identity))}.credential");
        _allowUnencryptedStorage = allowUnencryptedStorage;
    }

    /// <summary>Requests informed consent before saving unencrypted credentials on non-Windows platforms.</summary>
    /// <param name="warning">The warning output writer.</param>
    /// <param name="confirm">The interactive affirmative-confirmation callback.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when protected storage or explicit consent permits saving; otherwise, <see langword="false"/>.</returns>
    internal static async Task<bool> ConfirmStorageAsync(
        TextWriter warning,
        Func<CancellationToken, Task<bool>> confirm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(warning);
        ArgumentNullException.ThrowIfNull(confirm);
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        await warning.WriteLineAsync("WARNING: Remembered credentials in ~/.xenon will be stored UNENCRYPTED. Other programs running as your user can read them. Save only after explicit affirmative confirmation.").ConfigureAwait(false);
        return await confirm(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads a complete credential only when every persisted field agrees with the resolved binding.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching credential, or <see langword="null"/> for a missing entry.</returns>
    /// <exception cref="InvalidDataException">The existing entry is corrupt, unsafe or does not match the independently resolved binding.</exception>
    internal async Task<DPoPAccessCredentials?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await AcquireSessionAsync(cancellationToken).ConfigureAwait(false);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return await ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>Saves a complete matching credential atomically, retaining any credential with a later expiration.</summary>
    /// <param name="credentials">The credential received from authentication or an awaited refresh notification.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when saved; otherwise, <see langword="false"/> when a newer credential already exists.</returns>
    [SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Flush(true) is required for durable refresh-token storage; FlushAsync does not request an operating-system disk flush.")]
    internal async Task<bool> SaveAsync(AccessCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (!OperatingSystem.IsWindows() && !_allowUnencryptedStorage)
        {
            throw new InvalidOperationException("Explicit consent is required before saving unencrypted credentials.");
        }

        if (credentials is not DPoPAccessCredentials oauth || !Matches(oauth))
        {
            throw new InvalidOperationException("Credentials do not match the resolved account, client, scope and personal data server.");
        }

        // Snapshot all fields before awaiting so a later callback cannot change the pending write.
        CliCredentialCacheEntry entry = new(
            Version: 1,
            Binding: _binding,
            AccessJwt: oauth.AccessJwt,
            RefreshToken: oauth.RefreshToken,
            DPoPProofKey: oauth.DPoPProofKey,
            DPoPNonce: oauth.DPoPNonce,
            ClientSigningKeyId: oauth.ClientSigningKeyId);
        DPoPAccessCredentials snapshot = CreateCredentials(entry);
        if (!Matches(snapshot))
        {
            throw new InvalidOperationException("Credentials changed identity while being saved.");
        }

        await AcquireSessionAsync(cancellationToken).ConfigureAwait(false);
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        byte[]? plaintext = null;
        byte[]? persisted = null;
        string stagingPath = Path.Combine(_directory, $".oauth-{Guid.NewGuid():N}.pending");
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DPoPAccessCredentials? stored = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (stored is not null && stored.ExpiresOn > snapshot.ExpiresOn)
            {
                return false;
            }

            plaintext = JsonSerializer.SerializeToUtf8Bytes(entry, CliCredentialCacheJsonContext.Default.CliCredentialCacheEntry);
            persisted = OperatingSystem.IsWindows() ? CliCredentialProtection.Protect(plaintext) : plaintext;
            FileStream staging = OpenPrivateFile(stagingPath, System.IO.FileMode.CreateNew, FileShare.None);
            await using (ConfiguredAsyncDisposable stagingLifetime = staging.ConfigureAwait(false))
            {
                await staging.WriteAsync(persisted, cancellationToken).ConfigureAwait(false);
                await staging.FlushAsync(cancellationToken).ConfigureAwait(false);
                staging.Flush(flushToDisk: true);
            }

            RejectLink(_path);
            File.Move(stagingPath, _path, overwrite: true);
        }
        finally
        {
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            if (persisted is not null && !ReferenceEquals(plaintext, persisted))
            {
                CryptographicOperations.ZeroMemory(persisted);
            }

            try
            {
                File.Delete(stagingPath);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        return true;
    }

    /// <summary>Acquires the account session lease before restoring or creating credentials.</summary>
    /// <param name="cancellationToken">The cancellation token while waiting for another process.</param>
    /// <returns>The task representing lease acquisition.</returns>
    /// <remarks>
    /// <para>The lease remains held until this cache is disposed. Keep it throughout the entire agent workflow, including API calls and background refresh, and dispose the agent before disposing this cache.</para>
    /// </remarks>
    internal async Task AcquireSessionAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_sessionLease is null)
            {
                PrepareDirectory();
                _sessionLease = await AcquireLeaseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>Deletes every persisted credential from the selected private cache directory.</summary>
    /// <param name="directory">The cache directory, or <see langword="null"/> to use the user's <c>.xenon</c> directory.</param>
    /// <param name="cancellationToken">The cancellation token while waiting for active credential operations.</param>
    /// <returns>The number of credential files deleted.</returns>
    internal static async Task<int> DeleteAllAsync(string? directory = null, CancellationToken cancellationToken = default)
    {
        string cacheDirectory = Path.GetFullPath(directory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".xenon"));
        if (!Directory.Exists(cacheDirectory))
        {
            return 0;
        }

        RejectLink(cacheDirectory);
        string[] credentialPaths = Directory.GetFiles(cacheDirectory, "oauth-*.credential", SearchOption.TopDirectoryOnly);
        int deleted = 0;
        foreach (string credentialPath in credentialPaths)
        {
            RejectLink(credentialPath);
            FileStream lease = await AcquireLeaseAsync(credentialPath, cancellationToken).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable leaseLifetime = lease.ConfigureAwait(false);
            RejectLink(credentialPath);
            if (File.Exists(credentialPath))
            {
                File.Delete(credentialPath);
                deleted++;
            }
        }

        return deleted;
    }

    /// <summary>Releases the workflow lease after the associated agent has stopped using its credentials.</summary>
    /// <returns>The task representing lease release.</returns>
    public async ValueTask DisposeAsync()
    {
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_disposed)
            {
                _disposed = true;
                foreach (IDisposable attachment in _attachments)
                {
                    attachment.Dispose();
                }

                _attachments.Clear();
                if (_sessionLease is not null)
                {
                    await _sessionLease.DisposeAsync().ConfigureAwait(false);
                    _sessionLease = null;
                }
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>Attaches awaited durable refresh persistence without discarding an existing handler.</summary>
    /// <param name="agent">The newly created agent dedicated to this binding.</param>
    /// <returns>A subscription that restores the previous handler when disposed; cache disposal also restores it.</returns>
    internal IDisposable Attach(AtProtoAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ObjectDisposedException.ThrowIf(_disposed, this);
        Func<AtProto.Events.CredentialsUpdatedEventArgs, CancellationToken, Task>? previous = agent.CredentialsUpdatedAsync;
        Func<AtProto.Events.CredentialsUpdatedEventArgs, CancellationToken, Task> handler = async (update, cancellationToken) =>
        {
            if (update.Did.ToString() != _binding.Actor || update.Service != _binding.Service)
            {
                throw new InvalidOperationException("The refreshed session does not match the cache binding.");
            }

            await SaveAsync(update.AccessCredentials, cancellationToken).ConfigureAwait(false);
            if (previous is not null)
            {
                await previous(update, cancellationToken).ConfigureAwait(false);
            }
        };
        agent.CredentialsUpdatedAsync = handler;
        CredentialCacheSubscription subscription = new(() =>
        {
            if (ReferenceEquals(agent.CredentialsUpdatedAsync, handler))
            {
                agent.CredentialsUpdatedAsync = previous;
            }
        });
        _attachments.Add(subscription);
        return subscription;
    }

    /// <summary>Checks the effective OAuth session against all independent binding fields.</summary>
    private bool Matches(DPoPAccessCredentials credentials) =>
        credentials.Did.ToString().Equals(_binding.Actor, StringComparison.Ordinal) &&
        credentials.ExpiresOn != DateTimeOffset.MinValue &&
        credentials.Service.AbsoluteUri.Equals(_binding.Service.AbsoluteUri, StringComparison.Ordinal) &&
        string.Equals(credentials.OAuthClientId, _binding.ClientMetadataId, StringComparison.Ordinal) &&
        string.Equals(credentials.RequestedScope, _binding.RequestedScope, StringComparison.Ordinal);

    /// <summary>Reconstructs every persisted OAuth credential field without inferring missing metadata.</summary>
    private static DPoPAccessCredentials CreateCredentials(CliCredentialCacheEntry entry) => new(
        service: entry.Binding.Service,
        accessJwt: entry.AccessJwt,
        refreshToken: entry.RefreshToken,
        dPoPProofKey: entry.DPoPProofKey,
        dPoPNonce: entry.DPoPNonce)
    {
        OAuthClientId = entry.Binding.ClientMetadataId,
        RequestedScope = entry.Binding.RequestedScope,
        ClientSigningKeyId = entry.ClientSigningKeyId
    };

    /// <summary>Reads untrusted local data and reports corruption without exposing credential content.</summary>
    private async Task<DPoPAccessCredentials?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        RejectLink(_path);
        if (!OperatingSystem.IsWindows() && File.GetUnixFileMode(_path) != FileMode)
        {
            throw new InvalidDataException("The credential cache file does not have owner-only read/write permissions.");
        }

        byte[]? plaintext = null;
        byte[]? persisted = null;
        try
        {
            FileStream input = new(_path, System.IO.FileMode.Open, FileAccess.Read, FileShare.Read);
            await using ConfiguredAsyncDisposable inputLifetime = input.ConfigureAwait(false);
            if (input.Length is <= 0 or > MaximumFileLength)
            {
                throw new InvalidDataException("The credential cache file has an invalid size.");
            }

            persisted = new byte[checked((int)input.Length)];
            await input.ReadExactlyAsync(persisted, cancellationToken).ConfigureAwait(false);
            plaintext = OperatingSystem.IsWindows() ? CliCredentialProtection.Unprotect(persisted) : persisted;
            CliCredentialCacheEntry? entry = JsonSerializer.Deserialize(plaintext, CliCredentialCacheJsonContext.Default.CliCredentialCacheEntry);
            if (entry is null || entry.Version != 1 || entry.Binding != _binding)
            {
                throw new InvalidDataException("The credential cache envelope does not match the resolved session.");
            }

            DPoPAccessCredentials credential = CreateCredentials(entry);
            if (!Matches(credential))
            {
                throw new InvalidDataException("The credential cache does not match the resolved session.");
            }

            return credential;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or CryptographicException or NotSupportedException)
        {
            throw new InvalidDataException("The credential cache is corrupt or cannot be decrypted.");
        }
        finally
        {
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            if (persisted is not null && !ReferenceEquals(plaintext, persisted))
            {
                CryptographicOperations.ZeroMemory(persisted);
            }
        }
    }

    /// <summary>Creates a private directory and rejects symbolic-link cache roots.</summary>
    private void PrepareDirectory()
    {
        RejectLink(_directory);
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(_directory);
        }
        else
        {
            Directory.CreateDirectory(_directory, DirectoryMode);
            File.SetUnixFileMode(_directory, DirectoryMode);
        }
    }

    /// <summary>Serializes readers and writers across processes using a persistent lock file.</summary>
    private async Task<FileStream> AcquireLeaseAsync(CancellationToken cancellationToken)
        => await AcquireLeaseAsync(_path, cancellationToken).ConfigureAwait(false);

    /// <summary>Serializes access to one credential file using its persistent lock file.</summary>
    private static async Task<FileStream> AcquireLeaseAsync(string credentialPath, CancellationToken cancellationToken)
    {
        string lockPath = credentialPath + ".lock";
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectLink(lockPath);
            try
            {
                return OpenPrivateFile(lockPath, System.IO.FileMode.OpenOrCreate, FileShare.None);
            }
            catch (IOException exception) when ((exception.HResult & 0xFFFF) is 11 or 32 or 33)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Opens a file with owner-only permissions at creation on Unix.</summary>
    private static FileStream OpenPrivateFile(string path, System.IO.FileMode mode, FileShare share)
    {
        FileStreamOptions options = new()
        {
            Mode = mode,
            Access = FileAccess.ReadWrite,
            Share = share,
            Options = FileOptions.Asynchronous
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = FileMode;
        }

        return new FileStream(path, options);
    }

    /// <summary>Rejects cache paths redirected through a symbolic link or reparse point.</summary>
    private static void RejectLink(string path)
    {
        if (Path.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Credential cache paths must not be symbolic links.");
        }
    }
}

/// <summary>Restores an agent's prior persistence handler at most once.</summary>
/// <param name="detach">The handler cleanup action.</param>
internal sealed class CredentialCacheSubscription(Action detach) : IDisposable
{
    private Action? _detach = detach;

    /// <summary>Detaches the cache handler without replacing a subsequently assigned handler.</summary>
    public void Dispose() => Interlocked.Exchange(ref _detach, null)?.Invoke();
}

/// <summary>Stores an explicit versioned credential snapshot without reflective serialization.</summary>
/// <param name="Version">The envelope format version.</param>
/// <param name="Binding">The exact account and authorization binding.</param>
/// <param name="AccessJwt">The access token.</param>
/// <param name="RefreshToken">The refresh token.</param>
/// <param name="DPoPProofKey">The private proof key.</param>
/// <param name="DPoPNonce">The current proof nonce.</param>
/// <param name="ClientSigningKeyId">The original client signing key identifier.</param>
internal sealed record CliCredentialCacheEntry(
    int Version,
    CliCredentialCacheBinding Binding,
    string AccessJwt,
    string RefreshToken,
    string DPoPProofKey,
    string DPoPNonce,
    string? ClientSigningKeyId)
{
    /// <summary>Prevents credential snapshots from exposing secrets in diagnostics.</summary>
    /// <returns>A constant redacted representation.</returns>
    public override string ToString() => "[REDACTED]";
}

/// <summary>Supplies generated metadata for the complete credential-cache format.</summary>
[JsonSerializable(typeof(CliCredentialCacheBinding))]
[JsonSerializable(typeof(CliCredentialCacheEntry))]
internal sealed partial class CliCredentialCacheJsonContext : JsonSerializerContext;
