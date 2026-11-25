// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Bounds per-peer authentication and denial-audit work across all remote administration routes.</summary>
/// <param name="clock">The monotonic clock shared with authentication.</param>
internal sealed class RemoteAdministrationAdmission(TimeProvider clock)
{
#if NET9_0_OR_GREATER
    private readonly Lock _admissionLock = new();
#else
    private readonly object _admissionLock = new();
#endif
    private readonly Dictionary<IPAddress, Peer> _peers = [];
    private long _windowStarted = clock.GetTimestamp();
    private int _denialAudits;

    /// <summary>
    /// Reserves one unauthenticated invocation-denial audit row from the process-wide budget shared across all peers.
    /// </summary>
    /// <param name="settings">The administration options.</param>
    /// <param name="firstSuppression"><see langword="true"/> when this call is the first to be refused in the current window.</param>
    /// <returns><see langword="true"/> if the denial should be audited; otherwise, <see langword="false"/>.</returns>
    internal bool TryReserveDenialAudit(LabelerAdministrationOptions settings, out bool firstSuppression)
    {
        lock (_admissionLock)
        {
            RollWindow();
            if (_denialAudits < settings.MaximumDenialAuditsPerMinute)
            {
                _denialAudits++;
                firstSuppression = false;
                return true;
            }

            firstSuppression = _denialAudits == settings.MaximumDenialAuditsPerMinute;
            _denialAudits++;
            return false;
        }
    }

    /// <summary>Admits a request using only the connection address established by trusted forwarded-header middleware.</summary>
    internal IDisposable? TryAcquire(IPAddress? address, LabelerAdministrationOptions settings)
    {
        IPAddress peerAddress = address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address ?? IPAddress.None;
        lock (_admissionLock)
        {
            RollWindow();
            if (!_peers.TryGetValue(peerAddress, out Peer? peer))
            {
                if (_peers.Count >= settings.MaximumTrackedPeers)
                {
                    return null;
                }
                peer = new();
                _peers.Add(peerAddress, peer);
            }

            if (peer.Requests >= settings.RequestsPerMinutePerPeer ||
                peer.Active >= settings.MaximumConcurrentRequestsPerPeer)
            {
                return null;
            }

            peer.Requests++;
            peer.Active++;
            return new AdmissionLease(this, peer);
        }
    }

    /// <summary>Starts a new admission window when the current one has elapsed. The caller must hold the admission lock.</summary>
    private void RollWindow()
    {
        long now = clock.GetTimestamp();
        if (clock.GetElapsedTime(_windowStarted, now) < TimeSpan.FromMinutes(1))
        {
            return;
        }

        _windowStarted = now;
        _denialAudits = 0;
        // Reclaim only at window boundaries: changing addresses cannot evict a live allowance.
        foreach (IPAddress expired in _peers.Where(entry => entry.Value.Active == 0).Select(entry => entry.Key).ToArray())
        {
            _peers.Remove(expired);
        }
        foreach (Peer active in _peers.Values)
        {
            active.Requests = 0;
        }
    }

    /// <summary>Retains bounded counters while requests are active or their admission window remains open.</summary>
    private sealed class Peer
    {
        internal int Requests { get; set; }
        internal int Active { get; set; }
    }

    /// <summary>Releases concurrency admission exactly once, including exceptional and cancelled requests.</summary>
    private sealed class AdmissionLease(RemoteAdministrationAdmission owner, Peer peer) : IDisposable
    {
        private RemoteAdministrationAdmission? _owner = owner;

        /// <inheritdoc/>
        public void Dispose()
        {
            RemoteAdministrationAdmission? current = Interlocked.Exchange(ref _owner, null);
            if (current is not null)
            {
                lock (current._admissionLock)
                {
                    peer.Active--;
                }
            }
        }
    }
}
