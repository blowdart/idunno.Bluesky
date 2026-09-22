// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates a record from an AtProto Jetstream.
/// </summary>
public record AtJetstreamEvent
{
    /// <summary>
    /// The <see cref="AtProto.Did"/> of the account the event refers to.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Guarded because the property is declared non-nullable and a jetstream is remote input. Marking a property
    /// as required makes the serializer insist the property is present, not that its value is not <see langword="null" />, so
    /// without this a message carrying an explicit <see langword="null" /> leaves a <see langword="null" /> behind a non-nullable annotation.</para>
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
    /// Gets the timestamp for the record, in Unix microseconds.
    /// </summary>
    [JsonPropertyName("time_us")]
    [JsonInclude]
    [JsonRequired]
    public required long TimeStamp { get; init; }

    /// <summary>
    /// Gets the kind of the event.
    /// </summary>
    /// <remarks>
    /// <para>A kind which this library does not know about is reported as <see cref="JetStreamEventKind.Unknown"/>, so
    /// an event kind added to the jetstream after this library was built does not make the whole event unreadable.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public required JetStreamEventKind Kind { get; init; }

    /// <summary>
    /// A list of keys and element data that do not map to any strongly typed properties.
    /// </summary>
    /// <remarks>
    /// <para>Settable, and so nullable, because deserialization needs to be able to set it. Consumers should check for
    /// <see langword="null"/> rather than rely on the initial value surviving.</para>
    /// </remarks>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();

    /// <summary>
    /// Gets the timestamp for the record as a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <remarks>
    /// <para><see cref="TimeStamp"/> is remote input, and the range of a <see cref="long"/> of microseconds is far wider
    /// than the range a <see cref="System.DateTimeOffset"/> can hold, so a value outside that range is clamped to
    /// <see cref="System.DateTimeOffset.MinValue"/> or <see cref="System.DateTimeOffset.MaxValue"/> rather than throwing
    /// out of a property getter.</para>
    /// </remarks>
    [JsonIgnore]
    public DateTimeOffset DateTimeOffset
    {
        get
        {
            long milliseconds = TimeStamp / 1000;

            if (milliseconds < MinimumUnixTimeMilliseconds)
            {
                return System.DateTimeOffset.MinValue;
            }

            if (milliseconds > MaximumUnixTimeMilliseconds)
            {
                return System.DateTimeOffset.MaxValue;
            }

            return System.DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToUniversalTime();
        }
    }

    /// <summary>
    /// The smallest number of Unix milliseconds <see cref="System.DateTimeOffset.FromUnixTimeMilliseconds(long)"/> accepts.
    /// </summary>
    private const long MinimumUnixTimeMilliseconds = -62135596800000;

    /// <summary>
    /// The largest number of Unix milliseconds <see cref="System.DateTimeOffset.FromUnixTimeMilliseconds(long)"/> accepts.
    /// </summary>
    private const long MaximumUnixTimeMilliseconds = 253402300799999;
}
