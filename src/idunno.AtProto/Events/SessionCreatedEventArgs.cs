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
        /// <exception cref="ArgumentNullException">Thrown if any of the parameters are null</exception>
        public SessionCreatedEventArgs(Did did, Uri service, Handle handle, string accessJwt, string refreshJwt)
        {
            ArgumentNullException.ThrowIfNull(did);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(handle);
            ArgumentNullException.ThrowIfNullOrEmpty(accessJwt);
            ArgumentNullException.ThrowIfNullOrEmpty(refreshJwt);

            AccessJwt = accessJwt;
            RefreshJwt = refreshJwt;
            Did = did;
            Handle = handle;
            Service = service;
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

    }
}
