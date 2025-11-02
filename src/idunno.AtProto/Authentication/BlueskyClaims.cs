// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Defines Bluesky specific claims for use in <see cref="System.Security.Claims.ClaimsIdentity"/>
    /// </summary>
    public static class BlueskyClaims
    {
        /// <summary>
        /// The claim name for a <see cref="Did"/>.
        /// </summary>
        public const string Did = "bluesky:did";

        /// <summary>
        /// The claim name for an access token.
        /// </summary>
        public const string AccessToken = "bluesky:token:access";

        /// <summary>
        /// The claim name for a refresh token.
        /// </summary>
        public const string RefreshToken = "bluesky:token:refresh";

        /// <summary>
        /// The claim name for a DPoPProof.
        /// </summary>
        public const string DPoPProof = "bluesky:dpop:proof";

        /// <summary>
        /// The claim name for a DPoPNonce.
        /// </summary>
        public const string DPoPNonce = "bluesky:dpop:nonce";
    }
}
