// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication;

/// <summary>
/// Represents a reference to a published AT Protocol permission-set lexicon.
/// </summary>
/// <remarks>
/// <para>The authorization server resolves the lexicon; the client does not expand its permissions.</para>
/// <para>Configuration binding accepts a string <c>Nsid</c> and an optional string <c>Audience</c>.
/// Enable <c>ErrorOnUnknownConfiguration</c> when binding to reject invalid collection entries.</para>
/// <para>See <see href="https://atproto.com/specs/permission#permission-sets">Permission Sets</see>.</para>
/// </remarks>
public sealed class OAuthPermissionSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthPermissionSet"/> class.
    /// </summary>
    /// <param name="nsid">The NSID of the published permission-set lexicon.</param>
    /// <param name="audience">An optional audience inherited by RPC permissions, as a DID followed by a service fragment, or <c>*</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="nsid"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="audience"/> is not a DID service reference or <c>*</c>.</exception>
    public OAuthPermissionSet(Nsid nsid, string? audience = null)
    {
        ArgumentNullException.ThrowIfNull(nsid);

        if (audience is not null && audience != "*")
        {
            int fragmentIndex = audience.IndexOf('#', StringComparison.Ordinal);
            if (fragmentIndex <= 0 ||
                fragmentIndex == audience.Length - 1 ||
                !Did.TryParse(audience[..fragmentIndex], out _) ||
                audience[(fragmentIndex + 1)..].Contains('#', StringComparison.Ordinal) ||
                audience.Any(char.IsWhiteSpace) ||
                audience.Any(char.IsControl))
            {
                throw new ArgumentException("The audience must be a DID followed by a service fragment, or a wildcard.", nameof(audience));
            }
        }

        Nsid = nsid;
        Audience = audience;
    }

    /// <summary>
    /// Gets the NSID of the published permission-set lexicon.
    /// </summary>
    public Nsid Nsid { get; }

    /// <summary>
    /// Gets the audience inherited by RPC permissions, or <see langword="null"/> when no audience is specified.
    /// </summary>
    public string? Audience { get; }

    /// <summary>
    /// Returns the OAuth <c>include:</c> scope for this permission set.
    /// </summary>
    /// <returns>The scope with its audience percent-encoded when present.</returns>
    public override string ToString() =>
        Audience is null ? $"include:{Nsid}" : $"include:{Nsid}?aud={Uri.EscapeDataString(Audience)}";

    /// <summary>
    /// Converts a permission set to its OAuth <c>include:</c> scope.
    /// </summary>
    /// <param name="permissionSet">The permission set to convert.</param>
    /// <returns>The OAuth scope for the permission set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="permissionSet"/> is <see langword="null"/>.</exception>
    public static implicit operator string(OAuthPermissionSet permissionSet)
    {
        ArgumentNullException.ThrowIfNull(permissionSet);

        return permissionSet.ToString();
    }
}
