// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Issuance;

/// <summary>Reconciles protocol decisions without losing precision from signed timestamps.</summary>
/// <remarks>
/// <para>Database sequence orders distribution, not supersession. The same rules run before signing and
/// inside provider commits so a caller cannot accidentally commit a stale or contradictory no-op.</para>
/// </remarks>
internal static class LabelDecisionRules
{
    /// <summary>Compares exact creation instants, including fractional digits beyond .NET ticks.</summary>
    internal static int CompareCreation(SignedLabelSnapshot left, SignedLabelSnapshot right) =>
        CompareTimestamp(
            left: left,
            right: right.Label.CreationTimestamp,
            rightRemainder: Fraction(right, "cts"),
            field: "cts");

    /// <summary>Determines whether an existing label still applies at the supplied service time.</summary>
    internal static bool IsActive(SignedLabelSnapshot signed, DateTimeOffset now)
    {
        Label label = signed.Label;
        return !label.IsNegationLabel && (label.ExpiresAt is not DateTimeOffset ||
            CompareTimestamp(
                left: signed,
                right: now,
                rightRemainder: string.Empty,
                field: "exp") > 0);
    }

    /// <summary>Determines whether an issuance needs a new signed event rather than an audited no-op.</summary>
    internal static bool NeedsLabel(LabelOperationRequest request, StoredLabel? current, DateTimeOffset now)
    {
        if (request.Kind == LabelOperationKind.Negate)
        {
            return current is not null && !current.SignedLabel.Label.IsNegationLabel;
        }

        return current is null || !IsActive(current.SignedLabel, now) ||
            current.SignedLabel.Label.Cid != request.Target.Cid ||
            !HasExpiry(current.SignedLabel, request.ExpiresAt);
    }

    /// <summary>Compares expiry instants without treating a sub-tick historic expiry as tick-precision input.</summary>
    internal static bool HasExpiry(SignedLabelSnapshot signed, DateTimeOffset? expiry) =>
        signed.Label.ExpiresAt is null ? expiry is null :
            expiry is { } instant && CompareTimestamp(
                left: signed,
                right: instant,
                rightRemainder: string.Empty,
                field: "exp") == 0;

    /// <summary>Compares a new tick-precision creation time to a prior exact signed creation instant.</summary>
    internal static bool IsLater(SignedLabelSnapshot signed, SignedLabelSnapshot predecessor) =>
        CompareCreation(signed, predecessor) > 0;

    /// <summary>Orders instants by .NET ticks and then retained signed digits that cannot fit in a tick.</summary>
    private static int CompareTimestamp(
        SignedLabelSnapshot left,
        DateTimeOffset right,
        string rightRemainder,
        string field)
    {
        Label label = left.Label;
        DateTimeOffset instant = field == "cts" ? label.CreationTimestamp : label.ExpiresAt!.Value;
        int ticks = instant.CompareTo(right);
        if (ticks != 0)
        {
            return ticks;
        }

        // Public DateTimeOffset values truncate past seven fractional digits. Compare the remaining
        // digits with implicit trailing zeros rather than rounding away a distinct signed instant.
        string remainder = Fraction(left, field);
        for (int i = 0; i < Math.Max(remainder.Length, rightRemainder.Length); i++)
        {
            char a = i < remainder.Length ? remainder[i] : '0';
            char b = i < rightRemainder.Length ? rightRemainder[i] : '0';
            if (a != b)
            {
                return a.CompareTo(b);
            }
        }

        return 0;
    }

    /// <summary>Extracts fractional digits beyond tick precision from the exact signed timestamp text.</summary>
    private static string Fraction(SignedLabelSnapshot signed, string field)
    {
        using JsonDocument document = JsonDocument.Parse(signed.GetJsonBytes());
        string timestamp = document.RootElement.GetProperty(field).GetString()!;
        int point = timestamp.IndexOf('.', StringComparison.Ordinal);
        if (point < 0)
        {
            return string.Empty;
        }

        int end = point + 1;
        while (end < timestamp.Length && char.IsAsciiDigit(timestamp[end]))
        {
            end++;
        }

        return end > point + 8 ? timestamp[(point + 8)..end] : string.Empty;
    }
}
