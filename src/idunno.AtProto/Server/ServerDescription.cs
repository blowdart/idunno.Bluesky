// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Server
{
    /// <summary>
    /// Information about an AT Proto server
    /// </summary>
    public sealed record ServerDescription
    {
        /// <summary>
        /// Creates a new instance of <see cref="ServerDescription"/> with the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The DID of the server whose description this belongs to.</param>
        [JsonConstructor]
        public ServerDescription(Did did)
        {
            Did = did;
        }

        /// <summary>
        /// Gets the DID of this server.
        /// </summary>
        /// <value>
        /// The DID of this server.
        /// </value>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; internal set; }

        /// <summary>
        /// Gets a flag indicating whether the server needs an invite code.
        /// </summary>
        /// <value>
        /// A flag indicating whether the server needs an invite code.
        /// </value>
        [JsonInclude]
        public bool InviteCodeRequired { get; internal set; }

        /// <summary>
        /// Gets a flag indicating whether the server will perform verification on phone numbers.
        /// </summary>
        /// <value>
        /// A flag indicating whether the server will perform verification on phone numbers.
        /// </value>
        [JsonInclude]
        public bool PhoneVerificationRequired { get; internal set; }

        /// <summary>
        /// Gets a list of one or more domains that users can create handles with on this server.
        /// </summary>
        /// <value>
        /// A list of one or more domains that users can create handles with on this server.
        /// </value>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyList<string> AvailableUserDomains { get; internal set; } = new List<string>();

        /// <summary>
        /// Gets any links the server provides with its description.
        /// </summary>
        /// <value>
        /// Any links the server provides with its description.
        /// </value>
        [JsonInclude]
        public Links? Links { get; internal set; }

        /// <summary>
        /// Gets any contact information the server provides with its description.
        /// </summary>
        /// <value>
        /// Any contact information the server provides with its description.
        /// </value>
        [JsonInclude]
        public Contact? Contact { get; internal set; }

    }

    /// <summary>
    /// Links a server may provide as part its description.
    /// </summary>
    public sealed record Links
    {
        /// <summary>
        /// Gets a URI to the server's privacy policy.
        /// </summary>
        /// <value>
        /// A URI to the server's privacy policy.
        /// </value>
        [JsonInclude]
        public Uri? PrivacyPolicy { get; internal set; }

        /// <summary>
        /// Gets a URI to the server's terms of service.
        /// </summary>
        /// <value>
        /// A URI to the server's terms of service.
        /// </value>
        [JsonInclude]
        public Uri? TermsOfService { get; internal set; }
    }

    /// <summary>
    /// Contact information a server may provide as part of its description.
    /// </summary>
    public sealed record Contact
    {
        /// <summary>
        /// Gets the email address associated with the server.
        /// </summary>
        /// <value>
        /// The email address associated with the server.
        /// </value>
        [JsonInclude]
        public string? Email { get; internal set; }
    }
}
