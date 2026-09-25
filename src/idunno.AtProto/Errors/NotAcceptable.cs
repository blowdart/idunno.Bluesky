// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Represents an error indicating that the service cannot produce a response matching the request's Accept headers, corresponding to an HTTP 406.
/// </summary>
public sealed class NotAcceptable : AtProtoError
{
    /// <summary>
    /// Creates a new instance of the <see cref="NotAcceptable"/>.
    /// </summary>
    /// <param name="atErrorDetail">The <see cref="AtErrorDetail"/> instance containing error details.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="atErrorDetail"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atErrorDetail"/> does not have the expected error title.</exception>
    public NotAcceptable(AtErrorDetail atErrorDetail) : base(atErrorDetail)
    {
        ArgumentNullException.ThrowIfNull(atErrorDetail);

        if (!string.Equals(atErrorDetail.Error, ErrorTitle, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The provided AtErrorDetail does not have the expected title '{ErrorTitle}'.", nameof(atErrorDetail));
        }
    }

    internal const string ErrorTitle = "NotAcceptable";
}
