// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Server.Models
{
    /// <summary>
    /// Information about an AT Proto server
    /// </summary>
    public sealed record ServerDescription
    {
        /// <summary>
        /// Creates a new instance of <see cref="ServerDescription"/>.
        /// </summary>
        /// <param name="did">The DID of the server whose description this belongs to.</param>
        /// <param name="inviteCodeRequired">Flag indicating whether an invite code is required to create an account on this server.</param>
        /// <param name="phoneVerificationRequired">Flag indicating whether supplemental verification is required to create an account on this server.</param>
        /// <param name="availableUserDomains">A list of domains that users can create handles with on this server.</param>
        /// <param name="links">Any links the server provides with its description.</param>
        /// <param name="contact">A contact for the server.</param>
        [JsonConstructor]
        internal ServerDescription(
            Did did,
            bool inviteCodeRequired,
            bool phoneVerificationRequired,
            IReadOnlyList<string> availableUserDomains,
            Links? links,
            Contact? contact)
        {
            ArgumentNullException.ThrowIfNull(did);

            Did = did;

            if (contact is not null && !string.IsNullOrEmpty(contact.Email))
            {
                Contact = contact;
            }

            if (links is not null && (links.PrivacyPolicy is not null || links.TermsOfService is not null))
            {
                Links = links;
            }

            AvailableUserDomains = availableUserDomains;
            InviteCodeRequired = inviteCodeRequired;
            PhoneVerificationRequired = phoneVerificationRequired;
        }

        /// <summary>
        /// Gets the DID of this server.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; internal set; }

        /// <summary>
        /// Gets a flag indicating whether the server needs an invite code.
        /// </summary>
        [JsonInclude]
        public bool InviteCodeRequired { get; internal set; }

        /// <summary>
        /// Gets a flag indicating whether the server will perform verification on phone numbers.
        /// </summary>
        [JsonInclude]
        public bool PhoneVerificationRequired { get; internal set; }

        /// <summary>
        /// Gets a list of one or more domains that users can create handles with on this server.
        /// </summary>
        public IReadOnlyList<string> AvailableUserDomains { get; internal set; } = [];

        /// <summary>
        /// Gets any links the server provides with its description.
        /// </summary>
        [JsonInclude]
        public Links? Links { get; internal set; }

        /// <summary>
        /// Gets any contact information the server provides with its description.
        /// </summary>
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
        [JsonInclude]
        public Uri? PrivacyPolicy { get; internal set; }

        /// <summary>
        /// Gets a URI to the server's terms of service.
        /// </summary>
        [JsonInclude]
        public Uri? TermsOfService { get; internal set; }
    }

    /// <summary>
    /// Contact information a server may provide as part of its description.
    /// </summary>
    public sealed record Contact
    {
        /// <summary>
        /// Creates a new instance of <see cref="Contact"/>
        /// </summary>
        /// <param name="email">The email address for the contact.</param>
        [JsonConstructor]
        public Contact(string email)
        {
            Email = email;
        }

        /// <summary>
        /// Gets the email address associated with the server.
        /// </summary>
        [JsonInclude]
        public string Email { get; internal set; }

        /// <summary>
        /// Provides a string representation of this Contact.
        /// </summary>
        public override string ToString() => Email;
    }
}
