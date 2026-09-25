// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates the properties of a Jetstream identity event.
/// </summary>
public sealed record AtJetstreamIdentityEvent : AtJetstreamEvent
{
    /// <summary>
    /// Gets the details of the identity change that triggered this event.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public required AtJetStreamIdentity Identity
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    }
}
