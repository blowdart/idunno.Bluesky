// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Represents an error indicating that the space does not have a member-space policy, so its members cannot be managed.
/// </summary>
/// <remarks>
/// <para>This error is defined by the experimental <c>com.atproto.memberspace</c> lexicons.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class PolicyMismatch : AtProtoError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyMismatch"/> class.
    /// </summary>
    /// <param name="atErrorDetail">The error details to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="atErrorDetail"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="atErrorDetail"/> does not have the expected error title.</exception>
    public PolicyMismatch(AtErrorDetail atErrorDetail) : base(atErrorDetail)
    {
        ArgumentNullException.ThrowIfNull(atErrorDetail);

        if (!string.Equals(atErrorDetail.Error, ErrorTitle, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The provided AtErrorDetail does not have the expected title '{ErrorTitle}'.", nameof(atErrorDetail));
        }
    }

    internal const string ErrorTitle = "PolicyMismatch";
}
