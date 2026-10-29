// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;

#pragma warning disable IDE0130 // Preserve the established namespace independently of folder layout.
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130

/// <summary>
/// A view of a link shown on an actor's profile.
/// </summary>
public sealed record ProfileLinkView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileLinkView"/> record.
    /// </summary>
    /// <param name="uri">The AT URI of the link record.</param>
    /// <param name="cid">The content identifier of the link record.</param>
    /// <param name="url">The destination address for the link.</param>
    /// <param name="title">The optional title for the link.</param>
    /// <param name="icon">The optional address of the destination site's icon.</param>
    /// <param name="labels">Labels applied to the link.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, <paramref name="cid"/> or <paramref name="url"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">An entry in <paramref name="labels"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public ProfileLinkView(AtUri uri, Cid cid, Uri url, string? title, Uri? icon, IReadOnlyCollection<Label>? labels)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(cid);
        ArgumentNullException.ThrowIfNull(url);

        Uri = uri;
        Cid = cid;
        Url = url;
        Title = title;
        Icon = icon;
        Labels = labels;
    }

    /// <summary>
    /// Gets the AT URI of the link record.
    /// </summary>
    [JsonRequired]
    public AtUri Uri { get; init; }

    /// <summary>
    /// Gets the content identifier of the link record.
    /// </summary>
    [JsonRequired]
    public Cid Cid { get; init; }

    /// <summary>
    /// Gets the destination URI for the link.
    /// </summary>
    [JsonRequired]
    public Uri Url { get; init; }

    /// <summary>
    /// Gets the optional title for the link.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the optional URI of the destination site's icon.
    /// </summary>
    public Uri? Icon { get; init; }

    /// <summary>
    /// Gets labels applied to the link.
    /// </summary>
    /// <exception cref="JsonException">An entry is <see langword="null"/>.</exception>
    public IReadOnlyCollection<Label>? Labels
    {
        get;
        init
        {
            if (value?.Any(label => label is null) == true)
            {
                throw new JsonException("Profile link labels cannot contain null entries.");
            }

            field = value is null ? null : Array.AsReadOnly(value.ToArray());
        }
    }
}
