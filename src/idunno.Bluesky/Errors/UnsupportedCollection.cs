// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;

#pragma warning disable IDE0130
namespace idunno.Bluesky;
#pragma warning restore IDE0130

/// <summary>
/// Represents an error indicating that the record referred to belongs to a collection which cannot be bookmarked.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "The type name matches the error name declared by the app.bsky.bookmark lexicons.")]
public sealed class UnsupportedCollection : BlueskyError
{
    /// <summary>
    /// Creates a new instance of the <see cref="UnsupportedCollection"/>.
    /// </summary>
    /// <param name="atErrorDetail">The <see cref="AtErrorDetail"/> instance containing error details.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atErrorDetail"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atErrorDetail"/> does not have the expected error title.</exception>
    public UnsupportedCollection(AtErrorDetail atErrorDetail) : base(atErrorDetail)
    {
        ArgumentNullException.ThrowIfNull(atErrorDetail);

        if (!string.Equals(atErrorDetail.Error, ErrorTitle, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The provided AtErrorDetail does not have the expected title '{ErrorTitle}'.", nameof(atErrorDetail));
        }
    }

    internal const string ErrorTitle = "UnsupportedCollection";
}
