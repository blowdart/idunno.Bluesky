// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Represents a view over an embedded video in a post.
/// </summary>
public record EmbeddedVideoView : EmbeddedView
{
    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedVideoView"/>.
    /// </summary>
    /// <param name="cid">The <see cref="AtProto.Cid">Content Identifier</see> for the video.</param>
    /// <param name="playlistUri">The <see cref="Uri"/> for the playlist of the video.</param>
    /// <param name="thumbnailUri">The <see cref="Uri"/> for the video thumbnail.</param>
    /// <param name="altText">The alt text description of the video, for accessibility.</param>
    /// <param name="presentation">An optional hint to the client about how to present the video.</param>
    /// <param name="aspectRatio">An optional aspect ratio for the video.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cid"/> or <paramref name="playlistUri"/> is <see langword="null" />.</exception>
    [JsonConstructor]
    internal EmbeddedVideoView(Cid cid, Uri playlistUri, Uri? thumbnailUri, string? altText, AspectRatio? aspectRatio, string? presentation)
    {
        ArgumentNullException.ThrowIfNull(cid);
        ArgumentNullException.ThrowIfNull(playlistUri);

        Cid = cid;
        PlaylistUri = playlistUri;
        ThumbnailUri = thumbnailUri;
        AltText = altText;
        Presentation = presentation;
        AspectRatio = aspectRatio;
    }

    /// <summary>
    /// Gets the <see cref="AtProto.Cid">Content Identifier</see> for the video.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public Cid Cid { get; init; }

    /// <summary>
    /// Gets the <see cref="Uri"/> for the playlist of the video.
    /// </summary>
    [JsonPropertyName("playlist")]
    [JsonInclude]
    [JsonRequired]
    public Uri PlaylistUri { get; init; }

    /// <summary>
    /// Gets the <see cref="Uri"/> for the video thumbnail, if the service provided one.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("thumbnail")]
    public Uri? ThumbnailUri { get; init; }

    /// <summary>
    /// Gets the alt text description of the video, for accessibility, if the service provided one.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("alt")]
    public string? AltText { get; init; }

    /// <summary>
    /// Gets an optional aspect ratio for the video.
    /// </summary>
    [JsonInclude]
    public AspectRatio? AspectRatio { get; init; }

    /// <summary>
    /// Gets an optional hint to the client about how to present the video.
    /// Known values are provided by <see cref="VideoPresentationKnownValues"/>, but may contain any value.
    /// </summary>
    [JsonInclude]
    public string? Presentation { get; init; }
}