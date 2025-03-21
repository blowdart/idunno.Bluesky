﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Events
{
    /// <summary>
    /// Encapsulations information about credentials that have been updated on an agent, typically by an access token refresh, or by DPoP nonce changes.
    /// </summary>
    public sealed class CredentialsUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of <see cref="CredentialsUpdatedEventArgs"/>
        /// </summary>
        /// <param name="did">The <see cref="Did"/> the session was created for.</param>
        /// <param name="service">The <see cref="Uri"/> of the service the session was created on.</param>
        /// <param name="accessCredentials">The new access credentials for the session</param>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="accessCredentials"/> AccessJwt or RefreshJwt are null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> or <paramref name="service"/> is null.</exception>
        public CredentialsUpdatedEventArgs(
            Did did,
            Uri service,
            AccessCredentials accessCredentials)
        {
            ArgumentNullException.ThrowIfNull(did);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);

            ArgumentException.ThrowIfNullOrWhiteSpace(accessCredentials.AccessJwt);
            ArgumentException.ThrowIfNullOrWhiteSpace(accessCredentials.RefreshToken);

            Did = did;
            Service = service;
            AccessCredentials = accessCredentials;
        }

        /// <summary>
        /// Gets the <see cref="AtProto.Did"/> the session was created for.
        /// </summary>
        public Did Did { get; }

        /// <summary>
        /// Gets the <see cref="Uri"/> of the service the session was created on.
        /// </summary>
        public Uri Service { get; }

        /// <summary>
        /// Gets the access credentials for the session..
        /// </summary>
        public AccessCredentials AccessCredentials { get; }
    }
}
