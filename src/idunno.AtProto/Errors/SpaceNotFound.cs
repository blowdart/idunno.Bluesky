// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Represents an error indicating that the requested space could not be found.
/// </summary>
/// <remarks>
/// <para>A space authorization request may return this error in place of <see cref="AccountNotAuthorized"/>, to avoid revealing that the space exists.</para>
/// <para>This error is defined by the experimental <c>com.atproto.space</c> and <c>com.atproto.memberspace</c> lexicons.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class SpaceNotFound : AtProtoError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpaceNotFound"/> class.
    /// </summary>
    /// <param name="atErrorDetail">The error details to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="atErrorDetail"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="atErrorDetail"/> does not have the expected error title.</exception>
    public SpaceNotFound(AtErrorDetail atErrorDetail) : base(atErrorDetail)
    {
        ArgumentNullException.ThrowIfNull(atErrorDetail);

        if (!string.Equals(atErrorDetail.Error, ErrorTitle, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The provided AtErrorDetail does not have the expected title '{ErrorTitle}'.", nameof(atErrorDetail));
        }
    }

    internal const string ErrorTitle = "SpaceNotFound";
}
