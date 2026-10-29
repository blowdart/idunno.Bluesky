// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Contains untrusted public content for an encoded, locally rendered moderation preview.</summary>
internal sealed record SubjectPreview(
    string Kind,
    string? Url,
    string? DisplayName = null,
    string? Handle = null,
    string? Text = null,
    DateTimeOffset? CreatedAt = null,
    string? Cid = null,
    bool DifferentVersion = false,
    PreviewImage[]? Images = null,
    string? MediaNotice = null,
    string? Error = null,
    bool Verified = false,
    string? VideoToken = null,
    string? VideoAlt = null,
    bool Gallery = false,
    PreviewImage? Avatar = null,
    PreviewImage? Banner = null,
    int? Followers = null,
    int? Following = null,
    int? Posts = null,
    string[]? MediaWarnings = null,
    bool ModerationUnavailable = false,
    int? VideoWidth = null,
    int? VideoHeight = null,
    bool MatchesReportedVersion = false);

/// <summary>Contains a bounded raster image fetched by the server, or an explicit unavailable-image explanation.</summary>
internal sealed record PreviewImage(string? DataUrl, string Alt, string? Error = null);
