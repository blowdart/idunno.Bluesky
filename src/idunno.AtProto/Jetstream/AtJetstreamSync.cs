// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates the properties of a sync operation in a Jetstream event.
/// </summary>
/// <remarks>
/// <para>A sync event says the commit chain for a repo is broken, so a consumer which keeps a copy of the repo should
/// fetch it again rather than apply further commits to the copy it has.</para>
/// </remarks>
public sealed record AtJetstreamSync
{
    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> of the repo the event refers to.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Guarded because the property is declared non-nullable and a jetstream is remote input. Marking a property
    /// as required makes the serializer insist the property is present, not that its value is not <see langword="null" />.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public required Did Did
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    }

    /// <summary>
    /// Gets the upstream sequence number for the change.
    /// </summary>
    /// <remarks>
    /// <para>This is the relay's sequence number, not the jetstream's. Use <see cref="AtJetstreamEvent.Sequence"/> as a cursor.</para>
    /// </remarks>
    [JsonPropertyName("seq")]
    public long Sequence { get; init; }

    /// <summary>
    /// Gets the revision of the commit the repo is now at, if any.
    /// </summary>
    public string? Rev { get; init; }

    /// <summary>
    /// Gets the CAR file, if any, containing the signed commit block for the repo.
    /// </summary>
    public Bytes? Blocks { get; init; }

    /// <summary>
    /// Gets the upstream timestamp for the change.
    /// </summary>
    [JsonPropertyName("time")]
    public DateTimeOffset TimeStamp { get; init; }
}