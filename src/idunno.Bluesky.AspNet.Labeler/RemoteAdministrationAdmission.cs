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

    /// <summary>Admits a request using only the connection address established by trusted forwarded-header middleware.</summary>
    internal IDisposable? TryAcquire(IPAddress? address, LabelerAdministrationOptions settings)
    {
        IPAddress peerAddress = address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address ?? IPAddress.None;
        lock (_admissionLock)
        {
            long now = clock.GetTimestamp();
            if (clock.GetElapsedTime(_windowStarted, now) >= TimeSpan.FromMinutes(1))
            {
                _windowStarted = now;
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
