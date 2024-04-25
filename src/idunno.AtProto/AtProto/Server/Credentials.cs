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
        public Credentials(string identifier, string password)
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
        }


        /// <summary>
        /// Gets the  handle or other identifier supported by the server for the authenticating user.
        /// </summary>
        [JsonInclude]
        public string Identifier { get; }

        /// <summary>
        /// Sets a password for the <see cref="Identifier"/>.
        /// </summary>
        [JsonInclude]
        public string Password { get; }

        public override string ToString()
        {
            return $"{Identifier}:****";
        }
    }
}
