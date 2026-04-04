// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates the properties of a identity operation in a Jetstream event.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(AtJetStreamIdentity), "com.atproto.sync.subscribeRepos#identity")]
public record AtJetStreamIdentity
{
    /// <summary>
    /// Gets the <see cref="AtProto.Did"/> of the account that triggered the event.
    /// </summary>
    public required Did Did { get; init; }

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
