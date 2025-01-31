// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Properties for a credential that has an access jwt.
    /// </summary>
    public interface IAccessCredential
    {
        /// <summary>
        /// Gets a string representation of the JWT to use when making authenticated access requests.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when setting the value and the value is null or whitespace.</exception>
        public string AccessJwt { get; set; }
    }
}
