// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

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
        /// <param name="authenticationType">The type of authentication that raised this event.</param>
        /// <param name="accessCredentials">The initial access credentials for the session.</param>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when <paramref name="did"/>, <paramref name="service"/>, <paramref name="handle"/> or <paramref name="authenticationType"/> is null.
        /// </exception>
        public SessionCreatedEventArgs(
            Did did,
            Uri service,
            Handle handle,
            AuthenticationType authenticationType,
            AccessCredentials accessCredentials)
        {
            ArgumentNullException.ThrowIfNull(did);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(handle);
            ArgumentNullException.ThrowIfNull(authenticationType);
            ArgumentNullException.ThrowIfNull(accessCredentials);

            Did = did;
            Handle = handle;
            Service = service;
            AuthenticationType = authenticationType;
            AccessCredentials = accessCredentials;
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
        /// Gets the type of authentication that raised this event.
        /// </summary>
        public AuthenticationType AuthenticationType { get; }

        /// <summary>
        /// The initial access credentials for the session.
        /// </summary>
        public AccessCredentials AccessCredentials { get; }
    }
}
