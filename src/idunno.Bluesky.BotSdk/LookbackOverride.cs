// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

using idunno.AtProto.Jetstream;

namespace idunno.Bluesky.BotSdk;

// An explicit one-time instruction, not a recurring configuration setting. Clearing it after
// commit prevents reader recovery from skipping more history; the database retains the new start.
/// <summary>Tracks a user-directed one-time forward-only history skip until it has durably committed.</summary>
/// <param name="cutoff">The fixed user-requested cutoff, or <see langword="null"/> for normal recovery.</param>
internal sealed class LookbackOverride(DateTimeOffset? cutoff)
{
    public DateTimeOffset? Cutoff { get; private set; } = cutoff;
    /// <summary>Clears the instruction after durable application so recovery does not repeat the skip.</summary>
    public void Applied() => Cutoff = null;

    /// <summary>Clears an override when no prior replay state exists to override.</summary>
    /// <param name="state">The durable state loaded at startup.</param>
    /// <returns><see langword="true"/> when the instruction was ignored; otherwise, <see langword="false"/>.</returns>
    public bool IgnoreIfUninitialized(ReplayState state)
    {
        if (Cutoff is null || state.RequestJson is not null)
        {
            return false;
        }

        Applied();

        return true;
    }

    /// <summary>Converts a UTC cutoff to the live service's timestamp cursor units.</summary>
    /// <param name="cutoff">The fixed cutoff time.</param>
    /// <returns>The UTC microseconds since the Unix epoch, not an archive sequence.</returns>
    public static long TimestampCursor(DateTimeOffset cutoff) =>
        (cutoff - DateTimeOffset.UnixEpoch).Ticks / 10;

    /// <summary>Chooses an exclusive archive boundary before the event found by a timestamp probe.</summary>
    /// <param name="item">The probe event with a valid sequence and witnessed time.</param>
    /// <returns>The preceding sequence so the probe event itself is still durably handled.</returns>
    public static long BoundaryBefore(JetstreamEvent item)
    {
        if (item.Sequence is not > 1 || item.WitnessedAt is null)
        {
            throw new InvalidDataException("Timestamp lookup returned no usable sequence or witnessed time.");
        }

        // The live cursor is inclusive; archive AfterSeq is exclusive. Replay the probe
        // event again rather than marking an eligible post processed without handling it.
        return item.Sequence.Value - 1;
    }

    /// <summary>Parses explicit whole-number skip durations without culture-sensitive or ambiguous units.</summary>
    /// <param name="value">The number followed by s, m, h, or d.</param>
    /// <returns>The requested duration.</returns>
    public static TimeSpan Parse(string value)
    {
        // Require a unit and whole nonnegative count. Avoid culture-dependent or ambiguous
        // duration parsing for a command that intentionally discards unprocessed history.
        if (value.Length < 2 || !long.TryParse(value[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out long count))
        {
            throw new FormatException("Lookback must be a nonnegative whole number followed by s, m, h, or d (for example 30m).");
        }

        long unit = value[^1] switch
        {
            's' => TimeSpan.TicksPerSecond,
            'm' => TimeSpan.TicksPerMinute,
            'h' => TimeSpan.TicksPerHour,
            'd' => TimeSpan.TicksPerDay,
            _ => throw new FormatException("Lookback units are s, m, h, or d.")
        };

        if (count > TimeSpan.MaxValue.Ticks / unit)
        {
            throw new FormatException("Lookback is too large.");
        }

        return TimeSpan.FromTicks(count * unit);
    }
}
