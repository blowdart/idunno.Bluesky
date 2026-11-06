// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Shares bounded, host-local authenticated Ozone and Xenon activity for five minutes.</summary>
internal sealed class OzoneTeamPresence(TimeProvider clock)
{
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private readonly Dictionary<Did, DateTimeOffset> _activity = [];

    /// <summary>Records an authorized caller while pruning expired or revoked membership observations.</summary>
    internal void Observe(Did actor, IReadOnlyList<LabelTeamMember> team)
    {
        lock (_gate)
        {
            Prune(team);
            if (team.Any(member => member.Did == actor && member.Active && !member.Removed))
            {
                DateTimeOffset now = clock.GetUtcNow();
                if (!_activity.TryGetValue(actor, out DateTimeOffset previous) || now > previous)
                {
                    _activity[actor] = now;
                }
            }
        }
    }

    /// <summary>Returns live members ordered by latest activity, with deterministic ties.</summary>
    internal OzoneOnlineMember[] List(IReadOnlyList<LabelTeamMember> team, int limit)
    {
        lock (_gate)
        {
            Prune(team);
            return [.. _activity
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key.Value, StringComparer.Ordinal)
                .Take(limit)
                .Select(entry => new OzoneOnlineMember(entry.Key.Value, entry.Value))];
        }
    }

    /// <summary>Bounds observations to the currently authorized team and the recent-activity window.</summary>
    private void Prune(IReadOnlyList<LabelTeamMember> team)
    {
        HashSet<Did> active = [.. team.Where(member => member.Active && !member.Removed).Select(member => member.Did)];
        DateTimeOffset cutoff = clock.GetUtcNow() - TimeSpan.FromMinutes(5);
        foreach (Did did in _activity.Where(entry => entry.Value <= cutoff || !active.Contains(entry.Key)).Select(entry => entry.Key).ToArray())
        {
            _activity.Remove(did);
        }
    }
}
