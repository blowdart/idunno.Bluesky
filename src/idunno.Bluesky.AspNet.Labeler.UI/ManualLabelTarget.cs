// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Converts supported Bluesky web links without fetching arbitrary pasted URLs.</summary>
internal static class ManualLabelTarget
{
    /// <summary>Maps exact Bluesky routes to account identifiers or complete record URIs, retaining unsupported input for explicit validation.</summary>
    internal static string Normalize(string target)
    {
        if (target.StartsWith("at://", StringComparison.Ordinal))
        {
            string repository = target[5..];
            if (AtIdentifier.TryParse(repository, out AtIdentifier? identifier))
            {
                return identifier.ToString();
            }

            return target;
        }

        if (!Uri.TryCreate(target, UriKind.Absolute, out Uri? url) ||
            url.Scheme != Uri.UriSchemeHttps || url.Host != "bsky.app" ||
            !url.IsDefaultPort || url.UserInfo.Length != 0)
        {
            return target;
        }

        string[] parts = url.AbsolutePath.TrimEnd('/').Split('/');
        if (parts.Length < 3)
        {
            return target;
        }

        string account = Uri.UnescapeDataString(parts[2]);
        if (!AtIdentifier.TryParse(account, out _))
        {
            return target;
        }

        if (parts.Length == 3 && parts[1] == "profile")
        {
            return account;
        }

        string? collection = parts switch
        {
            ["", "profile", _, "post", _] => "app.bsky.feed.post",
            ["", "profile", _, "lists", _] => "app.bsky.graph.list",
            ["", "profile", _, "feed", _] => "app.bsky.feed.generator",
            ["", "starter-pack", _, _] => "app.bsky.graph.starterpack",
            _ => null
        };
        if (collection is null)
        {
            return target;
        }

        string key = Uri.UnescapeDataString(parts[^1]);
        string record = $"at://{account}/{collection}/{key}";
        return key.Length > 0 &&
            !key.Contains('/', StringComparison.Ordinal) &&
            !key.Contains('?', StringComparison.Ordinal) &&
            !key.Contains('#', StringComparison.Ordinal) &&
            AtUri.TryParse(record, out AtUri? uri) && uri.RecordKey is not null
            ? uri.ToString()
            : target;
    }
}
