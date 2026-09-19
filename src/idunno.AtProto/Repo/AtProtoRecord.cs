// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo;

/// <summary>
/// A base record for the value property on an <see cref="AtProtoRepositoryRecord{TRecord}"/>.
/// </summary>
public record AtProtoRecord
{
    /// <summary>
    /// Creates a new instance of <see cref="AtProtoRecord"/>
    /// </summary>
    [JsonConstructor]
    public AtProtoRecord() { }

    /// <summary>
    /// Creates a new instance of <see cref="AtProtoRecord"/> from the specified <paramref name="record"/>.
    /// </summary>
    /// <param name="record">The <see cref="AtProtoRecord"/> to create the new instance from.</param>
    public AtProtoRecord(AtProtoRecord record)
    {
        if (record is not null)
        {
            ExtensionData = new Dictionary<string, JsonElement>(record.ExtensionData);
        }
    }

    /// <summary>
    /// A list of keys and element data that do not map to any strongly typed properties.
    /// </summary>
    [NotNull]
    [ExcludeFromCodeCoverage]
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();

    /// <summary>
    /// Determines whether the specified <see cref="AtProtoRecord"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="AtProtoRecord"/> to compare against the current instance.</param>
    /// <returns><see langword="true" /> if <paramref name="other"/> is equal to the current instance, otherwise <see langword="false" />.</returns>
    /// <remarks>
    /// <para><see cref="ExtensionData"/> is compared by its contents rather than by reference.</para>
    /// </remarks>
    public virtual bool Equals(AtProtoRecord? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || EqualityContract != other.EqualityContract)
        {
            return false;
        }

        return ExtensionDataEquals(ExtensionData, other.ExtensionData);
    }

    /// <summary>
    /// Returns the hash code for the current instance.
    /// </summary>
    /// <returns>The hash code for the current instance.</returns>
    /// <remarks>
    /// <para>The hash code is derived from the keys of <see cref="ExtensionData"/> rather than from its identity.</para>
    /// </remarks>
    public override int GetHashCode()
    {
        HashCode hashCode = new();

        hashCode.Add(EqualityContract);
        hashCode.Add(ExtensionData is null ? 0 : ExtensionData.Count);

        int keyHashCode = 0;

        if (ExtensionData is not null)
        {
            foreach (string key in ExtensionData.Keys)
            {
                keyHashCode ^= StringComparer.Ordinal.GetHashCode(key);
            }
        }

        hashCode.Add(keyHashCode);

        return hashCode.ToHashCode();
    }

    private static bool ExtensionDataEquals(IDictionary<string, JsonElement>? left, IDictionary<string, JsonElement>? right)
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