// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates the properties of a commit operation in a Jetstream event.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1133", Justification = "Retained for source compatibility with v1 consumers.")]
[Obsolete("Use JetstreamCommit for Jetstream v2 records.")]
public record AtJetstreamCommit
{
    /// <summary>
    /// Gets the type of the operation the commit refers to.
    /// </summary>
    /// <remarks>
    /// <para>An operation this library does not know about is reported as
    /// <see cref="JetstreamCommitOperation.Unknown"/>, so an operation added to the jetstream after this library was
    /// built does not make the whole commit unreadable.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public required JetstreamCommitOperation Operation { get; init; }

    /// <summary>
    /// Gets the <see cref="Nsid"/> of the collection the operation happened against.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Guarded because the property is declared non-nullable and a jetstream is remote input. Marking a property
    /// as required makes the serializer insist the property is present, not that its value is not <see langword="null" />.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public required Nsid Collection
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    }

    /// <summary>
    /// Gets the revision key for the operation.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Guarded because the property is declared non-nullable and a jetstream is remote input.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public required string Rev
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    }

    /// <summary>
    /// Gets the record key of the record the operation happened against.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Guarded because the property is declared non-nullable and a jetstream is remote input.</para>
    /// </remarks>
    [JsonPropertyName("rkey")]
    [JsonInclude]
    [JsonRequired]
    public required RecordKey RKey
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    }

    /// <summary>
    /// Gets the value, if any, of the record that triggered the commit event.
    /// </summary>
    /// <remarks>
    /// <para>A record can be of any type, including one this library knows nothing about, so it is presented as raw
    /// JSON rather than as a strongly typed value.</para>
    /// </remarks>
    public JsonElement? Record { get; init; }

    /// <summary>
    /// Gets the content identifier for the commit event.
    /// </summary>
    public Cid? Cid
    {
        get => ExplicitCid ?? DeferredCid?.Value;
        init => ExplicitCid = value;
    }

    internal Cid? ExplicitCid { get; init; }

    internal Lazy<Cid>? DeferredCid { get; set; }

    internal void SetDagCborPayload(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        byte[] ownedPayload = (byte[])payload.Clone();
        DeferredCid = new Lazy<Cid>(() => Cid.FromDagCbor(ownedPayload));
    }
}
