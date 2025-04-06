// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


using System.Text.Json.Serialization;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// The authentication type of an <see cref="AtProtoCredential"/> instance.
    /// </summary>
    public enum AuthenticationType
    {
        /// <summary>
        /// The authentication type is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The credentials came from the CreateSession api with a username and password.
        /// </summary>
        UsernamePassword = 1,

        /// <summary>
        /// The credentials came from the CreateSession api with a username, password and auth token.
        /// </summary>
        UsernamePasswordAuthFactorToken = 2,

        /// <summary>
        /// The credentials came from an OAuth flow.
        /// </summary>
        OAuth = 3,

        /// <summary>
        /// The credentials came from the getServiceAuth API.
        /// </summary>
        Service
    }
}
