// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Represents an error indicating that the access token has insufficient authorization scope.
/// </summary>
/// <remarks>
/// <para>The <c>insufficient_scope</c> error name is not formally defined by the AT Protocol endpoint lexicons.
/// Unlike the reference implementation's <see cref="ScopeMissingError"/>, it is defined by
/// <see href="https://www.rfc-editor.org/rfc/rfc6750#section-3.1">RFC 6750 section 3.1</see>.</para>
/// <para>This error should accompany an HTTP response status code of 403 (Forbidden).</para>
/// </remarks>
public sealed class InsufficientScope : AtProtoError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InsufficientScope"/> class.
    /// </summary>
    /// <param name="atErrorDetail">The error details to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="atErrorDetail"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="atErrorDetail"/> does not have the expected error title.</exception>
    public InsufficientScope(AtErrorDetail atErrorDetail) : base(atErrorDetail)
    {
        ArgumentNullException.ThrowIfNull(atErrorDetail);

        if (!string.Equals(atErrorDetail.Error, ErrorTitle, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The provided AtErrorDetail does not have the expected title '{ErrorTitle}'.", nameof(atErrorDetail));
        }
    }

    internal const string ErrorTitle = "insufficient_scope";
}
