// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Properties for a credential that has a refresh token.
    /// </summary>
    public interface IRefreshCredential
    {

        /// <summary>
        /// Gets or sets a string representation of the token to use when a new access token is required.
        /// </summary>
        public string RefreshToken { get; }
    }
}
