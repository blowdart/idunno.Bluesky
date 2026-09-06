// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

#pragma warning disable IDE0130
namespace idunno.Bluesky;
#pragma warning restore IDE0130

/// <summary>
/// Represents an error indicating that the part number is outside the upload's valid part range
/// </summary>
public sealed class InvalidPartNumber : BlueskyError
{
    /// <summary>
    /// Creates a new instance of the <see cref="InvalidPartNumber"/>.
    /// </summary>
    /// <param name="atErrorDetail">The <see cref="AtErrorDetail"/> instance containing error details.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atErrorDetail"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atErrorDetail"/> does not have the expected error title.</exception>
    public InvalidPartNumber(AtErrorDetail atErrorDetail) : base(atErrorDetail)
    {
        ArgumentNullException.ThrowIfNull(atErrorDetail);

        if (!string.Equals(atErrorDetail.Error, ErrorTitle, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The provided AtErrorDetail does not have the expected title '{ErrorTitle}'.", nameof(atErrorDetail));
        }
    }

    internal const string ErrorTitle = "InvalidPartNumber";
}