// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.AtProto.Repo;

/// <summary>
/// Compares and hashes JSON extension data dictionaries by their contents rather than by reference.
/// </summary>
internal static class ExtensionDataComparer
{
    /// <summary>
    /// Returns a flag indicating whether <paramref name="left"/> and <paramref name="right"/> hold the same entries.
    /// </summary>
    /// <param name="left">The first extension data dictionary to compare.</param>
    /// <param name="right">The second extension data dictionary to compare.</param>
    public static bool Equals(IDictionary<string, JsonElement>? left, IDictionary<string, JsonElement>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        int leftCount = left is null ? 0 : left.Count;
        int rightCount = right is null ? 0 : right.Count;

        if (leftCount != rightCount)
        {
            return false;
        }

        if (leftCount == 0)
        {
            return true;
        }

        foreach (KeyValuePair<string, JsonElement> entry in left!)
        {
            if (!right!.TryGetValue(entry.Key, out JsonElement rightValue) ||
                !JsonElementEquals(entry.Value, rightValue))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a hash code for <paramref name="extensionData"/> derived from its keys rather than from its identity.
    /// </summary>
    /// <param name="extensionData">The extension data dictionary to hash.</param>
    public static int GetHashCode(IDictionary<string, JsonElement>? extensionData)
    {
        HashCode hashCode = new();

        hashCode.Add(extensionData is null ? 0 : extensionData.Count);

        int keyHashCode = 0;

        if (extensionData is not null)
        {
            foreach (string key in extensionData.Keys)
            {
                keyHashCode ^= StringComparer.Ordinal.GetHashCode(key);
            }
        }

        hashCode.Add(keyHashCode);

        return hashCode.ToHashCode();
    }

    private static bool JsonElementEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False => true,
            _ => string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal)
        };
    }
}
