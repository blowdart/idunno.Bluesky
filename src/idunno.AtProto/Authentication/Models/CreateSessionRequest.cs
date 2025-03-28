// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Authentication.Models
{
    internal sealed record CreateSessionRequest
    {
        /// <summary>
        /// Gets the handle or other identifier supported by the server for the authenticating user.
        /// </summary>
        public required string Identifier { get; set; }

        /// <summary>
        /// Gets the password for the <see cref="Identifier"/>.
        /// </summary>
        public required string Password { get; set; }

        /// <summary>
        /// Gets the authentication token for the <see cref="Identifier"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AuthFactorToken { get; set; }

        /// <summary>
        /// When true, instead of throwing error for takendown accounts, a valid response with a narrow scoped token will be returned.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? AllowTakendown { get; set; }
    }
}
