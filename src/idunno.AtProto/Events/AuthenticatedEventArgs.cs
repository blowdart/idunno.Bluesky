// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Events
{
    /// <summary>
    /// Encapsulations information about credentials that have been set on an agent.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> the credentials belong to.</param>
    /// <param name="service">The <see cref="Uri"/> of the service that credentials are for.</param>
    /// <param name="accessCredentials">The initial access credentials for the session.</param>
    public sealed class AuthenticatedEventArgs(
            Did did,
            Uri service,
            AccessCredentials accessCredentials) : BaseAuthenticationEventArgs(did, service)
    {
        /// <summary>
        /// Gets the newly issued access credentials.
        /// </summary>
        public AccessCredentials AccessCredentials { get; } = accessCredentials;
    }
}
