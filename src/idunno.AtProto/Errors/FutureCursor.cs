// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Represents an error indicating that the specified cursor is in the future.
/// </summary>
public sealed class FutureCursor : AtProtoError
{
    /// <summary>
    /// Creates a new instance of the <see cref="FutureCursor"/>.
    /// </summary>
    /// <param name="atErrorDetail">The <see cref="AtErrorDetail"/> instance containing error details.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atErrorDetail"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atErrorDetail"/> does not have the expected error title.</exception>
    internal FutureCursor(AtErrorDetail atErrorDetail) : base(atErrorDetail)
    {
        ArgumentNullException.ThrowIfNull(atErrorDetail);

        if (!string.Equals(atErrorDetail.Error, ErrorTitle, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The provided AtErrorDetail does not have the expected title '{ErrorTitle}'.", nameof(atErrorDetail));
        }
    }

    internal const string ErrorTitle = "FutureCursor";
}