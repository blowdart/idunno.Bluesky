// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Reserves verified service tokens in process memory.
/// </summary>
/// <remarks>
/// <para>Reservations are retained until the token's expiry plus five seconds of clock skew. When the guard is full and no
/// reservation has expired, new tokens are rejected rather than accepted without replay protection.</para>
/// <para>This guard protects only a single process. Applications running more than one instance must use an
/// <see cref="IServiceTokenReplayGuard"/> backed by shared storage.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class InMemoryServiceTokenReplayGuard : IServiceTokenReplayGuard
{
    /// <summary>
    /// The default maximum number of retained tokens.
    /// </summary>
    public const int DefaultCapacity = 10_000;

    private static readonly TimeSpan s_clockSkew = TimeSpan.FromSeconds(5);

#pragma warning disable IDE0330 // System.Threading.Lock is unavailable on the net8.0 target.
    private readonly object _lock = new();
#pragma warning restore IDE0330
    private readonly Dictionary<string, DateTimeOffset> _identifiers = [];
    private readonly Dictionary<string, DateTimeOffset> _hashes = [];
    private readonly int _capacity;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryServiceTokenReplayGuard"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of retained tokens.</param>
    /// <param name="timeProvider">The clock used to expire reservations, or <see langword="null"/> to use the system clock.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is zero or negative.</exception>
    public InMemoryServiceTokenReplayGuard(int capacity = DefaultCapacity, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = capacity;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> is <see langword="null"/>.</exception>
    public ValueTask<bool> TryReserveAsync(ServiceAuthenticationResult token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        cancellationToken.ThrowIfCancellationRequested();

        string identifier = $"{token.Issuer}\n{token.TokenId}";
        DateTimeOffset retainUntil = token.ExpiresAt + s_clockSkew;
        DateTimeOffset now = _timeProvider.GetUtcNow();

        lock (_lock)
        {
            if (IsReserved(_identifiers, identifier, now) || IsReserved(_hashes, token.TokenHash, now))
            {
                return ValueTask.FromResult(false);
            }

            if (_identifiers.Count >= _capacity)
            {
                RemoveExpired(_identifiers, now);
                RemoveExpired(_hashes, now);

                if (_identifiers.Count >= _capacity)
                {
                    return ValueTask.FromResult(false);
                }
            }

            _identifiers[identifier] = retainUntil;
            _hashes[token.TokenHash] = retainUntil;
        }

        return ValueTask.FromResult(true);
    }

    /// <summary>
    /// Determines whether a key has an unexpired reservation, removing it if it has expired.
    /// </summary>
    /// <param name="reservations">The reservations to search.</param>
    /// <param name="key">The key to look for.</param>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> if the key is reserved; otherwise, <see langword="false"/>.</returns>
    private static bool IsReserved(Dictionary<string, DateTimeOffset> reservations, string key, DateTimeOffset now)
    {
        if (!reservations.TryGetValue(key, out DateTimeOffset retainUntil))
        {
            return false;
        }

        if (retainUntil > now)
        {
            return true;
        }

        reservations.Remove(key);
        return false;
    }

    /// <summary>
    /// Removes every reservation that has expired.
    /// </summary>
    /// <param name="reservations">The reservations to prune.</param>
    /// <param name="now">The current time.</param>
    private static void RemoveExpired(Dictionary<string, DateTimeOffset> reservations, DateTimeOffset now)
    {
        foreach (KeyValuePair<string, DateTimeOffset> reservation in reservations)
        {
            if (reservation.Value <= now)
            {
                reservations.Remove(reservation.Key);
            }
        }
    }
}
