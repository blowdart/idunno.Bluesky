// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Defines AtProto specific claims for use in <see cref="System.Security.Claims.ClaimsIdentity"/>
    /// </summary>
    public static class AtProtoClaims
    {
        /// <summary>
        /// The claim name for a <see cref="Did"/>.
        /// </summary>
        public const string Did = "atproto:did";

        /// <summary>
        /// The claim name for an access token.
        /// </summary>
        public const string AccessToken = "atproto:token:access";

        /// <summary>
        /// The claim name for a refresh token.
        /// </summary>
        public const string RefreshToken = "atproto:token:refresh";

        /// <summary>
        /// The claim name for a DPoPProof.
        /// </summary>
        public const string DPoPProof = "atproto:dpop:proof";

        /// <summary>
        /// The claim name for a DPoPNonce.
        /// </summary>
        public const string DPoPNonce = "atproto:dpop:nonce";
    }
}
