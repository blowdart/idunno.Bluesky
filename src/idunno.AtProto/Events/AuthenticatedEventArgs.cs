// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Events
{
    /// <summary>
    /// Encapsulations information about credentials that have been set on an agent.
    /// </summary>
    public sealed class AuthenticatedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of <see cref="AuthenticatedEventArgs"/>
        /// </summary>
        /// <param name="accessCredentials">The initial access credentials for the session.</param>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when <paramref name="accessCredentials"/> is null.
        /// </exception>
        public AuthenticatedEventArgs(
            AccessCredentials accessCredentials)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);

            AccessCredentials = accessCredentials;
        }

        /// <summary>
        /// The initial access credentials for the session.
        /// </summary>
        public AccessCredentials AccessCredentials { get; }
    }
}
