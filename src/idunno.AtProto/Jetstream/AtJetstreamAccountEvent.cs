// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates the properties of a Jetstream account event.
/// </summary>
public sealed record AtJetstreamAccountEvent : AtJetstreamEvent
{
    /// <summary>
    /// Gets the account state change that triggered the event.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public required AtJetstreamAccount Account
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    }
}
