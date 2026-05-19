// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace idunno.AtProto.Authentication;

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
        /// Gets the <see cref="Did"/> from the claims principal, if any.
        /// </summary>
        /// <value>The <see cref="Did"/> if present; otherwise, <see langword="null"/>.</value>
        public Did? Did
        {
            get
            {
                if (principal == null)
                {
                    return null;
                }

                Claim? claim = principal.Claims.FirstOrDefault(c => c.Type == AtProtoClaims.Did);
                if (claim is null)
                {
                    return null;
                }

                if (!Did.TryParse(claim.Value, out Did? did))
                {
                    return null;
                }

                return did;
            }
        }
    }
}
