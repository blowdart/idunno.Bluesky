// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Events
{
    /// <summary>
    /// Encapsulations information about credentials that have been updated on an agent, typically by an access token refresh, or by DPoP nonce changes.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> the session was created for.</param>
    /// <param name="service">The <see cref="Uri"/> of the service the session was created on.</param>
    /// <param name="accessCredentials">The new access credentials for the session</param>
    public sealed class CredentialsUpdatedEventArgs(
        Did did,
        Uri service,
        AccessCredentials accessCredentials) : BaseAuthenticationEventArgs(did, service)
    {
        /// <summary>
        /// Gets the access credentials for the session..
        /// </summary>
        public AccessCredentials AccessCredentials { get; } = accessCredentials;
    }
}
