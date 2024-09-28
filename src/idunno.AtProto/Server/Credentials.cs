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
