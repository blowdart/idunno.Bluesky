// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream
{
    /// <summary>
    /// Encapsulates a record from an AtProto Jetstream.
    /// </summary>
    public record AtJetstreamEvent
    {
        private DateTimeOffset? _timeStamp;

        /// <summary>
        /// The <see cref="AtProto.Did"/> of the account the event refers to.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public required Did Did { get; init; }

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
        [JsonInclude]
        [JsonRequired]
        [JsonConverter(typeof(JsonStringEnumConverter<JetStreamEventKind>))]
        public required JetStreamEventKind Kind { get; init; }

        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [NotNull]
        [JsonExtensionData]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();

        /// <summary>
        /// Gets the timestamp for the record as a <see cref="DateTimeOffset"/>.
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset DateTimeOffset
        {
            get
            {
                _timeStamp ??= DateTimeOffset.FromUnixTimeMilliseconds(TimeStamp / 1000).ToUniversalTime();

                return _timeStamp.Value;
            }
        }
    }
}
