// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Represents an error indicating that a required authorization scope is missing.
/// </summary>
/// <remarks>
/// <para>The error name is not formally defined by the AT Protocol specifications or endpoint lexicons.
/// It comes from the <see href="https://github.com/bluesky-social/atproto/blob/main/packages/oauth/oauth-scopes/src/scope-missing-error.ts">reference implementation</see>.</para>
/// <para>This error should accompany an HTTP response status code of 403 (Forbidden).</para>
/// </remarks>
public sealed class ScopeMissingError : AtProtoError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeMissingError"/> class.
    /// </summary>
    /// <param name="atErrorDetail">The error details to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="atErrorDetail"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="atErrorDetail"/> does not have the expected error title.</exception>
    public ScopeMissingError(AtErrorDetail atErrorDetail) : base(atErrorDetail)
    {
        ArgumentNullException.ThrowIfNull(atErrorDetail);

        if (!string.Equals(atErrorDetail.Error, ErrorTitle, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The provided AtErrorDetail does not have the expected title '{ErrorTitle}'.", nameof(atErrorDetail));
        }
    }

    internal const string ErrorTitle = "ScopeMissingError";
}
