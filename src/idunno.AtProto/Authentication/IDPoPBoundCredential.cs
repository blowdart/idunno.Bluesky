// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Providers the properties necessary for DPoP bound tokens.
    /// </summary>
    public interface IDPoPBoundCredential
    {

        /// <summary>
        /// Gets or sets a string representation of the DPoP proof key to use when signing requests.
        /// </summary>
        public string DPoPProofKey { get; set; }

        /// <summary>
        /// Gets a string representation of the DPoP nonce to use when signing requests.
        /// </summary>
        public string DPoPNonce { get; set; }
    }
}
