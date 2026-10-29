// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Retains the canonical identity or record target while presenting a verified handle.</summary>
/// <param name="Value">The retained DID or record URI, never a replacement authorization identity.</param>
/// <param name="Detailed">Whether to display the canonical identifier below the handle.</param>
/// <param name="Subject">Whether to describe the target kind instead of appending its record path.</param>
internal sealed record IdentityDisplayModel(string? Value, bool Detailed = false, bool Subject = false)
{
    /// <summary>Gets a locally bundled Font Awesome icon and readable description without abbreviating the canonical identifier.</summary>
    internal (string Icon, string Prefix, string Kind) SubjectDescription =>
        Value is not null && AtUri.TryParse(Value, out AtUri? uri)
            ? uri.Collection?.ToString() switch
            {
                "app.bsky.feed.post" => ("envelope", "Post by ", "post"),
                "app.bsky.actor.profile" => ("user", string.Empty, "profile"),
                "app.bsky.graph.list" => ("list", "List by ", "list"),
                "app.bsky.feed.generator" => ("newspaper", "Feed by ", "feed"),
                "app.bsky.graph.starterpack" => ("users", "Starter pack by ", "starter pack"),
                _ => ("file-lines", "Record by ", "record")
            }
            : ("user", string.Empty, "profile");

    /// <summary>Gets the DID whose bidirectionally verified handle can be displayed.</summary>
    internal Did? Did
    {
        get
        {
            if (Value is null)
            {
                return null;
            }

            if (AtProto.Did.TryParse(Value, out Did? did))
            {
                return did;
            }

            return AtUri.TryParse(Value, out AtUri? uri) ? uri.Authority as Did : null;
        }
    }

    /// <summary>Gets the unchanged collection and record key, if this identity is a record target.</summary>
    internal string RecordPath => Value is not null && AtUri.TryParse(Value, out AtUri? uri) ? uri.AbsolutePath ?? string.Empty : string.Empty;
}
