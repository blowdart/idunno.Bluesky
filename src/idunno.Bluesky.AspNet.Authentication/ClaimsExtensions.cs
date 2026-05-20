// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using idunno.AtProto;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Adds some utilities to the <see cref="ClaimsPrincipal"/> and <see cref="ClaimsIdentity"/> classes.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Nested type is an extension property.")]
public static class ClaimsExtensions
{
    /// <summary>Extension methods for <see cref="ClaimsPrincipal"/>.</summary>
    extension(ClaimsPrincipal principal)
    {
        /// <summary>
        /// Gets the Bluesky Display Name from the claims principal, if any.
        /// </summary>
        /// <value>The display name if present; otherwise, <see langword="null"/>.</value>
        public string? DisplayName
        {
            get
            {
                if (principal == null)
                {
                    return null;
                }

                Claim? claim = principal.Claims.FirstOrDefault(c => c.Type == Bluesky.ClaimTypes.DisplayName);

                return claim?.Value;
            }
        }

        /// <summary>
        /// Gets the Bluesky Handle from the claims principal, if any.
        /// </summary>
        /// <value>The <see cref="AtProto.Handle"/> if present; otherwise, <see langword="null"/>.</value>
        public Handle? Handle
        {
            get
            {
                if (principal == null)
                {
                    return null;
                }

                Claim? claim = principal.Claims.FirstOrDefault(c => c.Type == Bluesky.ClaimTypes.Handle);

                if (claim is null || claim.Value is null)
                {
                    return null;
                }

                if (!Handle.TryParse(claim.Value, out Handle? handle))
                {
                    return null;
                }

                return handle;
            }
        }

        /// <summary>
        /// Gets the user's self description from the claims principal, if any.
        /// </summary>
        /// <value>The description if present; otherwise, <see langword="null"/>.</value>
        public string? Description
        {
            get
            {
                if (principal == null)
                {
                    return null;
                }
                Claim? claim = principal.Claims.FirstOrDefault(c => c.Type == Bluesky.ClaimTypes.Description);
                return claim?.Value;
            }
        }

        /// <summary>
        /// Gets the user's specified pronouns from the claims principal, if any.
        /// </summary>
        /// <value>The pronouns if present; otherwise, <see langword="null"/>.</value>
        public string? Pronouns
        {
            get
            {
                if (principal == null)
                {
                    return null;
                }
                Claim? claim = principal.Claims.FirstOrDefault(c => c.Type == Bluesky.ClaimTypes.Pronouns);
                return claim?.Value;
            }
        }

        /// <summary>
        /// Gets the user's specified website from the claims principal, if any and it converts to a valid <see cref="Uri"/>.
        /// </summary>
        /// <value>The website if present; otherwise, <see langword="null"/>.</value>
        public Uri? Website
        {
            get
            {
                if (principal == null)
                {
                    return null;
                }
                Claim? claim = principal.Claims.FirstOrDefault(c => c.Type == Bluesky.ClaimTypes.Website);

                if (claim is not null && claim.Value is not null && Uri.TryCreate(claim.Value, UriKind.Absolute, out Uri? uri))
                {
                    return uri;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the URI for the user's specified avatar from the claims principal, if any and it converts to a valid <see cref="Uri"/>.
        /// </summary>
        /// <value>The avatar if present; otherwise, <see langword="null"/>.</value>
        public Uri? Avatar
        {
            get
            {
                if (principal == null)
                {
                    return null;
                }
                Claim? claim = principal.Claims.FirstOrDefault(c => c.Type == Bluesky.ClaimTypes.Avatar);

                if (claim is not null && claim.Value is not null && Uri.TryCreate(claim.Value, UriKind.Absolute, out Uri? uri))
                {
                    return uri;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the URI for the user's specified profile banner from the claims principal, if any and it converts to a valid <see cref="Uri"/>.
        /// </summary>
        /// <value>The banner if present; otherwise, <see langword="null"/>.</value>
        public Uri? Banner
        {
            get
            {
                if (principal == null)
                {
                    return null;
                }
                Claim? claim = principal.Claims.FirstOrDefault(c => c.Type == Bluesky.ClaimTypes.Banner);

                if (claim is not null && claim.Value is not null && Uri.TryCreate(claim.Value, UriKind.Absolute, out Uri? uri))
                {
                    return uri;
                }

                return null;
            }
        }
    }
}
