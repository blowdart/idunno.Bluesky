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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> is <see langword="null"/>.</exception>
    public AtProtoRecord(AtProtoRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        ExtensionData = new Dictionary<string, JsonElement>(record.ExtensionData);
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

        return ExtensionDataComparer.Equals(ExtensionData, other.ExtensionData);
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
        hashCode.Add(ExtensionDataComparer.GetHashCode(ExtensionData));

        return hashCode.ToHashCode();
    }
}
