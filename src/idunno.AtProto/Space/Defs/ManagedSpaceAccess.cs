// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Describes access returned by a managing application.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed record ManagedSpaceAccess
{
    /// <summary>
    /// Gets or sets a value that indicates whether reading is allowed.
    /// </summary>
    /// <value><see langword="true"/> if reading is allowed; otherwise, <see langword="false"/>.</value>
    [JsonRequired]
    public required bool Read { get; init; }

    /// <summary>
    /// Gets or sets a value that indicates whether writing is allowed.
    /// </summary>
    /// <value><see langword="true"/> if writing is allowed; otherwise, <see langword="false"/>.</value>
    [JsonRequired]
    public required bool Write { get; init; }
}
