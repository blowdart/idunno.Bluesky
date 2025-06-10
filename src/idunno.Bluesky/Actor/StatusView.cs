// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Nodes;

using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Provides a view over an actor's current status
    /// </summary>
    /// <param name="Status">The status for the account.</param>
    /// <param name="Record">A record associated with the status</param>
    /// <param name="Embed">An optional <see cref="EmbeddedView"/> associated with the status.</param>
    /// <param name="ExpiresAt">An optional <see cref="DateTimeOffset" /> when this status will expire</param>
    /// <param name="IsActive">Gets a flag indicating if the status is not expired. Only present <paramref name="ExpiresAt"/> was set.</param>
    public sealed record StatusView(string Status, JsonNode Record, EmbeddedView? Embed, DateTimeOffset? ExpiresAt, bool? IsActive)
    {
    }
}
