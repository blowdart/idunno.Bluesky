// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream.Models;

/// <summary>
/// The payload of a <see cref="JetstreamProtocolVersion.V2"/> event message.
/// </summary>
/// <remarks>
/// <para>Every event kind shares the sequence number, did and timestamps, so one type carries the fields of all of
/// them and the <c>$type</c> of the payload decides which are read.</para>
/// </remarks>
internal sealed record JetstreamV2EventPayload
{
    [JsonPropertyName("seq")]
    [JsonRequired]
    public required long Sequence { get; init; }

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

    [JsonRequired]
    public required DateTimeOffset Time { get; init; }

    public DateTimeOffset? WitnessedAt { get; init; }

    public string? Rev { get; init; }

    public JetstreamCommitOperation? Operation { get; init; }

    public Nsid? Collection { get; init; }

    [JsonPropertyName("rkey")]
    public RecordKey? RKey { get; init; }

    public JsonElement? Record { get; init; }

    public Cid? Cid { get; init; }

    public AtJetStreamIdentity? Identity { get; init; }

    public AtJetstreamAccount? Account { get; init; }

    public AtJetstreamSync? Sync { get; init; }

    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; }
}