// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

#pragma warning disable IDE0130
namespace idunno.Bluesky;
#pragma warning restore IDE0130

/// <summary>
/// Represents an error indicating that the user has already left the conversation.
/// </summary>
public sealed class AlreadyLeftConvo : BlueskyError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlreadyLeftConvo"/> class.
    /// </summary>
    /// <param name="atErrorDetail">The error details to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="atErrorDetail"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="atErrorDetail"/> does not have the expected error title.</exception>
    public AlreadyLeftConvo(AtErrorDetail atErrorDetail) : base(atErrorDetail)
    {
        ArgumentNullException.ThrowIfNull(atErrorDetail);

        if (!string.Equals(atErrorDetail.Error, ErrorTitle, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The provided AtErrorDetail does not have the expected title '{ErrorTitle}'.", nameof(atErrorDetail));
        }
    }

    internal const string ErrorTitle = "AlreadyLeftConvo";
}
