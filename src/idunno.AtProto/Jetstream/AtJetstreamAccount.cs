// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates the properties of an account operation in a Jetstream event.
/// </summary>
public record AtJetstreamAccount
{
    /// <summary>
    /// Flag indicating the active status of the account.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public required bool Active { get; init; }

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
    /// Gets the sequence number for the change.
    /// </summary>
    [JsonPropertyName("seq")]
    [JsonInclude]
    [JsonRequired]
    public required ulong Sequence { get; init; }

    /// <summary>
    /// Gets the status for the account, if any.
    /// </summary>
    public AccountStatus? Status { get; init; }

    /// <summary>
    /// Gets the timestamp for the change.
    /// </summary>
    [JsonPropertyName("time")]
    public DateTimeOffset TimeStamp { get; init; }
}
