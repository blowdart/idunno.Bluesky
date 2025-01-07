// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Events
{
    /// <summary>
    /// A class holding information about a session that has just been created.
    /// </summary>
    public sealed class SessionCreatedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of <see cref="SessionCreatedEventArgs"/>
        /// </summary>
        /// <param name="did">The <see cref="Did"/> the session was created for.</param>
        /// <param name="service">The <see cref="Uri"/> of the service the session was created on.</param>
        /// <param name="handle">The <see cref="Handle"/> the session was created for.</param>
        /// <param name="accessJwt">The access token for the session.</param>
        /// <param name="refreshJwt">The refresh token for the session.</param>
        /// <param name="authenticationType">The type of authentication that raised this event.</param>
        /// <param name="dPoPKey">The key used to sign requests when the authentication type is OAuth.</param>
        /// <param name="dPoPNonce">The nonce used when signing requests if <paramref name="dPoPKey"/> is not null.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="accessJwt"/> or <paramref name="refreshJwt"/> are null or empty.</exception>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when <paramref name="did"/>, <paramref name="service"/>, <paramref name="handle"/> or <paramref name="authenticationType"/> is null.
        /// </exception>
        public SessionCreatedEventArgs(
            Did did,
            Uri service,
            Handle handle,
            string accessJwt,
            string refreshJwt,
            AuthenticationType authenticationType,
            string? dPoPKey = null,
            string? dPoPNonce = null)
        {
            ArgumentNullException.ThrowIfNull(did);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(handle);
            ArgumentNullException.ThrowIfNull(authenticationType);

            ArgumentException.ThrowIfNullOrEmpty(accessJwt);
            ArgumentException.ThrowIfNullOrEmpty(refreshJwt);

            AccessJwt = accessJwt;
            RefreshJwt = refreshJwt;
            Did = did;
            Handle = handle;
            Service = service;
            AuthenticationType = authenticationType;

            DPoPKey = dPoPKey;
            DPoPNonce = dPoPNonce;
        }

        /// <summary>
        /// Gets the <see cref="Did"/> the session was created for.
        /// </summary>
        public Did Did { get; }

        /// <summary>
        /// Gets the <see cref="Uri"/> of the service the session was created on.
        /// </summary>
        public Uri Service { get; }

        /// <summary>
        /// Gets the <see cref="Handle"/> the session was created for.
        /// </summary>
        public Handle Handle { get; }

        /// <summary>
        /// Gets a string representation of the access token for the session.
        /// </summary>
        public string AccessJwt { get; }

        /// <summary>
        /// Gets a string representation of the refresh token for the session.
        /// </summary>
        public string RefreshJwt { get; }

        /// <summary>
        /// Gets the type of authentication that raised this event.
        /// </summary>
        public AuthenticationType AuthenticationType { get; }

        /// <summary>
        /// Gets the key used to sign requests when the authentication type is OAuth
        /// </summary>
        public string? DPoPKey { get; }

        /// <summary>
        /// Gets the nonce used in signing requests when the authentication type is OAuth and DPopKey is not null
        /// </summary>
        public string? DPoPNonce { get;  }
    }
}
