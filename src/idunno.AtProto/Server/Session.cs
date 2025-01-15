// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Models;
using idunno.AtProto.Authentication;

namespace idunno.AtProto.Server
{
    /// <summary>
    /// Encapsulates information about, and the state of a session on an atproto service.
    /// </summary>
    public sealed record Session
    {
        internal Session(Uri service, CreateSessionResponse createSessionResponse)
        {
            Handle = createSessionResponse.Handle;
            Did = createSessionResponse.Did;

            DidDoc = createSessionResponse.DidDoc;
            Email = createSessionResponse.Email;
            EmailConfirmed = createSessionResponse.EmailConfirmed;
            EmailAuthFactor = createSessionResponse.EmailAuthFactor;
            IsAccountActive = createSessionResponse.Active;
            AccountStatus = createSessionResponse.Status;

            AccessCredentials = new AccessCredentials(
                service,
                createSessionResponse.AccessJwt,
                createSessionResponse.RefreshJwt,
                createSessionResponse.DPoPProofKey,
                createSessionResponse.DPoPNonce);
        }

        internal Session(GetSessionResponse getSessionResponse, AccessCredentials accessCredentials)
        {
            AccessCredentials = accessCredentials;

            Did = getSessionResponse.Did;
            Handle = getSessionResponse.Handle;

            Email = getSessionResponse.Email;
            EmailConfirmed = getSessionResponse.EmailConfirmed;
            EmailAuthFactor = getSessionResponse.EmailAuthFactor;
            DidDoc = getSessionResponse.DidDoc;
            IsAccountActive = getSessionResponse.Active;
            AccountStatus = getSessionResponse.Status;
        }

        /// <summary>
        /// Gets the <see cref="AccessCredentials"/> for this session.
        /// </summary>
        public AccessCredentials AccessCredentials { get; init; }

        /// <summary>
        /// Gets the access token for the actor whose authentication produced this Session instance.
        /// </summary>
        /// <remarks>
        /// <para>The access token is attached automatically to every API call through an agent that requires authentication.</para>
        /// </remarks>
        [Obsolete("This property is obsolete. Use AccessCredentials.AccessToken instead.", false)]
        public string AccessJwt => AccessCredentials.AccessJwt;

        /// <summary>
        /// Gets the <see cref="DateTime"/> the access token expires on, if an access token is present.
        /// </summary>
        [Obsolete("This property is obsolete. Use AccessCredentials.AccessJwtExpiresOn instead.", false)]
        public DateTimeOffset AccessJwtExpiresOn => AccessCredentials.AccessJwtExpiresOn;

        /// <summary>
        /// Gets the refresh token for the actor whose authentication produced this Session instance.
        /// </summary>
        /// <remarks>
        /// <para>The refresh token is used to exchange an expiring access token for a new access token.</para>
        /// </remarks>
        [Obsolete("This property is obsolete. Use AccessCredentials.RefreshJwt instead.", false)]
        public string RefreshJwt => AccessCredentials.RefreshJwt;

        /// <summary>
        /// Gets the <see cref="Did"/> of the actor whose authentication produced this Session instance.
        /// </summary>
        public Did Did { get; init; }

        /// <summary>
        /// Gets the <see cref="Handle"/> of the actor whose authentication produced this Session instance.
        /// </summary>
        public Handle Handle { get; init; }

        /// <summary>
        /// Gets the <see cref="DidDoc " /> of the actor whose authentication produced this Session instance.
        /// </summary>
        /// <remarks>
        /// <para>A DID document actus as a profile for its subject, containing metadata about the subject such
        /// as signing keys and service endpoints to where the subject's data is stored.</para>
        /// </remarks>
        public DidDocument? DidDoc { get; init; }

        /// <summary>
        /// Gets the email of the actor whose authentication produced this Session instance.
        /// </summary>
        public string? Email { get; init; }

        /// <summary>
        /// Gets a flag indicating if the <see cref="Email"/> has been confirmed.
        /// </summary>
        public bool? EmailConfirmed { get; init; }

        /// <summary>
        /// Gets a flag indicating whether the actor used an email authentication factor during login.
        /// </summary>
        public bool? EmailAuthFactor { get; init; }

        /// <summary>
        /// Gets a flag indicating whether the actor's account is active or not.
        /// </summary>
        public bool? IsAccountActive { get; init; }

        /// <summary>
        /// Indicates a possible reason for why the actor's account is not active.
        /// If <see cref="IsAccountActive"/> is false and no status is supplied, then the host makes no claim
        /// for why the repository is no longer being hosted.
        /// </summary>
        public AccountStatus? AccountStatus { get; init; } 

        /// <summary>
        /// Gets the URI of the service that the <see cref="Session"/> instance was created on.
        /// </summary>
        public Uri Service => AccessCredentials.Service;
    }
}
