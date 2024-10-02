// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Server
{
    /// <summary>
    /// Provides a class representing the credential information needed to create a session on a server.
    /// </summary>
    public class Credentials
    {
        /// <summary>
        /// Creates a new <see cref="Credentials"/> instance with the specified <paramref name="identifier"/>, <paramref name="password"/> and optional <paramref name="authFactorToken"/>.
        /// </summary>
        /// <param name="identifier">The identifier to use when authenticating.</param>
        /// <param name="password">The password to use when authenticating.</param>
        /// <param name="authFactorToken">An optional email authentication factor.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="identifier"/> or <paramref name="password"/> is null or empty.</exception>
        [JsonConstructor]
        public Credentials(string identifier, string password, string? authFactorToken = null)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentNullException(nameof(identifier));
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            Identifier = identifier;
            Password = password;
            AuthFactorToken = authFactorToken;
        }

        /// <summary>
        /// Gets the handle or other identifier supported by the server for the authenticating user.
        /// </summary>
        [JsonInclude]
        public string Identifier { get; }

        /// <summary>
        /// Gets the password for the <see cref="Identifier"/>.
        /// </summary>
        [JsonInclude]
        public string Password { get; }

        /// <summary>
        /// Gets the authentication token for the <see cref="Identifier"/>.
        /// </summary>
        [JsonInclude]
        public string? AuthFactorToken { get; }

        /// <summary>
        /// Provides a string representation of this <see cref="Credentials"/>.
        /// The password or auth factor token is not included in the string representation.
        /// </summary>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(AuthFactorToken))
            {
                return $"{Identifier}:****";
            }
            else
            {
                return $"{Identifier}:****:****";
            }

        }
    }
}
