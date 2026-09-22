// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo;

/// <summary>
/// Represents an <see cref="AtProtoRecord"/> retrieved from a repository.
/// </summary>
public record AtProtoRepositoryRecord : AtProtoRepositoryObject
{
    /// <summary>
    /// Creates a new instance of <see cref="AtProtoRepositoryRecord"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the record in an atproto repository.</param>
    /// <param name="cid">The <see cref="Cid"/> of the record in an atproto repository.</param>
    /// <param name="value">The value of the record.</param>
    public AtProtoRepositoryRecord(AtUri uri, Cid cid, JsonObject? value) : base(uri, cid)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the value of the record.
    /// </summary>
    public JsonObject? Value { get; }

    /// <summary>
    /// A list of keys and element data that do not map to any strongly typed properties.
    /// </summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();

    /// <summary>
    /// Determines whether the specified <see cref="AtProtoRepositoryRecord"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="AtProtoRepositoryRecord"/> to compare against the current instance.</param>
    /// <returns><see langword="true" /> if <paramref name="other"/> is equal to the current instance, otherwise <see langword="false" />.</returns>
    /// <remarks>
    /// <para><see cref="ExtensionData"/> is compared by its contents rather than by reference.</para>
    /// </remarks>
    public virtual bool Equals(AtProtoRepositoryRecord? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || EqualityContract != other.EqualityContract)
        {
            return false;
        }

        return Uri == other.Uri &&
            Cid == other.Cid &&
            JsonNode.DeepEquals(Value, other.Value) &&
            ExtensionDataComparer.Equals(ExtensionData, other.ExtensionData);
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
        hashCode.Add(Uri);
        hashCode.Add(Cid);
        hashCode.Add(ExtensionDataComparer.GetHashCode(ExtensionData));

        return hashCode.ToHashCode();
    }
}

/// <summary>
/// Represents an <see cref="AtProtoRecord"/> retrieved from a repository.
/// </summary>
/// <typeparam name="TRecord">The type of the record.</typeparam>
public record AtProtoRepositoryRecord<TRecord> : AtProtoRepositoryObject where TRecord : AtProtoRecord
{
    /// <summary>
    /// Creates a new instance of at AT Proto record.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the record in an atproto repository.</param>
    /// <param name="cid">The <see cref="Cid"/> of the record in an atproto repository.</param>
    /// <param name="value">The value of the record.</param>
    public AtProtoRepositoryRecord(AtUri uri, Cid cid, TRecord value) : base(uri, cid)
    {
        Value = value;
    }

    /// <summary>
    /// Gets or sets the value of the record.
    /// </summary>
    [JsonRequired]
    public TRecord Value { get; set; }

    /// <summary>
    /// A list of keys and element data that do not map to any strongly typed properties.
    /// </summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();

    /// <summary>
    /// Determines whether the specified <see cref="AtProtoRepositoryRecord{TRecord}"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="AtProtoRepositoryRecord{TRecord}"/> to compare against the current instance.</param>
    /// <returns><see langword="true" /> if <paramref name="other"/> is equal to the current instance, otherwise <see langword="false" />.</returns>
    /// <remarks>
    /// <para><see cref="ExtensionData"/> is compared by its contents rather than by reference.</para>
    /// </remarks>
    public virtual bool Equals(AtProtoRepositoryRecord<TRecord>? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || EqualityContract != other.EqualityContract)
        {
            return false;
        }

        return Uri == other.Uri &&
            Cid == other.Cid &&
            EqualityComparer<TRecord>.Default.Equals(Value, other.Value) &&
            ExtensionDataComparer.Equals(ExtensionData, other.ExtensionData);
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
        hashCode.Add(Uri);
        hashCode.Add(Cid);
        hashCode.Add(Value);
        hashCode.Add(ExtensionDataComparer.GetHashCode(ExtensionData));

        return hashCode.ToHashCode();
    }
}