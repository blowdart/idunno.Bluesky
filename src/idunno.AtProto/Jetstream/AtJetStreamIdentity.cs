// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates the properties of a identity operation in a Jetstream event.
/// </summary>
public record AtJetStreamIdentity
{
    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> of the account that triggered the event.
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
    /// Gets the <see cref="AtProto.Handle"/> for the <see cref="Did"/>, if any.
    /// </summary>
    public Handle? Handle { get; init; }

    /// <summary>
    /// Gets the sequence number for the change.
    /// </summary>
    [JsonPropertyName("seq")]
    public ulong Sequence { get; init; }

    /// <summary>
    /// Gets the timestamp for the change.
    /// </summary>
    [JsonPropertyName("time")]
    public DateTimeOffset TimeStamp { get; init; }
}
