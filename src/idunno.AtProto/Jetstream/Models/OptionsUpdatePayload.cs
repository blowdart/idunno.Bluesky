// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream.Models;

internal sealed record OptionsUpdatePayload
{
    public Nsid[]? WantedCollections { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Did"/>s to limit commit events to.
    /// </summary>
    /// <remarks>
    /// <para>Named explicitly rather than left to the camel case naming policy, which produces "wantedDIDs" from this
    /// property name. The jetstream reads "wantedDids", and a server which matches property names exactly would read a
    /// message carrying the wrong name as one setting no did filter at all, which is a subscription to everything.</para>
    /// </remarks>
    [JsonPropertyName("wantedDids")]
    public Did[]? WantedDIDs { get; set; }

    public required int MaxMessageSizeBytes { get; init; }
}