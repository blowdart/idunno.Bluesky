// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Events
{
    /// <summary>
    /// Defines the types of authentication that can be used to authenticate the agent.
    /// </summary>
    public enum AuthenticationType
    {
        /// <summary>
        /// Authentication was with a username and password.
        /// </summary>
        HandlePassword = 0,

        /// <summary>
        /// Authentication was with a username, password and MFA token.
        /// </summary>
        HandlePasswordToken = 1,

        /// <summary>
        /// Authentication was via OAuth.
        /// </summary>
        OAuth = 2,

        /// <summary>
        /// Authentication was via a restored access or refresh token.
        /// </summary>
        Token = 3 
    }
}
